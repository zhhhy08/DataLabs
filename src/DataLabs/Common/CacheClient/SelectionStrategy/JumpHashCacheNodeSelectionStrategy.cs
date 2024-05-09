namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class JumpHashCacheNodeSelectionStrategy : ICacheNodeSelectionStrategy
    {
        private readonly DataLabCacheNode[] _candidates;

        public JumpHashCacheNodeSelectionStrategy(DataLabCacheNode[] candidates)
        {
            GuardHelper.ArgumentNotNullOrEmpty(candidates);
            _candidates = candidates;
        }

        public DataLabCacheNode SelectNode(ulong keyhash)
        {
            var index = HashUtils.JumpConsistentHash(keyhash, _candidates.Length);
            return _candidates[index];
        }
    }
}