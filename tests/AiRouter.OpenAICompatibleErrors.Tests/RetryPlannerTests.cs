using System.Net;

namespace AiRouter.OpenAICompatibleErrors.Tests;

public sealed class RetryPlannerTests
{
    public static TheoryData<ErrorKind, RetryAction, string> PermanentCases => new()
    {
        { ErrorKind.Cancelled, RetryAction.DoNotRetry, "caller_cancelled" },
        { ErrorKind.Authentication, RetryAction.DoNotRetry, "non_transient_error" },
        { ErrorKind.Permission, RetryAction.DoNotRetry, "non_transient_error" },
        { ErrorKind.NotFound, RetryAction.DoNotRetry, "non_transient_error" },
        { ErrorKind.InvalidRequest, RetryAction.DoNotRetry, "non_transient_error" },
    };

    [Theory]
    [MemberData(nameof(PermanentCases))]
    public void PermanentFailuresAreNeverRetried(ErrorKind kind, RetryAction expected, string reason)
    {
        var error = ErrorFor(kind);
        var plan = RetryPlanner.Plan(error, SafeContext());

        Assert.Equal(expected, plan.Action);
        Assert.Equal(reason, plan.Reason);
        Assert.Null(plan.Delay);
    }

    [Theory]
    [InlineData(ErrorKind.Network)]
    [InlineData(ErrorKind.Timeout)]
    [InlineData(ErrorKind.Server)]
    public void TransientFailuresRetryOnlyWithSafeReplay(ErrorKind kind)
    {
        var error = ErrorFor(kind);

        var safe = RetryPlanner.Plan(error, SafeContext());
        var unknown = RetryPlanner.Plan(error, new RetryContext(1, ReplaySafety.Unknown));
        var unsafePlan = RetryPlanner.Plan(error, new RetryContext(1, ReplaySafety.Unsafe));

        Assert.Equal(RetryAction.Retry, safe.Action);
        Assert.Equal(TimeSpan.FromMilliseconds(250), safe.Delay);
        Assert.Equal(RetryAction.ManualDecision, unknown.Action);
        Assert.Equal("replay_safety_unknown", unknown.Reason);
        Assert.Equal(RetryAction.DoNotRetry, unsafePlan.Action);
        Assert.Equal("replay_unsafe", unsafePlan.Reason);
    }

    [Theory]
    [InlineData(StreamProgress.TransportBytes)]
    [InlineData(StreamProgress.Uncertain)]
    public void AmbiguousStreamBoundaryRequiresManualDecision(StreamProgress progress)
    {
        var plan = RetryPlanner.Plan(
            ErrorFor(ErrorKind.Server),
            new RetryContext(1, ReplaySafety.Safe, progress));

        Assert.Equal(RetryAction.ManualDecision, plan.Action);
        Assert.Equal("stream_boundary_uncertain", plan.Reason);
    }

    [Theory]
    [InlineData(StreamProgress.SemanticOutput)]
    [InlineData(StreamProgress.Terminal)]
    public void CommittedStreamIsNeverReplayed(StreamProgress progress)
    {
        var plan = RetryPlanner.Plan(
            ErrorFor(ErrorKind.Server),
            new RetryContext(1, ReplaySafety.Safe, progress));

        Assert.Equal(RetryAction.DoNotRetry, plan.Action);
        Assert.Equal("stream_committed", plan.Reason);
    }

    [Fact]
    public void Unknown429IsNotAutomaticallyRetried()
    {
        var error = ErrorNormalizer.FromStatusCode(HttpStatusCode.TooManyRequests);

        var plan = RetryPlanner.Plan(error, SafeContext());

        Assert.Equal(RetryAction.ManualDecision, plan.Action);
        Assert.Equal("rate_limit_or_quota_ambiguous", plan.Reason);
    }

