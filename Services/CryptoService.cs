using System;
using System.Security.Cryptography;
using System.Text;

namespace Encryptum.Services;

public class CryptoService : ICryptoService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int KeySize = 32;
    private const int Iterations = 600_000;

    public byte[] Encrypt(byte[] plaintext, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[SaltSize + NonceSize + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, SaltSize + NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize + ciphertext.Length, tag.Length);

        CryptographicOperations.ZeroMemory(key);
        return result;
    }

    public byte[] Decrypt(byte[] data, string password)
    {
        var salt = new byte[SaltSize];
        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(data, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(data, SaltSize, nonce, 0, NonceSize);

        var ciphertextLength = data.Length - SaltSize - NonceSize - 16;
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[16];
        Buffer.BlockCopy(data, SaltSize + NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(data, data.Length - 16, tag, 0, 16);

        var key = DeriveKey(password, salt);
        var plaintext = new byte[ciphertextLength];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        CryptographicOperations.ZeroMemory(key);
        return plaintext;
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize);
    }
}