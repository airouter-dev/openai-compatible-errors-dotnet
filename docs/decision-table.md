# Retry decision table

This document makes the planner's ordering explicit. It is a policy reference, not a promise that retrying any remote system will succeed.

## Evaluation order

`RetryPlanner.Plan` evaluates evidence in this order:

1. Known permanent failures: caller cancellation, authentication, permission, not found, invalid request, and trusted quota exhaustion return `DoNotRetry`.
2. Attempt budget: an attempt number at or above `MaximumAttempts` returns `DoNotRetry`.
3. Stream boundary: semantic output or a terminal event returns `DoNotRetry`; transport bytes or an uncertain parser state returns `ManualDecision`.
4. Replay safety: unsafe replay returns `DoNotRetry`; unknown replay safety returns `ManualDecision`.
5. Truncated server delay: a `Retry-After` above the configured bound returns `ManualDecision`.
6. Failure category: only known transient categories can return `Retry`.

Earlier `DoNotRetry` evidence is never relaxed by later transient evidence.

## Failure categories

| Normalized kind | Additional evidence | Action | Reason |
| --- | --- | --- | --- |
| `Cancelled` | none | `DoNotRetry` | `caller_cancelled` |
| `Authentication`, `Permission`, `NotFound`, `InvalidRequest` | none | `DoNotRetry` | `non_transient_error` |
| `RateLimitOrQuota` | `QuotaExhausted` | `DoNotRetry` | `quota_exhausted` |
| `RateLimitOrQuota` | `Transient` | eligible for retry | `transient_rate_limit` |
| `RateLimitOrQuota` | `Unknown` | `ManualDecision` | `rate_limit_or_quota_ambiguous` |
| `Network`, `Timeout` | none | eligible for retry | `transient_error` |
| `Server` | 500, 502, 503, or 504 | eligible for retry | `transient_error` |
| `Server` | every other 5xx | `ManualDecision` | `server_status_ambiguous` |
| `Conflict` | none | `ManualDecision` | `conflict_semantics_unknown` |
| `Unknown` | none | `ManualDecision` | `error_semantics_unknown` |

“Eligible” still requires an available attempt, safe replay, an uncommitted stream, and an untruncated server delay.

## Stream evidence

| `StreamProgress` | Meaning | Action before failure classification |
| --- | --- | --- |
| `None` | No response-body evidence | Continue evaluation |
| `TransportBytes` | Bytes arrived but semantic meaning is unknown | `ManualDecision` |
| `Uncertain` | Parser or event boundary is not trusted | `ManualDecision` |
| `SemanticOutput` | Text, reasoning, refusal, audio, image, or tool data was exposed | `DoNotRetry` |
| `Terminal` | Success, failure, incomplete, or another terminal event was observed | `DoNotRetry` |

The enum's numeric order is intentional because `ReplayEvidenceTracker` only advances. Invalid enum values are rejected by `RetryContext`; the planner also contains fail-closed default branches as defense in depth.

## Delay calculation

For failed attempt `n`, client delay begins at `BaseDelay` and doubles for each subsequent failed attempt until `MaximumDelay`. The algorithm uses checked bounds rather than floating-point exponentiation.

When a valid `Retry-After` is larger, it becomes the returned minimum delay. When it is smaller, it cannot reduce client backoff. When it exceeds `ErrorNormalizationOptions.MaximumRetryAfter`, the planner returns `ManualDecision` instead of pretending that a truncated delay is the server's requested minimum.
