namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.AspNetCore.Builder;
    using System.Net;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Middlewares;
    using Microsoft.AspNetCore.Connections;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils;
    using Microsoft.OpenApi.Models;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.SwaggerGenerationFilters;

    /// <summary>
    /// Geneva Actions Handler entry point
    /// </summary>
    [ExcludeFromCodeCoverage]
    class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigMapUtil.Reset();
            ConfigMapUtil.Initialize(builder.Configuration);

            builder.Services.AddSingleton<IConfiguration>(ConfigMapUtil.Configuration);
            builder.Services.AddSingleton<IConfigurationWithCallBack>(ConfigMapUtil.Configuration);

            builder.Services.AddSingleton<HttpClient>(new HttpClient(new SocketsHttpHandler
            {
                ConnectTimeout = RestClientOptions.DefaultConnectTimeout,
                PooledConnectionIdleTimeout = RestClientOptions.DefaultPooledConnectionIdleTimeout,
                PooledConnectionLifetime = RestClientOptions.DefaultPooledConnectionLifetime,
                MaxConnectionsPerServer = RestClientOptions.DefaultMaxConnectionsPerServer
            }));

            // Adding KubernetesProvider for interacting with kube api server
            builder.Services.AddKubernetesClient();

            // Add services to the  container.
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            if (builder.Environment.IsEnvironment("Local"))
            {
                var serviceName = "datalabs";
                builder.Services.AddSwaggerGen(c =>
                {
                    // NOTE: The order of the filters is important; be careful when you reorder them!
                    c.SwaggerDoc(serviceName, new OpenApiInfo { Title = serviceName, Version = serviceName });
                    c.EnableAnnotations();
                    //Add the request body filter to correctly generate the v2 parameter names.
                    c.RequestBodyFilter<SetBodyNameExtension>();
                    c.OperationFilter<CancellationTokenOperationFilter>();
                    c.OperationFilter<UpdateOperationsAndParametersFilter>();
                    c.OperationFilter<DataClassificationOperationFilter>();
                    c.CustomSchemaIds((type) => type.FullName);

                    // We want to have drop down without ref but enum values.
                    c.UseInlineDefinitionsForEnums();
                    c.DocumentFilter<DeleteUnneededDocumentFilter>();
                });

                // Always generate swagger in json format
                // This should be added at the end.
                builder.Services.AddSwaggerGenNewtonsoftSupport();
            }

            if (!builder.Environment.IsEnvironment("Local"))
            {
                builder.WebHost
                .UseKestrel(options =>
                {
                    int port = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.GenevaActionsHandlerDefaultPort);
                    var httpsConnectionAdapterOptions = new HttpsConnectionAdapterOptions()
                    {
                        ClientCertificateMode = ClientCertificateMode.AllowCertificate,
                        ServerCertificateSelector = (ConnectionContext? context, string? host) =>
                        {
                            var certificateName = ConfigMapUtil.Configuration.GetValue(SolutionConstants.GenevaActionDstsCertificateName, "");
                            GuardHelper.ArgumentNotNullOrEmpty(certificateName);

                            var secretProviderManager = SecretProviderManager.Instance;
                            var clientCertificate = secretProviderManager.GetCertificateWithListener(
                                certificateName: certificateName,
                                listener: new NoOpCertificateListener(),
                                allowMultiListeners: true);
                            GuardHelper.ArgumentNotNull(clientCertificate);

                            return clientCertificate;
                        }
                    };
                    options.Listen(IPAddress.IPv6Any, port, listenOptions =>
                    {
                        listenOptions.UseHttps(httpsConnectionAdapterOptions);
                    });
                });
            }
            
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsEnvironment("Local"))
            {
                app.UseSwagger(options => options.SerializeAsV2 = true);
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            if (!app.Environment.IsEnvironment("Local"))
            {
                app.UseDSTSAuthMiddleware();
            }

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}