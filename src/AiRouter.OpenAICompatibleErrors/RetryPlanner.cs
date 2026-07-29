namespace AiRouter.OpenAICompatibleErrors;

/// <summary>Plans bounded retries without sending, sleeping, logging, or replaying an operation.</summary>
public static class RetryPlanner
{
    /// <summary>Returns a conservative action from normalized failure and caller-supplied replay evidence.</summary>
    public static RetryPlan Plan(
        NormalizedError error,
        RetryContext context,
        RetryPolicyOptions? options = null)
    {
        error = EnsureNotNull(error, nameof(error));
        context = EnsureNotNull(context, nameof(context));

        var policy = options ?? new RetryPolicyOptions();
        policy.Validate();

        var permanent = PlanPermanentFailure(error, context);
        if (permanent is not null)
        {
            return permanent;
        }

        if (context.AttemptNumber >= policy.MaximumAttempts)
        {
            return DoNotRetry("attempt_budget_exhausted");
        }

        switch (context.StreamProgress)
        {
            case StreamProgress.None:
                break;
            case StreamProgress.SemanticOutput:
            case StreamProgress.Terminal:
                return DoNotRetry("stream_committed");
            case StreamProgress.TransportBytes:
            case StreamProgress.Uncertain:
                return ManualDecision("stream_boundary_uncertain");
            default:
                return ManualDecision("stream_progress_invalid");
        }

        switch (context.ReplaySafety)
        {
            case ReplaySafety.Safe:
                break;
            case ReplaySafety.Unsafe:
                return DoNotRetry("replay_unsafe");
            case ReplaySafety.Unknown:
                return ManualDecision("replay_safety_unknown");
            default:
                return ManualDecision("replay_safety_invalid");
        }

        if (error.RetryAfterWasClamped)
        {
            return ManualDecision("retry_after_exceeds_limit");
        }

        switch (error.Kind)
        {
            case ErrorKind.Network:
            case ErrorKind.Timeout:
                return Retry(error, context, policy, "transient_error");
            case ErrorKind.Server:
                return IsKnownTransientServerStatus(error.StatusCode)
                    ? Retry(error, context, policy, "transient_error")
                    : ManualDecision("server_status_ambiguous");
            case ErrorKind.RateLimitOrQuota:
                return context.RateLimitCause == RateLimitCause.Transient
                    ? Retry(error, context, policy, "transient_rate_limit")
                    : ManualDecision("rate_limit_or_quota_ambiguous");
            case ErrorKind.Conflict:
                return ManualDecision("conflict_semantics_unknown");
            default:
                return ManualDecision("error_semantics_unknown");
        }
    }

    private static RetryPlan? PlanPermanentFailure(NormalizedError error, RetryContext context)
    {
        switch (error.Kind)
        {
            case ErrorKind.Cancelled:
                return DoNotRetry("caller_cancelled");
            case ErrorKind.Authentication:
            case ErrorKind.Permission:
            case ErrorKind.NotFound:
            case ErrorKind.InvalidRequest:
                return DoNotRetry("non_transient_error");
            case ErrorKind.RateLimitOrQuota when context.RateLimitCause == RateLimitCause.QuotaExhausted:
                return DoNotRetry("quota_exhausted");
            default:
                return null;
        }
    }

    private static RetryPlan Retry(
        NormalizedError error,
        RetryContext context,
        RetryPolicyOptions policy,
        string reason)
    {
        var exponential = CalculateExponentialDelay(context.AttemptNumber, policy);
        var usesServerDelay = error.RetryAfter.HasValue && error.RetryAfter.Value > exponential;
        var delay = usesServerDelay ? error.RetryAfter!.Value : exponential;
        return new RetryPlan(RetryAction.Retry, delay, reason, usesServerDelay);
    }

    private static TimeSpan CalculateExponentialDelay(int attemptNumber, RetryPolicyOptions policy)
    {
        var delayTicks = policy.BaseDelay.Ticks;
        var maximumTicks = policy.MaximumDelay.Ticks;

        if (delayTicks == 0)
        {
            return TimeSpan.Zero;
        }

        for (var index = 1; index < attemptNumber && delayTicks < maximumTicks; index++)
        {
            if (delayTicks > maximumTicks / 2)
            {
                return policy.MaximumDelay;
            }

            delayTicks *= 2;
        }

        return TimeSpan.FromTicks(Math.Min(delayTicks, maximumTicks));
    }

    private static bool IsKnownTransientServerStatus(int? statusCode)
    {
        return statusCode == 500 || statusCode == 502 || statusCode == 503 || statusCode == 504;
    }

    private static RetryPlan DoNotRetry(string reason)
    {
        return new RetryPlan(RetryAction.DoNotRetry, null, reason, false);
    }

    private static RetryPlan ManualDecision(string reason)
    {
        return new RetryPlan(RetryAction.ManualDecision, null, reason, false);
    }

    private static T EnsureNotNull<T>(T? value, string parameterName)
        where T : class
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif
        return value;
    }
}
