namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Caller-supplied evidence that disambiguates an HTTP 429 response.</summary>
public enum RateLimitCause
{
    /// <summary>No trustworthy evidence distinguishes throttling from exhausted quota.</summary>
    Unknown = 0,

    /// <summary>Trusted, provider-specific evidence identifies temporary throttling.</summary>
    Transient = 1,

    /// <summary>Trusted, provider-specific evidence identifies exhausted quota or billing limits.</summary>
    QuotaExhausted = 2,
}
