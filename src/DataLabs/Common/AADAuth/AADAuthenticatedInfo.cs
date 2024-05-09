namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AADAuth
{
    using System;

    public struct AADAuthenticatedInfo
    {
        public long SigningTokenRefreshedSeq;
        public DateTime ValidFrom;
        public DateTime ValidTo;
        public string? AppId;
        public Exception? Exception;
        public bool IsSuccess;
    }
}
