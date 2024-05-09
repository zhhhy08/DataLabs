// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Garnet
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using FASTER.core;
    using Garnet.common;
    using Garnet.server;

    /// <summary>
    /// Functions to implement custom tranasction
    /// for ARG Pacific store for updating a resource in garnet
    /// ARGPacificUpdateResource
    /// We want to convert lua script using custom transaction
    /// </summary>
    /*
    local prev_version = redis.call('GET', @ResourceVersion)
    -- Proceed only if current notification is not stale
    if prev_version == false or(type(prev_version) == 'string' and prev_version < @Version) then
        -- Update resource payload
        redis.call('SET', @ResourceId, @Resource)
        redis.call('EXPIREAT', @ResourceId, @Expiration)

        if @SubscriptionContainer ~= '' then
            -- Add or update the entry for target resource in subscription container
            redis.call('ZADD', @SubscriptionContainer, @Score, @SubscriptionContainerEntry)
            redis.call('EXPIREAT', @SubscriptionContainer, @Expiration)
        end

        if @ResourceGroupContainer ~= '' then
            -- Add or update the entry for target resource in resourceGroup container
            redis.call('ZADD', @ResourceGroupContainer, @Score, @ResourceGroupContainerEntry)
            redis.call('EXPIREAT', @ResourceGroupContainer, @Expiration)
        end

        if @ParentResourceContainer ~= '' then
            -- Add or update the entry for target resource in parent container
            redis.call('ZADD', @ParentResourceContainer, @Score, @ParentResourceContainerEntry)
            redis.call('EXPIREAT', @ParentResourceContainer, @Expiration)
        end

        -- Update the version info for target resource
        redis.call('SET', @ResourceVersion, @Version)
        redis.call('EXPIREAT', @ResourceVersion, @Expiration)
    end


    ResourceId = (RedisKey)redisEntity.RowKey,
    ResourceVersion = (RedisKey)redisEntity.ResourceVersionKey,
    SubscriptionContainer = (RedisKey)redisEntity.SubscriptionContainerKey,
    SubscriptionContainerEntry = (RedisValue)redisEntity.SubscriptionContainerEntry,
    ResourceGroupContainer = (RedisKey)redisEntity.ResourceGroupContainerKey,
    ResourceGroupContainerEntry = (RedisValue)redisEntity.ResourceGroupContainerEntry,
    ParentResourceContainer = (RedisKey)redisEntity.ParentResourceContainerKey,
    ParentResourceContainerEntry = (RedisValue)redisEntity.ParentResourceContainerEntry,
    Expiration = (RedisValue)redisEntity.ExpireAt.Value.ToUnixTimeSeconds(),
    Version = (RedisValue)redisEntity.Version,
    Score = (RedisValue)(long)redisEntity.SortedSetScore,
    Resource = (RedisValue)await redisEntity.GetRedisPayloadAsync(cancellationToken, jsonFormatter).IgnoreContext()
    */
    [ExcludeFromCodeCoverage]
    sealed class ARGPacificUpdateResourceTxn : CustomTransactionProcedure
    {
        public override bool Prepare<TGarnetReadApi>(TGarnetReadApi api, ArgSlice input)
        {
            int offset = 0;


            ArgSlice versionKey = GetNextArg(input, ref offset);
            GetNextArg(input, ref offset); // version
            AddKey(versionKey, Tsavorite.core.LockType.Exclusive, false);

            ArgSlice resourceId = GetNextArg(input, ref offset);
            AddKey(resourceId, Tsavorite.core.LockType.Exclusive, false);

            GetNextArg(input, ref offset); //resource
            GetNextArg(input, ref offset); //expiration
            ArgSlice subscriptionContainerKey = GetNextArg(input, ref offset);
            if (subscriptionContainerKey.Length > 0)
            {
                AddKey(subscriptionContainerKey, Tsavorite.core.LockType.Exclusive, true);
            }

            GetNextArg(input, ref offset); //subscriptionContainerEntry
            GetNextArg(input, ref offset); //score

            ArgSlice resourceGroupContainerKey = GetNextArg(input, ref offset);
            if (resourceGroupContainerKey.Length > 0)
            {
                AddKey(resourceGroupContainerKey, Tsavorite.core.LockType.Exclusive, true);
            }

            GetNextArg(input, ref offset); // resourceGroupContainerEntry

            ArgSlice parentResourceContainerKey = GetNextArg(input, ref offset);
            if (parentResourceContainerKey.Length > 0)
            {
                AddKey(parentResourceContainerKey, Tsavorite.core.LockType.Exclusive, true);
            }

            return true;
        }

        public override void Main<TGarnetApi>(TGarnetApi api, ArgSlice input, ref MemoryResult<byte> output)
        {
            int offset = 0;

            ArgSlice versionKey = GetNextArg(input, ref offset);
            ArgSlice version = GetNextArg(input, ref offset);

            // Skip transaction if already stale
            if (api.GET(versionKey, out var exisitingVersionValue) == GarnetStatus.OK)
            {
                if (exisitingVersionValue.Length > 0 && BitConverter.ToInt64(version.ReadOnlySpan) < BitConverter.ToInt64(exisitingVersionValue.ReadOnlySpan))
                {
                    WriteSimpleString(ref output, "SKIPPED");
                    return;
                }
            }

            ArgSlice resourceId = GetNextArg(input, ref offset);
            ArgSlice resource = GetNextArg(input, ref offset);
            ArgSlice expiration = GetNextArg(input, ref offset);

            api.SETEX(resourceId, resource, expiration);
            api.SETEX(versionKey, version, expiration);

            ArgSlice subscriptionContainerKey = GetNextArg(input, ref offset);
            ArgSlice subscriptionContainerEntry = GetNextArg(input, ref offset);
            ArgSlice score = GetNextArg(input, ref offset);

            if (subscriptionContainerKey.Length > 0)
            {
                api.SortedSetAdd(subscriptionContainerKey, score, subscriptionContainerEntry, out _);
                api.EXPIRE(subscriptionContainerKey, expiration, out _, StoreType.Object);
            }

            ArgSlice resourceGroupContainerKey = GetNextArg(input, ref offset);
            ArgSlice resourceGroupContainerEntry = GetNextArg(input, ref offset);

            if (resourceGroupContainerKey.Length > 0)
            {
                api.SortedSetAdd(resourceGroupContainerKey, score, resourceGroupContainerEntry, out _);
                api.EXPIRE(resourceGroupContainerKey, expiration, out _, StoreType.Object);
            }

            ArgSlice parentResourceContainerKey = GetNextArg(input, ref offset);
            ArgSlice parentResourceContainerEntry = GetNextArg(input, ref offset);

            if (parentResourceContainerKey.Length > 0)
            {
                api.SortedSetAdd(parentResourceContainerKey, score, parentResourceContainerEntry, out _);
                api.EXPIRE(parentResourceContainerKey, expiration, out _, StoreType.Object);
            }

            WriteSimpleString(ref output, "SUCCESS");
        }
    }
}