namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Describes a body-free, provider-neutral failure category.</summary>
public enum ErrorKind
{
    /// <summary>The failure could not be classified safely.</summary>
    Unknown = 0,

    /// <summary>The request failed before a usable HTTP response was available.</summary>
    Network = 1,

    /// <summary>The request or upstream operation timed out.</summary>
    Timeout = 2,

    /// <summary>The caller explicitly cancelled the operation.</summary>
    Cancelled = 3,

    /// <summary>Authentication failed, normally HTTP 401.</summary>
    Authentication = 4,

    /// <summary>The credential is not permitted to perform the operation, normally HTTP 403.</summary>
    Permission = 5,

    /// <summary>The requested endpoint or resource was not found, normally HTTP 404.</summary>
    NotFound = 6,

    /// <summary>The request was invalid, normally HTTP 400 or 422.</summary>
    InvalidRequest = 7,

    /// <summary>The operation conflicted with current state, normally HTTP 409.</summary>
    Conflict = 8,

    /// <summary>HTTP 429 without unsafe body inspection; it may mean transient throttling or exhausted quota.</summary>
    RateLimitOrQuota = 9,

    /// <summary>The upstream returned an HTTP 5xx response.</summary>
    Server = 10,
}
