namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SelectionStrategy
{
    /// <summary>
    /// Represent a selection strategy for selecting primitive of type TCandidate.
    /// </summary>
    /// <typeparam name="TCandidate">The type of the candidate.</typeparam>
    public interface ISelectionStrategy<TCandidate, TSelectionKey>
    {
        /// <summary>
        /// Selects the candidates based on the selectionKey
        /// </summary>
        /// <param name="candidates">The candidates.</param>
        /// <param name="selectionKey">The selection key.</param>
        TCandidate Select(TCandidate[] candidates, TSelectionKey selectionKey);
    }
}