using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Encryptum.Services;

public class CryptoService : ICryptoService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int KeySize = 32;
    private const int TagSize = 16;

    // Files written by v1+ start with this magic + a 1-byte format version.
    // Files without it are legacy (version 0) and use the parameters below.
    private static readonly byte[] Magic = "ENCV"u8.ToArray();
    private const byte CurrentVersion = 1;

    private readonly record struct KdfParams(int Iterations, HashAlgorithmName Hash);

    // Add a new entry (and bump CurrentVersion) when KDF parameters change.
    // Old entries MUST stay so existing vaults keep opening.
    private static readonly Dictionary<byte, KdfParams> Versions = new()
    {
        [0] = new KdfParams(600_000, HashAlgorithmName.SHA256), // legacy: no header
        [1] = new KdfParams(600_000, HashAlgorithmName.SHA256),
    };

    public byte[] Encrypt(byte[] plaintext, byte[] password)
    {
        var p = Versions[CurrentVersion];
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt, p);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        var header = Magic.Length + 1;
        var result = new byte[header + SaltSize + NonceSize + ciphertext.Length + TagSize];
        var o = 0;
        Buffer.BlockCopy(Magic, 0, result, o, Magic.Length); o += Magic.Length;
        result[o++] = CurrentVersion;
        Buffer.BlockCopy(salt, 0, result, o, SaltSize); o += SaltSize;
        Buffer.BlockCopy(nonce, 0, result, o, NonceSize); o += NonceSize;
        Buffer.BlockCopy(ciphertext, 0, result, o, ciphertext.Length); o += ciphertext.Length;
        Buffer.BlockCopy(tag, 0, result, o, TagSize);
        return result;
    }

    public byte[] Decrypt(byte[] data, byte[] password)
    {
        var offset = 0;
        byte version = 0;
        if (data.Length >= Magic.Length + 1 && HasMagic(data) && Versions.ContainsKey(data[Magic.Length]))
        {
            version = data[Magic.Length];
            offset = Magic.Length + 1;
        }
        var p = Versions[version];

        var ctLen = data.Length - offset - SaltSize - NonceSize - TagSize;
        if (ctLen < 0)
            throw new CryptographicException("Vault file is corrupt or truncated.");

        var salt = new byte[SaltSize];
        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(data, offset, salt, 0, SaltSize);
        Buffer.BlockCopy(data, offset + SaltSize, nonce, 0, NonceSize);

        var ciphertext = new byte[ctLen];
        var tag = new byte[TagSize];
        Buffer.BlockCopy(data, offset + SaltSize + NonceSize, ciphertext, 0, ctLen);
        Buffer.BlockCopy(data, data.Length - TagSize, tag, 0, TagSize);

        var key = DeriveKey(password, salt, p);
        var plaintext = new byte[ctLen];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        return plaintext;
    }

    private static bool HasMagic(byte[] data)
    {
        for (var i = 0; i < Magic.Length; i++)
            if (data[i] != Magic[i]) return false;
        return true;
    }

    private static byte[] DeriveKey(byte[] password, byte[] salt, KdfParams p)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, p.Iterations, p.Hash, KeySize);
}
