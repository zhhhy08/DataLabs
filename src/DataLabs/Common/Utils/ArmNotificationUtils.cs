namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// This class was copied from ARN repo and modified to reduce dependencies on other libararies and to add nullable annotations. https://msazure.visualstudio.com/One/_git/Mgmt-Governance-Notifications?path=/src/Libraries/AzureResourceManager/Utilities/ArmNotificationUtils.cs&version=GBmain&_a=contents
    /// Some additional utility functions have been added ontop of ParseEventType for solution use
    /// </summary>
    public class ArmNotificationUtils
    {
        #region Tracing

        private readonly static ActivityMonitorFactory ArmNotificationUtilsHandleCustomActionsSuffixesChange
            = new("ArmNotificationUtils.HandleCustomActionsSuffixesChange");

        #endregion

        #region Consts

        // If updating this, please find below usages of /* Delimiter */ 
        // And alter there too
        private const char Delimiter = '/';

        private const string WildcardResourceType = "*";

        #endregion

        #region Fields

        // In ARN repo implementation this was a hotconfigurable property. We should keep this in sync with ARN repo if we need to support additional actions.
        private static List<string> _customActionsSuffixes = new List<string>() { "action", "event" };

        #endregion

        #region Public Methods

        public static string GetResourceProviderNamespaceFromType(string resourceType)
        {
            GuardHelper.ArgumentNotNullOrEmpty(resourceType);

            return resourceType.SplitAndRemoveEmpty(Delimiter)[0];
        }

        // Note: One limitation of this method is that it cannot differentiate nested types from actions        
        public static ParsedEventType ParseEventType(string eventType)
        {
            // TODO 6206884: Sync with ARM and verify this method supports everything that ARM supports
            // Format of linked notification action:
            // Provider/ResourceType/action OR Provider/ResourceType/action/actionstring
            // OR
            // Provider/*/action (Added by ARN. Not Supported by ARM linked notifications)
            // Or 
            // Provider/*/* (Added by ARN. Not Supported by ARM linked notifications)
            // OR
            // */action OR */action/actionstring
            // Similar format for "action" we support for "event" (Added by ARN)
            // Provider/ResourceType/event/eventstring is supported
            // For Provider/ResourceType/eventstring we do not differentiate resource type from segment 1 of action
            GuardHelper.ArgumentNotNullOrEmpty(eventType);

            var trimmedLinkedNotificationAction = eventType.Trim(Delimiter);
            var linkedNotificationActionSplitList = trimmedLinkedNotificationAction.Split(Delimiter);

            // Length cannot be less than 2
            if (linkedNotificationActionSplitList.Length < 2)
            {
                throw new InvalidDataException($"Invalid linked notification action: {eventType}");
            }

            // If the length is 2, first item has to be a *
            if (linkedNotificationActionSplitList.Length == 2 && linkedNotificationActionSplitList[0] != WildcardResourceType)
            {
                throw new InvalidDataException($"Invalid linked notification action: {eventType}");
            }

            string? action = null;

            foreach (var customActionSuffix in _customActionsSuffixes)
            {
                // If it ends with "action" suffix, length cannot be less than 3
                if (linkedNotificationActionSplitList[linkedNotificationActionSplitList.Length - 1].Equals(customActionSuffix, StringComparison.OrdinalIgnoreCase) &&
                    linkedNotificationActionSplitList.Length < 3)
                {
                    throw new InvalidDataException($"Invalid linked notification action: {eventType}");
                }

                if (linkedNotificationActionSplitList[linkedNotificationActionSplitList.Length - 1].Equals(customActionSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    action = $"{linkedNotificationActionSplitList[linkedNotificationActionSplitList.Length - 2]}{Delimiter}{customActionSuffix}";
                    break;
                }
            }

            action ??= linkedNotificationActionSplitList[linkedNotificationActionSplitList.Length - 1];

            var startIndexForRemoval = trimmedLinkedNotificationAction.Length - action.Length - 1 /* Delimiter */;
            var resourceProviderNamespace = linkedNotificationActionSplitList[0];

            var resourceTypeNameWithProvider = trimmedLinkedNotificationAction.Remove(startIndexForRemoval);

            var resourceType = resourceTypeNameWithProvider.Contains(Delimiter) // false if */action
                ? resourceTypeNameWithProvider.Remove(0, resourceProviderNamespace.Length + 1 /* Delimiter */)
                : string.Empty;

            return new ParsedEventType
            {
                Action = action,
                ResourceType = resourceType,
                ResourceProviderNamespace = resourceProviderNamespace
            };
        }

        public static string ParseResourceTypeFromEventType(string eventType)
        {
            var parsedEventType = ParseEventType(eventType);
            var resourceTypeWithNamespace = parsedEventType.ResourceProviderNamespace + Delimiter + parsedEventType.ResourceType;
            return resourceTypeWithNamespace;
        }

        public static string GetResourceTypeFromResourceData(NotificationResourceDataV3<GenericResource> resourceData, string eventType)
        {
            GuardHelper.ArgumentNotNull(resourceData);
            return GetResourceType(resourceData.ResourceId, eventType, resourceData.ArmResource);
        }

        /// <summary>
        /// First attempts to extract resourceType (with namespace) from resourceId, then tries getting resourceType from eventType, finally falling back on armResource.Type
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="eventType"></param>
        /// <param name="armResource"></param>
        /// <returns></returns>
        public static string GetResourceType(string resourceId, string eventType, GenericResource armResource)
        {
            var resourceTypeFromResourceId = resourceId.GetResourceType(); 

            if (string.IsNullOrEmpty(resourceTypeFromResourceId))
            {
                var resourceTypeFromEventType = ParseResourceTypeFromEventType(eventType);

                //eventType is expected to always be populated correctly, but this fallback is added in case it isn't
                if(string.IsNullOrEmpty(resourceTypeFromEventType))
                {
                    return armResource.Type;
                }

                return resourceTypeFromEventType;
            }
            return resourceTypeFromResourceId;
        }


        #endregion
    }

    public class ParsedEventType
    {
        public const char Delimiter = '/';

        public const string WildcardProviderNamespace = "*";

        public const string WildcardResourceTypeName = "*";

        public const string WildcardAction = "*";

        public const string SnapshotAction = "snapshot";

        public required string ResourceProviderNamespace
        {
            get;
            set;
        }

        public required string ResourceType
        {
            get;
            set;
        }

        public required string Action
        {
            get;
            set;
        }

        public string GetResourceTypeWithNamespace()
        {
            return string.IsNullOrEmpty(ResourceType)
                ? ResourceProviderNamespace
                : $"{ResourceProviderNamespace}{Delimiter}{ResourceType}";
        }

        public bool IsSnapshotAction()
        {
            return string.Equals(this.Action, SnapshotAction, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
