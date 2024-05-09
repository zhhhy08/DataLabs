namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /*
     *  RegionConfigManager manages the primary and backup region configs for the paired regions.
     *  Primary region - region where the service is currently deployed.
     *  Backup region - region for which the current region is configured as backup according to the paired region config 
     *  Each region gets initialized at startup with the necessary access for notifications processed for the primary region and
     *  notifications processed for the backup region
     */

    public static class RegionConfigManager
    {
        private static Dictionary<string, RegionConfig> regionPairToConfigMap = new();
        private static string? primaryRegionName;
        private static string? backupRegionName;

        private static HashBlobNameProvider? _hashBlobNameProvider;
        private static FixedBlobContainerProviderFactory? _fixedBlobContainerProviderFactory;

        private static bool isInitialized = false;

        //This is used for unit tests
        public static void Initialize(IConfiguration configuration, RegionConfig primaryRegionConfig, RegionConfig backupRegionConfig)
        {
            primaryRegionName = configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);
            backupRegionName = configuration.GetValue<string>(SolutionConstants.BackupRegionName);

            GuardHelper.ArgumentNotNullOrEmpty(primaryRegionName);
            GuardHelper.ArgumentNotNullOrEmpty(backupRegionName);

            regionPairToConfigMap[primaryRegionName] =  primaryRegionConfig;
            regionPairToConfigMap[backupRegionName] = backupRegionConfig;
            isInitialized = true;
        }

        public static void Initialize(IConfiguration configuration, CancellationToken cancellationToken)
        {
            primaryRegionName = configuration.GetValue<string>(SolutionConstants.PrimaryRegionName);
            backupRegionName = configuration.GetValue<string>(SolutionConstants.BackupRegionName);

            GuardHelper.ArgumentNotNullOrEmpty(primaryRegionName);
            GuardHelper.ArgumentNotNullOrEmpty(backupRegionName);

            var useSourceOfTruth = configuration.GetValue<bool>(SolutionConstants.UseSourceOfTruth);

            var sourceOfTruthStorageAccountNamesPrimary = string.Empty;
            var sourceOfTruthStorageAccountNamesBackup = string.Empty;

            if (useSourceOfTruth)
            {
                sourceOfTruthStorageAccountNamesPrimary = configuration.GetValue<string>(SolutionConstants.BlobStorageAccountNames);
                sourceOfTruthStorageAccountNamesBackup = configuration.GetValue<string>(SolutionConstants.BackupBlobStorageAccountNames);

                _fixedBlobContainerProviderFactory = new();
                _hashBlobNameProvider = new();

                GuardHelper.ArgumentNotNullOrEmpty(sourceOfTruthStorageAccountNamesPrimary);
                GuardHelper.ArgumentNotNullOrEmpty(sourceOfTruthStorageAccountNamesBackup);

            }

            regionPairToConfigMap[primaryRegionName] =
                new RegionConfig
                {
                    RegionLocationName = primaryRegionName,
                    sourceOfTruthStorageAccountNames = sourceOfTruthStorageAccountNamesPrimary,
                    outputBlobClient = GetOutputBlobClient(sourceOfTruthStorageAccountNamesPrimary, useSourceOfTruth, configuration, cancellationToken)
                };
            regionPairToConfigMap[backupRegionName] =
                new RegionConfig
                {
                    RegionLocationName = backupRegionName,
                    sourceOfTruthStorageAccountNames = sourceOfTruthStorageAccountNamesBackup,
                    outputBlobClient = GetOutputBlobClient(sourceOfTruthStorageAccountNamesBackup, useSourceOfTruth, configuration, cancellationToken)
                };

            GuardHelper.IsArgumentEqual(regionPairToConfigMap.Count, 2);
            if (useSourceOfTruth)
            {
                foreach (var item in regionPairToConfigMap.Values)
                {
                    GuardHelper.ArgumentNotNull(item.outputBlobClient);
                }
            }
            isInitialized = true;
        }

        public static RegionConfig GetRegionConfig(string regionPairName)
        {
            GuardHelper.ArgumentConstraintCheck(isInitialized, "RegionConfigManager is not initialized");

            var regionName = string.IsNullOrEmpty(regionPairName) ? primaryRegionName : regionPairName;

            GuardHelper.ArgumentNotNullOrEmpty(regionName);
            
            if (regionPairToConfigMap.TryGetValue(regionName, out var config))
            {
                return config;
            }

            throw new NotSupportedException();
        }

        public static bool IsBackupRegionPairName(string regionPairName)
        {
             return regionPairName == backupRegionName;
        }

        private static IOutputBlobClient? GetOutputBlobClient(string sourceOfTruthStorageAccountNames, bool useSourceOfTruth, IConfiguration configuration, CancellationToken cancellationToken)
        {
            if (!useSourceOfTruth)
            {
                return null;
            }
            GuardHelper.ArgumentNotNullOrEmpty(sourceOfTruthStorageAccountNames);
            GuardHelper.ArgumentNotNull(_fixedBlobContainerProviderFactory);
            GuardHelper.ArgumentNotNull(_hashBlobNameProvider);
            var storageAccountSelector = new HashBasedStorageAccountSelector(_fixedBlobContainerProviderFactory, sourceOfTruthStorageAccountNames, cancellationToken);
            return new OutputBlobClient(storageAccountSelector, _hashBlobNameProvider, configuration);
        }
    }
}