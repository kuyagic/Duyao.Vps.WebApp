using System.Text;

namespace telegram.bot.webfile.Helper;

public static class Utils
{
    private static byte[] Arc4(byte[] data, byte[] key)
    {
        var s = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) % 256;
            Swap(s, i, j);
        }

        var result = new byte[data.Length];
        var x = 0;
        var y = 0;
        for (var i = 0; i < data.Length; i++)
        {
            x = (x + 1) % 256;
            y = (y + s[x]) % 256;
            Swap(s, x, y);
            result[i] = (byte)(data[i] ^ s[(s[x] + s[y]) % 256]);
        }

        return result;
    }

    private static void Swap(byte[] arr, int i, int j)
    {
        (arr[i], arr[j]) = (arr[j], arr[i]); // 使用元组交换
    }

    private static string LongsToHex(long num1, long num2, long num3)
    {
        // 假设数字范围允许，使用 int 节省空间
        var bytes1 = BitConverter.GetBytes((int)num1);
        var bytes2 = BitConverter.GetBytes((int)num2);
        var bytes3 = BitConverter.GetBytes((int)num3);

        // 合并字节数组
        var combinedBytes = bytes1.Concat(bytes2).Concat(bytes3).ToArray();

        // 转换为十六进制字符串
        return BitConverter.ToString(combinedBytes).Replace("-", "").ToLower();
    }

    private static (long messageId, long datetime, long chatId) HexToLongs(string hex)
    {
        // 将十六进制字符串转换为字节数组
        var combinedBytes = Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();

        // 假设使用 int 存储，从字节数组中提取数字
        var num1 = BitConverter.ToInt32(combinedBytes, 0);
        var num2 = BitConverter.ToInt32(combinedBytes, 4);
        var num3 = BitConverter.ToInt32(combinedBytes, 8);

        return (num1, num2, num3);
    }
    
    public static string Arc4Encode(string text, string key = "secret@null")
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var textBytes = Encoding.UTF8.GetBytes(text);

        var encryptedBytes = Arc4(textBytes, keyBytes);
        return Convert.ToBase64String(encryptedBytes).Replace('+', '-').Replace('/', '_')
            //.Replace('=', '.')
            ;
    }

    public static string? Arc4Decode(string text, string key = "secret@null")
    {
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var textBytes = Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/')
                //.Replace('.', '=')
            );
            var decryptedBytes = Arc4(textBytes, keyBytes);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解码错误: {ex.Message}");
            return null;
        }
    }

    public static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var i = 0;
        double dblSByte = bytes;
        while (dblSByte >= 1024 && i < suffixes.Length - 1)
        {
            dblSByte /= 1024;
            i++;
        }

        return string.Format("{0:0.##} {1}", dblSByte, suffixes[i]);
    }

    public static string HashTelegramFileInfo(long chatId, int messageId, long magic = 17459216)
    {
        return LongsToHex(messageId ^ magic, DateTime.Now.Ticks, chatId);
    }

    public static (long chatId, long messageId) RevealTelegramFileInfo(string hash, long magic = 17459216)
    {
        var ret = HexToLongs(hash);
        return (ret.chatId, ret.messageId ^ magic);
    }

    public static byte[] SubArray(byte[] data, long index, long length)
    {
        var result = new byte[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }

    public static byte[] SubArray(byte[] data, long index)
    {
        return SubArray(data, index, data.Length - index);
    }

    /// <summary>
    ///     GetConfig
    ///     Env first
    /// </summary>
    /// <param name="configValue"></param>
    /// <param name="envKey"></param>
    /// <param name="defaultValue"></param>
    /// <param name="convertFunc"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T SelectConfigValue<T>(T configValue, string envKey, T defaultValue,
        Func<string, T>? convertFunc = null)
    {
        if (defaultValue == null)
        {
            throw new ArgumentNullException(nameof(defaultValue));
        }

        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrEmpty(envValue))
        {
            if (configValue == null) return defaultValue;
            return configValue;
        }

        try
        {
            if (convertFunc != null)
            {
                return convertFunc.Invoke(envValue);
            }

            var underlyingType = Nullable.GetUnderlyingType(typeof(T));
            if (underlyingType != null)
            {
                // 可空类型
                return (T)Convert.ChangeType(envValue, underlyingType);
            }

            // 非可空类型
            return (T)Convert.ChangeType(envValue, typeof(T));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            if (configValue == null) return defaultValue;
            return configValue;
        }
    }
}