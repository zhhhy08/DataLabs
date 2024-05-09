namespace Microsoft.WindowsAzure.IdMappingService.Services.Contracts
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Class represent the properties payload of the IdMapping Identifiers notifications
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ResourceIdentifiers
    {
        [JsonProperty(PropertyName = "resourceType", NullValueHandling = NullValueHandling.Ignore)]
        public string ResourceType { get; private set; }

        [JsonProperty(PropertyName = "resourceId", NullValueHandling = NullValueHandling.Ignore)]
        public string ArmResourceId { get; private set; }

        [JsonProperty(PropertyName = "resourceUpdateTimestamp", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? ResourceUpdateTimestamp { get; private set; }

        [JsonProperty(PropertyName = "identifierCreationTimestamp", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime IdentifierCreationTimestamp { get; private set; }

        [JsonProperty(PropertyName = "resourceIdentifiers")]
        public IList<Identifier> Identifiers {get; private set; }

        [JsonConstructor]
        private ResourceIdentifiers()
        {
        }

        public ResourceIdentifiers(string resourceType, string resourceId, DateTime? resourceUpdateTimestamp, IList<Identifier> identifiers, DateTime? identifierCreationTimestamp = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(resourceType, nameof(resourceType));
            GuardHelper.ArgumentNotNullOrEmpty(resourceId, nameof(resourceId));

            ResourceType = resourceType;
            ArmResourceId = resourceId;
            ResourceUpdateTimestamp = resourceUpdateTimestamp;
            Identifiers = identifiers;
            IdentifierCreationTimestamp = identifierCreationTimestamp ?? DateTime.UtcNow;
        }
    }

    //TODO: mark this class to use Name as json property name and value as json property value when serializing to json
    [JsonObject]
    public class Identifier {

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string Value { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ArmId { get; set; }

        /// <summary>
        /// This represents a resourceType (including RP Namespace) that is different from the resourceType of the parent resource. 
        /// This identifier is stored under the "index" of the overriden resourceType. This effects the keys used to store the identifier and how the identifier will be queried.
        /// Null if no override is set.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string OverrideResourceTypeIndex { get; set; }
    }
}
