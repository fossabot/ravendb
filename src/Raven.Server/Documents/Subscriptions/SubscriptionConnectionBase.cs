﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Exceptions.Commercial;
using Raven.Client.Exceptions.Database;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Client.Http;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Commands;
using Raven.Client.ServerWide.Tcp;
using Raven.Server.Documents.TcpHandlers;
using Raven.Server.Json;
using Raven.Server.Rachis;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Sparrow.Logging;
using Sparrow.Utils;

namespace Raven.Server.Documents.Subscriptions
{
    public abstract class SubscriptionConnectionBase : IDisposable
    {
        private static readonly byte[] Heartbeat = Encoding.UTF8.GetBytes("\r\n");
        public const long NonExistentBatch = -1;
        public const int WaitForChangedDocumentsTimeoutInMs = 3000;

        protected readonly ServerStore _serverStore;
        private readonly IDisposable _tcpConnectionDisposable;
        internal readonly (IDisposable ReleaseBuffer, JsonOperationContext.MemoryBuffer Buffer) _copiedBuffer;

        internal SubscriptionWorkerOptions _options;
        internal readonly Logger _logger;

        public readonly ConcurrentQueue<string> RecentSubscriptionStatuses = new ConcurrentQueue<string>();
        internal bool _isDisposed;
        public SubscriptionWorkerOptions Options => _options;
        public SubscriptionException ConnectionException;
        public long SubscriptionId { get; set; }
        public string DatabaseName;
        public readonly TcpConnectionOptions TcpConnection;
        public readonly CancellationTokenSource CancellationTokenSource;

        public SubscriptionOpeningStrategy Strategy => _options.Strategy;
        public readonly string ClientUri;

        public abstract Task ReportExceptionAsync(SubscriptionError error, Exception e);
        protected abstract Task OnClientAckAsync();
        public abstract Task SendNoopAckAsync();

        internal async Task<SubscriptionConnectionClientMessage> GetReplyFromClientAsync()
        {
            try
            {
                using (TcpConnection.ContextPool.AllocateOperationContext(out JsonOperationContext context))
                using (var blittable = await context.ParseToMemoryAsync(
                           TcpConnection.Stream,
                           "Reply from subscription client",
                           BlittableJsonDocumentBuilder.UsageMode.None,
                           _copiedBuffer.Buffer))
                {
                    TcpConnection._lastEtagReceived = -1;
                    TcpConnection.RegisterBytesReceived(blittable.Size);
                    return JsonDeserializationServer.SubscriptionConnectionClientMessage(blittable);
                }
            }
            catch (EndOfStreamException e)
            {
                throw new SubscriptionConnectionDownException("No reply from the subscription client.", e);
            }
            catch (IOException)
            {
                if (_isDisposed == false)
                    throw;

                return new SubscriptionConnectionClientMessage
                {
                    ChangeVector = null,
                    Type = SubscriptionConnectionClientMessage.MessageType.DisposedNotification
                };
            }
            catch (ObjectDisposedException)
            {
                return new SubscriptionConnectionClientMessage
                {
                    ChangeVector = null,
                    Type = SubscriptionConnectionClientMessage.MessageType.DisposedNotification
                };
            }
        }

        public string WorkerId => _options.WorkerId ??= Guid.NewGuid().ToString();
        public SubscriptionState SubscriptionState;
        public SubscriptionConnection.ParsedSubscription Subscription;
        
        protected readonly TcpConnectionHeaderMessage.SupportedFeatures _supportedFeatures;
        public readonly SubscriptionStatsCollector Stats;

        public Task SubscriptionConnectionTask;

