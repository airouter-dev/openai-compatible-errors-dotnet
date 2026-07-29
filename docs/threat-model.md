# Threat model and trust boundaries

## Assets the package tries not to retain

- prompts, completions, reasoning text, refusals, audio, images, and embeddings;
- tool names and tool arguments;
- provider error bodies;
- exception messages, stacks, `Data`, inner exceptions, and object graphs;
- credentials, authorization headers, request bodies, and request objects.

`NormalizedError` contains only enums, an optional numeric HTTP status, an optional bounded delay, and a clamp flag. `ReplayEvidenceTracker` contains one integer-backed enum.

## Untrusted inputs

HTTP status values, response headers, exceptions, stream events, serialized enums, and application configuration may be malformed or adversarial. The package responds by bounding delays, rejecting undefined enums, avoiding response-body parsing, and returning `ManualDecision` when evidence is insufficient.

## Trusted inputs supplied by the application

The package cannot independently prove:

- whether repeating the complete business operation is safe;
- whether a 429 is temporary throttling or exhausted quota;
- whether an `OperationCanceledException` came from caller cancellation or a timeout;
- whether received bytes contained semantic output;
- whether application-specific conflict semantics permit retry.

Callers must supply those facts only from evidence they trust. A guessed value can defeat the conservative policy.

## Replay boundary

Retry safety covers more than the HTTP method. A request can cause billing, tool execution, external writes, user-visible streaming output, or another side effect before the client sees the failure. The package therefore requires both `ReplaySafety.Safe` and stream-boundary evidence before authorizing a retry.

The package does not clone or resend `HttpRequestMessage` because request content can be single-use, non-seekable, stateful, or sensitive.

## Header handling

Only the BCL typed `Retry-After` representation is used. Negative delays are ignored, past dates become zero, and accepted values are bounded to at most one day by configuration. A value above the configured maximum is marked as clamped and cannot authorize automatic retry.

## Target-framework difference

The `net8.0` asset can inspect `HttpRequestException.StatusCode` and `HttpRequestError`. The `netstandard2.0` API surface cannot do so portably. It returns `Unknown` for `HttpRequestException` rather than assuming a transient network failure. A dedicated test project loads the actual `netstandard2.0` DLL and enforces this behavior.

## Out of scope

- transport security and certificate validation, which belong to the configured HTTP stack;
- provider-specific body schemas;
- application logging and telemetry pipelines;
- distributed retry coordination, jitter, hedging, and circuit breaking;
- request idempotency keys and business transactions;
- compromise of the build platform or package registry.

The repository mitigates the last item with pinned dependencies, locked restore, immutable workflow action references, package-content inspection, GitHub artifact attestations, and NuGet Trusted Publishing. Those controls reduce risk but do not make the supply chain infallible.
