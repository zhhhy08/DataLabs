namespace Microsoft.Azure.ARMDataInsights.ArmDataCacheService
{
    internal class Constants
    {

        // monitor constants
        public const string Action = "action";
        public const string State = "state";
        public const string Success = "success";
        public const string Updated = "updated";


        //registration states
        public const string Deleted = "deleted";

        //actions
        public const string Delete = "delete";
        public const string Ingest = "ingest";

        // fixed date to calcute elapsed time for scores
        public static readonly DateTime offSetStartTime = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
