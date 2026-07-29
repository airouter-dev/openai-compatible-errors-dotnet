namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Caller-supplied evidence that explains an <see cref="OperationCanceledException"/>.</summary>
public enum CancellationOrigin
{
    /// <summary>The application cannot prove whether the caller or a timeout caused cancellation.</summary>
    Unknown = 0,

    /// <summary>The application's own cancellation request stopped the operation.</summary>
    Caller = 1,

    /// <summary>A trusted timeout mechanism stopped the operation.</summary>
    Timeout = 2,
}
