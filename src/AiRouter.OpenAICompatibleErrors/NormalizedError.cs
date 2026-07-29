using System.Globalization;

namespace AiRouter.OpenAICompatibleErrors;

/// <summary>A bounded failure description that never retains a response body or exception message.</summary>
public sealed class NormalizedError
{
    internal NormalizedError(
        ErrorKind kind,
        ErrorSource source,
        int? statusCode,
        TimeSpan? retryAfter,
        bool retryAfterWasClamped)
    {
        Kind = kind;
        Source = source;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        RetryAfterWasClamped = retryAfterWasClamped;
    }

    /// <summary>Gets the provider-neutral failure category.</summary>
    public ErrorKind Kind { get; }

    /// <summary>Gets the bounded input source.</summary>
    public ErrorSource Source { get; }

    /// <summary>Gets the numeric HTTP status code when one was available.</summary>
    public int? StatusCode { get; }

    /// <summary>Gets the bounded Retry-After delay when one was valid.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Gets a value indicating whether Retry-After exceeded the configured maximum.</summary>
    public bool RetryAfterWasClamped { get; }

    /// <summary>Returns a body-free diagnostic suitable for structured logs.</summary>
    public override string ToString()
    {
        var status = StatusCode.HasValue
            ? StatusCode.Value.ToString(CultureInfo.InvariantCulture)
            : "none";
        var retryAfterMilliseconds = RetryAfter.HasValue
            ? RetryAfter.Value.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)
            : "none";

        return string.Format(
            CultureInfo.InvariantCulture,
            "kind={0} source={1} status={2} retry_after_ms={3} retry_after_clamped={4}",
            Kind,
            Source,
            status,
            retryAfterMilliseconds,
            RetryAfterWasClamped ? "true" : "false");
    }
}
