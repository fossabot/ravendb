﻿using System;
using System.Collections.Generic;
using System.Threading;
using Raven.Client;
using Raven.Client.ServerWide;
using Raven.Client.Util;
using Raven.Server.Config;
using Raven.Server.Documents.Queries;
using Raven.Server.Documents.Sharding.Executors;
using Raven.Server.Documents.Sharding.Subscriptions;
using Raven.Server.Documents.TcpHandlers;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils;
using Sparrow.Collections;
using Sparrow.Json;
using Sparrow.Logging;
using Sparrow.Utils;

namespace Raven.Server.Documents.Sharding
{
    public partial class ShardedDatabaseContext : IDisposable
    {
        private readonly CancellationTokenSource _databaseShutdown = new CancellationTokenSource();
        public CancellationToken DatabaseShutdown => _databaseShutdown.Token;

        public readonly ServerStore ServerStore;

        private DatabaseRecord _record;
        public QueryMetadataCache QueryMetadataCache = new();
        private readonly Logger _logger;

        public readonly ShardExecutor ShardExecutor;
        public readonly AllNodesExecutor AllNodesExecutor;
        public DatabaseRecord DatabaseRecord => _record;

        public RavenConfiguration Configuration { get; internal set; }

        public readonly SystemTime Time;

        public readonly RachisLogIndexNotifications RachisLogIndexNotifications;

        public readonly ConcurrentSet<TcpConnectionOptions> RunningTcpConnections = new ConcurrentSet<TcpConnectionOptions>();

        public ShardedDatabaseContext(ServerStore serverStore, DatabaseRecord record)
        {
            DevelopmentHelper.ShardingToDo(DevelopmentHelper.TeamMember.Karmel, DevelopmentHelper.Severity.Normal, "reduce the record to the needed fields");
            DevelopmentHelper.ShardingToDo(DevelopmentHelper.TeamMember.Karmel, DevelopmentHelper.Severity.Normal, "Need to refresh all this in case we will add/remove new shard");

            ServerStore = serverStore;
            _record = record;
            _logger = LoggingSource.Instance.GetLogger<ShardedDatabaseContext>(DatabaseName);

            Time = serverStore.Server.Time;

            UpdateConfiguration(record.Settings);

            Indexes = new ShardedIndexesContext(this, serverStore);

            ShardExecutor = new ShardExecutor(ServerStore, this);
            AllNodesExecutor = new AllNodesExecutor(ServerStore, this);

            NotificationCenter = new ShardedDatabaseNotificationCenter(this);
            Streaming = new ShardedStreaming();
            Cluster = new ShardedCluster(this);
            Changes = new ShardedDocumentsChanges(this);
            Operations = new ShardedOperations(this);
            Subscriptions = new ShardedSubscriptions(this, serverStore);

            RachisLogIndexNotifications = new RachisLogIndexNotifications(_databaseShutdown.Token);
            Replication = new ShardedReplicationContext(this, serverStore);
        }

        public IDisposable AllocateContext(out JsonOperationContext context) => ServerStore.ContextPool.AllocateOperationContext(out context);


        public void UpdateDatabaseRecord(RawDatabaseRecord record, long index)
        {
            UpdateConfiguration(record.Settings);

            Indexes.Update(record, index);

            Subscriptions.Update(record);

            Interlocked.Exchange(ref _record, record);

            RachisLogIndexNotifications.NotifyListenersAbout(index, e: null);
        }

        public string DatabaseName => _record.DatabaseName;

        public int NumberOfShardNodes => _record.Sharding.Shards.Length;

        public char IdentityPartsSeparator => _record.Client?.IdentityPartsSeparator ?? Constants.Identities.DefaultSeparator;

        public bool Encrypted => _record.Encrypted;

        public int ShardCount => _record.Sharding.Shards.Length;

        public DatabaseTopology[] ShardsTopology => _record.Sharding.Shards;

        public int GetShardNumber(int shardBucket) => ShardHelper.GetShardNumber(_record.Sharding.BucketRanges, shardBucket);

        public int GetShardNumber(TransactionOperationContext context, string id)
        {
            var bucket = ShardHelper.GetBucket(context, id);

            return ShardHelper.GetShardNumber(_record.Sharding.BucketRanges, bucket);
        }

        public bool HasTopologyChanged(long etag)
        {
            // TODO fix this
            return _record.Topology?.Stamp?.Index > etag;
        }

        private void UpdateConfiguration(Dictionary<string, string> settings)
        {
            Configuration = DatabasesLandlord.CreateDatabaseConfiguration(ServerStore, DatabaseName, settings);
        }

        public void Dispose()
        {
            DevelopmentHelper.ShardingToDo(DevelopmentHelper.TeamMember.Karmel, DevelopmentHelper.Severity.Normal, "needs an ExceptionAggregator like DocumentDatabase");

            if (_logger.IsInfoEnabled)
                _logger.Info($"Disposing {nameof(ShardedDatabaseContext)} of {DatabaseName}.");

            _databaseShutdown.Cancel();

            try
            {
                Replication.Dispose();
            }
            catch
            {
                // ignored
            }
            try
            {
                ShardExecutor.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                AllNodesExecutor.Dispose();
            }
            catch
            {
                // ignored
            }

            foreach (var connection in Subscriptions.SubscriptionsConnectionsState)
            {
                connection.Value.Dispose();
            }

            _databaseShutdown.Dispose();
        }
    }
}