namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Monotonic evidence about a streaming response's replay boundary.</summary>
public enum StreamProgress
{
    /// <summary>No response-body evidence has been observed.</summary>
    None = 0,

    /// <summary>Transport bytes arrived, but the caller has not classified them as semantic output.</summary>
    TransportBytes = 1,

    /// <summary>The stream state cannot be classified confidently.</summary>
    Uncertain = 2,

    /// <summary>User-visible text, reasoning, audio, image, refusal, or tool-call data was observed.</summary>
    SemanticOutput = 3,

    /// <summary>A success, failure, incomplete, or other terminal event was observed.</summary>
    Terminal = 4,
}
