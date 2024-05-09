namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Enums;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Exception;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// CAS Restrictions Response
    /// Placed in Partner package instead of Common project due to needing Nuget from ARG for InsensitiveDictionary
    /// </summary>
    public class CapacityRestrictionsResponse
    {
        [JsonRequired]
        [JsonConverter(typeof(StringEnumConverter))]
        public CapacityRestrictionsResponseCode ResponseCode { get; }

        public string? ErrorMessage { get; }

        public InsensitiveDictionary<RestrictionsInfo[]>? RestrictionsByOfferFamily { get; }

        public InsensitiveDictionary<string>? RestrictedSkuNamesToOfferFamily { get; }

        [JsonConstructor]
        public CapacityRestrictionsResponse(CapacityRestrictionsResponseCode responseCode, string? errorMessage = null, InsensitiveDictionary<RestrictionsInfo[]>? restrictionsByOfferFamily = null, InsensitiveDictionary<string>? restrictedSkuNamesToOfferFamily = null)
        {
            ResponseCode = responseCode;
            ErrorMessage = errorMessage;
            RestrictionsByOfferFamily = restrictionsByOfferFamily;
            RestrictedSkuNamesToOfferFamily = restrictedSkuNamesToOfferFamily;
        }

        public CapacityRestrictionsResponse(InsensitiveDictionary<RestrictionsInfo[]> restrictionsBySkuName, CapacityMetadata capacityMetadata, InsensitiveHashSet skusWithDistinctRestrictions, string provider)
        {
            Dictionary<string, SkuInfo> skuNameToSkuInfoMapping = GetSkuNameToSkuInfoMapping(capacityMetadata, provider);
            InsensitiveDictionary<string> insensitiveDictionary = new InsensitiveDictionary<string>();
            InsensitiveDictionary<RestrictionsInfo[]> insensitiveDictionary2 = new InsensitiveDictionary<RestrictionsInfo[]>();
            foreach (KeyValuePair<string, RestrictionsInfo[]> item in restrictionsBySkuName)
            {
                string key = item.Key;
                string empty = string.Empty;
                RestrictionsInfo[] value = item.Value;
                if (skusWithDistinctRestrictions != null && skusWithDistinctRestrictions.Contains(key))
                {
                    empty = "DISTINCT_" + key;
                }
                else
                {
                    if (!skuNameToSkuInfoMapping.TryGetValue(key, out var value2))
                    {
                        continue;
                    }

                    empty = value2.OfferFamily;
                }

                if (!insensitiveDictionary2.ContainsKey(empty))
                {
                    insensitiveDictionary2[empty] = value;
                }

                insensitiveDictionary[key] = empty;
            }

            ResponseCode = CapacityRestrictionsResponseCode.OK;
            RestrictedSkuNamesToOfferFamily = insensitiveDictionary;
            RestrictionsByOfferFamily = insensitiveDictionary2;
        }

        private Dictionary<string, SkuInfo> GetSkuNameToSkuInfoMapping(CapacityMetadata capacityMetadata, string provider)
        {
            Dictionary<string, Dictionary<string, SkuInfo>> resourceFamilyToOfferUnitToSkuInfoMapping = capacityMetadata.ResourceFamilyToOfferUnitToSkuInfoMapping;
            string text = CapacityStorageUtility.NormalizeResourceFamily(provider);
            if (!resourceFamilyToOfferUnitToSkuInfoMapping.ContainsKey(text))
            {
                throw new CapacityException("Invalid input: No offer mappings found for resourceFamily:" + text, CapacityExceptionErrorCode.NoOfferMappingForResourceFamily);
            }

            Dictionary<string, SkuInfo> dictionary = new Dictionary<string, SkuInfo>();
            foreach (KeyValuePair<string, SkuInfo> item in resourceFamilyToOfferUnitToSkuInfoMapping[text])
            {
                string key = CapacityStorageUtility.NormalizeOfferUnit(item.Key);
                dictionary[key] = item.Value;
            }

            if (capacityMetadata.Settings.EnableDedicatedHost && text.Equals("COMPUTE"))
            {
                string text2 = CapacityStorageUtility.NormalizeResourceFamily("COMPUTEDEDICATEDHOST");
                if (!resourceFamilyToOfferUnitToSkuInfoMapping.ContainsKey(text2))
                {
                    throw new CapacityException("Invalid input: No offer mappings found for resourceFamily:" + text2, CapacityExceptionErrorCode.NoOfferMappingForResourceFamily);
                }

                {
                    foreach (KeyValuePair<string, SkuInfo> item2 in resourceFamilyToOfferUnitToSkuInfoMapping[text2])
                    {
                        string key2 = CapacityStorageUtility.NormalizeOfferUnit(item2.Key);
                        dictionary[key2] = item2.Value;
                    }

                    return dictionary;
                }
            }

            return dictionary;
        }

        public InsensitiveDictionary<RestrictionsInfo[]> GetRestrictionsBySkuName()
        {
            if (ResponseCode != 0 || RestrictedSkuNamesToOfferFamily == null || RestrictionsByOfferFamily == null)
            {
                return new InsensitiveDictionary<RestrictionsInfo[]>();
            }

            InsensitiveDictionary<RestrictionsInfo[]> insensitiveDictionary = new InsensitiveDictionary<RestrictionsInfo[]>(RestrictedSkuNamesToOfferFamily.Count);
            foreach (KeyValuePair<string, string> item in RestrictedSkuNamesToOfferFamily)
            {
                string key = item.Key;
                string value = item.Value;
                if (RestrictionsByOfferFamily.TryGetValue(value, out var value2))
                {
                    insensitiveDictionary[key] = value2;
                }
            }

            return insensitiveDictionary;
        }
    }
}
