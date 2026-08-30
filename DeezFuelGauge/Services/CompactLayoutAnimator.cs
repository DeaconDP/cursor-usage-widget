namespace DeezFuelGauge.Services;

public readonly record struct CompactAnimSample(
    double Width,
    double Height,
    double X,
    double Y);

public static class CompactLayoutAnimator
{
    public static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(220);
    public static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(170);
    public const double FullFadeStart = 0.20;
    public const double CompactCullThreshold = 0.95;
    public const double FullCullThreshold = 0.05;

    public static double EaseOutCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - t, 3);
    }

    public static double EaseInCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * t;
    }

    public static double EaseOutQuad(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - (1 - t) * (1 - t);
    }

    public static double EaseInQuad(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t;
    }

    public static double ApplyEase(double linearT, bool expanding) =>
        expanding ? EaseOutQuad(linearT) : EaseInQuad(linearT);

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double CompactOpacity(double progress) =>
        Math.Clamp(1 - progress, 0, 1);

    public static double FullOpacity(double progress)
    {
        if (progress <= FullFadeStart)
            return 0;

        return Math.Clamp((progress - FullFadeStart) / (1 - FullFadeStart), 0, 1);
    }

    public static bool ShouldRenderCompact(double progress) =>
        progress < CompactCullThreshold;

    public static bool ShouldRenderFull(double progress) =>
        progress > FullCullThreshold;

    public static TimeSpan DurationFor(double fromProgress, double toProgress)
    {
        var remaining = Math.Clamp(Math.Abs(toProgress - fromProgress), 0, 1);
        var full = toProgress > fromProgress ? ExpandDuration : CollapseDuration;
        return TimeSpan.FromMilliseconds(Math.Max(1, full.TotalMilliseconds * remaining));
    }

    public static CompactAnimSample Interpolate(
        CompactAnimSample start,
        CompactAnimSample end,
        double linearT,
        bool expanding,
        bool reduceMotion)
    {
        if (reduceMotion || linearT >= 1)
            return end;

        var t = ApplyEase(Math.Clamp(linearT, 0, 1), expanding);
        return new CompactAnimSample(
            Lerp(start.Width, end.Width, t),
            Lerp(start.Height, end.Height, t),
            Lerp(start.X, end.X, t),
            Lerp(start.Y, end.Y, t));
    }

    public static double InterpolateProgress(
        double startProgress,
        double endProgress,
        double linearT,
        bool expanding,
        bool reduceMotion)
    {
        if (reduceMotion || linearT >= 1)
            return endProgress;

        var t = ApplyEase(Math.Clamp(linearT, 0, 1), expanding);
        return Lerp(startProgress, endProgress, t);
    }
}
