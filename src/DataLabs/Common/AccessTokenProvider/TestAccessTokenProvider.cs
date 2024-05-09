namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider
{
    using System.Threading;
    using System.Threading.Tasks;

    public class TestAccessTokenProvider : IAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(string? tenantId, string[]? scopes, CancellationToken cancellationToken)
        {
            return Task.FromResult("TestAccessToken");
        }

        public void Dispose()
        {
        }

    }
}
