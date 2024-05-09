namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class CacheClientReadResult<T>
    {
        public List<DataLabCacheNode> SelectedCacheNodes { get; }

        public bool HasSuccess => SuccessNodeCount > 0;
        public bool HasFailed => FailedNodeCount > 0;

        public int SuccessNodeCount => SuccessNodeResults?.Count ?? 0;
        public int FailedNodeCount => FailedNodeResults?.Count ?? 0;

        public List<CacheClientNodeResult<T>>? SuccessNodeResults { get; private set; }
        public List<CacheClientNodeResult<T>>? FailedNodeResults { get; private set; }

        public DataLabCacheNode? FirstSuccessCacheNode => SuccessNodeResults?.Count > 0 ? SuccessNodeResults[0].CacheNode : null;
        public DataLabCacheNode? FirstFailedCacheNode => FailedNodeResults?.Count > 0 ? FailedNodeResults[0].CacheNode : null;

        public DataLabCacheNode? LastSuccessCacheNode => SuccessNodeResults?.Count > 0 ? SuccessNodeResults[^1].CacheNode : null;
        public DataLabCacheNode? LastFailedCacheNode => FailedNodeResults?.Count > 0 ? FailedNodeResults[^1].CacheNode : null;

        private int _readQuorum;

        public CacheClientReadResult(List<DataLabCacheNode> selectedCacheNodes, int readQuorum)
        {
            SelectedCacheNodes = selectedCacheNodes;
            _readQuorum = readQuorum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddSuccessNode(CacheClientNodeResult<T> nodeResult)
        {
            SuccessNodeResults ??= new List<CacheClientNodeResult<T>>(_readQuorum);
            SuccessNodeResults.Add(nodeResult);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFailedNode(CacheClientNodeResult<T> nodeResult)
        {
            FailedNodeResults ??= new List<CacheClientNodeResult<T>>(1);
            FailedNodeResults.Add(nodeResult);
        }
    }

    public class CacheClientWriteResult<T>
    {
        public List<DataLabCacheNode> SelectedCacheNodes { get; }
        public List<CacheClientNodeResult<T>>? SuccessNodeResults { get; private set; }
        public List<CacheClientNodeResult<T>>? FailedNodeResults { get; private set; }

        public int SuccessNodeCount => SuccessNodeResults?.Count ?? 0;
        public int FailedNodeCount => FailedNodeResults?.Count ?? 0;

        public CacheClientWriteResult(List<DataLabCacheNode> selectedCacheNodes)
        {
            SelectedCacheNodes = selectedCacheNodes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddSuccessNode(CacheClientNodeResult<T> nodeResult)
        {
            SuccessNodeResults ??= new List<CacheClientNodeResult<T>>(3);
            SuccessNodeResults.Add(nodeResult);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFailedNode(CacheClientNodeResult<T> nodeResult)
        {
            FailedNodeResults ??= new List<CacheClientNodeResult<T>>(1);
            FailedNodeResults.Add(nodeResult);
        }
    }

    public class CacheClientNodeResult<T>
    {
        public required DataLabCacheNode CacheNode { get; init; }
        public required T Result { get; init; }
        public Exception? Exception { get; init; }
        public bool IsSuccess { get; init; }
    }
}