    [Fact]
    public void TrustedTransient429CanRetry()
    {
        var error = ErrorNormalizer.FromStatusCode(
            HttpStatusCode.TooManyRequests,
            TimeSpan.FromSeconds(3));
        var context = new RetryContext(1, ReplaySafety.Safe, rateLimitCause: RateLimitCause.Transient);

        var plan = RetryPlanner.Plan(error, context);

        Assert.Equal(RetryAction.Retry, plan.Action);
        Assert.Equal(TimeSpan.FromSeconds(3), plan.Delay);
        Assert.True(plan.UsesServerDelay);
        Assert.Equal("transient_rate_limit", plan.Reason);
    }

    [Theory]
    [InlineData(503)]
    [InlineData(429)]
    public void ClampedRetryAfterRequiresManualDecision(int statusCode)
    {
        var error = ErrorNormalizer.FromStatusCode(
            (HttpStatusCode)statusCode,
            TimeSpan.FromHours(3),
            new ErrorNormalizationOptions { MaximumRetryAfter = TimeSpan.FromMinutes(2) });
        var context = new RetryContext(
            1,
            ReplaySafety.Safe,
            rateLimitCause: statusCode == 429 ? RateLimitCause.Transient : RateLimitCause.Unknown);

        var plan = RetryPlanner.Plan(error, context);

        Assert.Equal(RetryAction.ManualDecision, plan.Action);
        Assert.Equal("retry_after_exceeds_limit", plan.Reason);
        Assert.Null(plan.Delay);
    }

    [Fact]
    public void QuotaExhaustionIsNeverRetried()
    {
        var error = ErrorNormalizer.FromStatusCode(HttpStatusCode.TooManyRequests);
        var context = new RetryContext(1, ReplaySafety.Safe, rateLimitCause: RateLimitCause.QuotaExhausted);

        var plan = RetryPlanner.Plan(error, context);

        Assert.Equal(RetryAction.DoNotRetry, plan.Action);
        Assert.Equal("quota_exhausted", plan.Reason);
    }

