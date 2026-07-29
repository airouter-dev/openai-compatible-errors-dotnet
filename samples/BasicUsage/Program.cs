using System.Net;
using System.Net.Http.Headers;
using AiRouter.OpenAICompatibleErrors;

using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

var error = ErrorNormalizer.FromResponse(response);
var plan = RetryPlanner.Plan(
    error,
    new RetryContext(
        attemptNumber: 1,
        replaySafety: ReplaySafety.Safe,
        streamProgress: StreamProgress.None));

Console.WriteLine(error);
Console.WriteLine($"action={plan.Action} delay_ms={plan.Delay?.TotalMilliseconds} reason={plan.Reason}");

if (plan.Action != RetryAction.Retry || plan.Delay != TimeSpan.FromSeconds(3))
{
    return 1;
}

return 0;
