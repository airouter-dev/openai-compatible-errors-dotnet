namespace AiRouter.OpenAICompatibleErrors;

/// <summary>A deterministic decision; it never sleeps or executes a request.</summary>
public sealed class RetryPlan
{
    internal RetryPlan(RetryAction action, TimeSpan? delay, string reason, bool usesServerDelay)
    {
        Action = action;
        Delay = delay;
        Reason = reason;
        UsesServerDelay = usesServerDelay;
    }

    /// <summary>Gets the conservative action.</summary>
    public RetryAction Action { get; }

    /// <summary>Gets the minimum delay before another attempt, only when <see cref="Action"/> is Retry.</summary>
    public TimeSpan? Delay { get; }

    /// <summary>Gets a stable, body-free reason code.</summary>
    public string Reason { get; }

    /// <summary>Gets a value indicating whether Retry-After increased the returned delay.</summary>
    public bool UsesServerDelay { get; }
}
