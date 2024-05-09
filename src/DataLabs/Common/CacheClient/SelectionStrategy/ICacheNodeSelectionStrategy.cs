namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy
{
    using System;

    public interface ICacheNodeSelectionStrategy
    {
        public DataLabCacheNode SelectNode(ulong keyhash);

        public static ICacheNodeSelectionStrategy CreateCacheNodeSelectionStrategy(
            DataLabCacheNode[] candidates, DataLabCachePoolConfig dataLabCachePoolConfig)
        {
            return dataLabCachePoolConfig.NodeSelectionMode switch
            {
                CacheNodeSelectionMode.HashModular => new HashModularCacheNodeSelectionStrategy(candidates),
                CacheNodeSelectionMode.ConsistentHashing => new ConsistentHashingCacheNodeSelectionStrategy(candidates, 8129),
                CacheNodeSelectionMode.JumpHash => new JumpHashCacheNodeSelectionStrategy(candidates),
                _ => throw new NotSupportedException($"CacheNodeSelectionMode {dataLabCachePoolConfig.NodeSelectionMode} is not supported"),
            };
        }
    }
}