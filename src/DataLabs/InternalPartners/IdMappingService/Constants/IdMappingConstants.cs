namespace Microsoft.WindowsAzure.IdMappingService.Services.Constants
{
    public class IdMappingConstants
    {
        #region General Constants

        public const string DefaultResourceName = "default";
        public const string DeleteEventSuffix = "/delete";
        public const string EventGridDataVersion = "3.0";

        #endregion

        #region Identifier Constants

        public const string IdentifierResourceType = "Microsoft.Idmapping/Identifiers";
        public const string IdentifierPublisherInfo = "Microsoft.Idmapping";
        //TODO determine where API version should live
        public const string IdentifierApiVersion = "2023-01-01";

        public const string CompositeInternalIdIdentifierName = "CompositeInternalId";
        public const string GlobalCompositeInternalIdIdentifierName = "GlobalCompositeInternalId";

        #endregion
    }
}
