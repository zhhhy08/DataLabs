namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis
{
    using System;

    [AttributeUsage(AttributeTargets.Parameter)]
    public class AcisParamDescriptionAttribute : Attribute
    {
        public readonly string Description;

        public AcisParamDescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}
