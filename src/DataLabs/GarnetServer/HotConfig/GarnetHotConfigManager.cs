namespace Microsoft.WindowsAzure.Governance.DataLabs.GarnetServer.HotConfigUtils
{
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Newtonsoft.Json;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    /*
     * This class will have to be implemented through jarvis action
     * However, due to timeline of Sku team, let's do temporary below class to unblock SKU Team
     */
    [ExcludeFromCodeCoverage]
    internal class GarnetHotConfigManager
    {
        private static readonly ILogger<GarnetHotConfigManager> Logger = 
            DataLabLoggerFactory.CreateLogger<GarnetHotConfigManager>();

        private readonly int _cachePort;

        /* This class need to be created after all server up and running, so that it can update the cache with hot config */
        public GarnetHotConfigManager()
        {
            var cachePortEnv = Environment.GetEnvironmentVariable(GarnetConstants.CACHE_SERVICE_PORT) ?? "3278";
            _cachePort = int.Parse(cachePortEnv);

            var initHotConfigVal = ConfigMapUtil.Configuration.GetValueWithCallBack(
                GarnetConstants.HotConfigActions, UpdateHotConfigActions, string.Empty);

            if (!string.IsNullOrWhiteSpace(initHotConfigVal))
            {
                // Let's call UpdateHotConfig after one minute delay
                _ = Task.Run(() => Task.Delay(TimeSpan.FromSeconds(60))
                    .ContinueWith((antecedent, info) => UpdateHotConfigActions((string)info), initHotConfigVal,
                    TaskContinuationOptions.None));
            }
        }

        private async Task UpdateHotConfigActions(string hotConfig)
        {
            try
            {
                if (string.IsNullOrEmpty(hotConfig))
                {
                    Logger.LogWarning("Hot config string is empty or null");
                    return;
                }

                var settings = JsonConvert.DeserializeObject<GarnetHotConfigModel>(hotConfig);
                if (settings == null || settings.SortedSetAdd == null)
                {
                    Logger.LogWarning("Hot config settings is null");
                    return;
                }

                using var connection = ConnectionMultiplexer.Connect("127.0.0.1:" + _cachePort);
                var database = connection.GetDatabase();

                foreach (var setting in settings.SortedSetAdd)
                {
                    var cacheKey = setting.Key;
                    var sortedSetEntries = new List<SortedSetEntry>();
                    foreach (var value in setting.Value)
                    {
                        sortedSetEntries.Add(new SortedSetEntry(value, 0));
                    }

                    await database.SortedSetAddAsync(cacheKey, [.. sortedSetEntries]);
                    Logger.LogWarning("Successfully updated SortedSetAdd with hot config. Key: {cacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to update cache with hot config");
            }
        }
    }

    class GarnetHotConfigModel
    {
        public IDictionary<string, List<string>> SortedSetAdd { get; set; }
    }
}
