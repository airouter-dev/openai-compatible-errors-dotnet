namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Supplies application evidence for a single retry decision.</summary>
public sealed class RetryContext
{
    /// <summary>Initializes a retry context.</summary>
    /// <param name="attemptNumber">The failed attempt number, starting at one.</param>
    /// <param name="replaySafety">Whether repeating the complete operation is safe.</param>
    /// <param name="streamProgress">Observed streaming response evidence.</param>
    /// <param name="rateLimitCause">Trusted evidence that disambiguates HTTP 429.</param>
    public RetryContext(
        int attemptNumber,
        ReplaySafety replaySafety,
        StreamProgress streamProgress = StreamProgress.None,
        RateLimitCause rateLimitCause = RateLimitCause.Unknown)
    {
        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "AttemptNumber must be positive.");
        }

        ValidateReplaySafety(replaySafety, nameof(replaySafety));
        ValidateStreamProgress(streamProgress, nameof(streamProgress));
        ValidateRateLimitCause(rateLimitCause, nameof(rateLimitCause));

        AttemptNumber = attemptNumber;
        ReplaySafety = replaySafety;
        StreamProgress = streamProgress;
        RateLimitCause = rateLimitCause;
    }

    /// <summary>Gets the failed attempt number, starting at one.</summary>
    public int AttemptNumber { get; }

    /// <summary>Gets the caller's whole-operation replay-safety evidence.</summary>
    public ReplaySafety ReplaySafety { get; }

    /// <summary>Gets the streaming response evidence.</summary>
    public StreamProgress StreamProgress { get; }

    /// <summary>Gets trusted evidence that disambiguates HTTP 429.</summary>
    public RateLimitCause RateLimitCause { get; }

    private static void ValidateReplaySafety(ReplaySafety value, string parameterName)
    {
        if (value < ReplaySafety.Unknown || value > ReplaySafety.Unsafe)
        {
            throw new ArgumentOutOfRangeException(parameterName, "ReplaySafety is not defined.");
        }
    }

    private static void ValidateStreamProgress(StreamProgress value, string parameterName)
    {
        if (value < StreamProgress.None || value > StreamProgress.Terminal)
        {
            throw new ArgumentOutOfRangeException(parameterName, "StreamProgress is not defined.");
        }
    }

    private static void ValidateRateLimitCause(RateLimitCause value, string parameterName)
    {
        if (value < RateLimitCause.Unknown || value > RateLimitCause.QuotaExhausted)
        {
            throw new ArgumentOutOfRangeException(parameterName, "RateLimitCause is not defined.");
        }
    }
}
