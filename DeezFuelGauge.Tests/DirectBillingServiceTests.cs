using System.Diagnostics;
using System.Net;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class DirectBillingServiceTests
{
    [Fact]
    public async Task EnrichAsync_returns_unavailable_pro_limits_when_disabled()
    {
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { ShowProLimits = false },
            Gemini = new ProviderBillingSettings { ShowProLimits = false },
            OpenRouter = new ProviderBillingSettings { ShowProLimits = false },
            OpenCode = new ProviderBillingSettings { ShowProLimits = false, ShowDirectSource = false },
            Fal = new ProviderBillingSettings { ShowProLimits = false },
            Xai = new ProviderBillingSettings { ShowProLimits = false },
            GrokBot = new ProviderBillingSettings { ShowProLimits = false }
        };

        var source = new UsageSnapshot { PercentUsed = 42 };
        using var service = CreateServiceWithoutLocalAuth();
        var enriched = await service.EnrichAsync(source, settings);

        Assert.False(enriched.Codex.IsAvailable);
        Assert.False(enriched.Antigravity.IsAvailable);
        Assert.False(enriched.OpenRouter.IsAvailable);
        Assert.False(enriched.OpenCode.IsAvailable);
        Assert.False(enriched.Fal.IsAvailable);
        Assert.False(enriched.Xai.IsAvailable);
        Assert.False(enriched.GrokBot.IsAvailable);
        Assert.Equal(42, enriched.PercentUsed);
    }

    [Fact]
    public async Task EnrichAsync_fetches_enabled_providers_in_parallel()
    {
        var handler = new ConcurrentTrackingHandler();
        var openAiKey = CredentialStore.Store("test-openai", "sk-openai-test");

        try
        {
            var settings = new WidgetSettings
            {
                OpenAi = new ProviderBillingSettings
                {
                    ShowDirectSource = true,
                    ShowProLimits = true,
                    CredentialId = openAiKey
                },
                Claude = new ProviderBillingSettings { ShowProLimits = false },
                Gemini = new ProviderBillingSettings { ShowProLimits = false },
                OpenRouter = new ProviderBillingSettings { ShowProLimits = false },
                OpenCode = new ProviderBillingSettings { ShowProLimits = false, ShowDirectSource = false },
                Fal = new ProviderBillingSettings { ShowProLimits = false },
                Xai = new ProviderBillingSettings { ShowProLimits = false },
                GrokBot = new ProviderBillingSettings { ShowProLimits = false }
            };

            var http = new HttpClient(handler);
            using var service = new DirectBillingService(
                new OpenAiBillingClient(http),
                new CodexUsageClient(
                    http,
                    authFilePathResolver: () => null,
                    authResolver: new CodexAuthResolver(
                        authFileReader: () => new CodexAuth("token", "account"),
                        browserCookieReader: () => null,
                        savedSessionReader: _ => null)),
                new AnthropicBillingClient(http),
                new ClaudeProUsageClient(http),
                new AntigravityUsageClient(http),
                new OpenRouterUsageClient(http),
                new OpenCodeUsageClient(http),
                new FalUsageClient(http));

            var source = new UsageSnapshot { PercentUsed = 10 };
            var sw = Stopwatch.StartNew();
            var enriched = await service.EnrichAsync(source, settings);
            sw.Stop();

            Assert.Equal(10, enriched.PercentUsed);
            Assert.True(handler.TotalRequests >= 3, $"expected HTTP calls from OpenAI and Codex, saw {handler.TotalRequests}");
            Assert.True(handler.MaxConcurrent >= 2, $"expected overlapping OpenAI/Codex fetches, max concurrent was {handler.MaxConcurrent}");
            Assert.True(
                sw.ElapsedMilliseconds < 500,
                $"expected parallel fetches (OpenAI grants+costs sequential ~150ms overlapping Codex), took {sw.ElapsedMilliseconds}ms");
        }
        finally
        {
            CredentialStore.Delete(openAiKey);
        }
    }

    private static DirectBillingService CreateServiceWithoutLocalAuth()
    {
        var failingHttp = new HttpClient(new AlwaysNotFoundHandler());
        var emptyTokens = new AntigravityOAuthTokens();

        return new DirectBillingService(
            codex: new CodexUsageClient(
                failingHttp,
                authFilePathResolver: () => null,
                authResolver: new CodexAuthResolver(
                    authFileReader: () => null,
                    browserCookieReader: () => null,
                    savedSessionReader: _ => null)),
            antigravity: new AntigravityUsageClient(
                failingHttp,
                authResolver: new GeminiAuthResolver(() => emptyTokens, () => emptyTokens)),
            openRouter: new OpenRouterUsageClient(failingHttp),
            openCode: new OpenCodeUsageClient(
                failingHttp,
                authResolver: new OpenCodeAuthResolver(
                    apiKeyReader: () => null,
                    browserCookieReader: () => null,
                    savedSessionReader: _ => null)),
            fal: new FalUsageClient(failingHttp));
    }

    private sealed class AlwaysNotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class ConcurrentTrackingHandler : HttpMessageHandler
    {
        private readonly object _lock = new();
        private int _inFlight;

        public int MaxConcurrent { get; private set; }
        public int TotalRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                TotalRequests++;
                _inFlight++;
                MaxConcurrent = Math.Max(MaxConcurrent, _inFlight);
            }

            try
            {
                await Task.Delay(75, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{}")
                };
            }
            finally
            {
                lock (_lock)
                    _inFlight--;
            }
        }
    }
}
