namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SelectionStrategy
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// The round robin selection strategy.
    /// This class is not initialized with an array of candidates, they're passed every time Select method is used.
    /// Only current index is held in its state.
    /// To achieve real round robin scenario, Select method should be invoked every time with the same array of candidates.
    /// However, it's safe to provide different arrays, overflows won't happen thanks to the modulo operation in determining index.
    /// </summary>
    public class RoundRobinSelectionStrategy<TCandidate, TSelectionKey> : ISelectionStrategy<TCandidate, TSelectionKey>
    {
        private long _currentClientIndex = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCandidate Select(TCandidate[] candidates, TSelectionKey selectionKey)
        {
            GuardHelper.ArgumentNotNullOrEmpty(candidates);

            // Get a candidate index aligned with the round robin fashion
            var roundRobinClientIndex = Math.Abs(Interlocked.Increment(ref this._currentClientIndex)) % candidates.Length;
            return candidates[roundRobinClientIndex];
        }
    }
}