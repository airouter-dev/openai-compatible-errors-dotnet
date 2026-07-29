using System.Net;
using System.Reflection;
using System.Runtime.Versioning;

namespace AiRouter.OpenAICompatibleErrors.NetStandardAsset.Tests;

public sealed class NetStandardAssetTests
{
    [Fact]
    public void TestProcessLoadsTheNetStandardAsset()
    {
        var framework = typeof(ErrorNormalizer).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()?
            .FrameworkName;

        Assert.Equal(".NETStandard,Version=v2.0", framework);
    }

    [Fact]
    public void StatusBearingHttpRequestExceptionFailsClosedOnNetStandard()
    {
        var exception = new HttpRequestException(
            "private upstream body",
            null,
            HttpStatusCode.Unauthorized);

        var error = ErrorNormalizer.FromException(exception);
        var plan = RetryPlanner.Plan(error, new RetryContext(1, ReplaySafety.Safe));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
        Assert.Null(error.StatusCode);
        Assert.Equal(RetryAction.ManualDecision, plan.Action);
        Assert.Equal("error_semantics_unknown", plan.Reason);
    }

    [Fact]
    public void UnknownCancellationFailsClosedOnNetStandard()
    {
        var error = ErrorNormalizer.FromException(new OperationCanceledException("private"));
        var plan = RetryPlanner.Plan(error, new RetryContext(1, ReplaySafety.Safe));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
        Assert.Equal(RetryAction.ManualDecision, plan.Action);
    }
}
