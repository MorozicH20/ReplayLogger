using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Modding;

namespace ReplayLogger
{
    internal static class ModsChecking
    {
        public static List<string> ScanMods(string modsDir)
        {
            List<string> modInfo = new() { KeyloggerLogEncryption.EncryptLog(Modding.ModHooks.ModVersion) };
            List<string> unregisteredMods = new();

            List<string> modDirectories = Directory.GetDirectories(modsDir)
             .Where(dir => !Path.GetFileName(dir).Equals("Disabled", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(dir).Equals("Vasi", StringComparison.OrdinalIgnoreCase))
             .ToList();

            foreach (string modDirectory in modDirectories)
            {
                try
                {
                    string[] dllFiles = Directory.GetFiles(modDirectory, "*.dll");

                    if (dllFiles.Length > 0)
                    {
                        string modDllPath = dllFiles[0];

                        try
                        {
                            Assembly modAssembly = Assembly.LoadFile(modDllPath);

                            Type[] modTypes = modAssembly.GetTypes()
                                .Where(t => typeof(Modding.IMod).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                                .ToArray();

                            if (modTypes.Length > 0)
                            {
                                Type modType = modTypes[0];

                                
                                string modName = modType.Name;
                                string modVersion = "Unknown";

                                try
                                {
                                    object modInstance = Activator.CreateInstance(modType);

                                    MethodInfo getVersionMethod = modType.GetMethod("GetVersion");

                                    if (getVersionMethod != null)
                                    {
                                        object versionResult = getVersionMethod.Invoke(modInstance, null);

                                        if (versionResult != null)
                                        {
                                            modVersion = versionResult.ToString();
                                        }
                                    }
                                    else
                                    {
                                        Modding.Logger.LogError($"Warning: Mod class '{modType.FullName}' does not have a GetVersion method.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Modding.Logger.LogError($"Error getting version from '{modType.FullName}': {ex.Message}");
                                }


                                string hash = CalculateSHA256(modDllPath);

                                modInfo.Add(KeyloggerLogEncryption.EncryptLog($"{modName}|{modVersion}|{hash}"));
                            }
                            else
                            {
                                unregisteredMods.Add(modDirectory);
                            }

                        }
                        catch (Exception ex)
                        {
                            Modding.Logger.LogError($"Error loading assembly {modDllPath}: {ex.Message}");
                            unregisteredMods.Add(modDirectory);
                        }
                    }
                    else
                    {
                        unregisteredMods.Add(modDirectory);
                    }
                }
                catch (Exception ex)
                {
                    Modding.Logger.LogError($"Error processing directory {modDirectory}: {ex.Message}");
                    unregisteredMods.Add(modDirectory);
                }
            }

            if (unregisteredMods.Count == 0)
            {
                return modInfo;
            }
            else
            {
                List<string> EncDirs = new();
                foreach (var dir in unregisteredMods)
                {
                    EncDirs.Add(KeyloggerLogEncryption.EncryptLog(dir));
                }
                List<string> report = [.. modInfo, KeyloggerLogEncryption.EncryptLog("Обнаружены незарегистрированные модификации:"), .. EncDirs];
                return report;
            }
        }

        public static string CalculateSHA256(string filePath)
        {
            try
            {
                if(filePath==null || filePath == "") return null;
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

