namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Auth;
    using Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Monitoring;

    public static class ResourceFetcherUtils
    {
        public static HashSet<string> IgnoreHttpHeadersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Set-Cookie"
        };

        public class ResourceFetcherExceptionDetail
        {
            public string? Message { get; set; }
            public string? Component { get; set; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAuthError(HttpContext context, ResourceFetcherAuthError authError)
        {
            var response = context.Response;

            /*
             * From Http spec RFC 9110
             * The 401 (Unauthorized) status code indicates that the request has not been applied because it lacks valid authentication credentials for the target resource
             * The 403 (Forbidden) status code indicates that the server understood the request but refuses to fulfill it
             */

            // 401 (Unauthorized) cases
            if (authError == ResourceFetcherAuthError.NO_PARTNER_IN_REQUEST ||
                authError == ResourceFetcherAuthError.NO_TOKEN_IN_HEADER ||
                authError == ResourceFetcherAuthError.AAD_TOKEN_EXPIRED ||
                authError == ResourceFetcherAuthError.AAD_TOKEN_AUTH_FAIL)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
            }

            // Add AuthError to Header
            response.Headers.TryAdd(CommonHttpHeaders.DataLabs_AuthError, authError.FastEnumToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogResponseInfo(HttpResponseMessage response, OpenTelemetryActivityWrapper activity)
        {
            // StatusCode 
            var statusCodeString = ((int)response.StatusCode).ToString();
            activity.SetTag(SolutionConstants.HttpStatusCode, statusCodeString);

            // ReasonPhrase
            var reasonPhrase = response.ReasonPhrase;
            if (reasonPhrase != null)
            {
                activity.SetTag(SolutionConstants.SourceReasonPhrase, reasonPhrase);
            }

            // Http Version
            var versionString = SolutionUtils.GetHttpVersionString(response.Version);
            if (versionString != null)
            {
                activity.SetTag(SolutionConstants.SourceHttpVersion, versionString);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCorrelationIdAndResourceId(
            string? correlationId,
            string? resourceIdColumnValue,
            OpenTelemetryActivityWrapper otelActivity,
            IActivity activity)
        {
            // Set CorrelationId and Resource Id
            otelActivity.InputCorrelationId = correlationId;
            otelActivity.InputResourceId = resourceIdColumnValue;

            activity.CorrelationId = correlationId;
            activity.InputResourceId = resourceIdColumnValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StatusCodeResult ForbidResult()
        {
            return new StatusCodeResult(403);  // Forbidden
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StatusCodeResult BadRequestResult()
        {
            return new StatusCodeResult(400);  // Forbidden
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IActionResult SetResourceFetcherException(
            string partnerName,
            string callMethod,
            int retryFlowCount,
            Exception exception,
            string component,
            OpenTelemetryActivityWrapper activity,
            IActivityMonitor monitor)
        {
            if (exception is OperationCanceledException && exception.InnerException is TimeoutException)
            {
                exception = exception.InnerException;
            }

            ResourceFetcherMetricProvider.AddRequestErrorCounter(partnerName, callMethod, retryFlowCount, exception);

            activity.ExportToActivityMonitor(monitor.Activity);
            monitor.OnError(exception);

            activity.RecordException(callMethod, exception);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);

            var exceptionDetail = new ResourceFetcherExceptionDetail()
            {
                Message = exception.Message,
                Component = component
            };
            
            return new ObjectResult(exceptionDetail)
            {
                StatusCode = 500  // Internal Server Error
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<IActionResult> CopyClientResponseAsync(
            HttpResponseMessage clientResponseMessage,
            HttpResponse targetResponse,
            string partnerName,
            int retryFlowCount,
            string callMethod,
            OpenTelemetryActivityWrapper activity,
            IActivityMonitor monitor,
            CancellationToken cancellationToken)
        {
            // Log StatusCode and Version
            LogResponseInfo(clientResponseMessage, activity);

            ResourceFetcherMetricProvider.AddClientResponseCounter(partnerName, callMethod, retryFlowCount, clientResponseMessage.StatusCode);

            // Resource Fetcher always return OK. Actual response code is added to Response Header
            targetResponse.StatusCode = 200;

            // Copy Http Status code to Target Response Header
            int intStatusCode = (int)clientResponseMessage.StatusCode;
            var intStatusCodeString = intStatusCode.ToString();
            targetResponse.Headers.Append(CommonHttpHeaders.DataLabs_Source_StatusCode, intStatusCodeString);
            activity.SetTag(SolutionConstants.SourceStatusCode, intStatusCodeString);

            if (clientResponseMessage.Headers.ETag != null)
            {
                targetResponse.Headers.ETag = clientResponseMessage.Headers.ETag.Tag;
            }

            // Copy Source Resource Headers to Target Response Header
            foreach (var header in clientResponseMessage.Headers)
            {
                var sourceHeaderKey = header.Key;
                if (IgnoreHttpHeadersSet.Contains(sourceHeaderKey))
                {
                    continue;
                }

                var targetHeaderKey = CommonHttpHeaders.DataLabs_Source_Header_Prefix + sourceHeaderKey;
                var value = header.Value?.ToArray();
                if (value == null || value.Length == 0)
                {
                    targetResponse.Headers.TryAdd(targetHeaderKey, string.Empty);

                    // Log Header
                    monitor.Activity[sourceHeaderKey] = string.Empty;
                }
                else
                {
                    targetResponse.Headers.TryAdd(targetHeaderKey, value);

                    // Log Header
                    if (value.Length > 1)
                    {
                        monitor.Activity.LogCollectionAndCount(sourceHeaderKey, value);
                    }
                    else
                    {
                        monitor.Activity[sourceHeaderKey] = value[0];
                    }
                }
            }

            // Write Response Content
            if (clientResponseMessage.Content != null)
            {
                await clientResponseMessage.Content.CopyToAsync(targetResponse.Body, cancellationToken).ConfigureAwait(false);
            }

            activity.ExportToActivityMonitor(monitor.Activity);
            monitor.OnCompleted();

            return ControllerBase.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParseQueryString(string queryString, ref KeyPairList<string, string> keyValuePairs)
        {
            var enumerable = new QueryStringEnumerable(queryString);
            foreach (var pair in enumerable)
            {
                keyValuePairs.Add(new KeyValuePair<string, string>(pair.DecodeName().ToString(), pair.DecodeValue().ToString()));
            }
        }
    }
}
