namespace Microsoft.WindowsAzure.IdMappingService.Services
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class ConfigMappingSpecificationService : IMappingSpecificationService
    {
        #region Fields
        private Dictionary<string, InternalIdSpecification> _specificationDictionary;
        #endregion

        public ConfigMappingSpecificationService()
        {
            IntializeWithConfig();
        }

        public InternalIdSpecification GetInternalIdSpecification(string ResourceType)
        {
            GuardHelper.ArgumentNotNullOrEmpty(ResourceType, nameof(ResourceType));
            if (_specificationDictionary.TryGetValue(ResourceType, out InternalIdSpecification spec))
            {
                return spec;
            } else
            {
                throw new ArgumentException($"Cannot retrieve InternalIdSpecification for resourceType: {ResourceType}");
            }
        }

        #region Private Methods

        private void IntializeWithConfig()
        {
           var jsonConfigSpecification = File.ReadAllText(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "Config",
                @"IdMappingConfigurationSpecification.json"));

            var configSpecifications = JsonConvert.DeserializeObject<Dictionary<string, InternalIdSpecification>>(jsonConfigSpecification);

            foreach (var (resourceType, specification) in configSpecifications)
            {
                specification.ResourceType = resourceType;
            }
            _specificationDictionary = new Dictionary<string, InternalIdSpecification>(configSpecifications, StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
