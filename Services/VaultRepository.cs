using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Encryptum.Models;

namespace Encryptum.Services;

public interface IVaultRepository
{
    VaultData Load(string password);
    VaultData TryLoad(string password);
    void Save(VaultData data, string password);
}

public class VaultRepository : IVaultRepository
{
    private static readonly string VaultPath = Path.Combine(AppContext.BaseDirectory, "vault.dat");

    private readonly ICryptoService _crypto;

    public VaultRepository(ICryptoService crypto)
    {
        _crypto = crypto;
    }

    public VaultData Load(string password)
    {
        if (!File.Exists(VaultPath))
            return new VaultData();

        var encrypted = File.ReadAllBytes(VaultPath);
        var jsonBytes = _crypto.Decrypt(encrypted, password);
        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
        return JsonSerializer.Deserialize(json, VaultDataContext.Default.VaultData) ?? new VaultData();
    }

    public VaultData TryLoad(string password)
    {
        if (!File.Exists(VaultPath))
            return new VaultData();

        var encrypted = File.ReadAllBytes(VaultPath);
        var jsonBytes = _crypto.Decrypt(encrypted, password);
        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
        return JsonSerializer.Deserialize(json, VaultDataContext.Default.VaultData) ?? new VaultData();
    }

    public void Save(VaultData data, string password)
    {
        var json = JsonSerializer.Serialize(data, VaultDataContext.Default.VaultData);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        var encrypted = _crypto.Encrypt(jsonBytes, password);
        var tempPath = VaultPath + ".tmp";
        File.WriteAllBytes(tempPath, encrypted);
        File.Move(tempPath, VaultPath, overwrite: true);
    }


}

[JsonSerializable(typeof(VaultData))]
[JsonSerializable(typeof(VirtualFolder))]
[JsonSerializable(typeof(VirtualFile))]
internal partial class VaultDataContext : JsonSerializerContext;