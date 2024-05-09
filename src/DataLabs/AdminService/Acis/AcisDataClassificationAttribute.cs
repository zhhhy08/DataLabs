namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AcisDataClassificationAttribute : Attribute
    {
        public readonly AcisDataClassificationLevel ClassificationLevel;

        /// <summary>
        /// Attribute for data classification of geneva action data
        /// </summary>
        /// <param name="classificationLevel">The highest level (by enum value) of the data exposed.</param>
        public AcisDataClassificationAttribute(AcisDataClassificationLevel classificationLevel)
        {
            ClassificationLevel = classificationLevel;
        }
    }

    // Fork of https://msazure.visualstudio.com/One/_git/EngSys-Acis-Legacy?path=/src/Acis/AcisSMEContracts/Models/DataClassificationLevel.cs&_a=contents&version=GBmaster
    // Does not have flags as that is not supported in swagger operations see: https://eng.ms/docs/products/geneva/actions/compliance/declarativecompliance#classify-swagger-operation
    public enum AcisDataClassificationLevel
    {
        NotClassified = 1,
        AccessControlData = 2,
        CustomerContent = 4,
        EUII = 8,
        SupportData = 16,
        Feedback = 32,
        AccountData = 64,
        PublicPersonalData = 128,
        EUPI = 256,
        NoCustomerContent = 512
    }
}
