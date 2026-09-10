using System.Collections.Concurrent;
using System.IO;
using System.Text;
using DeezFuelGauge.Services.Credentials;

namespace DeezFuelGauge.Services;

public static class CredentialStore
{
    private static readonly string CredentialsDir = Path.Combine(PlatformPaths.SettingsDirectory, "credentials");
    private static readonly ConcurrentDictionary<string, string?> MemoryCache = new(StringComparer.Ordinal);

    public static string Store(string provider, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret cannot be empty.", nameof(secret));

        Directory.CreateDirectory(CredentialsDir);
        var id = $"{provider}-{Guid.NewGuid():N}";
        var path = GetPath(id);
        var protectedBytes = CredentialProtector.Protect(Encoding.UTF8.GetBytes(secret));
        File.WriteAllBytes(path, protectedBytes);
        MemoryCache[id] = secret;
        return id;
    }

    public static string? Retrieve(string? credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
            return null;

        if (MemoryCache.TryGetValue(credentialId, out var cached))
            return cached;

        var path = GetPath(credentialId);
        if (!File.Exists(path))
        {
            MemoryCache[credentialId] = null;
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var plainBytes = CredentialProtector.Unprotect(protectedBytes);
            var secret = Encoding.UTF8.GetString(plainBytes);
            MemoryCache[credentialId] = secret;
            return secret;
        }
        catch
        {
            MemoryCache[credentialId] = null;
            return null;
        }
    }

    public static void Delete(string? credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
            return;

        MemoryCache.TryRemove(credentialId, out _);

        var path = GetPath(credentialId);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    public static string Upsert(string provider, string? existingId, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret cannot be empty.", nameof(secret));

        if (string.IsNullOrWhiteSpace(existingId))
            return Store(provider, secret);

        Directory.CreateDirectory(CredentialsDir);
        var path = GetPath(existingId);
        var protectedBytes = CredentialProtector.Protect(Encoding.UTF8.GetBytes(secret));
        File.WriteAllBytes(path, protectedBytes);
        MemoryCache[existingId] = secret;
        return existingId;
    }

    public static void Replace(string provider, string? existingId, string newSecret, Action<string?> setCredentialId)
    {
        if (string.IsNullOrWhiteSpace(newSecret))
        {
            Delete(existingId);
            setCredentialId(null);
            return;
        }

        setCredentialId(Upsert(provider, existingId, newSecret));
    }

    private static string GetPath(string credentialId)
    {
        var safeId = credentialId.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        return Path.Combine(CredentialsDir, $"{safeId}.cred");
    }
}
