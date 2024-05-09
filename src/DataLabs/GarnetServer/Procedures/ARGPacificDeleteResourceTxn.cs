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
    /// for ARG Pacific store for deleting a resource in garnet
    /// ARGPacificDeleteResource
    /// We want to convert lua script using custom transaction
    /// </summary>
    /*
    local prev_version = redis.call('GET', @ResourceVersion)
    -- Only proceed if current delete notification is not stale
    if prev_version == false or (type(prev_version) == 'string' and prev_version < @Version) then
        -- Delete resource payload
        redis.call('DEL', @ResourceId)

        if @SubscriptionContainer ~= '' then
            -- Delete the entry of target resource from the subscription container
            redis.call('ZREM', @SubscriptionContainer, @SubscriptionContainerEntry)
            redis.call('EXPIREAT', @SubscriptionContainer, @Expiration)
        end

        if @ResourceGroupContainer ~= '' then
            -- Delete the entry of target resource from the resourceGroup container
            redis.call('ZREM', @ResourceGroupContainer, @ResourceGroupContainerEntry)
            redis.call('EXPIREAT', @ResourceGroupContainer, @Expiration)
        end

        if @ParentResourceContainer ~= '' then
            -- Delete the entry for target resource in parent container
            redis.call('ZREM', @ParentResourceContainer, @ParentResourceContainerEntry)
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
    Version = (RedisValue)redisEntity.Version
    */
    [ExcludeFromCodeCoverage]
    sealed class ARGPacificDeleteResourceTxn : CustomTransactionProcedure
    {
        public override bool Prepare<TGarnetReadApi>(TGarnetReadApi api, ArgSlice input)
        {
            int offset = 0;

            ArgSlice versionKey = GetNextArg(input, ref offset);
            AddKey(versionKey, Tsavorite.core.LockType.Exclusive, false);

            GetNextArg(input, ref offset); // version

            ArgSlice resourceId = GetNextArg(input, ref offset);
            AddKey(resourceId, Tsavorite.core.LockType.Exclusive, false);

            GetNextArg(input, ref offset); // expiration

            ArgSlice subscriptionContainerKey = GetNextArg(input, ref offset);
            if (subscriptionContainerKey.Length > 0)
            {
                AddKey(subscriptionContainerKey, Tsavorite.core.LockType.Exclusive, true);
            }

            GetNextArg(input, ref offset); //subscriptionContainerEntry

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
            
            // Skip transaction if stale
            if (api.GET(versionKey, out var exisitingVersionValue) == GarnetStatus.OK)
            {
                if (exisitingVersionValue.Length > 0 && BitConverter.ToInt64(version.ReadOnlySpan) < BitConverter.ToInt64(exisitingVersionValue.ReadOnlySpan))
                {
                    WriteSimpleString(ref output, "SKIPPED");
                    return;
                }
            }

            ArgSlice resourceId = GetNextArg(input, ref offset);
            ArgSlice expiration = GetNextArg(input, ref offset);

            api.DELETE(resourceId, StoreType.Main);

            ArgSlice subscriptionContainerKey = GetNextArg(input, ref offset);
            ArgSlice subscriptionContainerEntry = GetNextArg(input, ref offset);

            if (subscriptionContainerKey.Length > 0)
            {
                api.SortedSetRemove(subscriptionContainerKey, subscriptionContainerEntry, out _);
                api.EXPIRE(subscriptionContainerKey, expiration, out _, StoreType.Object);
            }

            ArgSlice resourceGroupContainerKey = GetNextArg(input, ref offset);
            ArgSlice resourceGroupContainerEntry = GetNextArg(input, ref offset);

            if (resourceGroupContainerKey.Length > 0)
            {
                api.SortedSetRemove(resourceGroupContainerKey, resourceGroupContainerEntry, out _);
                api.EXPIRE(resourceGroupContainerKey, expiration, out _, StoreType.Object);
            }

            ArgSlice parentResourceContainerKey = GetNextArg(input, ref offset);
            ArgSlice parentResourceContainerEntry = GetNextArg(input, ref offset);

            if (parentResourceContainerKey.Length > 0)
            {
                api.SortedSetRemove(parentResourceContainerKey, parentResourceContainerEntry, out _);
                api.EXPIRE(parentResourceContainerKey, expiration, out _, StoreType.Object);
            }

            api.SETEX(versionKey, version, expiration);

            WriteSimpleString(ref output, "SUCCESS");
        }
    }
}