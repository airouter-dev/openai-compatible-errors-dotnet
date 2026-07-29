using System.Net;
using AiRouter.OpenAICompatibleErrors;

var serverError = ErrorNormalizer.FromStatusCode(
    HttpStatusCode.ServiceUnavailable,
    TimeSpan.FromSeconds(2));
var retry = RetryPlanner.Plan(
    serverError,
    new RetryContext(1, ReplaySafety.Safe));

if (retry.Action != RetryAction.Retry ||
    retry.Delay != TimeSpan.FromSeconds(2) ||
    retry.Reason != "transient_error")
{
    Console.Error.WriteLine("Expected a bounded retry for a replay-safe 503.");
    return 1;
}

var rateLimit = ErrorNormalizer.FromStatusCode(HttpStatusCode.TooManyRequests);
var ambiguous = RetryPlanner.Plan(
    rateLimit,
    new RetryContext(1, ReplaySafety.Safe));

if (ambiguous.Action != RetryAction.ManualDecision ||
    ambiguous.Reason != "rate_limit_or_quota_ambiguous")
{
    Console.Error.WriteLine("An ambiguous 429 must fail closed.");
    return 1;
}

Console.WriteLine(
    $"consumer={Environment.Version} retry={retry.Action} ambiguous_429={ambiguous.Action}");
return 0;
