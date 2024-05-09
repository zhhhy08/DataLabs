namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class ConsistentHashingCacheNodeSelectionStrategy : ICacheNodeSelectionStrategy
    {
        private readonly ConsistentHashing<DataLabCacheNode> _consistentHashing;
        
        public ConsistentHashingCacheNodeSelectionStrategy(DataLabCacheNode[] candidates, int numVirtualNodes)
        {
            GuardHelper.ArgumentNotNullOrEmpty(candidates);
            GuardHelper.IsArgumentPositive(numVirtualNodes);
            _consistentHashing = new ConsistentHashing<DataLabCacheNode>(candidates, numVirtualNodes);
        }

        public DataLabCacheNode SelectNode(ulong keyhash)
        {
            return _consistentHashing.GetNode(keyhash);
        }
    }
}