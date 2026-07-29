# AiRouter.OpenAICompatibleErrors

[![CI](https://github.com/airouter-dev/openai-compatible-errors-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/airouter-dev/openai-compatible-errors-dotnet/actions/workflows/ci.yml)

Body-free HTTP error normalization and conservative retry planning for OpenAI-compatible .NET clients.

The package turns bounded BCL metadata into a small provider-neutral error model, then returns one of three decisions: `Retry`, `DoNotRetry`, or `ManualDecision`. It never sends a request, sleeps, logs, reads a response body, or replays an operation.

> This project is independently maintained by AI ROUTER. “OpenAI-compatible” describes an API shape; the project is not affiliated with, endorsed by, or sponsored by OpenAI.

## When this package helps

Use it when several call sites need the same conservative answers to questions such as:

- Was this a credential error, a known transient server response, an ambiguous 429, or an unknown failure?
- Is the complete logical operation safe to repeat?
- Did a streaming response already expose semantic output or tool-call data?
- Is `Retry-After` valid and within the delay your application is willing to honor?

If one local `switch` over `HttpStatusCode` is enough for your application, keep that switch and avoid another dependency. This package is useful when the policy boundary, replay evidence, and body-free logging contract need to stay consistent across clients.

## Install

```bash
dotnet add package AiRouter.OpenAICompatibleErrors --version 0.1.0
```

The package targets `netstandard2.0` and `net8.0` and has no runtime package dependencies.

## Quick start

```csharp
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

if (plan.Action == RetryAction.Retry)
{
    // Your application owns the delay, cancellation, request construction,
    // idempotency controls, telemetry, and next attempt.
    await Task.Delay(plan.Delay!.Value, cancellationToken);
}
```

The same executable example is in [`samples/BasicUsage`](https://github.com/airouter-dev/openai-compatible-errors-dotnet/tree/main/samples/BasicUsage).

## What “conservative” means

Automatic retry requires all of the following:

1. The failed attempt is still within the configured attempt budget.
2. The caller proves the *whole logical operation* is safe to replay.
3. No semantic streaming output or terminal event was observed.
4. The failure is a known transient category.
5. A server-directed delay was not truncated by the local safety bound.

Anything ambiguous returns `ManualDecision`; it does not silently become approval to retry.

| Evidence | Result |
| --- | --- |
| 401, 403, 404, invalid request, caller cancellation, exhausted quota | `DoNotRetry` |
| 500, 502, 503, or 504 + safe replay + uncommitted stream | `Retry` |
| Known connection/name-resolution/early-ended response + safe replay | `Retry` on `net8.0` |
| 429 without trusted throttling evidence | `ManualDecision` |
| 501, 505, private 5xx, unknown exception, unknown cancellation source | `ManualDecision` |
| Transport bytes or an uncertain stream boundary | `ManualDecision` |
| Text, tool-call fragments, or a terminal stream event | `DoNotRetry` |
| Unknown or unsafe replay semantics | `ManualDecision` or `DoNotRetry` |

The complete matrix and ordering rules are documented in [`docs/decision-table.md`](https://github.com/airouter-dev/openai-compatible-errors-dotnet/blob/main/docs/decision-table.md).

## Normalize without reading bodies

From an HTTP response:

```csharp
var error = ErrorNormalizer.FromResponse(response);

Console.WriteLine(error.Kind);       // Server
Console.WriteLine(error.StatusCode); // 503
Console.WriteLine(error);            // bounded metadata only
```

`FromResponse` inspects only the status and the BCL-parsed `Retry-After` header. It does not call `ReadAsStringAsync`, buffer content, or retain the response.

From a status code you already own:

```csharp
var error = ErrorNormalizer.FromStatusCode(
    HttpStatusCode.TooManyRequests,
    retryAfter: TimeSpan.FromSeconds(5));
```

From an exception:

```csharp
var error = ErrorNormalizer.FromException(exception);
```

Exception messages, stacks, `Data`, and inner exception graphs are not retained or printed. On `net8.0`, known `HttpRequestError` values and a present `HttpRequestException.StatusCode` are used. The `netstandard2.0` asset cannot safely inspect those newer properties, so an `HttpRequestException` without portable proof becomes `Unknown` and requires a manual decision.

## Cancellation must be explicit

An `OperationCanceledException` does not prove whether the caller cancelled or an internal timeout fired. Unknown evidence therefore fails closed:

```csharp
var unknown = ErrorNormalizer.FromException(new OperationCanceledException());
// unknown.Kind == ErrorKind.Unknown

var callerStopped = ErrorNormalizer.FromException(
    new OperationCanceledException(),
    CancellationOrigin.Caller);
// callerStopped.Kind == ErrorKind.Cancelled

var timedOut = ErrorNormalizer.FromException(
    new TaskCanceledException(),
    CancellationOrigin.Timeout);
// timedOut.Kind == ErrorKind.Timeout
```

Pass `CancellationOrigin.Timeout` only when a trusted timeout mechanism—not absence of caller cancellation—provides that evidence.

## 429: rate limit or exhausted quota

A body-free generic layer cannot safely distinguish temporary throttling from exhausted credit or a billing limit. A plain 429 therefore produces `ManualDecision` even when replay is safe:

```csharp
var error = ErrorNormalizer.FromStatusCode(HttpStatusCode.TooManyRequests);
var plan = RetryPlanner.Plan(error, new RetryContext(1, ReplaySafety.Safe));

// plan.Action == RetryAction.ManualDecision
// plan.Reason == "rate_limit_or_quota_ambiguous"
```

Only pass `RateLimitCause.Transient` after a trusted provider-specific layer has established temporary throttling:

```csharp
var context = new RetryContext(
    attemptNumber: 1,
    replaySafety: ReplaySafety.Safe,
    rateLimitCause: RateLimitCause.Transient);

var plan = RetryPlanner.Plan(error, context);
```

Do not infer that value by searching an untrusted response body for a convenient substring.

## Track streaming replay evidence

`ReplayEvidenceTracker` stores only an enum and advances atomically toward more restrictive states. It never retains text or tool arguments.

```csharp
var tracker = new ReplayEvidenceTracker();

tracker.ObserveTransportBytes(byteCount);
tracker.ObserveText(textDelta);                    // checks only empty/non-empty
tracker.ObserveToolCallFragment(argumentLength);   // accepts a count, not content
tracker.MarkUncertain();
tracker.ObserveTerminal();

var context = new RetryContext(
    attemptNumber,
    ReplaySafety.Safe,
    tracker.Snapshot);
```

`ReplaySafety.Safe` must describe the complete logical operation. A POST is not automatically unsafe and a GET is not automatically safe: idempotency keys, tool execution, billing, externally visible output, and application state all matter.

## Delay bounds

The normalizer accepts delta-seconds or HTTP-date values already parsed by `System.Net.Http.Headers`. The default maximum accepted `Retry-After` is two minutes:

```csharp
var error = ErrorNormalizer.FromResponse(
    response,
    now: clock.UtcNow,
    options: new ErrorNormalizationOptions
    {
        MaximumRetryAfter = TimeSpan.FromMinutes(10),
    });
```

The configurable bound must be between zero and one day. A larger server delay is marked as clamped and the planner returns `ManualDecision`; the shortened value is never represented as the server's true minimum wait.

Client backoff is deterministic and bounded:

```csharp
var policy = new RetryPolicyOptions
{
    MaximumAttempts = 3, // includes the first attempt
    BaseDelay = TimeSpan.FromMilliseconds(250),
    MaximumDelay = TimeSpan.FromSeconds(30),
};
```

The planner chooses the larger of the client backoff and a valid `Retry-After`. It deliberately does not add jitter because it has no random source and performs no scheduling; add jitter in the application layer if multiple workers could synchronize.

## Stable, body-free reason codes

`RetryPlan.Reason` is a short stable code such as:

- `transient_error`
- `attempt_budget_exhausted`
- `stream_committed`
- `replay_safety_unknown`
- `rate_limit_or_quota_ambiguous`
- `retry_after_exceeds_limit`

`NormalizedError.ToString()` emits only kind, source, numeric status, bounded delay, and the clamp flag. Treat even bounded operational metadata according to your own logging policy.

## Non-goals

The package does not:

- parse or retain provider response bodies;
- send HTTP requests or clone request content;
- sleep, schedule, or execute retries;
- decide whether a business operation is idempotent;
- execute tools or merge streamed output;
- provide an `HttpClient` handler, SDK, or provider integration;
- promise that any upstream service is available.

See [`docs/threat-model.md`](https://github.com/airouter-dev/openai-compatible-errors-dotnet/blob/main/docs/threat-model.md) for the assumptions behind those boundaries.

## Build and verify

The repository pins the .NET SDK and NuGet dependency graph.

```bash
dotnet restore AiRouter.OpenAICompatibleErrors.slnx --locked-mode
dotnet build AiRouter.OpenAICompatibleErrors.slnx -c Release --no-restore
dotnet test AiRouter.OpenAICompatibleErrors.slnx -c Release --no-build
dotnet run --project samples/BasicUsage/BasicUsage.csproj -c Release --no-build
dotnet pack src/AiRouter.OpenAICompatibleErrors/AiRouter.OpenAICompatibleErrors.csproj \
  -c Release --no-build
```

The tests run on .NET 8 and .NET 10. A separate test project forces the process to load the `netstandard2.0` DLL so its fail-closed behavior is tested rather than inferred.

## Security and support

Please use GitHub private vulnerability reporting for suspected security issues; do not paste production prompts, response bodies, credentials, or customer data into a public issue. See [`SECURITY.md`](https://github.com/airouter-dev/openai-compatible-errors-dotnet/blob/main/SECURITY.md).

The API is pre-1.0. Breaking changes may occur between minor versions and will be documented in [`CHANGELOG.md`](https://github.com/airouter-dev/openai-compatible-errors-dotnet/blob/main/CHANGELOG.md).

## License

MIT. See [`LICENSE`](https://github.com/airouter-dev/openai-compatible-errors-dotnet/blob/main/LICENSE).
