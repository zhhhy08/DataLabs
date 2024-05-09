// <copyright file="PartnerBlobClientExtensions.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public static class PartnerBlobClientExtensions
    {
        public static IServiceCollection AddPartnerBlobClient(this IServiceCollection services)
        {
            services.TryAddSingleton<IPartnerBlobClient, PartnerBlobClient>();
            return services;
        }
    }
}
