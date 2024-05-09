namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.IdentityModel.Protocols;
    using Microsoft.IdentityModel.Protocols.OpenIdConnect;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// Provides signing tokens from AAD authority for token validation purposes.
    /// </summary>
    public class AADIssuerSigningTokenProvider : IDisposable
    {
        private static readonly ILogger<AADIssuerSigningTokenProvider> Logger = 
            DataLabLoggerFactory.CreateLogger<AADIssuerSigningTokenProvider>();

        private static readonly ActivityMonitorFactory AADIssuerSigningTokenProviderRetrieveSigningTokensAsync = 
            new ("AADIssuerSigningTokenProvider.RetrieveSigningTokensAsync"); 

        public static readonly TimeSpan DefaultSigningTokensRefreshDuration = TimeSpan.FromDays(1);
        public static readonly TimeSpan DefaultSigningTokensRetrieveTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The issuer signing keys
        /// </summary>
        public IReadOnlyCollection<SecurityKey>? SigningTokens => _signingTokens;
        public long TokenRefreshedSequence => _numRefreshed;

        private readonly string _openIdConfigurationAddress;
        private readonly double _refreshTimerIntervalAfterInitial;
        private readonly double _initRefreshDelay;
        private readonly ConfigurableTimer _refreshTimer;
        private TimeSpan _failRefreshingDuration;
        private TimeSpan _signingTokensRetrieveTimeOut;


        private volatile bool _prevRefreshFailed;
        private IReadOnlyCollection<SecurityKey>? _signingTokens;
        
        private long _numRefreshed;

        private readonly object _updateLock = new object();
        private bool _disposed;

        public AADIssuerSigningTokenProvider(IConfiguration configuration, string authority, bool retrieveSigningTokens)
        {
            GuardHelper.ArgumentNotNullOrEmpty(authority);

            var openIdConfigurationURI = configuration.GetValue<string>(SolutionConstants.OpenIdConfigurationURI, "/common/v2.0/.well-known/openid-configuration");
            GuardHelper.ArgumentNotNullOrEmpty(openIdConfigurationURI);
            if (!openIdConfigurationURI.StartsWith("/"))
            {
                openIdConfigurationURI = "/" + openIdConfigurationURI;
            }
            _openIdConfigurationAddress = authority + openIdConfigurationURI;

            _signingTokensRetrieveTimeOut = configuration.GetValueWithCallBack<TimeSpan>(SolutionConstants.SigningTokensRetrieveTimeOut, UpdateSigningTokensRetrieveTimeOut, DefaultSigningTokensRetrieveTimeout);
            _signingTokensRetrieveTimeOut = _signingTokensRetrieveTimeOut == TimeSpan.Zero ? DefaultSigningTokensRetrieveTimeout : _signingTokensRetrieveTimeOut;

            if (retrieveSigningTokens)
            {
                _signingTokens = RetrieveSigningTokensAsync(_openIdConfigurationAddress).GetAwaiter().GetResult();
                GuardHelper.ArgumentConstraintCheck(_signingTokens?.Count > 0);
            }
            else
            {
                _signingTokens = Array.Empty<SecurityKey>();
            }

            _failRefreshingDuration = configuration.GetValueWithCallBack<TimeSpan>(
                SolutionConstants.SigningTokensFailedRefreshDuration, UpdateFailedRefreshDuration,
                TimeSpan.FromMinutes(10));

            _refreshTimer = new ConfigurableTimer(SolutionConstants.SigningTokensRefreshDuration, DefaultSigningTokensRefreshDuration);
            _refreshTimer.Elapsed += async (sender, e) => await RefreshSigningTokensAsync(sender, e);

            // Let's set random minutes for the first interval
            _refreshTimerIntervalAfterInitial = _refreshTimer.CurrentIntervalConfigValue.TotalMilliseconds;
            if (_refreshTimerIntervalAfterInitial > 0)
            {
                _initRefreshDelay = new Random(Guid.NewGuid().GetHashCode()).Next(((int)_refreshTimerIntervalAfterInitial)/2);
                _refreshTimer.Interval = _initRefreshDelay;
            }
        }

        public async Task RefreshSigningTokensAsync(object? sender, ElapsedEventArgs e)
        {
            var refreshSuccess = false;
            try
            {
                var signingTokens = await RetrieveSigningTokensAsync(_openIdConfigurationAddress).ConfigureAwait(false);
                if (signingTokens?.Count > 0)
                {
                    refreshSuccess = true;
                    Interlocked.Increment(ref _numRefreshed);
                    Interlocked.Exchange(ref _signingTokens, signingTokens);
                }
            }
            catch (Exception)
            {
                refreshSuccess = false;
                // No exception logging
                // Log is already done in RetrieveSigningTokensAsync
            }
            finally
            {
                if (!refreshSuccess)
                {
                    // Refresh fail. Let's reduce refreshing interval
                    _prevRefreshFailed = true;
                    _refreshTimer.Interval = _failRefreshingDuration.TotalMilliseconds;
                }
                else
                {
                    // Refresh success
                    if (_prevRefreshFailed)
                    {
                        // Prevous Refresh failed
                        // Let's set original refresh interval
                        _prevRefreshFailed = false;
                        _refreshTimer.Interval = _refreshTimerIntervalAfterInitial;
                    }
                    else
                    {
                        if (_initRefreshDelay > 0)
                        {
                            if (_refreshTimer.Interval != _refreshTimerIntervalAfterInitial)
                            {
                                _refreshTimer.Interval = _refreshTimerIntervalAfterInitial;
                            }
                        }
                    }
                }
            }
        }

        private async Task<IReadOnlyCollection<SecurityKey>?> RetrieveSigningTokensAsync(string openIdConfigurationAddress)
        {
            using var monitor = AADIssuerSigningTokenProviderRetrieveSigningTokensAsync.ToMonitor();

            try
            {
                monitor.OnStart(false);

                monitor.Activity["ConfigURL"] = openIdConfigurationAddress;

                using CancellationTokenSource tokenSource = new();
                tokenSource.CancelAfter(_signingTokensRetrieveTimeOut);

                var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(openIdConfigurationAddress, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
                var doc = await configManager.GetConfigurationAsync(tokenSource.Token).ConfigureAwait(false);
                var signingKeys = doc.SigningKeys?.ToList();

                monitor.OnCompleted();

                return signingKeys;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _refreshTimer.Stop();
            _disposed = true;
        }

        private Task UpdateFailedRefreshDuration(TimeSpan newInterval)
        {
            lock (_updateLock)
            {
                var oldInterval = _failRefreshingDuration;
                if (newInterval != oldInterval)
                {
                    _failRefreshingDuration = newInterval;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        SolutionConstants.SigningTokensFailedRefreshDuration,
                        oldInterval.ToString(), newInterval.ToString());
                }
                return Task.CompletedTask;
            }
        }

        private Task UpdateSigningTokensRetrieveTimeOut(TimeSpan newInterval)
        {
            lock (_updateLock)
            {
                var oldInterval = _signingTokensRetrieveTimeOut;
                if (newInterval != oldInterval)
                {
                    _signingTokensRetrieveTimeOut = newInterval;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}",
                        SolutionConstants.SigningTokensRetrieveTimeOut,
                        oldInterval.ToString(), newInterval.ToString());
                }

                return Task.CompletedTask;
            }
        }
    }
}
