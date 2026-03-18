using System.Security.Cryptography;
using System.Text;

namespace Duyao.NsTunnel;

public static class CryptoHelper
{
    private static readonly string Key = "youR_sEcret-key-32~chars-L0ng!!"; // 32字符密钥
    public static string Encrypt(string plainText)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            var iv = aes.IV;
            using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                var result = new byte[iv.Length + encryptedBytes.Length];
                Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, iv.Length, encryptedBytes.Length);
                return Convert.ToBase64String(result);
            }
        }
    }
    
    public static string? Decrypt(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            return null;
        }
        try
        {
            var buffer = Convert.FromBase64String(encryptedText);
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                var iv = new byte[aes.IV.Length];
                Buffer.BlockCopy(buffer, 0, iv, 0, iv.Length);
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    var encryptedBytes = new byte[buffer.Length - iv.Length];
                    Buffer.BlockCopy(buffer, iv.Length, encryptedBytes, 0, encryptedBytes.Length);
                    var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }
        catch
        {
            return null;
        }
    }
}