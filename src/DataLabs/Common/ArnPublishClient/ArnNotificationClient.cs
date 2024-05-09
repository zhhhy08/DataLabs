namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient
{
    using global::Azure;
    using global::Azure.Core;
    using global::Azure.Core.Pipeline;
    using global::Azure.Identity;
    using global::Azure.Storage.Blobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Configuration;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Helpers;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Logging;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.Data;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Runtime.ConstrainedExecution;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    [ExcludeFromCodeCoverage]
    public class ArnNotificationClient : IArnNotificationClient, ICertificateListener
    {
        #region Tracing

        private static readonly ActivityMonitorFactory ArnNotificationClientUpdateLoggingConfig =
            new ActivityMonitorFactory("ArnNotificationClient.UpdateLoggingConfig");

        private static readonly ActivityMonitorFactory ArnNotificationClientUpdatePublishPercentage =
            new ActivityMonitorFactory("ArnNotificationClient.UpdatePublishPercentage");

        private static readonly ActivityMonitorFactory ArnNotificationClientArnNotificationClient =
            new ActivityMonitorFactory("ArnNotificationClient.ArnNotificationClient");

        private static readonly ActivityMonitorFactory ArnNotificationClientPublishToArn =
            new ActivityMonitorFactory("ArnNotificationClient.PublishToArn");

        private static readonly ActivityMonitorFactory ArnNotificationClientRemoveExpiredBlobContainers =
            new ActivityMonitorFactory("ArnNotificationClient.RemoveExpiredBlobContainers");

        private static readonly ActivityMonitorFactory ArnNotificationClientCertificateChangedAsync =
            new ActivityMonitorFactory("ArnNotificationClient.CertificateChangedAsync");

        #endregion

        #region Fields

        private const string StorageScope = "https://storage.azure.com/.default";
        private const string ConnectionStringDelimiter = "|";
        private const int _publishPercentMultiplier = 100000;
        private readonly BearerTokenAuthenticationPolicy _policy = new BearerTokenAuthenticationPolicy(new ManagedIdentityCredential(), StorageScope);
        private readonly ArnNotificationClientLogger _logger;
        private readonly IList<BlobServiceClient>? _blobSvcClients;
        private readonly IExtendedArmNotificationClient? _eventGridClient;
        private readonly IExtendedArmNotificationClient? _pairedRegionEventGridClient;
        private IExtendedArmNotificationClient? _notificationReceiverClient;
        private readonly IList<string>? _arnPublishEventGridTopics;
        private readonly IList<string>? _pairedRegionArnPublishEventGridTopics;
        private int _publishPercentMultiplied;
        private NotificationsClient.Configuration.Environment _arnEnvironment;
        private ExtendedArmNotificationClientOptions _arnNotificationClientOptions;

        private string? _nrDstsCertName => ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.NrDstsCertificateName);
        private string? _nrDstsClientId => ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.NrDstsClientId);
        private string? _nrDstsClientHome => ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.NrDstsClientHome);
        private string? _nrEndpoint => ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.NotificationReceiverEndPoint);

        #endregion

        #region Properties

        public bool IsInitialized => _eventGridClient != null || _notificationReceiverClient != null;

        public bool IsBackupClientInitialized => _pairedRegionEventGridClient != null;

        #endregion

        #region Constructors

        public ArnNotificationClient()
        {
            using var monitor = ArnNotificationClientArnNotificationClient.ToMonitor();
            monitor.OnStart(false);

            try
            {
                var localTesting = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ArnPublishLocalTesting, false);
                var publisher = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ArnPublisherInfo, "Microsoft.DataLabs");
                var percent = ConfigMapUtil.Configuration.GetValueWithCallBack(SolutionConstants.ArnPublishPercentage, UpdatePublishPercentage, 100d);
                UpdatePublishPercentage(percent);
                var nrDstsEnabled = ConfigMapUtil.Configuration.GetValue(SolutionConstants.NrDstsIsEnabled, false);

                monitor.Activity["LocalTesting"] = localTesting;
                monitor.Activity["Publisher"] = publisher;
                monitor.Activity["PublishPercentMultiplied"] = _publishPercentMultiplied;
                monitor.Activity["NrDstsEnabled"] = nrDstsEnabled;

                // Prepare logger
                var enableDebugLog = ConfigMapUtil.Configuration.GetValueWithCallBack(SolutionConstants.IsArnClientLogDebugEnabled, UpdateDebugLoggingConfig, false);
                var enableInfoLog = ConfigMapUtil.Configuration.GetValueWithCallBack(SolutionConstants.IsArnClientLogInfoEnabled, UpdateInfoLoggingConfig, false);
                this._logger = new ArnNotificationClientLogger(enableDebugLog, enableInfoLog);

                // Prepare blob storage
                if (!localTesting)
                {
                    var storageAccountNames = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ArnPublishStorageAccountNames).ConvertToList();
                    monitor.Activity.LogCollectionAndCount("StorageAccountNames", storageAccountNames);
                    _blobSvcClients = storageAccountNames?.Select(x => CreateBlobServiceClient(GetBlobUri(x))).ToList();
                }

                _arnNotificationClientOptions = new ExtendedArmNotificationClientOptions()
                {
                    CustomPublisherInfo = publisher,
                    SchemaVersion = SchemaVersion.V3,
                    IgnorePayloadInlining = false
                };

                var maxBatchSize = ConfigMapUtil.Configuration.GetValue(SolutionConstants.ArnPublishMaxBatchSize, -1);
                if (maxBatchSize > 0)
                {
                    _arnNotificationClientOptions.MaxBatchSize = maxBatchSize;
                }

                #region Notification Receiver

                X509Certificate2? cert = null;

                if (nrDstsEnabled && !string.IsNullOrEmpty(_nrDstsCertName))
                {
                    cert = SecretProviderManager.Instance.GetCertificateWithListener(
                        certificateName: _nrDstsCertName,
                        listener: this,
                        allowMultiListeners: true);
                }

                if (!string.IsNullOrWhiteSpace(_nrEndpoint) &&
                    !_blobSvcClients.SafeFastEmpty() &&
                    !string.IsNullOrWhiteSpace(_nrDstsClientId) &&
                    !string.IsNullOrWhiteSpace(_nrDstsClientHome) &&
                    cert != null)
                {
                    _arnEnvironment = ConfigMapUtil.Configuration.GetValue(
                        SolutionConstants.NotificationReceiverEnvironment,
                        NotificationsClient.Configuration.Environment.AzureCloud);

                    monitor.Activity["NotificaitonReceiverEndPoint"] = _nrEndpoint;
                    monitor.Activity["ArnEnvironment"] = _arnEnvironment.SafeFastEnumToString();

                    var tokenCredential = new DstsTokenCredential(_nrDstsClientHome, _nrDstsClientId, cert);

                    var receiverConfig = new ReceiverConfigurationsBuilder(
                        endPoint: _nrEndpoint,
                        clientHomeDsts: _nrDstsClientHome,
                        tokenCredential: tokenCredential,
                        authenticationType: ReceiverAuthenticationType.dSTSTokenCredentials,
                        environment: _arnEnvironment).Build();

                    _notificationReceiverClient = new ExtendedArmNotificationClient(
                        receiverConfig: receiverConfig,
                        blobServiceClients: ImmutableList.CreateRange(_blobSvcClients!),
                        clientOptions: _arnNotificationClientOptions,
                        logger: _logger);
                }

                #endregion

                #region Event Grid

                // Prepare event grids domains and topics
                var eventGridDomainIds = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ArnPublishEventGridDomainIds).ConvertToList();
                var eventGridDomainEndpoints = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ArnPublishEventGridDomainEndpoints).ConvertToList();
                this._arnPublishEventGridTopics = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ArnPublishEventGridTopics).ConvertToList();

                var pairedRegionEventGridDomainIds = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PairedRegionArnPublishEventGridDomainIds).ConvertToList();
                var pairedRegionEventGridDomainEndpoints = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PairedRegionArnPublishEventGridDomainEndpoints).ConvertToList();
                this._pairedRegionArnPublishEventGridTopics = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.PairedRegionArnPublishEventGridTopics).ConvertToList();

                GuardHelper.ArgumentConstraintCheck(eventGridDomainIds?.Count == eventGridDomainEndpoints?.Count);
                GuardHelper.ArgumentConstraintCheck(eventGridDomainIds?.Count == this._arnPublishEventGridTopics?.Count);
                GuardHelper.ArgumentConstraintCheck(pairedRegionEventGridDomainIds?.Count == pairedRegionEventGridDomainEndpoints?.Count);
                GuardHelper.ArgumentConstraintCheck(pairedRegionEventGridDomainIds?.Count == this._pairedRegionArnPublishEventGridTopics?.Count);

                monitor.Activity.LogCollectionAndCount("EventGridDomainIds", eventGridDomainIds);
                monitor.Activity.LogCollectionAndCount("EventGridDomainEndpoints", eventGridDomainEndpoints);
                monitor.Activity.LogCollectionAndCount("EventGridTopics", this._arnPublishEventGridTopics);
                monitor.Activity.LogCollectionAndCount("PairedRegionEventGridDomainIds", pairedRegionEventGridDomainIds);
                monitor.Activity.LogCollectionAndCount("PairedRegionEventGridDomainEndpoints", pairedRegionEventGridDomainEndpoints);
                monitor.Activity.LogCollectionAndCount("PairedRegionEventGridTopics", this._pairedRegionArnPublishEventGridTopics);

                if (!localTesting &&
                    !eventGridDomainIds.SafeFastEmpty() &&
                    !eventGridDomainEndpoints.SafeFastEmpty() &&
                    !_arnPublishEventGridTopics.SafeFastEmpty() &&
                    !_blobSvcClients.SafeFastEmpty())
                {
                    var egConfigWithMI = new EventGridConfigurationsBuilder(
                        eventGridDomainIds,
                        eventGridDomainEndpoints!.Select(x => new NetworkCredential("", x).SecurePassword).ToList(),
                        EventGridConnectionType.Domain,
                        EventGridAuthenticationType.TokenCredential,
                        PublishDistributionType.EvenDistribution)
                    .WithTokenCredentials(eventGridDomainEndpoints!.Select(x => (TokenCredential)new DefaultAzureCredential()).ToList()).Build();

                    _eventGridClient = new ExtendedArmNotificationClient(
                        eventGridConfig: egConfigWithMI,
                        blobServiceClients: ImmutableList.CreateRange(_blobSvcClients!),
                        clientOptions: _arnNotificationClientOptions,
                        logger: _logger);

                }

                // in lower environments paired region and current region configs are the same
                if (!localTesting &&
                    !pairedRegionEventGridDomainIds.SafeFastEmpty() &&
                    !pairedRegionEventGridDomainEndpoints.SafeFastEmpty() &&
                    !_pairedRegionArnPublishEventGridTopics.SafeFastEmpty() &&
                    !_blobSvcClients.SafeFastEmpty())
                {
                    
                    var pairedegConfigWithMI = new EventGridConfigurationsBuilder(
                       pairedRegionEventGridDomainIds,
                       pairedRegionEventGridDomainEndpoints!.Select(x => new NetworkCredential("", x).SecurePassword).ToList(),
                       EventGridConnectionType.Domain,
                       EventGridAuthenticationType.TokenCredential,
                       PublishDistributionType.EvenDistribution)
                   .WithTokenCredentials(pairedRegionEventGridDomainEndpoints!.Select(x => (TokenCredential)new DefaultAzureCredential()).ToList()).Build();

                    _pairedRegionEventGridClient = new ExtendedArmNotificationClient(
                        eventGridConfig: pairedegConfigWithMI,
                        blobServiceClients: ImmutableList.CreateRange(_blobSvcClients!),
                        clientOptions: _arnNotificationClientOptions,
                        logger: _logger);

                }

                var eventGridDomainKeys = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ArnPublishEventGridDomainKeys).ConvertToList();
                var storageAccountSecureStrings = ConfigMapUtil.Configuration
                    .GetValue<string>(SolutionConstants.ArnPublishStorageAccountConnectionStrings)
                    .ConvertToList(ConnectionStringDelimiter);

                if (localTesting &&
                    !eventGridDomainIds.SafeFastEmpty() &&
                    !eventGridDomainEndpoints.SafeFastEmpty() &&
                    !_arnPublishEventGridTopics.SafeFastEmpty() &&
                    !eventGridDomainKeys.SafeFastEmpty() &&
                    !storageAccountSecureStrings.SafeFastEmpty())
                {
                    var egConfig = new EventGridConfigurationsBuilder(
                        eventGridDomainIds,
                        eventGridDomainEndpoints!.Select(x => new NetworkCredential("", x).SecurePassword).ToList(),
                        EventGridConnectionType.Domain,
                        EventGridAuthenticationType.KeyCredential,
                        sasTokenExpiryTimespan: null)
                        .WithKeyCredentials(eventGridDomainKeys!.Select(x => new AzureKeyCredential(x)).ToList())
                        .Build();

                    // for local testing, we are assuming both the clients point to the same eventgrid configurations
                    _eventGridClient = new ExtendedArmNotificationClient(
                        eventGridConfig: egConfig,
                        storageAccountConnectionStrings: storageAccountSecureStrings,
                        clientOptions: _arnNotificationClientOptions,
                        logger: new ConsoleLogger(isDebugEnabled: false, isErrorEnabled: true, isFatalEnabled: true, isInfoEnabled: false, isWarnEnabled: true));

                    _pairedRegionEventGridClient = new ExtendedArmNotificationClient(
                        eventGridConfig: egConfig,
                        storageAccountConnectionStrings: storageAccountSecureStrings,
                        clientOptions: _arnNotificationClientOptions,
                        logger: new ConsoleLogger(isDebugEnabled: false, isErrorEnabled: true, isFatalEnabled: true, isInfoEnabled: false, isWarnEnabled: true));
                }

                #endregion

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        #endregion

        #region Public Methods

        public async Task PublishToArn(
            IList<ResourceOperationBase> resourceOperations,
            DataBoundary? dataBoundary,
            FieldOverrides fieldOverrides,
            bool publishToPairedRegionClient,
            CancellationToken cancellationToken,
            AdditionalGroupingProperties additionalGroupingProperties = AdditionalGroupingProperties.None)
        {
            using var monitor = ArnNotificationClientPublishToArn.ToMonitor();
            monitor.OnStart(false);

            try
            {
                if (ThreadSafeRandom.Next(_publishPercentMultiplier) >= _publishPercentMultiplied)
                {
                    monitor.Activity["SkippedPublish"] = bool.TrueString;
                    monitor.OnCompleted();
                    return;
                }

                if (!IsInitialized || !IsBackupClientInitialized || resourceOperations.SafeFastEmpty())
                {
                    monitor.Activity["ResourceOperationsCount"] = resourceOperations?.Count;
                    monitor.Activity["IsInitialized"] = IsInitialized;
                    monitor.Activity["IsBackupClientInitialized"] = IsBackupClientInitialized;

                    var ex = new Exception("ARN Notification Client is not initialized");
                    monitor.OnError(ex);
                    throw ex;
                }

                var useEgClient = true;

                if (!publishToPairedRegionClient && _notificationReceiverClient != null)
                {
                    try
                    {
                        monitor.Activity["UseNotificationReceiver"] = bool.TrueString;
                        await _notificationReceiverClient.UploadAndNotifyMultipleAsync(
                            resourceOperations,
                            cancellationToken,
                            _arnPublishEventGridTopics,
                            monitor.Activity.Context,
                            fieldOverrides: fieldOverrides,
                            dataBoundary: dataBoundary,
                            additionalGroupingProperties: additionalGroupingProperties).IgnoreContext();

                        useEgClient = false;
                    }
                    catch (Exception ex)
                    {
                        monitor.Activity["NotificationReceiverError"] = ex.ToString().TruncateWithEllipsis(100);
                        monitor.Activity["FallbackToEGClient"] = bool.TrueString;
                    }
                }

                if(publishToPairedRegionClient && _pairedRegionEventGridClient != null && useEgClient)
                {
                    monitor.Activity["PublishToPairedRegionClient"] = true;
                    await _pairedRegionEventGridClient.UploadAndNotifyMultipleAsync(
                        resourceOperations,
                        cancellationToken,
                        _pairedRegionArnPublishEventGridTopics,
                        monitor.Activity.Context,
                        fieldOverrides: fieldOverrides,
                        dataBoundary: dataBoundary,
                        additionalGroupingProperties: additionalGroupingProperties).IgnoreContext();
                } 
                else if (_eventGridClient != null && useEgClient)
                {
                    monitor.Activity["PublishToPairedRegionClient"] = false;
                    await _eventGridClient.UploadAndNotifyMultipleAsync(
                        resourceOperations,
                        cancellationToken,
                        _arnPublishEventGridTopics,
                        monitor.Activity.Context,
                        fieldOverrides: fieldOverrides,
                        dataBoundary: dataBoundary,
                        additionalGroupingProperties: additionalGroupingProperties).IgnoreContext();
                }

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<StorageRemovalResult?> RemoveExpiredBlobContainers(CancellationToken cancellationToken)
        {
            using var monitor = ArnNotificationClientRemoveExpiredBlobContainers.ToMonitor();
            monitor.OnStart(false);

            try
            {
                StorageRemovalResult? result = null;

                if (_notificationReceiverClient != null)
                {
                    result = await _notificationReceiverClient.RemoveExpiredBlobContainers(cancellationToken, null).IgnoreContext();
                }
                else if (_eventGridClient != null)
                {
                    result = await _eventGridClient.RemoveExpiredBlobContainers(cancellationToken, null).IgnoreContext();
                }

                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public Task CertificateChangedAsync(X509Certificate2 certificate)
        {
            using var monitor = ArnNotificationClientCertificateChangedAsync.ToMonitor();
            monitor.OnStart(false);

            try
            {
                if (!string.IsNullOrWhiteSpace(_nrEndpoint) &&
                    !_blobSvcClients.SafeFastEmpty() &&
                    !string.IsNullOrWhiteSpace(_nrDstsClientId) &&
                    !string.IsNullOrWhiteSpace(_nrDstsClientHome))
                {
                    monitor.Activity["NotificaitonReceiverEndPoint"] = _nrEndpoint;
                    monitor.Activity["ArnEnvironment"] = _arnEnvironment.SafeFastEnumToString();

                    var tokenCredential = new DstsTokenCredential(_nrDstsClientHome, _nrDstsClientId, certificate);

                    var receiverConfig = new ReceiverConfigurationsBuilder(
                        endPoint: _nrEndpoint,
                        clientHomeDsts: _nrDstsClientHome,
                        tokenCredential: tokenCredential,
                        authenticationType: ReceiverAuthenticationType.dSTSTokenCredentials,
                        environment: _arnEnvironment).Build();

                    var notificationReceiverClient = new ExtendedArmNotificationClient(
                        receiverConfig: receiverConfig,
                        blobServiceClients: ImmutableList.CreateRange(_blobSvcClients!),
                        clientOptions: _arnNotificationClientOptions,
                        logger: _logger);

                    Interlocked.Exchange(ref _notificationReceiverClient, notificationReceiverClient);
                }

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Helpers

        private Uri GetBlobUri(string storageAccountName)
        {
            var storageSuffix = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.StorageSuffix);
            return new Uri($"https://{storageAccountName}.blob.{storageSuffix}");
        }

        private BlobServiceClient CreateBlobServiceClient(Uri blobUri)
        {
            var options = new BlobClientOptions();
            // Disable logging which causes locking in DiagnosticScopeFactory
            options.Diagnostics.IsLoggingEnabled = false;
            options.Diagnostics.IsDistributedTracingEnabled = false;
            options.AddPolicy(_policy, HttpPipelinePosition.PerRetry);
            return new BlobServiceClient(blobUri, new DefaultAzureCredential(), options);
        }

        private Task UpdateDebugLoggingConfig(bool isDebugEnabled)
        {
            using var methodMonitor = ArnNotificationClientUpdateLoggingConfig.ToMonitor();

            methodMonitor.Activity.Properties["IsDebugEnabledNewValue"] = isDebugEnabled;
            methodMonitor.Activity.Properties["LoggerExists"] = this._logger != null;
            methodMonitor.OnStart();

            try
            {
                this._logger?.UpdateLoggingConfig(isDebugEnabled, _logger?.IsInfoEnabled ?? false);
                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
            }

            return Task.CompletedTask;
        }

        private Task UpdateInfoLoggingConfig(bool isInfoEnabled)
        {
            using var methodMonitor = ArnNotificationClientUpdateLoggingConfig.ToMonitor();

            methodMonitor.Activity.Properties["IsInfoEnabledNewValue"] = isInfoEnabled;
            methodMonitor.Activity.Properties["LoggerExists"] = this._logger != null;
            methodMonitor.OnStart();

            try
            {
                this._logger?.UpdateLoggingConfig(_logger?.IsDebugEnabled ?? false, isInfoEnabled);
                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
            }

            return Task.CompletedTask;
        }

        private Task UpdatePublishPercentage(double publishPercentage)
        {
            using var methodMonitor = ArnNotificationClientUpdatePublishPercentage.ToMonitor();

            methodMonitor.OnStart();

            try
            {
                methodMonitor.Activity.Properties["PublishPercentageMultipliedOldValue"] = _publishPercentMultiplied;

                _publishPercentMultiplied = (int)(publishPercentage * _publishPercentMultiplier / 100);

                methodMonitor.Activity.Properties["PublishPercentageMultipliedNewValue"] = _publishPercentMultiplied;

                methodMonitor.OnCompleted();
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}
