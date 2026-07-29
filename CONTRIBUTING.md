# Contributing

Contributions that make failure handling more conservative, portable, or testable are welcome.

## Before opening a pull request

1. Explain the failure mode with synthetic inputs. Never include production prompts, response bodies, credentials, or customer data.
2. Preserve the no-body, no-send, no-sleep, no-log boundaries.
3. Add tests for the affected target framework and both positive and fail-closed paths.
4. Run the locked verification commands below.

```bash
dotnet restore AiRouter.OpenAICompatibleErrors.slnx --locked-mode
dotnet build AiRouter.OpenAICompatibleErrors.slnx -c Release --no-restore
dotnet test AiRouter.OpenAICompatibleErrors.slnx -c Release --no-build
dotnet run --project samples/BasicUsage/BasicUsage.csproj -c Release --no-build
```

If dependencies intentionally change, run `dotnet restore AiRouter.OpenAICompatibleErrors.slnx --force-evaluate` and include the lock-file changes in the same pull request.

## Design expectations

- Unknown evidence must not authorize automatic replay.
- `RetryAction` zero remains a fail-closed value.
- Public enums require validation at input boundaries.
- New target-specific behavior requires a test that loads that exact asset.
- Public APIs require XML documentation and a concrete use case.
- Runtime dependencies need a clear benefit that cannot reasonably be implemented with the BCL.

Use a focused conventional commit such as `fix: fail closed on unknown cancellation source`.
