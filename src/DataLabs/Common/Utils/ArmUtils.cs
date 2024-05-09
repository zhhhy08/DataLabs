// <copyright file="ArmUtils.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;

    /// <summary>
    /// ARM utils.
    /// </summary>
    public class ArmUtils
    {
        /*
        /// <summary>
        /// Gets the object type from the resource Id provided.
        /// Resource Id - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/
        /// {resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}
        /// Object type - {resourceProviderNamespace}/{typeName1}/{typeName2}
        /// Resource Id - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/
        /// {resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/providers/{resourceProviderNamespace2}/{typeName3}/{name3}
        /// Object type - {resourceProviderNamespace1}/{typeName1}/{typeName2}/{resourceProviderNamespace2}/{typeName3}
        /// </summary>
        /// <param name="resourceId">Resource Id.</param>
        /// <param name="failureComponent">Failure component.</param>
        /// <returns>Object type.</returns>
        /// <exception cref="ErrorResponseMessageException">If object type is null or empty.
        /// </exception>
        public static string GetFullResourceType(
            string resourceId,
            string failureComponent)
        {
            var resultResourceType = String.Empty;

            // We have to consider string case. /Providers/ and /providers/ and /PROVIDERS/ are same
            int firstIndex = resourceId.IndexOf(SolutionConstants.ProvidersPath, 0, StringComparison.OrdinalIgnoreCase);
            if (firstIndex < 0)
            {
                // If resource Id does not contain '/providers' in the path.
                // Let's use ARG resourceId extension to align with same result
                resultResourceType = resourceId.GetResourceType();
                resultResourceType = string.IsNullOrEmpty(resultResourceType) ? String.Empty : resultResourceType;
            }
            else
            {
                // we have /providers/
                var sb = new StringBuilder();

                ReadOnlySpan<char> resourceIdSpan = resourceId.AsSpan();
                int previousProvidersIndex = firstIndex + SolutionConstants.ProvidersPath.Length;
                int nextProvidersIndex;
                while ((nextProvidersIndex = resourceId.IndexOf(SolutionConstants.ProvidersPath, previousProvidersIndex, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    // previousProvidersIndex .. nextProvidersIndex(not inclusive)
                    AddKeySegment(resourceIdSpan, previousProvidersIndex, nextProvidersIndex, '/', sb);

                    previousProvidersIndex = nextProvidersIndex + SolutionConstants.ProvidersPath.Length;
                    if (previousProvidersIndex >= resourceId.Length)
                    {
                        break;
                    }
                }

                if (previousProvidersIndex < resourceId.Length)
                {
                    AddKeySegment(resourceIdSpan, previousProvidersIndex, resourceId.Length, '/', sb);
                }

                resultResourceType = sb.Length == 0 ? String.Empty : sb.ToString();
            }

            if (resultResourceType.Length > 0 && resultResourceType[resultResourceType.Length - 1] == '/')
            {
                resultResourceType = resultResourceType.Substring(0, resultResourceType.Length - 1);
            }

            if (resultResourceType.Length == 0)
            {
                string errorMessage = string.Format(
                    ErrorResponseMessages.IncorrectResourceId,
                    resourceId);

                throw new ErrorResponseMessageException(
                    httpStatus: HttpStatusCode.BadRequest,
                    errorCode: ErrorResponseCode.BadRequest,
                    errorMessage: errorMessage,
                    failureComponent: failureComponent);
            }

            return resultResourceType.ToLowerInvariant();
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? GetTenantId(EventGridNotification<NotificationDataV3<GenericResource>> eventGridNotification)
        {
            var notificationData = eventGridNotification?.Data;
            if (notificationData == null)
            {
                return null;
            }

            if (notificationData.Resources?.Count == 1)
            {
                var resourceData = notificationData.Resources[0];
                if (!string.IsNullOrWhiteSpace(resourceData.HomeTenantId))
                {
                    return resourceData.HomeTenantId;
                }
                if (!string.IsNullOrWhiteSpace(resourceData.ResourceHomeTenantId))
                {
                    return resourceData.ResourceHomeTenantId;
                }
            }

            if (!string.IsNullOrWhiteSpace(notificationData.HomeTenantId))
            {
                return notificationData.HomeTenantId;
            }
            if (!string.IsNullOrWhiteSpace(notificationData.ResourceHomeTenantId))
            {
                return notificationData.ResourceHomeTenantId;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTimeOffset GetEventTime(EventGridNotification<NotificationDataV3<GenericResource>> eventGridNotification)
        {
            var notificationData = eventGridNotification?.Data;
            if (notificationData == null)
            {
                return default;
            }

            if (notificationData.Resources?.Count == 1)
            {
                var resourceData = notificationData.Resources[0];
                var eventTime = resourceData.ResourceEventTime;
                if (eventTime.HasValue)
                {
                    return eventTime.Value;
                }
            }
            return eventGridNotification!.EventTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? GetAction(string? eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                return null;
            }

            var endIdx = eventType.Length - 1;

            // Trim ending '/'
            while (endIdx >= 0 && eventType[endIdx] == '/')
            {
                endIdx--;
            }

            if (endIdx < 0)
            {
                return null;
            }

            var lastSlashIdx = eventType.LastIndexOf('/', endIdx);
            if (lastSlashIdx < 0)
            {
                // There is no slash, return whole string (after trimming ending '/')
                return eventType.Substring(0, endIdx+1).ToLowerInvariant();
            }

            // We find the last forward slash
            var length = endIdx - lastSlashIdx;
            if (length <= 0)
            {
                return null;
            }

            var span = eventType.AsSpan(lastSlashIdx+1, length);

            // Some action could end with "/action". we consider it
            if (lastSlashIdx - 1 >= 0 && 
                span.Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                // one more search
                lastSlashIdx = eventType.LastIndexOf('/', lastSlashIdx - 1);
            }

            if (lastSlashIdx < 0)
            {
                // There is no valid slash, return whole string (after trimming ending '/')
                return eventType.Substring(0, endIdx + 1).ToLowerInvariant();
            }

            length = endIdx - lastSlashIdx;
            if (length <= 0)
            {
                return null;
            }
            return  eventType.Substring(lastSlashIdx + 1, length).ToLowerInvariant();
        }


        // For collection, provided resource id doesn't have resource name so we need to add dummy resource name to get resource type
        public static string? GetResourceTypeForCollectionCall(string? resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            // For collection, provided resource id doesn't have resource name so we need to add dummy resource name to get resource type
            if (resourceId[^1] == '/')
            {
                resourceId += "dummy";
            }
            else
            {
                resourceId += "/dummy";
            }

            return GetResourceType(resourceId);
        }

        //
        // Fast parsing for resource Type
        // Return resource Type is lowerCase
        // 
        public static string? GetResourceType(string? resourceId, bool providersTypeOnly = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            // We have to consider string case. /Providers/ and /providers/ and /PROVIDERS/ are same
            int lastProvidersIndex = resourceId.LastIndexOf(SolutionConstants.ProvidersPath, StringComparison.OrdinalIgnoreCase);
            if (lastProvidersIndex < 0)
            {
                if (providersTypeOnly)
                {
                    return null;
                }

                // If resource Id does not contain '/providers' in the path.
                // Let's use ARG resourceId extension to align with same result
                var retResourceType = resourceId.GetResourceType();
                return string.IsNullOrEmpty(retResourceType) ? null : retResourceType.ToLowerInvariant();
            }
            else
            {
                // we have /providers/
                var result = new StringBuilder();
                ReadOnlySpan<char> resourceIdSpan = resourceId.AsSpan();
                int previousProvidersIndex = lastProvidersIndex + SolutionConstants.ProvidersPath.Length;
                AddKeySegment(resourceIdSpan, previousProvidersIndex, resourceId.Length, '/', result);
                return result.Length == 0 ? null : result.ToString().ToLowerInvariant();
            }
        }

        // span[startIndex] should not start with delimiter
        private static void AddKeySegment(ReadOnlySpan<char> span, int startIndex, int endIndexNotInclusive, char delimiter, StringBuilder sb)
        {
            if (startIndex >= span.Length)
            {
                return;
            }

            if (endIndexNotInclusive > span.Length)
            {
                endIndexNotInclusive = span.Length;
            }

            if (span[startIndex] == delimiter)
            {
                startIndex++;
            }

            int numDelimiter = 0;
            int prevIndex = startIndex;
            for (int i = startIndex; i < endIndexNotInclusive; i++)
            {
                if (span[i] == delimiter)
                {
                    var delimiterIndex = i;
                    // special checking for repeated delimiter
                    while (i + 1 < endIndexNotInclusive && span[i + 1] == delimiter)
                    {
                        i++;
                    }
                    
                    if (numDelimiter++ == 0)
                    {
                        // First segement is ProviderNameSpace
                        // Let's add
                        if (sb.Length > 0)
                        {
                            sb.Append(delimiter);
                        }
                        var slice = span[prevIndex..delimiterIndex];
                        sb.Append(slice);
                        prevIndex = i + 1;
                        continue;
                    }

                    // We found delimiter
                    // Let's add/flush previous segment
                    if (numDelimiter % 2 == 0)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(delimiter);
                        }
                        var slice = span[prevIndex..delimiterIndex];
                        sb.Append(slice);
                    }else
                    {
                        prevIndex = i + 1;
                    }
                }
            }
        }

        /// <summary>
        /// Unformated ARM Id.
        /// </summary>
        /// <param name="id">ARM Id.</param>
        /// <param name="separator">Separator string.</param>
        /// <returns>Unformated ARM Id.</returns>
        private static string[] UnformatArmId(string id, string separator)
        {
            string[] tokens = id.Split(
                separator,
                StringSplitOptions.RemoveEmptyEntries);

            return tokens;
        }
    }
}
