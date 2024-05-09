namespace Microsoft.WindowsAzure.IdMappingService.Services.Contracts
{
    using Newtonsoft.Json;
    using System;
    using System.Linq;

    /// <summary>
    /// Represents information required for IdMapping to determine what the InternalIds are and how to parse them for a ResourceType
    /// </summary>
    public class InternalIdSpecification : IEquatable<InternalIdSpecification>
    {
        [JsonProperty(PropertyName = "paths", Required = Required.Always)]
        public InternalId[] InternalIdPaths { get; set; }

        /// <summary>
        /// Character used for concatenating InternalIds to form a CompositeInternalId. If null, no CompositeInternalId will indexed.
        /// </summary>
        [JsonProperty]
        public char? Delimiter { get; set; }
        
        /// <summary>
        /// When true, the InternalIdSpecification from the Manifest will be ignored and we will default to the specification from config. 
        /// Used primarily for types that are unable to easily update their manifest
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool OverrideManifest { get; set; }

        public string ResourceType { get; set; }

        public bool Equals(InternalIdSpecification other)
        {
            return other.InternalIdPaths.SequenceEqual(other.InternalIdPaths) 
                && Delimiter == other.Delimiter
                && OverrideManifest == other.OverrideManifest
                && ResourceType == other.ResourceType;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InternalIdPaths, Delimiter, OverrideManifest, ResourceType);
        }
    }

    public class InternalId
    {
        /// <summary>
        /// Path on the ArmResource to the field storing the internalId value 
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Path { get; set; }

        /// <summary>
        /// Name given for the InternalId, used as the public identifier of this internalId
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// Path on the ArmResource to the field storing the ArmId value (of another resource) to be used for this internalId mapping
        /// If null, the ArmId of the ArmResource will be used. If Non-null then either IsOptional should be true or FallbackToResourceArmId should be true.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string ArmIdPath { get; set; }

        /// <summary>
        /// Whether the internalId is expeced to always be parsed from the ArmResource. Defaults to false
        /// </summary>
        [JsonProperty(Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public bool IsOptional { get; set; }
        
        /// <summary>
        /// If an ArmIdPath is defined, this determines whether the ArmId of the ArmResource should be used as the ArmId for this internalId mapping if the ArmIdPath is not found. Defaults to false
        /// </summary>
        [JsonProperty(Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public bool FallbackToResourceArmId { get; set; }

        // <summary>
        /// This represents a resourceType (including RP Namespace) that is different from the resourceType of the parent resource. 
        /// This identifier is stored under the "index" of the overriden resourceType. This effects the keys used to store the identifier and how the identifier will be queried.
        /// Null if no override is set.
        /// </summary>
        [JsonProperty(Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public string OverrideResourceTypeIndex { get; set; }

        public override bool Equals(object obj)
        {
            return obj is InternalId id
               && Path == id.Path
               && Name == id.Name
               && ArmIdPath == id.ArmIdPath
               && IsOptional == id.IsOptional
               && OverrideResourceTypeIndex == id.OverrideResourceTypeIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Path, Name, ArmIdPath, OverrideResourceTypeIndex);
        }
    }
}
