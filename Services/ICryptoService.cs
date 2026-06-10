namespace Encryptum.Services;

public interface ICryptoService
{
    byte[] Encrypt(byte[] plaintext, byte[] password);
    byte[] Decrypt(byte[] data, byte[] password);
}
