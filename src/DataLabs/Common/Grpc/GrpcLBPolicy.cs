namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc
{
    public enum GrpcLBPolicy
    {
        LOCAL = 0,
        ROUND_ROBIN = 1
        //LOCAL_LOAD_AWARE = 2, TODO, task: 20852738
    }
}
