namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Exception
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS.Enums;
    using System.Runtime.Serialization;

    public class CapacityException : System.Exception
    {
        public CapacityErrorResponse? ErrorResponse { get; private set; }

        public CapacityException()
        {
        }

        public CapacityException(string message)
            : base(message)
        {
        }

        public CapacityException(string message, CapacityExceptionErrorCode code)
            : base(message)
        {
            ErrorResponse = new CapacityErrorResponse
            {
                Error = new CapacityErrorDetails
                {
                    Code = code.ToString(),
                    Message = message
                }
            };
        }

        public CapacityException(string message, System.Exception inner)
            : base(message, inner)
        {
        }
    }
}
