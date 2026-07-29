namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Defines bounded deterministic retry-planning limits.</summary>
public sealed class RetryPolicyOptions
{
    /// <summary>Gets or sets the total number of permitted attempts, including the first attempt.</summary>
    public int MaximumAttempts { get; set; } = 3;

    /// <summary>Gets or sets the deterministic delay after the first failed attempt.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets or sets the maximum client-computed exponential delay.</summary>
    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (MaximumAttempts < 1 || MaximumAttempts > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumAttempts),
                "MaximumAttempts must be between 1 and 100.");
        }

        if (BaseDelay < TimeSpan.Zero || BaseDelay > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BaseDelay),
                "BaseDelay must be between zero and one hour.");
        }

        if (MaximumDelay < BaseDelay || MaximumDelay > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumDelay),
                "MaximumDelay must be at least BaseDelay and no more than one day.");
        }
    }
}
