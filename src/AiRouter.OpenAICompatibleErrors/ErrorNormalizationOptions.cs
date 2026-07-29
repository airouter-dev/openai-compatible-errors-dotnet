namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Bounds untrusted response metadata during error normalization.</summary>
public sealed class ErrorNormalizationOptions
{
    /// <summary>Gets or sets the largest accepted server-directed retry delay.</summary>
    /// <remarks>Values above this limit are clamped. The default is two minutes.</remarks>
    public TimeSpan MaximumRetryAfter { get; set; } = TimeSpan.FromMinutes(2);

    internal TimeSpan GetValidatedMaximumRetryAfter()
    {
        if (MaximumRetryAfter < TimeSpan.Zero || MaximumRetryAfter > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumRetryAfter),
                "MaximumRetryAfter must be between zero and one day.");
        }

        return MaximumRetryAfter;
    }
}
