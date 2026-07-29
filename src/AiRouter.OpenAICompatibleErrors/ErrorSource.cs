namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Identifies the bounded input used to create a normalized error.</summary>
public enum ErrorSource
{
    /// <summary>An HTTP status code supplied directly by the caller.</summary>
    StatusCode = 0,

    /// <summary>An <see cref="System.Net.Http.HttpResponseMessage"/>.</summary>
    Response = 1,

    /// <summary>An exception, without retaining its message or object graph.</summary>
    Exception = 2,
}
