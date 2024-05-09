// This file is copied from Mgmt-Governance-ResourcesCache repo (ARG)
// There should not be any modifications to fields copied to maintain backward compatibility, though additional fields can be added.
// 
// TODO Align the monitoring with ARG.
using System.Threading;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts
{
    /// <summary>
    /// Activity - target of instrumentation
    /// </summary>
    public partial interface IActivityMonitor
    {
        private static readonly AsyncLocal<IActivity?> _currentActivity = new AsyncLocal<IActivity?>();

        public static void SetCurrentActivity(IActivity? activity)
        {
            if (activity == BasicActivity.Null)
            {
                activity = null;
            }

            _currentActivity.Value = activity;
        }
    }
}