/*
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Middlewares;
using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.ResourceProviders;
using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.RestApi;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceFetcherService.Middlewares
{
    [TestClass]
    public class AADTokenAuthMiddlewareTests
    {
        private static string abcSolution = "abcSolution";
        private static string abcSolutionClientId = Guid.NewGuid().ToString();
        private static string mappings = "microsoft.recoveryservices/vaults/replicationpolicies:ARM|2023-01-01;microsoft.compute/virtualmachines:ARM|2022-11-01";

        [TestInitialize]
        public void SetupConfig()
        {
            ConfigMapUtil.Reset();

            var appSettingsStub = new Dictionary<string, string>
            {
                { ConfigMapConstants.PartnerNamesConfigKey, abcSolution},
                { abcSolution + ConfigMapConstants.PartnerClientIdConfigSuffix, abcSolutionClientId},
                { abcSolution + ConfigMapConstants.PartnerResourceTypeMappingsConfigKeySuffix, mappings}
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
        }

        [TestMethod]
        public async Task TestAuthenticationMiddleware_SwaggerPath()
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                    .ConfigureServices(configureServices =>
                    {
                        configureServices.AddSingleton<IResourceProviderFactory>(c => new ResourceProviderFactory(null, null, null, null));
                    })
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseMiddleware<AADTokenAuthMiddleware>();
                    });
                })
                .StartAsync();

            var response = await host.GetTestClient().GetAsync("/swagger");

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [DataTestMethod]
        [DataRow(null, "", DisplayName = "TestAuthenticationMiddleware_HeaderPartnerNameNull")]
        [DataRow("", "", DisplayName = "TestAuthenticationMiddleware_HeaderPartnerNameEmpty")]
        [DataRow("ABCPartner", null, DisplayName = "TestAuthenticationMiddleware_HeaderAuthorizationNull")]
        [DataRow("ABCPartner", "", DisplayName = "TestAuthenticationMiddleware_HeaderAuthorizationEmpty")]
        public async Task TestAuthenticationMiddleware_EmptyHeaders(
            string partnerName,
            string token)
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                    .ConfigureServices(configureServices =>
                    {
                        configureServices.AddSingleton<IResourceProviderFactory>(c => new ResourceProviderFactory(null, null, null, null));
                    })
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseMiddleware<AADTokenAuthMiddleware>();
                    });
                })
                .StartAsync();

            var server = host.GetTestServer();
            server.BaseAddress = new Uri("https://awesomebaseaddress/A");

            var context = await server.SendAsync(c =>
            {
                c.Request.Method = "GET";
                c.Request.Headers.Add(CommonHttpHeaders.HeaderPartnerName, partnerName);
                c.Request.Headers.Add(CommonHttpHeaders.HeaderAuthorization, token);
            });

            Assert.AreEqual((int)HttpStatusCode.Forbidden, context.Response.StatusCode);
        }

        [DataTestMethod]
        [DataRow(
            "PQR",
            "ABC",
            "XYZ",
            "abcSolution",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJBQkMiLCJhdWQiOiJYWVoiLCJhcHBpZCI6IkRFRiJ9.Ajk8Q5Ei6OPZqZ4ezx4phPIvO9zd1uP3tYziFCkIBkg",
            DisplayName = "TestAuthenticationMiddleware_AppIdMismatch")]
        [DataRow(
            "DEF",
            "PQR",
            "XYZ",
            "abcSolution",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJBQkMiLCJhdWQiOiJYWVoiLCJhcHBpZCI6IkRFRiJ9.Ajk8Q5Ei6OPZqZ4ezx4phPIvO9zd1uP3tYziFCkIBkg",
            DisplayName = "TestAuthenticationMiddleware_IssuerMismatch")]
        [DataRow(
            "DEF",
            "ABC",
            "PQR",
            "abcSolution",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJBQkMiLCJhdWQiOiJYWVoiLCJhcHBpZCI6IkRFRiJ9.Ajk8Q5Ei6OPZqZ4ezx4phPIvO9zd1uP3tYziFCkIBkg",
            DisplayName = "TestAuthenticationMiddleware_AudienceMismatch")]
        [DataRow(
            "DEF",
            "ABC",
            "XYZ",
            "abcSolution",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.Et9HFtf9R3GEMA0IICOfFMVXY7kkTX1wr4qCyhIf58U",
            DisplayName = "TestAuthenticationMiddleware_EmptyClaims")]
        public async Task TestAuthenticationMiddleware_TokenValidation(
            string expectedAppId,
            string expectedIssuer,
            string expectedAudience,
            string partnerName,
            string token)
        {
            var appSettingsStub = new Dictionary<string, string>
                {
                    { ConfigMapConstants.PartnerNamesConfigKey, abcSolution},
                    { abcSolution + ConfigMapConstants.PartnerClientIdConfigSuffix, expectedAppId},
                    { abcSolution + ConfigMapConstants.PartnerResourceTypeMappingsConfigKeySuffix, mappings},
                    { ConfigMapConstants.AADTokenIssuer, expectedIssuer },
                    { ConfigMapConstants.AADTokenAudience, expectedAudience }
                };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);

            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                    .ConfigureServices(configureServices =>
                    {
                        configureServices.AddSingleton<IResourceProviderFactory>(c => new ResourceProviderFactory(null, null, null, null));
                    })
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseMiddleware<AADTokenAuthMiddleware>();
                    });
                })
                .StartAsync();

            var server = host.GetTestServer();
            server.BaseAddress = new Uri("https://awesomebaseaddress/A");

            var context = await server.SendAsync(c =>
            {
                c.Request.Method = "GET";
                c.Request.Headers.Add(CommonHttpHeaders.HeaderPartnerName, partnerName);
                c.Request.Headers.Add(CommonHttpHeaders.HeaderAuthorization, token);
            });

            Assert.IsNotNull(context.Response.Body);
            Assert.AreEqual((int)HttpStatusCode.Forbidden, context.Response.StatusCode);

            ConfigMapUtil.Reset();
        }
    }
}
*/