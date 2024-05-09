namespace SkuService.Common.Models.V1
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using System.Collections.Generic;

    // <summary>
    /// The ARM zones under combined zone rules.
    /// </summary>
    public class ARMZonesUnderCombinedRules
    {
        /// <summary>
        /// Gets ARM zones without required feature.
        /// </summary>
        public string[] ZonesWithoutRequiredFeature { get; private set; }

        /// <summary>
        /// Gets the ARM zones with required feature.
        /// </summary>
        public string[] ZonesWithRequiredFeature { get; private set; }

        /// <summary>
        /// Gets required feature shown in ARM.
        /// </summary>
        public string RequiredFeature { get; private set; } = default!;

        /// <summary>
        /// Initializes a new instance of the <see cref="ARMZonesUnderCombinedRules" /> class.
        /// </summary>
        public ARMZonesUnderCombinedRules()
        {
            this.ZonesWithoutRequiredFeature = EmptyArray<string>.Instance;
            this.ZonesWithRequiredFeature = EmptyArray<string>.Instance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ARMZonesUnderCombinedRules" /> class.
        /// </summary>
        /// <param name="zonesWithoutRequiredFeature">zones without required feature</param>
        /// <param name="zonesWithRequiredFeature">zones with required feature</param>
        /// <param name="requiredFeature">required feature</param>
        public ARMZonesUnderCombinedRules(List<string> zonesWithoutRequiredFeature, List<string> zonesWithRequiredFeature, string requiredFeature)
        {
            this.ZonesWithoutRequiredFeature = zonesWithoutRequiredFeature.ToArray();
            this.ZonesWithRequiredFeature = zonesWithRequiredFeature.ToArray();
            this.RequiredFeature = requiredFeature;
        }
    }
}
