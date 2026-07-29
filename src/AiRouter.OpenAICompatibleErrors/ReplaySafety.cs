namespace AiRouter.OpenAICompatibleErrors;

/// <summary>States whether repeating the complete logical operation is safe.</summary>
public enum ReplaySafety
{
    /// <summary>The caller cannot prove whether replay is safe.</summary>
    Unknown = 0,

    /// <summary>The caller has proved the operation can be repeated without duplicate side effects.</summary>
    Safe = 1,

    /// <summary>Repeating the operation can duplicate a charge, tool call, output, or other side effect.</summary>
    Unsafe = 2,
}
