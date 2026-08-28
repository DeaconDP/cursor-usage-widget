using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ProviderExpandPresenterTests
{
    [Fact]
    public void Toggle_expands_collapsed_provider()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.OpenAi,
            ProviderExpandState.None);

        Assert.False(state.Cursor);
        Assert.True(state.OpenAi);
        Assert.False(state.Claude);
        Assert.False(state.Gemini);
        Assert.False(state.OpenRouter);
        Assert.False(state.OpenCode);
        Assert.False(state.Fal);
        Assert.False(state.Xai);
    }

    [Fact]
    public void Toggle_collapses_expanded_provider()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.OpenAi,
            new ProviderExpandState(false, true, false, false, false, false, false, false));

        Assert.False(state.OpenAi);
    }

    [Fact]
    public void Toggle_accordion_collapses_other_providers_when_expanding()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.Gemini,
            new ProviderExpandState(true, false, false, false, false, false, false, false));

        Assert.False(state.Cursor);
        Assert.False(state.OpenAi);
        Assert.True(state.Gemini);
    }

    [Fact]
    public void Toggle_accordion_collapses_other_providers_when_expanding_claude()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.Claude,
            new ProviderExpandState(true, false, false, false, false, false, false, false));

        Assert.False(state.Cursor);
        Assert.True(state.Claude);
        Assert.False(state.Gemini);
    }

    [Fact]
    public void ExpandOnly_enables_single_provider()
    {
        var state = ProviderExpandState.ExpandOnly(ProviderSection.Gemini);

        Assert.False(state.Cursor);
        Assert.False(state.OpenAi);
        Assert.False(state.Claude);
        Assert.True(state.Gemini);
        Assert.False(state.OpenRouter);
        Assert.False(state.OpenCode);
        Assert.False(state.Fal);
        Assert.False(state.Xai);
    }

    [Fact]
    public void ExpandOnly_fal_enables_only_fal()
    {
        var state = ProviderExpandState.ExpandOnly(ProviderSection.Fal);

        Assert.True(state.Fal);
        Assert.False(state.Xai);
        Assert.False(state.OpenCode);
        Assert.False(state.OpenRouter);
    }

    [Fact]
    public void ExpandOnly_xai_enables_only_xai()
    {
        var state = ProviderExpandState.ExpandOnly(ProviderSection.Xai);

        Assert.True(state.Xai);
        Assert.False(state.Fal);
        Assert.False(state.OpenCode);
    }
}
