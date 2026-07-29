# Changelog

All notable changes are documented here. The project follows semantic versioning after `1.0.0`; before 1.0, breaking API changes may appear in minor releases.

## [0.1.0] - 2026-07-30

### Added

- Body-free normalization for `HttpResponseMessage`, `HttpStatusCode`, and exceptions.
- Bounded delta-seconds and HTTP-date `Retry-After` handling.
- Three-state replay-aware retry planning with deterministic bounded backoff.
- Explicit evidence for cancellation origin and 429 cause.
- Atomic, content-free streaming replay evidence tracking.
- `netstandard2.0` and `net8.0` package assets with fail-closed target-specific behavior.
- .NET 8, .NET 10, and forced `netstandard2.0` asset tests.

[0.1.0]: https://github.com/airouter-dev/openai-compatible-errors-dotnet/releases/tag/v0.1.0