    [Fact]
    public void AttemptBudgetStopsOtherwiseSafeRetry()
    {
        var options = new RetryPolicyOptions { MaximumAttempts = 3 };

        var plan = RetryPlanner.Plan(
            ErrorFor(ErrorKind.Network),
            new RetryContext(3, ReplaySafety.Safe),
            options);

        Assert.Equal(RetryAction.DoNotRetry, plan.Action);
        Assert.Equal("attempt_budget_exhausted", plan.Reason);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 400)]
    [InlineData(4, 500)]
    [InlineData(30, 500)]
    public void ExponentialDelayIsDeterministicAndBounded(int attempt, int expectedMilliseconds)
    {
        var options = new RetryPolicyOptions
        {
            MaximumAttempts = 40,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaximumDelay = TimeSpan.FromMilliseconds(500),
        };

        var plan = RetryPlanner.Plan(
            ErrorFor(ErrorKind.Network),
            new RetryContext(attempt, ReplaySafety.Safe),
            options);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), plan.Delay);
    }

    [Fact]
    public void SmallerServerDelayDoesNotReduceClientBackoff()
    {
        var error = ErrorNormalizer.FromStatusCode(
            HttpStatusCode.ServiceUnavailable,
            TimeSpan.FromMilliseconds(10));
        var context = new RetryContext(2, ReplaySafety.Safe);

        var plan = RetryPlanner.Plan(error, context);

        Assert.Equal(TimeSpan.FromMilliseconds(500), plan.Delay);
        Assert.False(plan.UsesServerDelay);
    }

    [Fact]
    public void ConflictAndUnknownErrorsRequireApplicationSemantics()
    {
        var conflict = RetryPlanner.Plan(ErrorFor(ErrorKind.Conflict), SafeContext());
        var unknown = RetryPlanner.Plan(ErrorFor(ErrorKind.Unknown), SafeContext());

        Assert.Equal("conflict_semantics_unknown", conflict.Reason);
        Assert.Equal("error_semantics_unknown", unknown.Reason);
        Assert.Equal(RetryAction.ManualDecision, conflict.Action);
        Assert.Equal(RetryAction.ManualDecision, unknown.Action);
    }

    [Theory]
    [InlineData(501)]
    [InlineData(505)]
    [InlineData(599)]
    public void AmbiguousServerStatusesAreNotAutomaticallyRetried(int statusCode)
    {
        var plan = RetryPlanner.Plan(
            ErrorNormalizer.FromStatusCode((HttpStatusCode)statusCode),
            SafeContext());

        Assert.Equal(RetryAction.ManualDecision, plan.Action);
        Assert.Equal("server_status_ambiguous", plan.Reason);
    }

    [Fact]
    public void DefaultRetryActionFailsClosed()
    {
        Assert.Equal(RetryAction.ManualDecision, default);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 5, 0)]
    [InlineData(1, 0, -1)]
    [InlineData(1, 0, 3)]
    public void UndefinedContextEnumsAreRejected(int replaySafety, int streamProgress, int rateLimitCause)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RetryContext(
                1,
                (ReplaySafety)replaySafety,
                (StreamProgress)streamProgress,
                (RateLimitCause)rateLimitCause));
    }

    [Fact]
    public void VeryHighAttemptReachesConfiguredMaximumWithoutOverflow()
    {
        var options = new RetryPolicyOptions
        {
            MaximumAttempts = 100,
            BaseDelay = TimeSpan.FromTicks(1),
            MaximumDelay = TimeSpan.FromDays(1),
        };

        var plan = RetryPlanner.Plan(
            ErrorFor(ErrorKind.Network),
            new RetryContext(99, ReplaySafety.Safe),
            options);

        Assert.Equal(TimeSpan.FromDays(1), plan.Delay);
    }

    [Fact]
    public void NullArgumentsAndInvalidOptionsAreRejected()
    {
        var error = ErrorFor(ErrorKind.Network);
        var context = SafeContext();

        Assert.Throws<ArgumentNullException>(() => RetryPlanner.Plan(null!, context));
        Assert.Throws<ArgumentNullException>(() => RetryPlanner.Plan(error, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryContext(0, ReplaySafety.Safe));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPlanner.Plan(error, context, new RetryPolicyOptions { MaximumAttempts = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPlanner.Plan(error, context, new RetryPolicyOptions { BaseDelay = TimeSpan.FromSeconds(-1) }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPlanner.Plan(
                error,
                context,
                new RetryPolicyOptions
                {
                    BaseDelay = TimeSpan.FromSeconds(2),
                    MaximumDelay = TimeSpan.FromSeconds(1),
                }));
    }

    private static RetryContext SafeContext()
    {
        return new RetryContext(1, ReplaySafety.Safe);
    }

    private static NormalizedError ErrorFor(ErrorKind kind)
    {
        switch (kind)
        {
            case ErrorKind.Network:
                return ErrorNormalizer.FromException(
                    new HttpRequestException(HttpRequestError.ConnectionError, null, null, null));
            case ErrorKind.Timeout:
                return ErrorNormalizer.FromException(new TimeoutException());
            case ErrorKind.Cancelled:
                return ErrorNormalizer.FromException(
                    new OperationCanceledException(),
                    CancellationOrigin.Caller);
            case ErrorKind.Authentication:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.Unauthorized);
            case ErrorKind.Permission:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.Forbidden);
            case ErrorKind.NotFound:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.NotFound);
            case ErrorKind.InvalidRequest:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.BadRequest);
            case ErrorKind.Conflict:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.Conflict);
            case ErrorKind.RateLimitOrQuota:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.TooManyRequests);
            case ErrorKind.Server:
                return ErrorNormalizer.FromStatusCode(HttpStatusCode.ServiceUnavailable);
            default:
                return ErrorNormalizer.FromStatusCode((HttpStatusCode)418);
        }
    }
}
