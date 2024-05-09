namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ResourceFetcherClient
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMAdminClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ARMClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.CasClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.QFDClient;

    public interface IResourceFetcherClient : IARMClient, IARMAdminClient, IQFDClient, ICasClient, IDisposable
    {
    }
}
