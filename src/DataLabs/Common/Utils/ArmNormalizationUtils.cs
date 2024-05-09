namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// ArmNormalizationUtils
    /// </summary>
    public static class ArmNormalizationUtils
    {
        /// <summary>
        /// Normalizes the type.
        /// </summary>
        /// <param name="type">The type.</param>
        public static string? NormalizeType(string? type)
        {
            return string.IsNullOrEmpty(type) ? type : type.ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes the location.
        /// </summary>
        /// <param name="location">The location.</param>
        public static string? NormalizeLocation(string? location)
        {
            return string.IsNullOrEmpty(location) ? location : location.Replace(" ", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes the guid to all lower case, hyphen format
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        public static string? NormalizeGuid(string? guid)
        {
            Guid guidObj;
            if (string.IsNullOrEmpty(guid) || !Guid.TryParse(guid, out guidObj))
            {
                return guid;
            }

            return guidObj.ToString("D");
        }

        /// <summary>
        /// Normalizes the resource group.
        /// </summary>
        /// <param name="resourceGroup">The resource group.</param>
        public static string? NormalizeResourceGroup(string? resourceGroup)
        {
            return string.IsNullOrEmpty(resourceGroup) ? resourceGroup : resourceGroup.ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes an ARM resource id (or a prefix of a resource id) to a canonical uppercase representation
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        public static string NormalizeId(string resourceId)
        {
            return string.IsNullOrEmpty(resourceId) ? resourceId : resourceId.ToUpperInvariant();
        }

        /// <summary>
        /// Normalizes an ARM resource id (or a prefix of a resource id) to a canonical lowercase representation
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        public static string NormalizeIdToLower(string resourceId)
        {
            return string.IsNullOrEmpty(resourceId) ? resourceId : resourceId.ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes the string.
        /// </summary>
        /// <param name="str">The string.</param>
        public static string NormalizeString(string str)
        {
            return string.IsNullOrEmpty(str) ? str : str.ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes the list of string.
        /// </summary>
        /// <param name="list">The list of string.</param>
        public static IList<string> NormalizeList(IList<string> list)
        {
            GuardHelper.ArgumentNotNull(list);

            return list.Select(item => NormalizeString(item)).ToList();
        }
    }
}
