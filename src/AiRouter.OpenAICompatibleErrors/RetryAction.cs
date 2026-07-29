namespace AiRouter.OpenAICompatibleErrors;

/// <summary>The conservative action returned by <see cref="RetryPlanner"/>.</summary>
public enum RetryAction
{
    /// <summary>The library lacks enough evidence; application-specific review is required.</summary>
    /// <remarks>This is the zero value so missing or default-initialized data fails closed.</remarks>
    ManualDecision = 0,

    /// <summary>The library authorizes another attempt within the supplied constraints.</summary>
    Retry = 1,

    /// <summary>The library has enough evidence to reject another attempt.</summary>
    DoNotRetry = 2,
}
