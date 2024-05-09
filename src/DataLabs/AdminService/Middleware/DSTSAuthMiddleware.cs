namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Middlewares
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.WindowsAzure.Security.Authentication;
    using System.Diagnostics.CodeAnalysis;
    using System.Security.Claims;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;

    /// <summary>
    /// Middleware for authntication and authorization.
    /// The code for authentication is refrenced from the DSTS sample code
    /// https://msazure.visualstudio.com/One/_git/EngSys-Security-dSTS?path=/Security/Samples/DatacenterAuthentication/WebAPIRestServer/Program.cs
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DSTSAuthMiddleware
    {
        private const string DstsHeader = "WWW-Authenticate-dSTS";
        private const string AuthorizationHeaderKey = "Authorization";

        private readonly RequestDelegate _next;
        private readonly WebSecurityTokenAuthenticator webSecurityTokenAuthenticator;
        private readonly ServerAuthenticationProvider serverAuthenticationProvider;

        public DSTSAuthMiddleware(RequestDelegate next)
        {
            this._next = next;
            var realm = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.DstsRealm) ?? "";
            var name = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.DstsName);
            var serviceDns = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ServiceDns);
            var serviceName = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.ServiceName);

            var homeDsts = new ServerHomeDsts(new Uri(realm), name);
            var serviceIdentity = new ServiceIdentity(serviceDns, serviceName);
            var datacenterServiceConfiguration = new DatacenterServiceConfiguration(homeDsts, serviceIdentity);
            this.webSecurityTokenAuthenticator = new WebSecurityTokenAuthenticator(datacenterServiceConfiguration);
            this.serverAuthenticationProvider = new ServerAuthenticationProvider(datacenterServiceConfiguration);
        }

        public async Task Invoke(
            HttpContext context)
        {
            var authorizationHeader = context.Request.Headers.FirstOrDefault(h => String.Equals(h.Key, AuthorizationHeaderKey, StringComparison.Ordinal));
            var isAuthenticated = false;

            if (authorizationHeader.Value.Count() == 1)
            {
                string? authenticationHeaderValue = authorizationHeader.Value.ElementAt(0);
                try
                {
                    ClaimsPrincipal claims = this.webSecurityTokenAuthenticator.Authenticate(authenticationHeaderValue);
                    isAuthenticated = claims.Identity != null && claims.Identity.IsAuthenticated && IsValidUserPrincipal(claims);
                }
                catch (WebServerAuthenticationException authenticationException)
                {
                    context.Response.Headers.Append(DstsHeader, authenticationException.WwwAuthenticateHeader);
                    await context.Response.WriteAsync("Unauthorized", context.RequestAborted);
                    return;
                }
            }

            if (isAuthenticated)
            {
                await this._next(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.Append(DstsHeader,
                           WwwAuthenticateHeaderFactory.CreateFromMetadata(serverAuthenticationProvider.CreateAuthenticationMetadata()));
                await context.Response.WriteAsync("Unauthorized", context.RequestAborted);
            }
        }

        private bool IsValidUserPrincipal(ClaimsPrincipal? claimsPrincipal)
        {
            var claimsIdentity = claimsPrincipal?.Identities.FirstOrDefault(identity => identity.Actor != null);
            var actor = claimsIdentity?.Actor;
            
            if (string.IsNullOrEmpty(actor?.Name))
            {
                return false;
            }

            if (!GetAllowedActors().Contains(actor.Name))
            {
                return false;
            }

            return true;
        }

        public static IReadOnlyCollection<string> GetAllowedActors()
        {
            string allowedActors = ConfigMapUtil.Configuration.GetValue<string>(SolutionConstants.AllowedActors, "") ?? "";
            return allowedActors.Split(',').ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}