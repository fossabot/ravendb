﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Sharding;
using Sparrow.Json;

namespace Raven.Server.ServerWide.Sharding;

public class RawShardingConfiguration
{
    private ShardingConfiguration _materializedSharding;

    private readonly BlittableJsonReaderObject _sharding;
    private readonly JsonOperationContext _context;

    public RawShardingConfiguration([NotNull] JsonOperationContext context, [NotNull] BlittableJsonReaderObject sharding)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sharding = sharding ?? throw new ArgumentNullException(nameof(sharding));
    }

    public RawShardingConfiguration(ShardingConfiguration sharding)
    {
        _materializedSharding = sharding;
    }

    public BlittableJsonReaderObject Raw
    {
        get
        {
            if (_sharding == null)
                throw new ArgumentNullException(nameof(_sharding));

            return _sharding;
        }
    }

    private string _shardedDatabaseId;

    public string ShardedDatabaseId
    {
        get
        {
            if (_materializedSharding != null)
                return _materializedSharding.DatabaseId;

            if (_shardedDatabaseId == null)
                _sharding.TryGet(nameof(ShardingConfiguration.DatabaseId), out _shardedDatabaseId);

            return _shardedDatabaseId;
        }
    }

    private Dictionary<int, ShardBucketMigration> _bucketMigrations;

    public Dictionary<int, ShardBucketMigration> BucketMigrations
    {
        get
        {
            if (_materializedSharding != null)
                return _materializedSharding.BucketMigrations;

            if (_bucketMigrations == null)
            {
                _bucketMigrations = new Dictionary<int, ShardBucketMigration>();
                if (_sharding.TryGet(nameof(ShardingConfiguration.BucketMigrations), out BlittableJsonReaderObject obj) && obj != null)
                {
                    var propertyDetails = new BlittableJsonReaderObject.PropertyDetails();
                    for (var i = 0; i < obj.Count; i++)
                    {
                        obj.GetPropertyByIndex(i, ref propertyDetails);

                        if (propertyDetails.Value == null)
                            continue;

                        if (propertyDetails.Value is BlittableJsonReaderObject bjro)
                            _bucketMigrations[int.Parse(propertyDetails.Name)] = JsonDeserializationCluster.BucketMigration(bjro);
                    }
                }
            }

            return _bucketMigrations;
        }
    }

    private Dictionary<int, DatabaseTopology> _shards;

    public Dictionary<int, DatabaseTopology> Shards
    {
        get
        {
            if (_materializedSharding != null)
                return _materializedSharding.Shards;

            if (_shards != null)
                return _shards;

            if (_sharding.TryGet(nameof(ShardingConfiguration.Shards), out BlittableJsonReaderObject dictionary) == false || dictionary == null)
                return null;

            _shards = new Dictionary<int, DatabaseTopology>(dictionary.Count);
            for (var index = 0; index < dictionary.Count; index++)
            {
                var shardTopology = new BlittableJsonReaderObject.PropertyDetails();
                dictionary.GetPropertyByIndex(index, ref shardTopology);
                
                _shards[GetShardNumberFromPropertyDetails(shardTopology)] = JsonDeserializationCluster.DatabaseTopology((BlittableJsonReaderObject)shardTopology.Value);
            }
            
            return _shards;
        }
    }

    internal static int GetShardNumberFromPropertyDetails(BlittableJsonReaderObject.PropertyDetails propertyDetails)
    {
        if (int.TryParse(propertyDetails.Name.ToString(), out int shardNumber) == false)
            throw new ArgumentException($"Error while trying to extract the shard number from the raw database record. Expected an int but got a {propertyDetails.Name}");

        return shardNumber;
    }

    private List<ShardBucketRange> _shardBucketRanges;

    public List<ShardBucketRange> ShardBucketRanges
    {
        get
        {
            if (_materializedSharding != null)
                return _materializedSharding.BucketRanges;

            if (_shardBucketRanges != null)
                return _shardBucketRanges;

            if (_sharding.TryGet(nameof(ShardingConfiguration.BucketRanges), out BlittableJsonReaderArray array) == false || array == null)
                return null;

            _shardBucketRanges = new List<ShardBucketRange>(array.Length);
            for (var index = 0; index < array.Length; index++)
            {
                var shardAllocation = (BlittableJsonReaderObject)array[index];
                _shardBucketRanges.Add(JsonDeserializationCluster.ShardRangeAssignment(shardAllocation));
            }

            return _shardBucketRanges;
        }
    }

    private OrchestratorConfiguration _orchestrator;

    public OrchestratorConfiguration Orchestrator
    {
        get
        {
            if (_materializedSharding != null)
                return _materializedSharding.Orchestrator;

            if (_orchestrator == null)
            {
                if (_sharding.TryGet(nameof(ShardingConfiguration.Orchestrator), out BlittableJsonReaderObject obj) && obj != null)
                    _orchestrator = JsonDeserializationCluster.OrchestratorConfiguration(obj);
            }

            return _orchestrator;
        }
    }

    public ShardingConfiguration Value
    {
        get
        {
            if (_materializedSharding != null)
                return _materializedSharding;

            _materializedSharding = JsonDeserializationCluster.ShardingConfiguration(_sharding);
            return _materializedSharding;
        }
    }

    public bool DoesShardHaveBuckets(int shardNumber) => ShardingConfiguration.DoesShardHaveBuckets(ShardBucketRanges, shardNumber);
}
