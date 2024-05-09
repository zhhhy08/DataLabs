namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth
{
    using System;

    public interface IAADTokenAuthenticator : IDisposable
    {
        public long SigningTokenRefreshedSeq { get; }
        public bool Authenticate(string tokenString, out AADAuthenticatedInfo authenticatedInfo);
    }
}
