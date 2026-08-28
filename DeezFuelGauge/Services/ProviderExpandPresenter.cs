namespace DeezFuelGauge.Services;

public enum ProviderSection
{
    Cursor,
    OpenAi,
    Claude,
    Gemini,
    OpenRouter,
    OpenCode,
    Fal,
    Xai
}

public readonly record struct ProviderExpandState(
    bool Cursor,
    bool OpenAi,
    bool Claude,
    bool Gemini,
    bool OpenRouter,
    bool OpenCode,
    bool Fal,
    bool Xai)
{
    public static ProviderExpandState None => new(false, false, false, false, false, false, false, false);

    public static ProviderExpandState ExpandOnly(ProviderSection section) => section switch
    {
        ProviderSection.Cursor => new(true, false, false, false, false, false, false, false),
        ProviderSection.OpenAi => new(false, true, false, false, false, false, false, false),
        ProviderSection.Claude => new(false, false, true, false, false, false, false, false),
        ProviderSection.Gemini => new(false, false, false, true, false, false, false, false),
        ProviderSection.OpenRouter => new(false, false, false, false, true, false, false, false),
        ProviderSection.OpenCode => new(false, false, false, false, false, true, false, false),
        ProviderSection.Fal => new(false, false, false, false, false, false, true, false),
        ProviderSection.Xai => new(false, false, false, false, false, false, false, true),
        _ => None
    };
}

public static class ProviderExpandPresenter
{
    public static ProviderExpandState Toggle(
        ProviderSection section,
        ProviderExpandState current)
    {
        var wasExpanded = IsExpanded(section, current);
        var expanding = !wasExpanded;

        if (expanding)
            current = ProviderExpandState.None;

        return section switch
        {
            ProviderSection.Cursor => current with { Cursor = expanding },
            ProviderSection.OpenAi => current with { OpenAi = expanding },
            ProviderSection.Claude => current with { Claude = expanding },
            ProviderSection.Gemini => current with { Gemini = expanding },
            ProviderSection.OpenRouter => current with { OpenRouter = expanding },
            ProviderSection.OpenCode => current with { OpenCode = expanding },
            ProviderSection.Fal => current with { Fal = expanding },
            ProviderSection.Xai => current with { Xai = expanding },
            _ => current
        };
    }

    private static bool IsExpanded(ProviderSection section, ProviderExpandState state) =>
        section switch
        {
            ProviderSection.Cursor => state.Cursor,
            ProviderSection.OpenAi => state.OpenAi,
            ProviderSection.Claude => state.Claude,
            ProviderSection.Gemini => state.Gemini,
            ProviderSection.OpenRouter => state.OpenRouter,
            ProviderSection.OpenCode => state.OpenCode,
            ProviderSection.Fal => state.Fal,
            ProviderSection.Xai => state.Xai,
            _ => false
        };
}
