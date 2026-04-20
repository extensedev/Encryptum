using System.Security.Cryptography;

namespace Encryptum.Services;

public interface ICryptoService
{
    byte[] Encrypt(byte[] plaintext, string password);
    byte[] Decrypt(byte[] ciphertext, string password);
}