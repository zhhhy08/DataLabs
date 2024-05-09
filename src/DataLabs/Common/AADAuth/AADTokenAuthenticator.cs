namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using Microsoft.Extensions.Configuration;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.IdentityModel.Validators;

    [ExcludeFromCodeCoverage]
    public class AADTokenAuthenticator : IAADTokenAuthenticator
    {
        private static readonly ActivityMonitorFactory AADTokenAuthenticatorAuthenticate = new("AADTokenAuthenticator.Authenticate");

        public static readonly NoAppIdException noAppIdException = new ();
        public static readonly ExpiredTokenException expiredTokenException = new ();

        public long SigningTokenRefreshedSeq => _signingTokenProvider.TokenRefreshedSequence;

        private const string AppIdClaim = "appid";

        private readonly AADIssuerSigningTokenProvider _signingTokenProvider;
        private readonly JwtSecurityTokenHandler _jwtSecurityTokenHandler;
        private readonly string[] _tokenIssuers;
        private readonly string[] _tokenAudiences;
        private readonly string _authority;

        public static AADTokenAuthenticator CreateAADTokenAuthenticatorFromDataLabConfig(IConfiguration configuration)
        {
            var tokenIssuer = configuration.GetValue<string>(SolutionConstants.AADTokenIssuer);
            var tokenAudience = configuration.GetValue<string>(SolutionConstants.AADTokenAudience);
            var authority = configuration.GetValue<string>(SolutionConstants.AADAuthority);

            GuardHelper.ArgumentNotNullOrEmpty(tokenIssuer);
            GuardHelper.ArgumentNotNullOrEmpty(tokenAudience);
            GuardHelper.ArgumentNotNullOrEmpty(authority);

            return new AADTokenAuthenticator(
                configuration: configuration,
                tokenIssuers: new string[] { tokenIssuer },
                tokenAudiences: new string[] { tokenAudience }, 
                authority: authority);
        }

        public AADTokenAuthenticator(
            IConfiguration configuration,
            string[] tokenIssuers,
            string[] tokenAudiences,
            string authority)
        {
            _tokenIssuers = tokenIssuers;
            GuardHelper.ArgumentNotNullOrEmpty(_tokenIssuers);

            _tokenAudiences = tokenAudiences;
            GuardHelper.ArgumentNotNullOrEmpty(_tokenAudiences);

            _authority = authority;
            GuardHelper.ArgumentNotNullOrEmpty(_authority);

            _jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
            _signingTokenProvider = new AADIssuerSigningTokenProvider(configuration, _authority, true);
        }

        // This method doens't throw exception. Instead, the exception is saved in authenticatedInfo
        public bool Authenticate(string tokenString, out AADAuthenticatedInfo authenticatedInfo)
        {
            using var monitor = AADTokenAuthenticatorAuthenticate.ToMonitor();

            try
            {
                GuardHelper.ArgumentNotNullOrEmpty(tokenString);

                monitor.OnStart(false);

                var signingTokenRefreshedSeq = _signingTokenProvider.TokenRefreshedSequence;
                var signingTokens = _signingTokenProvider.SigningTokens;

                var parameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    IssuerSigningKeys = signingTokens
                };
                parameters.EnableAadSigningKeyIssuerValidation();

                if (_tokenAudiences.Length == 1)
                {
                    parameters.ValidAudience = _tokenAudiences[0];
                }
                else
                {
                    parameters.ValidAudiences = _tokenAudiences;
                }

                if (_tokenIssuers.Length == 1)
                {
                    parameters.ValidIssuer = _tokenIssuers[0];
                }
                else
                {
                    parameters.ValidIssuers = _tokenIssuers;
                }

                var identity = _jwtSecurityTokenHandler.ValidateToken(tokenString, parameters, out var token);

                var claims = identity.Claims
                    .GroupBy(claim => claim.Type)
                    .ToDictionary(group => group.Key, group => string.Join(',', group.Select(c => c.Value)), StringComparer.OrdinalIgnoreCase);

                if (!claims.TryGetValue(AppIdClaim, out var appId))
                {
                    authenticatedInfo = new AADAuthenticatedInfo
                    {
                        Exception = noAppIdException,
                        IsSuccess = false
                    };

                    monitor.OnError(noAppIdException);
                    return false;
                }

                var now = DateTime.UtcNow;
                var timeValid = now >= token.ValidFrom && now <= token.ValidTo;
                if (!timeValid)
                {
                    authenticatedInfo = new AADAuthenticatedInfo
                    {
                        Exception = expiredTokenException,
                        IsSuccess = false
                    };

                    monitor.OnError(expiredTokenException);
                    return false;
                }

                authenticatedInfo = new AADAuthenticatedInfo
                {
                    SigningTokenRefreshedSeq = signingTokenRefreshedSeq,
                    ValidFrom = token.ValidFrom,
                    ValidTo = token.ValidTo,
                    AppId = appId,
                    IsSuccess = true
                };

                monitor.OnCompleted();
                return true;
            }
            catch (Exception ex)
            {
                authenticatedInfo = new AADAuthenticatedInfo
                {
                    Exception = ex,
                    IsSuccess = false
                };

                monitor.OnError(ex);
                return false;
            }
        }

        public void Dispose()
        {
            _signingTokenProvider.Dispose();
        }

        public class NoAppIdException : Exception
        {
            public NoAppIdException() : base("NoAppIdInToken") 
            {
            }
        }

        public class ExpiredTokenException : Exception
        {
            public ExpiredTokenException() : base("ExpiredToken") {
            }
        }
    }
}
