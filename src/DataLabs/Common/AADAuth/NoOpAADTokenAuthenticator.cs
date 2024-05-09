namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth
{
    using System;

    public class NoOpAADTokenAuthenticator : IAADTokenAuthenticator
    {
        public long SigningTokenRefreshedSeq => 1;

        public bool Authenticate(string tokenString, out AADAuthenticatedInfo authenticatedInfo)
        {
            authenticatedInfo.AppId = "testclientId";
            authenticatedInfo.SigningTokenRefreshedSeq = SigningTokenRefreshedSeq;
            authenticatedInfo.ValidFrom = DateTime.UtcNow.AddDays(-1);
            authenticatedInfo.ValidTo = DateTime.UtcNow.AddDays(1);
            authenticatedInfo.Exception = null;
            authenticatedInfo.IsSuccess = true;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
