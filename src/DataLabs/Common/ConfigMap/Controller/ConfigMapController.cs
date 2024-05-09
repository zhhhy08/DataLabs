namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap.Controller
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;

    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("admin/common/[action]")]
    public class ConfigMapController
    {
        ActivityMonitorFactory ConfigMapControllerGetConfiguration = new("ConfigMapController.GetConfiguration");

        [HttpGet]
        [ActionName("getconfiguration")]
        public string GetConfiguration(string configkey)
        {
            using var monitor = ConfigMapControllerGetConfiguration.ToMonitor();
            monitor.Activity["configKey"] = configkey;
            
            var result = ConfigMapUtil.Configuration.GetValue<string>(configkey) ?? "[empty]";
            monitor.Activity["configValueIsEmpty"] = result == "[empty]";
            monitor.OnCompleted();

            return result;
        }
    }
}
