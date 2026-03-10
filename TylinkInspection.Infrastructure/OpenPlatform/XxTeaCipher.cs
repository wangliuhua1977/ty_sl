using System.Text;

namespace TylinkInspection.Infrastructure.OpenPlatform;

internal static class XxTeaCipher
{
    public static string Encrypt(string plainText, string appSecret)
    {
        var key = BuildKey(appSecret);
        var encryptedBytes = Encrypt(ToByteArray(Encoding.UTF8.GetBytes(plainText), includeLength: true), ToByteArray(key, includeLength: false));
        return Convert.ToHexString(ToBytes(encryptedBytes, includeLength: false));
    }

    public static string Decrypt(string cipherHex, string appSecret)
    {
        var cipherBytes = Convert.FromHexString(cipherHex);
        var key = BuildKey(appSecret);
        var decryptedBytes = Decrypt(ToByteArray(cipherBytes, includeLength: false), ToByteArray(key, includeLength: false));
        var plainBytes = ToBytes(decryptedBytes, includeLength: true);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] BuildKey(string appSecret)
    {
        return Encoding.UTF8.GetBytes(appSecret);
    }

    private static uint[] Encrypt(uint[] data, uint[] key)
    {
        var n = data.Length - 1;
        if (n < 1)
        {
            return data;
        }

        if (key.Length < 4)
        {
            Array.Resize(ref key, 4);
        }

        uint z = data[n];
        uint y = data[0];
        const uint delta = 0x9E3779B9;
        uint sum = 0;
        var q = 6 + 52 / (n + 1);

        while (q-- > 0)
        {
            sum += delta;
            var e = (sum >> 2) & 3;

            for (var p = 0; p < n; p++)
            {
                y = data[p + 1];
                z = data[p] += MX(sum, y, z, p, e, key);
            }

            y = data[0];
            z = data[n] += MX(sum, y, z, n, e, key);
        }

        return data;
    }

    private static uint[] Decrypt(uint[] data, uint[] key)
    {
        var n = data.Length - 1;
        if (n < 1)
        {
            return data;
        }

        if (key.Length < 4)
        {
            Array.Resize(ref key, 4);
        }

        const uint delta = 0x9E3779B9;
        var q = 6 + 52 / (n + 1);
        uint sum = (uint)(q * delta);

        while (sum != 0)
        {
            var e = (sum >> 2) & 3;

            for (var p = n; p > 0; p--)
            {
                var z = data[p - 1];
                var y = data[p];
                data[p] -= MX(sum, y, z, p, e, key);
            }

            var lastZ = data[n];
            var firstY = data[0];
            data[0] -= MX(sum, firstY, lastZ, 0, e, key);
            sum -= delta;
        }

        return data;
    }

    private static uint MX(uint sum, uint y, uint z, int p, uint e, uint[] key)
    {
        return ((z >> 5) ^ (y << 2)) + ((y >> 3) ^ (z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z));
    }

    private static uint[] ToByteArray(byte[] data, bool includeLength)
    {
        var length = (data.Length & 3) == 0 ? data.Length >> 2 : (data.Length >> 2) + 1;
        var result = includeLength ? new uint[length + 1] : new uint[length];
        if (includeLength)
        {
            result[length] = (uint)data.Length;
        }

        for (var i = 0; i < data.Length; i++)
        {
            result[i >> 2] |= (uint)data[i] << ((i & 3) << 3);
        }

        return result;
    }

    private static byte[] ToBytes(uint[] data, bool includeLength)
    {
        var n = data.Length << 2;
        if (includeLength)
        {
            var m = (int)data[^1];
            if (m < 0 || m > n)
            {
                throw new InvalidOperationException("XXTea 解密结果长度无效。");
            }

            n = m;
        }

        var result = new byte[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = (byte)(data[i >> 2] >> ((i & 3) << 3));
        }

        return result;
    }
}
