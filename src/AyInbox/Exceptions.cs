using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AyInbox
{
    /// <summary>Base class of every error this library throws.</summary>
    public class AyInboxException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public AyInboxException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>The API answered with a non-2xx status.</summary>
    public class ApiException : AyInboxException
    {
        /// <summary>HTTP status code.</summary>
        public int Status { get; }
        /// <summary>Stable machine key from the error envelope (derived from the status when absent).</summary>
        public string Code { get; }
        /// <summary>Human text from the API. Never branch on it.</summary>
        public string ApiMessage { get; }
        /// <summary>Finer key on a few codes, when present.</summary>
        public string? Reason { get; }
        /// <summary>Request id from the body or the <c>X-Request-Id</c> header.</summary>
        public string? RequestId { get; }
        /// <summary>The raw response body as text.</summary>
        public string Body { get; }
        /// <summary>Response headers (names compared case-insensitively).</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }
        /// <summary>Seconds to wait before retrying, when the API said so.</summary>
        public int? RetryAfterSeconds { get; }

        /// <summary>Creates the exception from parsed response parts.</summary>
        public ApiException(int status, string code, string apiMessage, string? reason, string? requestId,
            string body, IReadOnlyDictionary<string, string> headers, int? retryAfterSeconds)
            : base(BuildMessage(status, code, apiMessage, requestId))
        {
            Status = status;
            Code = code;
            ApiMessage = apiMessage;
            Reason = reason;
            RequestId = requestId;
            Body = body;
            Headers = headers;
            RetryAfterSeconds = retryAfterSeconds;
        }

        private static string BuildMessage(int status, string code, string msg, string? requestId) =>
            $"{status} {code}: {msg}" + (requestId != null ? $" (request {requestId})" : "");

        private static readonly Regex CodeLike = new Regex("^[a-z0-9_]+$", RegexOptions.CultureInvariant);

        internal static string DefaultCode(int status)
        {
            switch (status)
            {
                case 400: return "bad_request";
                case 401: return "unauthorized";
                case 402: return "insufficient_credits";
                case 403: return "forbidden";
                case 404: return "not_found";
                case 409: return "conflict";
                case 422: return "unprocessable";
                case 429: return "rate_limit_exceeded";
                case 501: return "not_implemented";
                case 503: return "service_unavailable";
                default: return status >= 500 ? "internal_error" : "error";
            }
        }

        internal static ApiException FromResponse(int status, string body, IReadOnlyDictionary<string, string> headers)
        {
            string? code = null, message = null, reason = null, requestId = null;
            int? retryAfter = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    code = Str(root, "code");
                    var err = Str(root, "error");
                    if (string.IsNullOrEmpty(code) && err != null && CodeLike.IsMatch(err)) code = err;
                    message = Str(root, "message");
                    reason = Str(root, "reason");
                    requestId = Str(root, "request_id");
                    if (root.TryGetProperty("retry_after", out var ra) && ra.ValueKind == JsonValueKind.Number)
                        retryAfter = (int)Math.Ceiling(ra.GetDouble());
                    else if (root.TryGetProperty("retry_after_ms", out var ms) && ms.ValueKind == JsonValueKind.Number)
                        retryAfter = (int)Math.Ceiling(ms.GetDouble() / 1000.0);
                }
            }
            catch (JsonException)
            {
            }

            if (string.IsNullOrEmpty(code)) code = DefaultCode(status);
            if (requestId == null && headers.TryGetValue("X-Request-Id", out var hid)) requestId = hid;
            var headerWait = AyInboxClient.ParseRetryAfter(headers);
            if (headerWait.HasValue) retryAfter = (int)Math.Ceiling(headerWait.Value.TotalSeconds);
            message ??= body.Length > 0 && body.Length <= 500 ? body : $"HTTP {status}";
            var c = code!;

            switch (status)
            {
                case 400: return new BadRequestException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 401: return new AuthenticationException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 402: return new PaymentRequiredException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 403: return new PermissionDeniedException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 404: return new NotFoundException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 409: return new ConflictException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 422: return new UnprocessableException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 429: return new RateLimitException(status, c, message, reason, requestId, body, headers, retryAfter);
                case 503: return new ServiceUnavailableException(status, c, message, reason, requestId, body, headers, retryAfter);
            }
            if (status >= 500) return new InternalServerException(status, c, message, reason, requestId, body, headers, retryAfter);
            return new ApiException(status, c, message, reason, requestId, body, headers, retryAfter);
        }

        private static string? Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

#pragma warning disable CS1591
    /// <summary>A validation error: 400 or 422.</summary>
    public abstract class ValidationException : ApiException
    {
        /// <summary>Creates the exception.</summary>
        protected ValidationException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>400: the request was malformed or a field is invalid.</summary>
    public sealed class BadRequestException : ValidationException
    {
        public BadRequestException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>422: the request was understood but cannot be processed.</summary>
    public sealed class UnprocessableException : ValidationException
    {
        public UnprocessableException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>401: the credential is missing, invalid or no longer valid.</summary>
    public sealed class AuthenticationException : ApiException
    {
        public AuthenticationException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>402: the workspace is out of credits or not entitled.</summary>
    public sealed class PaymentRequiredException : ApiException
    {
        public PaymentRequiredException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>403: the credential lacks a scope or the action is not allowed.</summary>
    public sealed class PermissionDeniedException : ApiException
    {
        public PermissionDeniedException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>404: the resource does not exist or is not visible to this credential.</summary>
    public sealed class NotFoundException : ApiException
    {
        public NotFoundException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>409: a conflict, including idempotency key conflicts.</summary>
    public sealed class ConflictException : ApiException
    {
        public ConflictException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>429: a rate limit. <see cref="ApiException.RetryAfterSeconds"/> says how long to wait.</summary>
    public sealed class RateLimitException : ApiException
    {
        public RateLimitException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>A server error: 5xx.</summary>
    public abstract class ServerException : ApiException
    {
        /// <summary>Creates the exception.</summary>
        protected ServerException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>500, 501, 502, 504 or another 5xx other than 503.</summary>
    public sealed class InternalServerException : ServerException
    {
        public InternalServerException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }

    /// <summary>503: the service or a dependency is unavailable for now.</summary>
    public sealed class ServiceUnavailableException : ServerException
    {
        public ServiceUnavailableException(int s, string c, string m, string? r, string? id, string b, IReadOnlyDictionary<string, string> h, int? ra) : base(s, c, m, r, id, b, h, ra) { }
    }
#pragma warning restore CS1591

    /// <summary>No HTTP response was received (DNS, TLS, connection reset).</summary>
    public class NetworkException : AyInboxException
    {
        /// <summary>Creates the exception.</summary>
        public NetworkException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>The connection failed before a response arrived.</summary>
    public sealed class ConnectionException : NetworkException
    {
        /// <summary>Creates the exception.</summary>
        public ConnectionException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>The call did not finish within its timeout.</summary>
    public sealed class AyInboxTimeoutException : NetworkException
    {
        /// <summary>Creates the exception.</summary>
        public AyInboxTimeoutException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>A webhook signature did not verify.</summary>
    public sealed class SignatureVerificationException : AyInboxException
    {
        /// <summary>Creates the exception.</summary>
        public SignatureVerificationException(string message) : base(message) { }
    }
}
