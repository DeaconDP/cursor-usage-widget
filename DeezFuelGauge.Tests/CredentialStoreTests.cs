using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CredentialStoreTests
{
    [Fact]
    public void Store_and_retrieve_round_trip()
    {
        var id = CredentialStore.Store("test-provider", "secret-value-123");

        try
        {
            var retrieved = CredentialStore.Retrieve(id);
            Assert.Equal("secret-value-123", retrieved);
        }
        finally
        {
            CredentialStore.Delete(id);
        }
    }

    [Fact]
    public void Delete_removes_credential()
    {
        var id = CredentialStore.Store("test-provider", "temporary-secret");
        CredentialStore.Delete(id);
        Assert.Null(CredentialStore.Retrieve(id));
    }

    [Fact]
    public void Replace_keeps_existing_id_and_updates_secret()
    {
        var id = CredentialStore.Store("test-provider", "original-secret");
        string? updatedId = null;

        try
        {
            CredentialStore.Replace("test-provider", id, "updated-secret", newId => updatedId = newId);

            Assert.Equal(id, updatedId);
            Assert.Equal("updated-secret", CredentialStore.Retrieve(id));
        }
        finally
        {
            CredentialStore.Delete(id);
            if (!string.IsNullOrWhiteSpace(updatedId) && updatedId != id)
                CredentialStore.Delete(updatedId);
        }
    }
}
