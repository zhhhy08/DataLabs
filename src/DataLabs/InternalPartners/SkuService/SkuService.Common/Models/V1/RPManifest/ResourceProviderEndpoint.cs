namespace SkuService.Common.Models.V1.RPManifest
{
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Microsoft.WindowsAzure.ResourceStack.Common.Storage;
    using Microsoft.WindowsAzure.ResourceStack.Frontdoor.Data.Entities.Registration;
    using Microsoft.WindowsAzure.ResourceStack.Providers.Common.Extensions;
    using Newtonsoft.Json;
    using SkuService.Common.Extensions;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using static SkuService.Common.Models.Enums;

    // TODO: Will remove this attribute once RP manifest is finalized
    [ExcludeFromCodeCoverage]
    public class ResourceProviderEndpoint : IEquatable<ResourceProviderEndpoint>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the endpoint is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets the <c>api</c> version supported by the endpoint.
        /// </summary>
        public string ApiVersion { get; set; } = default!;

        /// <summary>
        /// Gets or sets the <c>api</c> versions supported by the endpoint.
        /// </summary>
        public string[] ApiVersions { get; set; } = default!;

        /// <summary>
        /// Gets or sets the endpoint uri.
        /// </summary>
        public string EndpointUri { get; set; } = default!;

        /// <summary>
        /// Gets or sets the supported locations.
        /// </summary>
        public string[] Locations { get; set; } = default!;

        /// <summary>
        /// Gets or sets the zones.
        /// </summary>
        public string[] Zones { get; set; } = default!;

        /// <summary>
        /// Gets or sets the required features.
        /// </summary>
        public string[] RequiredFeatures { get; set; } = default!;

        /// <summary>
        /// Gets or sets the features rule.
        /// </summary>
        public FeaturesRule FeaturesRule { get; set; } = default!;

        /// <summary>
        /// Gets or sets the DSTS configuration.
        /// </summary>
        public DstsConfiguration DstsConfiguration { get; set; } = default!;

        /// <summary>
        /// Gets or sets the token auth configuration.
        /// </summary>
        public TokenAuthConfiguration TokenAuthConfiguration { get; set; } = default!;

        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the SKU link.
        /// </summary>
        public string SkuLink { get; set; } = default!;

        /// <summary>
        /// Gets or sets the endpoint type.
        /// </summary>
        public EndpointType? EndpointType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the management options.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public ResourceManagementOptions ResourceManagementOptions { get; set; } = default!;

        /// <summary>
        /// Gets the <c>api</c> versions for the end point
        /// </summary>
        public string[] GetApiVersions(string[] commonApiVersions, CommonApiVersionsMergeMode MergeType)
        {
            if (MergeType == CommonApiVersionsMergeMode.Overwrite)
            {
                if (this.ApiVersion == null && (this.ApiVersions == null || ApiVersions.Length == 0))
                {
                    return commonApiVersions;
                }
                else
                {
                    return this.ApiVersion == null
                        ? this.ApiVersions
                        : this.ApiVersions.ConcatArray(this.ApiVersion);
                }
            }
            var endpointApiVersions = this.ApiVersions.CoalesceEnumerable().ConcatArray(commonApiVersions.CoalesceEnumerable());
            return this.ApiVersion == null
                ? endpointApiVersions
                : endpointApiVersions.ConcatArray(this.ApiVersion);
        }

        /// <summary>
        /// Tests the endpoint for equality.
        /// </summary>
        /// <param name="other">endpoint to test against.</param>
        public bool Equals(ResourceProviderEndpoint? other)
        {
            return this.Enabled == other!.Enabled &&
                this.EndpointUri.EqualsInsensitively(other.EndpointUri) &&
                this.GetApiVersions(null!, CommonApiVersionsMergeMode.Merge).UnorderedSetEqualsInsensitively(other.GetApiVersions(null!, CommonApiVersionsMergeMode.Merge)) &&
                this.RequiredFeatures.CoalesceEnumerable().UnorderedSetEqualsInsensitively(other.RequiredFeatures.CoalesceEnumerable()) &&
                this.Locations.CoalesceEnumerable().SelectArray(location => StorageUtility.NormalizeLocationForStorage(location))
                    .UnorderedSetEqualsInsensitively(other.Locations.CoalesceEnumerable().SelectArray(location => StorageUtility.NormalizeLocationForStorage(location))) &&
                this.EndpointType == other.EndpointType &&
                this.FeaturesRule?.RequiredFeaturesPolicy == other.FeaturesRule?.RequiredFeaturesPolicy;
        }

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a ResourceProviderEndpoint object, have the same value.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        public override bool Equals(object? obj)
        {
            return obj is ResourceProviderEndpoint other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            var hashCode = 0;
            var endpointType = this.EndpointType ?? Enums.EndpointType.NotSpecified;

            foreach (var apiVersion in this.GetApiVersions(null!, CommonApiVersionsMergeMode.Merge).ToInsensitiveHashSet())
            {
                hashCode ^= apiVersion.GetHashCodeInvariantIgnoreCase();
            }

            foreach (var requiredFeature in this.RequiredFeatures.CoalesceEnumerable().ToInsensitiveHashSet())
            {
                hashCode ^= requiredFeature.GetHashCodeInvariantIgnoreCase();
            }

            var normalizedLocations = this.Locations.CoalesceEnumerable().SelectArray(location => StorageUtility.NormalizeLocationForStorage(location)).ToInsensitiveHashSet();
            foreach (var location in normalizedLocations)
            {
                hashCode ^= location.GetHashCode();
            }

            return this.Enabled.GetHashCode() ^
                this.EndpointUri.CoalesceString().GetHashCodeInvariantIgnoreCase() ^
                hashCode ^
                endpointType.GetHashCode() ^
                (this.FeaturesRule?.RequiredFeaturesPolicy ?? FeaturesPolicy.All).GetHashCode();
        }

        /// <summary>
        /// Tests the endpoint for equality.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="second">The second.</param>
        public static bool Equals(ResourceProviderEndpoint first, ResourceProviderEndpoint second)
        {
            if (first == second)
            {
                return true;
            }

            return first != null && first.Equals(second);
        }
    }
}
