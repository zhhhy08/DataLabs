namespace SkuService.Common.Models.V1
{
    /// <summary>
    /// Additional State Properties modeled for ARN/ARG
    /// </summary>
    public class AdditionalStateProperties
    {
        /// <summary>
        /// Block New Resource Creation
        /// </summary>
        public bool BlockNewResourceCreation { get; set; }

        /// <summary>
        /// Release Non Data Retention Resource
        /// </summary>
        public bool ReleaseNonDataRetentionResource { get; set; }
    }
}
