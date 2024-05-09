namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager
{
    public enum ClientProviderType
    {
        // Be careful
        // The enumeration values below should follow a sequential order starting from 0, as they serve as array indices
        None = 0,
        Cache = 1,
        Arm = 2,
        ArmAdmin = 3,
        Qfd = 4,
        Cas = 5,
        ResourceFetcher_Arm = 6,
        ResourceFetcher_Qfd = 7,
        ResourceFetcher_ArmAdmin = 8,
        ResourceFetcher_Cas = 9,
        OutputSourceoftruth = 10
    }
}
