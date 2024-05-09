namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SelectionStrategy
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Runtime.CompilerServices;

    public class RandomSelectionStrategy<TCandidate, TSelectionKey> : ISelectionStrategy<TCandidate, TSelectionKey>
    {
        public static RandomSelectionStrategy<TCandidate, TSelectionKey> Instance { get; } = new RandomSelectionStrategy<TCandidate, TSelectionKey>();

        /* 
         * For RandomSelection, there is no reason to create separate instance
         */
        private RandomSelectionStrategy()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCandidate Select(TCandidate[] candidates, TSelectionKey selectionKey)
        {
            GuardHelper.ArgumentNotNullOrEmpty(candidates);
            return candidates[Random.Shared.Next(0, candidates.Length)];
        }
    }
}
