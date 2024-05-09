namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts
{
    public enum EventTaskFinalStage
    {
        NONE,
        DROP,
        RETRY_QUEUE,
        POISON_QUEUE,
        SUCCESS,
        FAIL
    }
}
