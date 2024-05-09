namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherProxyService.Utils
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceProxyConfigManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    internal static class ResourceFetcherProxyUtils
    {
        private static readonly string[] _clientProviderTypeStartNameMap;
        private static readonly string[] _clientProviderTypeFoundNameMap;
        private static readonly string[] _clientProviderTypeNotFoundNameMap;
        private static readonly string[] _clientProviderTypeSkipNameMap;
        private static readonly string[] _clientProviderTypeTimeOutNameMap;

        static ResourceFetcherProxyUtils()
        {
            var values = StringEnumCache.GetValues<ClientProviderType>();
            int size = values.Count();
            _clientProviderTypeStartNameMap = new string[size];
            _clientProviderTypeFoundNameMap = new string[size];
            _clientProviderTypeNotFoundNameMap = new string[size];
            _clientProviderTypeSkipNameMap = new string[size];
            _clientProviderTypeTimeOutNameMap = new string[size];

            foreach (var key in values)
            {
                _clientProviderTypeStartNameMap[(int)key] = key.FastEnumToString() + ".Started";
                _clientProviderTypeFoundNameMap[(int)key] = key.FastEnumToString() + ".Found";
                _clientProviderTypeNotFoundNameMap[(int)key] = key.FastEnumToString() + ".NotFound";
                _clientProviderTypeSkipNameMap[(int)key] = key.FastEnumToString() + ".Skip";
                _clientProviderTypeTimeOutNameMap[(int)key] = key.FastEnumToString() + ".TimeOut";
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderStartEventName(ClientProviderType clientProviderType)
        {
            return _clientProviderTypeStartNameMap[(int)clientProviderType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderSkipEventName(ClientProviderType clientProviderType)
        {
            return _clientProviderTypeSkipNameMap[(int)clientProviderType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderFinishEventName(ClientProviderType clientProviderType, int httpStatusCode)
        {
            if (httpStatusCode == 200)
            {
                return GetClientProviderTypeFoundName(clientProviderType);
            }
            else if (httpStatusCode == 404)
            {
                return GetClientProviderTypeNotFoundName(clientProviderType);
            }
            else
            {
                return clientProviderType.FastEnumToString() + ".Error. StatusCode: " + httpStatusCode;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderExceptionEventName(string clientProviderTypeName,
            string? errorMessage, Exception ex)
        {
            return clientProviderTypeName + ".Exception. Message: " + errorMessage ?? ex.Message;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderTypeFoundName(ClientProviderType clientProviderType)
        {
            return _clientProviderTypeFoundNameMap[(int)clientProviderType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderTypeNotFoundName(ClientProviderType clientProviderType)
        {
            return _clientProviderTypeNotFoundNameMap[(int)clientProviderType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetClientProviderTypeTimeOutName(ClientProviderType clientProviderType)
        {
            return _clientProviderTypeTimeOutNameMap[(int)clientProviderType];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<ReadOnlyMemory<byte>> ConvertContentAsBytes(HttpContent content)
        {
            return content is ReadOnlyMemoryHttpContent readOnlyMemoryHttpContent ? 
                readOnlyMemoryHttpContent.MemoryContent :
                await content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

    }
}