        protected SubscriptionConnectionBase(TcpConnectionOptions tcpConnection, ServerStore serverStore, JsonOperationContext.MemoryBuffer memoryBuffer, IDisposable tcpConnectionDisposable,
            string database, CancellationToken token)
        {
            TcpConnection = tcpConnection;
            _serverStore = serverStore;
            _copiedBuffer = memoryBuffer.Clone(serverStore.ContextPool);
            _tcpConnectionDisposable = tcpConnectionDisposable;

            DatabaseName = database;
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _logger = LoggingSource.Instance.GetLogger(database, GetType().FullName);

            ClientUri = tcpConnection.TcpClient.Client.RemoteEndPoint.ToString();

            _supportedFeatures = TcpConnectionHeaderMessage.GetSupportedFeaturesFor(TcpConnectionHeaderMessage.OperationTypes.Subscription, tcpConnection.ProtocolVersion);
            Stats = new SubscriptionStatsCollector();
        }

        public abstract Task ProcessSubscriptionAsync();

        public async Task InitAsync()
        {
            var message = CreateStatusMessage(ConnectionStatus.Create);
            AddToStatusDescription(message);
            if (_logger.IsInfoEnabled)
            {
                _logger.Info(message);
            }

            // first, validate details and make sure subscription exists
            await RefreshAsync();
            
            AssertSupportedFeatures();
        }

        public async Task RefreshAsync(long? registerConnectionDurationInTicks = null)
        {
            SubscriptionState = await AssertSubscriptionConnectionDetails(registerConnectionDurationInTicks);
            Subscription = SubscriptionConnection.ParseSubscriptionQuery(SubscriptionState.Query);
        }

        public async Task<SubscriptionState> AssertSubscriptionConnectionDetails(long? registerConnectionDurationInTicks) => 
            await AssertSubscriptionConnectionDetails(SubscriptionId, _options.SubscriptionName, registerConnectionDurationInTicks, CancellationTokenSource.Token);

        protected virtual RawDatabaseRecord GetRecord(TransactionOperationContext context) => _serverStore.Cluster.ReadRawDatabaseRecord(context, DatabaseName);

