using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Modding;

namespace LegitimateChallenge
{
    internal static class ModsChecking
    {

        public static List<string> ParsingMods(List<ModVersion> Mods, string modsDir)
        {
            List<string> strMods = new() { KeyloggerLogEncryption.EncryptLog(Modding.ModHooks.ModVersion) };
            List<string> dirs = Directory.GetDirectories(modsDir).ToList();
            dirs.Remove(Path.Combine(modsDir, "Disabled"));
            dirs.Remove(Path.Combine(modsDir, "Vasi"));
            foreach (var mod in Mods)
            {
                strMods.Add(KeyloggerLogEncryption.EncryptLog($"{(mod.Name == ""||mod.Name==null ? "Modding API" : mod.Name)}|{mod.Version}|{mod.Hash}"));
                dirs.RemoveAll(dir => Path.GetFileName(dir).Replace(" ", "").Replace("_", "").Equals(mod.Name.Replace(" ", "").Replace("_",""), StringComparison.OrdinalIgnoreCase));
            }
            if (dirs.Count == 0)
            {
                return strMods;
            }
            else
            {

                List<string> EncDirs = new();
                foreach (var dir in dirs)
                {
                    EncDirs.Add(KeyloggerLogEncryption.EncryptLog(dir));
                }

                List<string> report = [.. strMods, KeyloggerLogEncryption.EncryptLog("Обнаружены незарегистрированные модификации:"), .. EncDirs];
                return report;
            }
        }

        public static string CalculateSHA256(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Modding.Logger.Log($"Файл не найден: {filePath}");
                    return null;
                }

                using (SHA256 sha256 = SHA256.Create())
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] hashBytes = sha256.ComputeHash(fileStream);

                        return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
                    }
                }
            }
            catch (Exception ex)
            {
                Modding.Logger.Log($"Ошибка при вычислении SHA256 для файла {filePath}: {ex.Message}");
                return null;
            }
        }
    }
}

