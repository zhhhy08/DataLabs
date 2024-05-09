namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client
{
    using System;

    public interface IEndPointSelector
    {
        public Uri[] GetPrimaryEndPoints { get; }
        public Uri[]? GetBackupEndPoints { get; }
        public Uri GetEndPoint(out int outIndex);
    }
}
