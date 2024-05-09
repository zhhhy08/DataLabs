namespace IdMappingService.CustomResourceTypeHandling.Contracts
{
    public enum IdentifierCreationHandlerType
    {
        /// <summary>
        /// Additive handlers create identifiers that are meant to be combined with the identifiers created by default handling
        /// </summary>
        Additive,

        /// <summary>
        /// Overwrite handlers define all identifiers for the resource with no additional processing done based on default handling.
        /// </summary>
        Overwrite,

    }
}