        private async Task<SubscriptionState> AssertSubscriptionConnectionDetails(long id, string name, long? registerConnectionDurationInTicks, CancellationToken token)
        {
            await _serverStore.WaitForCommitIndexChange(RachisConsensus.CommitIndexModification.GreaterOrEqual, id, token);

            using (_serverStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (context.OpenReadTransaction())
            using (var record = GetRecord(context))
            {
                var subscription = _serverStore.Cluster.Subscriptions.ReadSubscriptionStateByName(context, DatabaseName, name);
                var topology = record.Topology;

                var whoseTaskIsIt = _serverStore.WhoseTaskIsIt(topology, subscription, subscription);
                if (whoseTaskIsIt == null && record.DeletionInProgress.ContainsKey(_serverStore.NodeTag))
                    throw new DatabaseDoesNotExistException(
                        $"Stopping subscription '{name}' on node {_serverStore.NodeTag}, because database '{DatabaseName}' is being deleted.");

                if (whoseTaskIsIt != _serverStore.NodeTag)
                {
                    var databaseTopologyAvailabilityExplanation = new Dictionary<string, string>();

                    string generalState;
                    RachisState currentState = _serverStore.Engine.CurrentState;
                    if (currentState == RachisState.Candidate || currentState == RachisState.Passive)
                    {
                        generalState =
                            $"Current node ({_serverStore.NodeTag}) is in {currentState.ToString()} state therefore, we can't answer who's task is it and returning null";
                    }
                    else
                    {
                        generalState = currentState.ToString();
                    }

                    databaseTopologyAvailabilityExplanation["NodeState"] = generalState;

                    FillNodesAvailabilityReportForState(subscription, topology, databaseTopologyAvailabilityExplanation, stateGroup: topology.Rehabs, stateName: "rehab");
                    FillNodesAvailabilityReportForState(subscription, topology, databaseTopologyAvailabilityExplanation, stateGroup: topology.Promotables,
                        stateName: "promotable");

                    //whoseTaskIsIt!= null && whoseTaskIsIt == subscription.MentorNode 
                    foreach (var member in topology.Members)
                    {
                        if (whoseTaskIsIt != null)
                        {
                            if (whoseTaskIsIt == subscription.MentorNode && member == subscription.MentorNode)
                            {
                                databaseTopologyAvailabilityExplanation[member] = "Is the mentor node and a valid member of the topology, it should be the mentor node";
                            }
                            else if (whoseTaskIsIt != null && whoseTaskIsIt != member)
                            {
                                databaseTopologyAvailabilityExplanation[member] =
                                    "Is a valid member of the topology, but not chosen to be the node running the subscription";
                            }
                            else if (whoseTaskIsIt == member)
                            {
                                databaseTopologyAvailabilityExplanation[member] = "Is a valid member of the topology and is chosen to be running the subscription";
                            }
                        }
                        else
                        {
                            databaseTopologyAvailabilityExplanation[member] =
                                "Is a valid member of the topology but was not chosen to run the subscription, we didn't find any other match either";
                        }
                    }

                    throw new SubscriptionDoesNotBelongToNodeException(
                        $"Subscription with id '{id}' and name '{name}' can't be processed on current node ({_serverStore.NodeTag}), because it belongs to {whoseTaskIsIt}",
                        whoseTaskIsIt,
                        databaseTopologyAvailabilityExplanation, id) { RegisterConnectionDurationInTicks = registerConnectionDurationInTicks };
                }

                if (subscription.Disabled)
                    throw new SubscriptionClosedException($"The subscription with id '{id}' and name '{name}' is disabled and cannot be used until enabled");

                return subscription;
            }

            static void FillNodesAvailabilityReportForState(SubscriptionState subscription, DatabaseTopology topology,
                Dictionary<string, string> databaseTopologyAvailabilityExplenation, List<string> stateGroup, string stateName)
            {
                foreach (var nodeInGroup in stateGroup)
                {
                    var rehabMessage = string.Empty;
                    if (subscription.MentorNode == nodeInGroup)
                    {
                        rehabMessage = $"Although this node is a mentor, it's state is {stateName} and can't run the subscription";
                    }
                    else
                    {
                        rehabMessage = $"Node's state is {stateName}, can't run subscription";
                    }

                    if (topology.DemotionReasons.TryGetValue(nodeInGroup, out var demotionReason))
                    {
                        rehabMessage = rehabMessage + ". Reason:" + demotionReason;
                    }

                    databaseTopologyAvailabilityExplenation[nodeInGroup] = rehabMessage;
                }
            }
        }

        public void RecordConnectionInfo() => Stats.ConnectionScope.RecordConnectionInfo(SubscriptionState, ClientUri, Options.Strategy, WorkerId);

        private void AssertSupportedFeatures()
        {
            if (_options.Strategy == SubscriptionOpeningStrategy.Concurrent)
            {
                _serverStore.LicenseManager.AssertCanAddConcurrentDataSubscriptions();
            }

            if (_supportedFeatures.Subscription.Includes == false)
            {
                if (Subscription.Includes != null && Subscription.Includes.Length > 0)
                    throw new SubscriptionInvalidStateException(
                        $"A connection to Subscription Task with ID '{SubscriptionId}' cannot be opened because it requires the protocol to support Includes.");
            }

            if (_supportedFeatures.Subscription.CounterIncludes == false)
            {
                if (Subscription.CounterIncludes != null && Subscription.CounterIncludes.Length > 0)
                    throw new SubscriptionInvalidStateException(
                        $"A connection to Subscription Task with ID '{SubscriptionId}' cannot be opened because it requires the protocol to support Counter Includes.");
            }

            if (_supportedFeatures.Subscription.TimeSeriesIncludes == false)
            {
                if (Subscription.TimeSeriesIncludes != null && Subscription.TimeSeriesIncludes.TimeSeries.Count > 0)
                    throw new SubscriptionInvalidStateException(
                        $"A connection to Subscription Task with ID '{SubscriptionId}' cannot be opened because it requires the protocol to support TimeSeries Includes.");
            }
        }

        protected async Task LogExceptionAndReportToClientAsync(Exception e)
        {
            var errorMessage = CreateStatusMessage(ConnectionStatus.Fail, e.ToString());
            AddToStatusDescription($"{errorMessage}. Sending response to client");
            if (_logger.IsOperationsEnabled)
                _logger.Info(errorMessage, e);

            await ReportExceptionToClientAsync(e);
        }

        protected async Task ReportExceptionToClientAsync(Exception ex, int recursionDepth = 0)
        {
            if (recursionDepth == 2)
                return;
            try
            {
                switch (ex)
                {
                    case SubscriptionDoesNotExistException:
                    case DatabaseDoesNotExistException:
                        await WriteJsonAsync(new DynamicJsonValue
                        {
                            [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                            [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.NotFound),
                            [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                            [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString()
                        });
                        break;
                    case SubscriptionClosedException sce:
                        await WriteJsonAsync(new DynamicJsonValue
                        {
                            [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                            [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.Closed),
                            [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                            [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString(),
                            [nameof(SubscriptionConnectionServerMessage.Data)] = new DynamicJsonValue
                            {
                                [nameof(SubscriptionClosedException.CanReconnect)] = sce.CanReconnect
                            }
                        });
                        break;
                    case SubscriptionInvalidStateException:
                        await WriteJsonAsync(new DynamicJsonValue
                        {
                            [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                            [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.Invalid),
                            [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                            [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString()
                        });
                        break;
                    case SubscriptionInUseException:
                        await WriteJsonAsync(new DynamicJsonValue
                        {
                            [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                            [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.InUse),
                            [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                            [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString()
                        });
                        break;
                    case SubscriptionDoesNotBelongToNodeException subscriptionDoesNotBelongException:
                        {
                            if (string.IsNullOrEmpty(subscriptionDoesNotBelongException.AppropriateNode) == false)
                            {
                                try
                                {
                                    using (_serverStore.ContextPool.AllocateOperationContext(out TransactionOperationContext ctx))
                                    using (ctx.OpenReadTransaction())
                                    {
                                        // check that the subscription exists on AppropriateNode
                                        var clusterTopology = _serverStore.GetClusterTopology(ctx);
                                        using (var requester = ClusterRequestExecutor.CreateForSingleNode(
                                                   clusterTopology.GetUrlFromTag(subscriptionDoesNotBelongException.AppropriateNode), _serverStore.Server.Certificate.Certificate))
                                        {
                                            await requester.ExecuteAsync(new WaitForRaftIndexCommand(subscriptionDoesNotBelongException.Index), ctx);
                                        }
                                    }
                                }
                                catch
                                {
                                    // we let the client try to connect to AppropriateNode
                                }
                            }

                            AddToStatusDescription(CreateStatusMessage(ConnectionStatus.Info, "Redirecting subscription client to different server"));
                            if (_logger.IsInfoEnabled)
                            {
                                _logger.Info("Subscription does not belong to current node", ex);
                            }
                            await WriteJsonAsync(new DynamicJsonValue
                            {
                                [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                                [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.Redirect),
                                [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                                [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString(),
                                [nameof(SubscriptionConnectionServerMessage.Data)] = new DynamicJsonValue
                                {
                                    [nameof(SubscriptionConnectionServerMessage.SubscriptionRedirectData.RedirectedTag)] = subscriptionDoesNotBelongException.AppropriateNode,
                                    [nameof(SubscriptionConnectionServerMessage.SubscriptionRedirectData.CurrentTag)] = _serverStore.NodeTag,
                                    [nameof(SubscriptionConnectionServerMessage.SubscriptionRedirectData.RegisterConnectionDurationInTicks)] = subscriptionDoesNotBelongException.RegisterConnectionDurationInTicks,
                                    [nameof(SubscriptionConnectionServerMessage.SubscriptionRedirectData.Reasons)] =
                                        new DynamicJsonArray(subscriptionDoesNotBelongException.Reasons.Select(item => new DynamicJsonValue
                                        {
                                            [item.Key] = item.Value
                                        }))
                                }
                            });
                            break;
                        }
                    case SubscriptionChangeVectorUpdateConcurrencyException:
                        {
                            AddToStatusDescription(CreateStatusMessage(ConnectionStatus.Info, $"Subscription change vector update concurrency error, reporting to '{TcpConnection.TcpClient.Client.RemoteEndPoint}'"));
                            if (_logger.IsInfoEnabled)
                            {
                                _logger.Info("Subscription change vector update concurrency error", ex);
                            }
                            await WriteJsonAsync(new DynamicJsonValue
                            {
                                [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                                [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.ConcurrencyReconnect),
                                [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                                [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString()
                            });
                            break;
                        }
                    case LicenseLimitException:
                        await WriteJsonAsync(new DynamicJsonValue
                        {
                            [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.ConnectionStatus),
                            [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.Invalid),
                            [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                            [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString()
                        });
                        break;
                    case RachisApplyException commandExecution when commandExecution.InnerException is SubscriptionException:
                        await ReportExceptionToClientAsync(commandExecution.InnerException, recursionDepth - 1);
                        break;
                    default:
                        AddToStatusDescription(CreateStatusMessage(ConnectionStatus.Fail, $"Subscription error on subscription {ex}"));

                        if (_logger.IsInfoEnabled)
                        {
                            _logger.Info("Subscription error", ex);
                        }
                        await WriteJsonAsync(new DynamicJsonValue
                        {
                            [nameof(SubscriptionConnectionServerMessage.Type)] = nameof(SubscriptionConnectionServerMessage.MessageType.Error),
                            [nameof(SubscriptionConnectionServerMessage.Status)] = nameof(SubscriptionConnectionServerMessage.ConnectionStatus.None),
                            [nameof(SubscriptionConnectionServerMessage.Message)] = ex.Message,
                            [nameof(SubscriptionConnectionServerMessage.Exception)] = ex.ToString()
                        });
                        break;
                }
            }
            catch
            {
                // ignored
            }
        }

        public enum ConnectionStatus
        {
            Create,
            Fail,
            Info
        }

        public class StatusMessageDetails
        {
            public string DatabaseName;
            public string ClientType;
            public string SubscriptionType;
        }

        protected abstract StatusMessageDetails GetStatusMessageDetails();

        protected string CreateStatusMessage(ConnectionStatus status, string info = null)
        {
            var message = GetStatusMessageDetails();
            var dbNameStr = message.DatabaseName;
            var clientType = message.ClientType;
            var subsType = message.SubscriptionType;

            string m = null;
            switch (status)
            {
                case ConnectionStatus.Create:
                    m = $"[CREATE] Received a connection for {subsType}, {dbNameStr} from {clientType}";
                    break;
                case ConnectionStatus.Fail:
                    m = $"[FAIL] for {subsType}, {dbNameStr} from {clientType}";
                    break;
                case ConnectionStatus.Info:
                    return $"[INFO] Update for {subsType}, {dbNameStr}, with {clientType}: {info}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }

            if (info == null)
                return m;
            return $"{m}, {info}";
        }

        public virtual void FinishProcessing()
        {
            AddToStatusDescription(CreateStatusMessage(ConnectionStatus.Info, "Finished processing subscription"));
            
            if (_logger.IsInfoEnabled)
            {
                _logger.Info(
                    $"Finished processing subscription {SubscriptionId} / from client {TcpConnection.TcpClient.Client.RemoteEndPoint}");
            }
        }

        public void AddToStatusDescription(string message)
        {
            while (RecentSubscriptionStatuses.Count > 50)
            {
                RecentSubscriptionStatuses.TryDequeue(out _);
            }
            RecentSubscriptionStatuses.Enqueue(message);
        }

        public virtual async Task ParseSubscriptionOptionsAsync()
        {
            using (_serverStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (BlittableJsonReaderObject subscriptionCommandOptions = await context.ParseToMemoryAsync(
                       TcpConnection.Stream,
                       "subscription options",
                       BlittableJsonDocumentBuilder.UsageMode.None,
                       _copiedBuffer.Buffer,
                       token: CancellationTokenSource.Token))
            {
                _options = JsonDeserializationServer.SubscriptionConnectionOptions(subscriptionCommandOptions);

                if (string.IsNullOrEmpty(_options.SubscriptionName))
                    return;

                context.OpenReadTransaction();

                var subscriptionItemKey = SubscriptionState.GenerateSubscriptionItemKeyName(DatabaseName, _options.SubscriptionName);
                var translation = _serverStore.Cluster.Read(context, subscriptionItemKey);
                if (translation == null)
                    throw new SubscriptionDoesNotExistException("Cannot find any Subscription Task with name: " + _options.SubscriptionName);

                if (translation.TryGet(nameof(Client.Documents.Subscriptions.SubscriptionState.SubscriptionId), out long id) == false)
                    throw new SubscriptionClosedException("Could not figure out the Subscription Task ID for subscription named: " + _options.SubscriptionName);

                SubscriptionId = id;
            }
        }

        internal async Task WriteJsonAsync(DynamicJsonValue value)
        {
            int writtenBytes;
            using (TcpConnection.ContextPool.AllocateOperationContext(out JsonOperationContext context))
            await using (var writer = new AsyncBlittableJsonTextWriter(context, TcpConnection.Stream))
            {
                context.Write(writer, value);
                writtenBytes = writer.Position;
            }

            await TcpConnection.Stream.FlushAsync();
            TcpConnection.RegisterBytesSent(writtenBytes);
        }

        internal async Task SendHeartBeatAsync(string reason)
        {
            try
            {
                await TcpConnection.Stream.WriteAsync(Heartbeat, 0, Heartbeat.Length, CancellationTokenSource.Token);
                await TcpConnection.Stream.FlushAsync();

                if (_logger.IsInfoEnabled)
                {
                    _logger.Info($"Subscription {Options.SubscriptionName} is sending a Heartbeat message to the client. Reason: {reason}");
                }
            }
            catch (Exception ex)
            {
                throw new SubscriptionClosedException($"Cannot contact client anymore, closing subscription ({Options?.SubscriptionName})", canReconnect: ex is OperationCanceledException, ex);
            }

            TcpConnection.RegisterBytesSent(Heartbeat.Length);
        }

        internal async Task<Task<SubscriptionConnectionClientMessage>> WaitForClientAck(Task<SubscriptionConnectionClientMessage> replyFromClientTask)
        {
            SubscriptionConnectionClientMessage clientReply;
            while (true)
            {
                var result = await Task.WhenAny(replyFromClientTask,
                    TimeoutManager.WaitFor(TimeSpan.FromMilliseconds(5000), CancellationTokenSource.Token)).ConfigureAwait(false);
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                if (result == replyFromClientTask)
                {
                    clientReply = await replyFromClientTask;
                    if (clientReply.Type == SubscriptionConnectionClientMessage.MessageType.DisposedNotification)
                    {
                        CancellationTokenSource.Cancel();
                        break;
                    }

                    replyFromClientTask = GetReplyFromClientAsync();
                    break;
                }

                await SendHeartBeatAsync("Waiting for client ACK");
                await SendNoopAckAsync();
            }

            CancellationTokenSource.Token.ThrowIfCancellationRequested();

            switch (clientReply.Type)
            {
                case SubscriptionConnectionClientMessage.MessageType.Acknowledge:
                    {
                        await OnClientAckAsync();
                        break;
                    }
                //precaution, should not reach this case...
                case SubscriptionConnectionClientMessage.MessageType.DisposedNotification:
                    CancellationTokenSource.Cancel();
                    break;

                default:
                    throw new ArgumentException("Unknown message type from client " +
                                                clientReply.Type);
            }

            return replyFromClientTask;
        }

        public virtual void Dispose()
        {
            using (_copiedBuffer.ReleaseBuffer)
            {
                try
                {
                    CancellationTokenSource.Cancel();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    _tcpConnectionDisposable?.Dispose();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    TcpConnection.Dispose();
                }
                catch
                {
                    // ignored
                }


                try
                {
                    CancellationTokenSource.Dispose();
                }
                catch
                {
                    // ignored
                }

                RecentSubscriptionStatuses?.Clear();
            }

            Stats?.Dispose();
        }
    }
}
