using System.Net;
using System.Net.Http.Headers;

namespace AiRouter.OpenAICompatibleErrors.Tests;

public sealed class ErrorNormalizerTests
{
    [Theory]
    [InlineData(400, ErrorKind.InvalidRequest)]
    [InlineData(401, ErrorKind.Authentication)]
    [InlineData(403, ErrorKind.Permission)]
    [InlineData(404, ErrorKind.NotFound)]
    [InlineData(408, ErrorKind.Timeout)]
    [InlineData(409, ErrorKind.Conflict)]
    [InlineData(422, ErrorKind.InvalidRequest)]
    [InlineData(429, ErrorKind.RateLimitOrQuota)]
    [InlineData(500, ErrorKind.Server)]
    [InlineData(503, ErrorKind.Server)]
    [InlineData(599, ErrorKind.Server)]
    [InlineData(418, ErrorKind.Unknown)]
    public void FromStatusCodeClassifiesWithoutABody(int statusCode, ErrorKind expected)
    {
        var error = ErrorNormalizer.FromStatusCode((HttpStatusCode)statusCode);

        Assert.Equal(expected, error.Kind);
        Assert.Equal(statusCode, error.StatusCode);
        Assert.Equal(ErrorSource.StatusCode, error.Source);
        Assert.Null(error.RetryAfter);
    }

    [Fact]
    public void FromResponseNeverReadsContent()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new ThrowingContent(),
        };

        var error = ErrorNormalizer.FromResponse(response);

        Assert.Equal(ErrorKind.Server, error.Kind);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(ErrorSource.Response, error.Source);
    }

    [Fact]
    public void FromResponseUsesDeltaRetryAfter()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        var error = ErrorNormalizer.FromResponse(response, DateTimeOffset.UnixEpoch);

        Assert.Equal(TimeSpan.FromSeconds(7), error.RetryAfter);
        Assert.False(error.RetryAfterWasClamped);
    }

    [Fact]
    public void FromResponseUsesHttpDateRelativeToSuppliedClock()
    {
        var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(12));

        var error = ErrorNormalizer.FromResponse(response, now);

        Assert.Equal(TimeSpan.FromSeconds(12), error.RetryAfter);
    }

    [Fact]
    public void FromResponseTurnsPastHttpDateIntoZeroDelay()
    {
        var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddMinutes(-1));

        var error = ErrorNormalizer.FromResponse(response, now);

        Assert.Equal(TimeSpan.Zero, error.RetryAfter);
        Assert.False(error.RetryAfterWasClamped);
    }

    [Fact]
    public void FromResponseClampsOversizedRetryAfter()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(3));

        var error = ErrorNormalizer.FromResponse(
            response,
            DateTimeOffset.UnixEpoch,
            new ErrorNormalizationOptions { MaximumRetryAfter = TimeSpan.FromSeconds(45) });

        Assert.Equal(TimeSpan.FromSeconds(45), error.RetryAfter);
        Assert.True(error.RetryAfterWasClamped);
    }

    [Fact]
    public void FromResponseIgnoresMalformedRetryAfter()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        Assert.True(response.Headers.TryAddWithoutValidation("Retry-After", "secret\r\nnot-a-delay"));

        var error = ErrorNormalizer.FromResponse(response, DateTimeOffset.UnixEpoch);

        Assert.Null(error.RetryAfter);
        Assert.False(error.RetryAfterWasClamped);
    }

    [Fact]
    public void FromStatusCodeRejectsNegativePreparsedDelay()
    {
        var error = ErrorNormalizer.FromStatusCode(
            HttpStatusCode.TooManyRequests,
            TimeSpan.FromSeconds(-1));

        Assert.Null(error.RetryAfter);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1441)]
    public void OptionsRejectUnsafeMaximumRetryAfter(int minutes)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var options = new ErrorNormalizationOptions { MaximumRetryAfter = TimeSpan.FromMinutes(minutes) };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorNormalizer.FromResponse(response, DateTimeOffset.UnixEpoch, options));
    }

    [Fact]
    public void FromExceptionRequiresExplicitCancellationOrigin()
    {
        var unknown = ErrorNormalizer.FromException(new OperationCanceledException("private"));
        var cancelled = ErrorNormalizer.FromException(
            new OperationCanceledException("private"),
            CancellationOrigin.Caller);
        var timeout = ErrorNormalizer.FromException(
            new TaskCanceledException("private"),
            CancellationOrigin.Timeout);

        Assert.Equal(ErrorKind.Unknown, unknown.Kind);
        Assert.Equal(ErrorKind.Cancelled, cancelled.Kind);
        Assert.Equal(ErrorKind.Timeout, timeout.Kind);
    }

    [Fact]
    public void FromExceptionClassifiesTimeoutAndKnownNetworkFailures()
    {
        var timeout = ErrorNormalizer.FromException(new TimeoutException("private"));
        var network = ErrorNormalizer.FromException(
            new HttpRequestException(HttpRequestError.ConnectionError, "private", null, null));

        Assert.Equal(ErrorKind.Timeout, timeout.Kind);
        Assert.Equal(ErrorKind.Network, network.Kind);
    }

    [Theory]
    [InlineData(HttpRequestError.Unknown)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.UserAuthenticationError)]
    [InlineData(HttpRequestError.ProxyTunnelError)]
    [InlineData(HttpRequestError.ConfigurationLimitExceeded)]
    [InlineData(HttpRequestError.VersionNegotiationError)]
    [InlineData(HttpRequestError.HttpProtocolError)]
    [InlineData(HttpRequestError.InvalidResponse)]
    [InlineData(HttpRequestError.ExtendedConnectNotSupported)]
    public void AmbiguousHttpRequestErrorsFailClosed(HttpRequestError requestError)
    {
        var exception = new HttpRequestException(requestError, "private", null, null);

        var error = ErrorNormalizer.FromException(exception);

        Assert.Equal(ErrorKind.Unknown, error.Kind);
        Assert.Equal(ErrorSource.Exception, error.Source);
    }

    [Fact]
    public void FromHttpRequestExceptionUsesStatusCodeOnModernTargets()
    {
        var exception = new HttpRequestException(
            "private upstream body",
            null,
            HttpStatusCode.ServiceUnavailable);

        var error = ErrorNormalizer.FromException(exception);

        Assert.Equal(ErrorKind.Server, error.Kind);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(ErrorSource.Exception, error.Source);
    }

    [Fact]
    public void UnknownExceptionDoesNotRetainOrPrintMessage()
    {
        const string secret = "customer-prompt-must-not-appear";
        var error = ErrorNormalizer.FromException(new InvalidOperationException(secret));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            "kind=Unknown source=Exception status=none retry_after_ms=none retry_after_clamped=false",
            error.ToString());
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ErrorNormalizer.FromResponse(null!));
        Assert.Throws<ArgumentNullException>(() => ErrorNormalizer.FromException(null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void InvalidCancellationOriginIsRejected(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorNormalizer.FromException(new OperationCanceledException(), (CancellationOrigin)value));
    }

    private sealed class ThrowingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new InvalidOperationException("Content must not be read.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
