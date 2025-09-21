using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace telegram.bot.webfile.Helper;

public static class EccStringCipher
{
    private static ECCurve _curves = ECCurve.NamedCurves.brainpoolP160t1;
    
    public static void GenerateKeyPair(out string publicKey, out string privateKey)
    {
        using ECDsa ecdsa = ECDsa.Create(_curves);
        publicKey = Convert.ToHexString(ecdsa.ExportSubjectPublicKeyInfo()).ToLower(); // 获取公钥
        privateKey = Convert.ToHexString(ecdsa.ExportPkcs8PrivateKey()).ToLower(); // 获取私钥
    }

    public static string Decrypt(string? cipherText, string? localPrivateKey,string? remotePublicKey)
    {
        if (cipherText == null)
        {
            return string.Empty;
        }
        var parts = cipherText.Split(':');

        var remotePublicKeyBytes = Convert.FromHexString(remotePublicKey ?? "");
        var iv = Convert.FromHexString(parts[0]);
        var ciphertextBytes = Convert.FromHexString(parts[1]);

        var privateKeyBytes = Convert.FromHexString(localPrivateKey ?? "");

        using var localPrivateObj = ECDiffieHellman.Create(_curves);
        localPrivateObj.ImportPkcs8PrivateKey(privateKeyBytes, out _);

        using var remotePublicObj = ECDiffieHellman.Create(_curves);
        remotePublicObj.ImportSubjectPublicKeyInfo(remotePublicKeyBytes, out _);

        var sharedSecret = localPrivateObj.DeriveKeyMaterial(remotePublicObj.PublicKey);


        using var aes = Aes.Create();
        aes.Key = sharedSecret;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(ciphertextBytes);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        
        return srDecrypt.ReadToEnd();
    }

    public static string Encrypt(string plainText, string localPrivateKey, string remotePublicKey)
    {
        var remotePublicKeyBytes = Convert.FromHexString(remotePublicKey);
        var privateKeyBytes = Convert.FromHexString(localPrivateKey);

        // 1. Generate Ephemeral Key Pair
        using var localPrivateObj = ECDiffieHellman.Create(_curves);
        localPrivateObj.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        
        // 2. Create Recipient's Public Key Object
        using var remotePublicObj = ECDiffieHellman.Create(_curves);
        remotePublicObj.ImportSubjectPublicKeyInfo(remotePublicKeyBytes, out _);

        // 3. Derive Shared Secret
        var sharedSecret = localPrivateObj.DeriveKeyMaterial(remotePublicObj.PublicKey);

        // 4. AES Encryption
        using var aes = Aes.Create();
        
        aes.Key = sharedSecret;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        csEncrypt.Write(plaintextBytes, 0, plaintextBytes.Length);
        csEncrypt.FlushFinalBlock();

        var ciphertextBytes = msEncrypt.ToArray();

        // 5. Return Ephemeral Public Key, IV, and Ciphertext
        var sb = new StringBuilder();
        // sb.Append(Convert.ToHexString(ephemeralPublicKey).ToLower());
        // sb.Append(':');
                        
        sb.Append(Convert.ToHexString(aes.IV).ToLower());
        sb.Append(':');
                        
        sb.Append(Convert.ToHexString(ciphertextBytes).ToLower());
        return sb.ToString();
    }

    public static T? Decrypt<T>(string cipherText,string localPrivateKey, string remotePublicKey)
    {
        var plain = Decrypt(cipherText, localPrivateKey, remotePublicKey);
        try
        {
            return JsonSerializer.Deserialize<T>(plain);
        }
        catch
        {
            throw new BadHttpRequestException("invalid token");
        }
    }

    public static string Encrypt<T>(T obj, string localPrivateKey, string remotePublicKey)
    {
        try
        {
            var jsonStr = JsonSerializer.Serialize(obj);
            return Encrypt(jsonStr, localPrivateKey, remotePublicKey);
        }
        catch(Exception exp)
        {
            var debug = exp.Message;
            throw new BadHttpRequestException("invalid token");
        }
    }
}