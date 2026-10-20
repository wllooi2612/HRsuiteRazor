using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Hrsuite.WebApi.Helpers;

public static class CryptographyHelper
{
    public static string GetSHA1Hash(string input)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(input);
        byte[] hashBytes;
        using (var sha1 = SHA1.Create())
        {
            hashBytes = sha1.ComputeHash(bytes);
        }
        StringBuilder sb = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            sb.AppendFormat("{0:x2}", b);
        }
        return sb.ToString();
    }

    public static string Encrypt(string clearText, string encryptionKey)
    {
        // First get the SHA1 of the key
        string sha1Key = GetSHA1Hash(encryptionKey);

        byte[] keyBytes = Encoding.Unicode.GetBytes(sha1Key);
        byte[] hashBytes;
        using (var sha256 = SHA256.Create())
        {
            hashBytes = sha256.ComputeHash(keyBytes);
        }

        byte[] mainKey = new byte[32];
        byte[] mainIV = new byte[16];
        int diffBlock = 5;

        int x = 0;
        int y = 0;
        for (int n = 0; n < 32; n++)
        {
            mainKey[n] = hashBytes[x];
            if (n > diffBlock && n <= 15 + diffBlock)
            {
                mainIV[y] = hashBytes[x];
                y++;
            }
            x++;
            if (x >= hashBytes.Length) x = 0;
        }

        byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);

        #pragma warning disable SYSLIB0022 // RijndaelManaged is obsolete in net6+, but needed for exact compatibility
        using (var rijndael = new RijndaelManaged())
        #pragma warning restore SYSLIB0022
        {
            rijndael.KeySize = 256;
            rijndael.Key = mainKey;
            rijndael.IV = mainIV;
            rijndael.Mode = CipherMode.CBC;
            rijndael.Padding = PaddingMode.PKCS7;

            using (var encryptor = rijndael.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(clearBytes, 0, clearBytes.Length);
                    cs.FlushFinalBlock();
                }
                byte[] encBytes = ms.ToArray();
                StringBuilder sb = new StringBuilder();
                foreach (byte b in encBytes)
                {
                    sb.AppendFormat("{0:x2}", b);
                }
                return sb.ToString();
            }
        }
    }
}
