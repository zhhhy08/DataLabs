namespace SkuService.Common
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using SkuService.Common.DataProviders;
    using SkuService.Common.Extensions;
    using SkuService.Common.Utilities;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class ArmAdminConfigBackgroundService
    {
        private static readonly ActivityMonitorFactory ExecuteAsyncAsyncMonitorFactory = new("ArmAdminConfigBackgroundService.ExecuteAsync");
        private readonly IArmAdminDataProvider armAdminDataProvider;
        private readonly int configFetchIntervalInHours = 6;
        public ArmAdminConfigBackgroundService(IArmAdminDataProvider armAdminDataProvider)
        {
            this.armAdminDataProvider = armAdminDataProvider;
            if(ServiceRegistrations.GetCustomConfigDictionary.ContainsKey(Constants.ConfigFetchIntervalInHours))
            {
                ServiceRegistrations.GetCustomConfigDictionary.TryGetValue(Constants.ConfigFetchIntervalInHours, out string? configFetchInterval);
                _ = int.TryParse(configFetchInterval, out configFetchIntervalInHours);
            }
        }

        public async Task ExecuteAsync()
        {
            using var tokenSource = new CancellationTokenSource();
            var stoppingToken = tokenSource.Token;
            using var monitor = ExecuteAsyncAsyncMonitorFactory.ToMonitor();

            // Run once before starting the timer
            try
            {
                monitor.OnStart();
                await this.armAdminDataProvider.GetAndUpdateArmAdminConfigsAsync(stoppingToken);
                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
            }

            using var timer = new PeriodicTimer(TimeSpan.FromHours(configFetchIntervalInHours));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    monitor.OnStart();
                    await this.armAdminDataProvider.GetAndUpdateArmAdminConfigsAsync(stoppingToken);
                    monitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);
                }
            }
        }
    }
}
