using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Reflection;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.ResourceFetcherService.Attributes
{
    public class ARMAdminManifestClientDataAttribute : Attribute, ITestDataSource
    {

        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            yield return new object[]
            {
                "NullResponse",
                this._nullResponse,
                HttpStatusCode.InternalServerError,
                null,
                "Unexpected null response returned by downstream service"
            };

            yield return new object[]
            {
                "OkResponse",
                this._okResponse,
                HttpStatusCode.OK,
                "ABC",
                null
            };

            yield return new object[]
            {
                "NotFoundResponse",
                this._notFoundResponse,
                HttpStatusCode.NotFound,
                null,
                "NotFound"
            };
        }

        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            if (data != null)
            {
                return string.Format("{0}_{1}", methodInfo.Name, data[0]);
            }

            return string.Empty;
        }

        private readonly HttpResponseMessage _nullResponse = null;

        private readonly HttpResponseMessage _okResponse =
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("ABC")
            };

        private readonly HttpResponseMessage _notFoundResponse =
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            };
    }
}
