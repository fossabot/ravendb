﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Raven.Client.Documents.Queries;
using Raven.Client.Extensions;
using Raven.Client.Http;
using Raven.Server.Documents.Sharding.Commands.Querying;
using Raven.Server.Documents.Sharding.Executors;
using Raven.Server.Documents.Sharding.Handlers;
using Raven.Server.ServerWide.Context;
using Sparrow;
using Sparrow.Json;

namespace Raven.Server.Documents.Sharding.Operations.Queries;

public abstract class AbstractShardedQueryOperation<TCombinedResult, TResult, TIncludes> : IShardedReadOperation<QueryResult, TCombinedResult>
{
    private readonly ShardedDatabaseRequestHandler _requestHandler;

    protected readonly Dictionary<int, ShardedQueryCommand> QueryCommands;

    protected readonly TransactionOperationContext Context;
    protected long CombinedResultEtag;

    protected AbstractShardedQueryOperation(Dictionary<int, ShardedQueryCommand> queryCommands, TransactionOperationContext context, ShardedDatabaseRequestHandler requestHandler, string expectedEtag)
    {
        QueryCommands = queryCommands;
        Context = context;
        _requestHandler = requestHandler;
        ExpectedEtag = expectedEtag;
    }

    public HttpRequest HttpRequest { get => _requestHandler.HttpContext.Request; }

    public string ExpectedEtag { get; }

    public HashSet<string> MissingDocumentIncludes { get; private set; }

    RavenCommand<QueryResult> IShardedOperation<QueryResult, ShardedReadResult<TCombinedResult>>.CreateCommandForShard(int shardNumber) => QueryCommands[shardNumber];

    public string CombineCommandsEtag(Dictionary<int, ShardExecutionResult<QueryResult>> commands)
    {
        CombinedResultEtag = 0;

        foreach (var cmd in commands.Values)
        {
            CombinedResultEtag = Hashing.Combine(CombinedResultEtag, cmd.Result.ResultEtag);
        }

        return CharExtensions.ToInvariantString(CombinedResultEtag);
    }

    public abstract TCombinedResult CombineResults(Dictionary<int, ShardExecutionResult<QueryResult>> results);

    protected static void CombineSingleShardResultProperties(QueryResult<List<TResult>, List<TIncludes>> combinedResult, QueryResult singleShardResult)
    {
        combinedResult.TotalResults += singleShardResult.TotalResults;
        combinedResult.IsStale |= singleShardResult.IsStale;
        combinedResult.SkippedResults += singleShardResult.SkippedResults;
        combinedResult.IndexName = singleShardResult.IndexName;
        combinedResult.IncludedPaths = singleShardResult.IncludedPaths;

        if (combinedResult.IndexTimestamp < singleShardResult.IndexTimestamp)
            combinedResult.IndexTimestamp = singleShardResult.IndexTimestamp;

        if (combinedResult.LastQueryTime < singleShardResult.LastQueryTime)
            combinedResult.LastQueryTime = singleShardResult.LastQueryTime;

        if (singleShardResult.RaftCommandIndex.HasValue)
        {
            if (combinedResult.RaftCommandIndex == null || singleShardResult.RaftCommandIndex > combinedResult.RaftCommandIndex)
                combinedResult.RaftCommandIndex = singleShardResult.RaftCommandIndex;
        }
    }

    protected void HandleDocumentIncludes(QueryResult cmdResult, QueryResult<List<TResult>, List<TIncludes>> result)
    {
        foreach (var id in cmdResult.Includes.GetPropertyNames())
        {
            if (cmdResult.Includes.TryGet(id, out BlittableJsonReaderObject include) && include != null)
            {
                if (result.Includes is List<BlittableJsonReaderObject> blittableIncludes)
                    blittableIncludes.Add(include.Clone(Context));
                else if (result.Includes is List<Document> documentIncludes)
                    documentIncludes.Add(new Document { Id = Context.GetLazyString(id), Data = include.Clone(Context)});
                else
                    throw new NotSupportedException($"Unknown includes type: {result.Includes.GetType().FullName}");
            }
            else
            {
                (MissingDocumentIncludes ??= new HashSet<string>()).Add(id);
            }
        }
    }
}