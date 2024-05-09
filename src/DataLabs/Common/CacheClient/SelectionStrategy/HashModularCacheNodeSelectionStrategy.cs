namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient.SelectionStrategy
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class HashModularCacheNodeSelectionStrategy : ICacheNodeSelectionStrategy
    {
        private readonly DataLabCacheNode[] _candidates;

        public HashModularCacheNodeSelectionStrategy(DataLabCacheNode[] candidates)
        {
            GuardHelper.ArgumentNotNullOrEmpty(candidates);
            _candidates = candidates;
        }

        public DataLabCacheNode SelectNode(ulong keyhash)
        {
            var index = (int)(keyhash % (ulong)_candidates.Length);
            return _candidates[index];
        }
    }
}