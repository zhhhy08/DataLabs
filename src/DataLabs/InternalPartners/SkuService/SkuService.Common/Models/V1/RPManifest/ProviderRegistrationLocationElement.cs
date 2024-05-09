namespace SkuService.Common.Models.V1.RPManifest
{
    /// <summary>
    /// The provider registration location element.
    /// </summary>
    public class ProviderRegistrationLocationElement
    {
        /// <summary>
        /// The Location of allowed provider registration.
        /// </summary>
        public string Location { get; set; } = default!;

        /// <summary>
        /// Feature flags associated with location.
        /// </summary>
        public string[] FeatureFlags { get; set; } = default!;
    }
}
