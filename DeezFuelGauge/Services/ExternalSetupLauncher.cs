using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeezFuelGauge.Services;

public class ExternalSetupLauncher
{
    public const string AntigravityDownloadUrl = "https://antigravity.google/download";
    public const string GeminiCliInstallUrl = "https://github.com/google-gemini/gemini-cli#installation";

    public virtual void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public virtual bool IsCodexOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "codex.exe" : "codex";
            if (File.Exists(Path.Combine(dir.Trim(), executable)))
                return true;
        }

        return false;
    }

    public virtual bool TryLaunchCodexLogin()
    {
        if (!IsCodexOnPath())
            return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd.exe")
                {
                    Arguments = "/c start \"Codex Login\" cmd /k codex login",
                    UseShellExecute = true
                });
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("/usr/bin/osascript")
                {
                    ArgumentList = { "-e", "tell application \"Terminal\" to do script \"codex login\"" },
                    UseShellExecute = false
                });
                return true;
            }

            Process.Start(new ProcessStartInfo("codex")
            {
                Arguments = "login",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void OpenChatGpt() => OpenUrl("https://chatgpt.com");

    public void OpenClaudeAi() => OpenUrl("https://claude.ai/settings/usage");

    public virtual bool IsGeminiCliOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gemini.cmd" : "gemini";
            if (File.Exists(Path.Combine(dir.Trim(), executable)))
                return true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && File.Exists(Path.Combine(dir.Trim(), "gemini")))
                return true;
        }

        return false;
    }

    public virtual bool TryLaunchGeminiLogin()
    {
        if (!IsGeminiCliOnPath())
            return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd.exe")
                {
                    Arguments = "/c start \"Gemini Login\" cmd /k gemini",
                    UseShellExecute = true
                });
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("/usr/bin/osascript")
                {
                    ArgumentList = { "-e", "tell application \"Terminal\" to do script \"gemini\"" },
                    UseShellExecute = false
                });
                return true;
            }

            Process.Start(new ProcessStartInfo("gemini")
            {
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public virtual AppLaunchResult LaunchAntigravityIde()
    {
        foreach (var path in PlatformPaths.AntigravityIdeExecutablePaths)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
                continue;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo("open", ["-a", path]) { UseShellExecute = false });
                }
                else
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }

                return AppLaunchResult.Launched;
            }
            catch
            {
                // try next path
            }
        }

        try
        {
            OpenUrl(AntigravityDownloadUrl);
            return AppLaunchResult.OpenedFallbackUrl;
        }
        catch
        {
            return AppLaunchResult.Failed;
        }
    }

    public virtual AppLaunchResult LaunchCursorIde()
    {
        foreach (var path in PlatformPaths.CursorExecutablePaths)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
                continue;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo("open", ["-a", path]) { UseShellExecute = false });
                }
                else
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }

                return AppLaunchResult.Launched;
            }
            catch
            {
                // try next path
            }
        }

        try
        {
            OpenUrl("cursor://");
            return AppLaunchResult.OpenedFallbackUrl;
        }
        catch
        {
            return AppLaunchResult.Failed;
        }
    }

    public void OpenOpenCode() => OpenUrl("https://opencode.ai");

    public void OpenOpenRouter() => OpenUrl("https://openrouter.ai/settings/keys");

    public void OpenOpenRouterManagementKeys() => OpenUrl("https://openrouter.ai/settings/management-keys");

    public void OpenFal() => OpenUrl("https://fal.ai/dashboard/keys");

    public void OpenXai() => OpenUrl("https://console.x.ai");
}
