// <copyright file="IPartnerBlobClient.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;

    public interface IPartnerBlobClient
    {
        Task<List<TResponse>> GetResourcesAsync<TResponse>(string uri, int retryFlowCount, CancellationToken cancellationToken) where TResponse : class;
    }
}