using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeezFuelGauge.Setup;

public static class MacOsAppPackager
{
    public static readonly string[] RequiredNativeLibraries =
    [
        "libSkiaSharp.dylib",
        "libHarfBuzzSharp.dylib",
        "libAvaloniaNative.dylib"
    ];

    public static string Package(string repoRoot, string dotnetPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("macOS app packaging is only supported on macOS.");

        var rid = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "osx-arm64",
            Architecture.X64 => "osx-x64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported macOS architecture: {RuntimeInformation.ProcessArchitecture}")
        };

        var project = Path.Combine(repoRoot, "DeezFuelGauge", "DeezFuelGauge.csproj");
        var publishDir = Path.Combine(repoRoot, "DeezFuelGauge", "bin", "Release", "net8.0", rid, "publish");
        var appPath = Path.Combine(repoRoot, "Deez Fuel Gauge.app");
        var infoPlist = Path.Combine(repoRoot, "packaging", "macos", "Info.plist");
        var appHostPath = Path.Combine(publishDir, "DeezFuelGauge");

        // Incremental single-file publish can omit native .dylib files when publish/ already exists.
        if (Directory.Exists(publishDir))
            Directory.Delete(publishDir, recursive: true);

        RunProcess(dotnetPath, new[]
        {
            "publish", project,
            "-c", "Release",
            "-r", rid,
            "--self-contained", "true",
            "-p:UseAppHost=true",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=false",
            "--nologo"
        });

        if (!File.Exists(appHostPath))
            throw new FileNotFoundException("Published app host was not created.", appHostPath);

        var macOsDir = Path.Combine(appPath, "Contents", "MacOS");
        var resourcesDir = Path.Combine(appPath, "Contents", "Resources");

        if (Directory.Exists(appPath))
            Directory.Delete(appPath, recursive: true);

        Directory.CreateDirectory(macOsDir);
        Directory.CreateDirectory(resourcesDir);

        File.Copy(infoPlist, Path.Combine(appPath, "Contents", "Info.plist"), overwrite: true);
        File.WriteAllText(Path.Combine(appPath, "Contents", "PkgInfo"), "APPL????");

        var appIcon = Path.Combine(repoRoot, "packaging", "icons", "AppIcon.icns");
        if (File.Exists(appIcon))
            File.Copy(appIcon, Path.Combine(resourcesDir, "AppIcon.icns"), overwrite: true);

        var appIconBundle = Path.Combine(repoRoot, "packaging", "macos", "AppIcon.icon");
        if (Directory.Exists(appIconBundle))
            CopyDirectory(appIconBundle, Path.Combine(resourcesDir, "AppIcon.icon"));

        CopyPublishedFiles(publishDir, macOsDir);
        SignAppBundle(appPath);

        return appPath;
    }

    public static void OpenApp(string appPath)
    {
        RunProcess("/usr/bin/open", new[] { appPath });
    }

    public static string? FindDotnet() =>
        EnumerateDotnetCandidates().FirstOrDefault(HasDotNet8Sdk);

    public static bool HasDotNet8Sdk(string dotnetPath)
    {
        try
        {
            var output = RunProcessCapture(dotnetPath, new[] { "--list-sdks" });
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => line.StartsWith("8.", StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateDotnetCandidates()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extraPaths = new[]
        {
            "/usr/local/share/dotnet",
            "/opt/homebrew/bin",
            "/usr/local/bin",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools")
        };

        foreach (var directory in extraPaths.Concat(path.Split(':', StringSplitOptions.RemoveEmptyEntries)))
        {
            var candidate = Path.Combine(directory.Trim(), "dotnet");
            if (File.Exists(candidate) && seen.Add(candidate))
                yield return candidate;
        }

        var knownCandidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet"),
            "/usr/local/share/dotnet/dotnet",
            "/opt/homebrew/bin/dotnet",
            "/usr/local/bin/dotnet"
        };

        foreach (var candidate in knownCandidates)
        {
            if (File.Exists(candidate) && seen.Add(candidate))
                yield return candidate;
        }
    }

    public static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DeezFuelGauge.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    public static void CopyPublishedFiles(string publishDir, string macOsDir)
    {
        foreach (var file in Directory.GetFiles(publishDir))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(Path.GetExtension(fileName), ".pdb", StringComparison.OrdinalIgnoreCase))
                continue;

            var destination = Path.Combine(macOsDir, fileName);
            File.Copy(file, destination, overwrite: true);

            if (!fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
                MakeExecutable(destination);
        }

        foreach (var library in RequiredNativeLibraries)
        {
            if (!File.Exists(Path.Combine(macOsDir, library)))
                throw new FileNotFoundException($"Published native library was not copied: {library}", library);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir, StringComparison.Ordinal));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(sourceDir, destinationDir, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void MakeExecutable(string path)
    {
        if (!File.Exists(path))
            return;

        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            return;

        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

        File.SetUnixFileMode(path, mode);
    }

    private static void SignAppBundle(string appPath)
    {
        if (!Directory.Exists(appPath) || !File.Exists("/usr/bin/codesign"))
            return;

        SignFile(appPath);
        RunProcessOptional("/usr/bin/xattr", new[] { "-cr", appPath });
    }

    private static void SignFile(string path)
    {
        RunProcessOptional("/usr/bin/codesign", new[] { "--force", "--sign", "-", path });
    }

    private static string RunProcessCapture(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{fileName} exited with code {process.ExitCode}.{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}");

        return stdout;
    }

    private static void RunProcess(string fileName, IReadOnlyList<string> arguments) =>
        _ = RunProcessCapture(fileName, arguments);

    private static void RunProcessOptional(string fileName, IReadOnlyList<string> arguments)
    {
        if (!File.Exists(fileName))
            return;

        try
        {
            RunProcess(fileName, arguments);
        }
        catch
        {
            // Ad-hoc signing is best-effort for local installs.
        }
    }
}
