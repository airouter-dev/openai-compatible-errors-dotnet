using System.Net;
using System.Net.Http.Headers;

namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Creates body-free errors from BCL HTTP responses, status codes, and exceptions.</summary>
public static class ErrorNormalizer
{
    /// <summary>Normalizes an HTTP response without reading, buffering, or retaining its content.</summary>
    /// <param name="response">The response whose status and Retry-After header are inspected.</param>
    /// <param name="now">The clock value used for an HTTP-date Retry-After value.</param>
    /// <param name="options">Optional metadata bounds.</param>
    /// <returns>A bounded normalized error.</returns>
    public static NormalizedError FromResponse(
        HttpResponseMessage response,
        DateTimeOffset? now = null,
        ErrorNormalizationOptions? options = null)
    {
        response = EnsureNotNull(response, nameof(response));

        var maximum = GetMaximum(options);
        var parsed = ParseRetryAfter(response.Headers.RetryAfter, now ?? DateTimeOffset.UtcNow, maximum);
        return new NormalizedError(
            ClassifyStatusCode((int)response.StatusCode),
            ErrorSource.Response,
            (int)response.StatusCode,
            parsed.Delay,
            parsed.WasClamped);
    }

    /// <summary>Normalizes a status code and optional already-parsed Retry-After delay.</summary>
    public static NormalizedError FromStatusCode(
        HttpStatusCode statusCode,
        TimeSpan? retryAfter = null,
        ErrorNormalizationOptions? options = null)
    {
        var maximum = GetMaximum(options);
        var parsed = BoundDelay(retryAfter, maximum);
        return new NormalizedError(
            ClassifyStatusCode((int)statusCode),
            ErrorSource.StatusCode,
            (int)statusCode,
            parsed.Delay,
            parsed.WasClamped);
    }

    /// <summary>Normalizes an exception without retaining the exception, message, stack, or data.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="cancellationOrigin">
    /// Trusted evidence explaining an <see cref="OperationCanceledException"/>. Unknown fails closed.
    /// </param>
    public static NormalizedError FromException(
        Exception exception,
        CancellationOrigin cancellationOrigin = CancellationOrigin.Unknown)
    {
        exception = EnsureNotNull(exception, nameof(exception));
        ValidateCancellationOrigin(cancellationOrigin, nameof(cancellationOrigin));

        if (exception is OperationCanceledException)
        {
            switch (cancellationOrigin)
            {
                case CancellationOrigin.Caller:
                    return CreateExceptionError(ErrorKind.Cancelled, null);
                case CancellationOrigin.Timeout:
                    return CreateExceptionError(ErrorKind.Timeout, null);
                default:
                    return CreateExceptionError(ErrorKind.Unknown, null);
            }
        }

        if (exception is TimeoutException)
        {
            return CreateExceptionError(ErrorKind.Timeout, null);
        }

        if (exception is HttpRequestException httpException)
        {
#if NET8_0_OR_GREATER
            if (httpException.StatusCode.HasValue)
            {
                var statusCode = (int)httpException.StatusCode.Value;
                return CreateExceptionError(ClassifyStatusCode(statusCode), statusCode);
            }

            switch (httpException.HttpRequestError)
            {
                case HttpRequestError.NameResolutionError:
                case HttpRequestError.ConnectionError:
                case HttpRequestError.ResponseEnded:
                    return CreateExceptionError(ErrorKind.Network, null);
                default:
                    return CreateExceptionError(ErrorKind.Unknown, null);
            }
#else
            return CreateExceptionError(ErrorKind.Unknown, null);
#endif
        }

        return CreateExceptionError(ErrorKind.Unknown, null);
    }

    internal static ErrorKind ClassifyStatusCode(int statusCode)
    {
        switch (statusCode)
        {
            case 400:
            case 405:
            case 406:
            case 411:
            case 413:
            case 414:
            case 415:
            case 422:
                return ErrorKind.InvalidRequest;
            case 401:
                return ErrorKind.Authentication;
            case 403:
                return ErrorKind.Permission;
            case 404:
                return ErrorKind.NotFound;
            case 408:
                return ErrorKind.Timeout;
            case 409:
                return ErrorKind.Conflict;
            case 429:
                return ErrorKind.RateLimitOrQuota;
            default:
                return statusCode >= 500 && statusCode <= 599
                    ? ErrorKind.Server
                    : ErrorKind.Unknown;
        }
    }

    private static NormalizedError CreateExceptionError(ErrorKind kind, int? statusCode)
    {
        return new NormalizedError(kind, ErrorSource.Exception, statusCode, null, false);
    }

    private static void ValidateCancellationOrigin(CancellationOrigin value, string parameterName)
    {
        if (value < CancellationOrigin.Unknown || value > CancellationOrigin.Timeout)
        {
            throw new ArgumentOutOfRangeException(parameterName, "CancellationOrigin is not defined.");
        }
    }

    private static TimeSpan GetMaximum(ErrorNormalizationOptions? options)
    {
        return (options ?? new ErrorNormalizationOptions()).GetValidatedMaximumRetryAfter();
    }

    private static RetryAfterResult ParseRetryAfter(
        RetryConditionHeaderValue? value,
        DateTimeOffset now,
        TimeSpan maximum)
    {
        if (value is null)
        {
            return new RetryAfterResult(null, false);
        }

        if (value.Delta.HasValue)
        {
            return BoundDelay(value.Delta.Value, maximum);
        }

        if (value.Date.HasValue)
        {
            var delay = value.Date.Value - now;
            return BoundDelay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, maximum);
        }

        return new RetryAfterResult(null, false);
    }

    private static RetryAfterResult BoundDelay(TimeSpan? delay, TimeSpan maximum)
    {
        if (!delay.HasValue || delay.Value < TimeSpan.Zero)
        {
            return new RetryAfterResult(null, false);
        }

        if (delay.Value > maximum)
        {
            return new RetryAfterResult(maximum, true);
        }

        return new RetryAfterResult(delay.Value, false);
    }

    private readonly struct RetryAfterResult
    {
        public RetryAfterResult(TimeSpan? delay, bool wasClamped)
        {
            Delay = delay;
            WasClamped = wasClamped;
        }

        public TimeSpan? Delay { get; }

        public bool WasClamped { get; }
    }

    private static T EnsureNotNull<T>(T? value, string parameterName)
        where T : class
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif
        return value;
    }
}
