namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner
{
    /// <summary>
    /// Traffic tuner interface.
    /// </summary>
    public interface ITrafficTuner
    {
        bool HasRule { get; }

        /// <summary>
        /// Method to evaluate a message should be passed from IO service to a partner or not.
        /// Logic:
        /// If allow all tenants is true, return Allowed.
        /// If stop all tenants is true, return NotAllowed.
        /// If both of above is false, check included tenants list.
        ///     If passed tenant in request matched one of included tenants, check for excluded subscriptions or excluded resource types
        ///         If passed subscription matches one of excluded subscriptions,
        ///         or,
        ///         if passed resource type matches one of excluded resource types,
        ///             return NotAllowed.
        ///         else,
        ///             return Allowed.
        ///     Else,
        ///         return NotAllowed
        /// If message retry count exceeds cutoff, return NotAllowed.
        /// </summary>
        /// <param name="request">Traffic tuner input request.</param>
        /// <returns>Traffic tuner result (Allowed/ NotAllowed).</returns>
        (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) EvaluateTunerResult(in TrafficTunerRequest request);


        /// <summary>
        /// Update the value of traffic tuner rule if it exists
        /// </summary>
        /// <param name="trafficTunerRuleString"></param>
        void UpdateTrafficTunerRuleValue(string trafficTunerRuleString);
    }
}
