﻿using GlobalEnums;
using HutongGames.PlayMaker;
using IL;
using InControl;
using Modding;
using Newtonsoft.Json;
using On;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static UnityEngine.Networking.UnityWebRequest;
using UObject = UnityEngine.Object;


namespace ReplayLogger
{
    public class ReplayLogger : Mod
    {
        internal static ReplayLogger Instance;

        internal CustomCanvas customCanvas;
        private string dllDir;
        private string modsDir;

        private StreamWriter writer;
        private string lastString;
        private string lastScene;
        private (string name, List<string> list) currentPanteon;

        private long lastUnixTime;
        private long startUnixTime;

        private bool isPlayChalange = false;

        private List<ModVersion> Mods;
        private List<string> startMods;
        private List<string> endMods;

        private List<string> DamageAnfInv;

        public ReplayLogger() : base(ModInfo.Name) { }
        public override string GetVersion() => ModInfo.Version;

        public override void Initialize()
        {
            base.Initialize();
            Instance = this;
            On.SceneLoad.Begin += OpenFile;
            On.GameManager.Update += CheckPressedKey;
            ModHooks.ApplicationQuitHook += Close;
            On.QuitToMenu.Start += QuitToMenu_Start;
            On.BossSceneController.Update += BossSceneController_Update;
            On.HeroController.FixedUpdate += HeroController_FixedUpdate;

            On.SceneLoad.RecordEndTime += SceneLoad_RecordEndTime;

            dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            modsDir = new DirectoryInfo(dllDir).Parent.FullName;

            Mods = ModHooks.GetAllMods(false).Select(m => new ModVersion
            {
                Name = m.GetName(),
                Version = m.GetVersion().Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, "."),
                Path = CalculateModPath(modsDir, m.GetName())

            }).ToList();


            lastString = KeyloggerLogEncryption.GenerateKeyAndIV();

            CustomCanvas.flagSpriteFalse = CustomCanvas.LoadEmbeddedSprite("Geo.png");
            CustomCanvas.flagSpriteTrue = CustomCanvas.LoadEmbeddedSprite("ElegantKey.png");


            startMods = ModsChecking.ParsingMods(Mods, modsDir);
            endMods = new();
            DamageAnfInv = new();
        }

        private void SceneLoad_RecordEndTime(On.SceneLoad.orig_RecordEndTime orig, SceneLoad self, SceneLoad.Phases phase)
        {
            orig(self, phase);
            if (phase == SceneLoad.Phases.UnloadUnusedAssets)
            {
                Self_Finish();
            }
        }

        private void Self_Finish()
        {
            if (!isPlayChalange) return;
            infoBoss.Clear();
            Modding.Logger.Log("Clear");
        }

        private static string CalculateModPath(string modsDir, string modName)
        {
            var subDirectories = Directory.GetDirectories(modsDir);

            var matchingDirectory = subDirectories.FirstOrDefault(dir =>
                Path.GetFileName(dir).Replace(" ", "").Replace("_", "").Equals(modName.Replace(" ", "").Replace("_", ""), System.StringComparison.OrdinalIgnoreCase));

            if (matchingDirectory != null)
            {
                return Path.Combine(matchingDirectory, modName + ".dll");
            }
            else
            {
                Modding.Logger.LogError($"Ненайдена директория '{modName}' in '{modsDir}'.");
                return null;
            }
        }

        private void HeroController_FixedUpdate(On.HeroController.orig_FixedUpdate orig, HeroController self)
        {
            InvCheck();
            orig(self);
        }

        Dictionary<HealthManager, (int maxHP, int lastHP)> infoBoss = new();

        bool isInvincible = false;
        float invTimer;

        public void InvCheck()
        {
            if (!isPlayChalange) return;

            bool shouldBeInvincible =
            (HeroController.instance.cState.invulnerable ||
             PlayerData.instance.isInvincible ||
             HeroController.instance.cState.shadowDashing ||
             HeroController.instance.damageMode == DamageMode.HAZARD_ONLY ||
             HeroController.instance.damageMode == DamageMode.NO_DAMAGE);


            if (shouldBeInvincible && !isInvincible)
            {
                isInvincible = true;
                invTimer = 0f;

                long unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
                string hpInfo = "";
                foreach (var kvp in infoBoss.Values)
                {
                    hpInfo += $"|{kvp.lastHP}/{kvp.maxHP}";
                }
                DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|(INV ON)|"));
            }

            if (!shouldBeInvincible && isInvincible)
            {
                isInvincible = false;
                long unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
                string hpInfo = "";
                foreach (var kvp in infoBoss.Values)
                {
                    hpInfo += $"|{kvp.lastHP}/{kvp.maxHP}";
                }
                DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|(INV OFF, {invTimer.ToString("F3")})|"));
                invTimer = 0f;
            }

            if (isInvincible)
                invTimer += Time.fixedDeltaTime;
        }

        bool isChange;

        public void EnemyUpdate()
        {
            if (!isPlayChalange) return;


            //int layerMask = 1 << (int)PhysLayers.ENEMIES;
            //Collider2D[] array = Physics2D.OverlapBoxAll(HeroController.instance.transform.position, new Vector2(500, 500), 1f, layerMask);
            //if (array != null || array.Length > 0)
            //{

            //    foreach (Collider2D t in array)
            //    {
            //        HealthManager healthManager = t.gameObject.GetComponent<HealthManager>();

            //        if (healthManager != null && healthManager.hp > 0 && !infoBoss.ContainsKey(healthManager))
            //            infoBoss.Add(healthManager, (healthManager.hp, 0));
            //        if (healthManager == null)
            //        {

            //            Component[] components = t.GetComponents<Component>();
            //            Modding.Logger.Log(t.name);
            //            foreach (Component component in components)
            //            {
            //                Modding.Logger.Log("Component: " + component.GetType().Name);
            //            }
            //        }

            //    }

            //}
            HealthManager[] healthManagers = UObject.FindObjectsOfType<HealthManager>();

            if (healthManagers != null || healthManagers.Length > 0)
            {

                foreach (HealthManager t in healthManagers)
                {
                    HealthManager healthManager = t;

                    if (healthManager != null && healthManager.hp > 0 && !infoBoss.ContainsKey(healthManager))
                        infoBoss.Add(healthManager, (healthManager.hp, 0));

                }

            }
            long unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
            string hpInfo = "";
            List<HealthManager> bossKeys = infoBoss.Keys.ToList();
            foreach (var boss in bossKeys)
            {
                if (boss.hp != infoBoss[boss].lastHP)
                {
                    infoBoss[boss] = (infoBoss[boss].maxHP, boss.hp);
                    isChange = true;
                }

                hpInfo += $"|{infoBoss[boss].lastHP}/{infoBoss[boss].maxHP}";
            }

            if (isChange)
            {
                DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|"));
            }
            isChange = false;
            infoBoss.RemoveAll(kvp => kvp.Key.isDead == true || kvp.Key.hp <= 0);
        }

        private void BossSceneController_Update(On.BossSceneController.orig_Update orig, BossSceneController self)
        {
            EnemyUpdate();
            orig(self);
        }


        private IEnumerator QuitToMenu_Start(On.QuitToMenu.orig_Start orig, QuitToMenu self)
        {
            Close();
            return orig(self);
        }

        private void StartLoad()
        {
            customCanvas?.StartUpdateSprite();
        }

        private string currentNameLog;
        private void OpenFile(On.SceneLoad.orig_Begin orig, SceneLoad self)
        {

            var dataTimeNow = (DateTimeOffset)DateTime.Now;
            lastUnixTime = dataTimeNow.ToUnixTimeMilliseconds();
            var dataTime = dataTimeNow.ToString("dd.MM.yyyy HH:mm:ss.fff");
            if (isPlayChalange && self.TargetSceneName.Contains("GG_End_Seq"))
            {
                Close();
            }

            if (self.TargetSceneName.Contains("GG_Boss_Door") || (self.TargetSceneName.Contains("GG_Vengefly_V") && lastScene == "GG_Atrium_Roof"))
            {
                startUnixTime = lastUnixTime;
                int curentPlayTime = (int)(PlayerData.instance.playTime * 100);
                isPlayChalange = true;

                try
                {
                    currentNameLog = Path.Combine(dllDir, $"KeyLog{DateTime.UtcNow.Ticks}.log");
                    writer = new StreamWriter(currentNameLog, false);
                    foreach (string log in startMods)
                    {
                        writer?.WriteLine(log);
                        writer?.Flush();

                    }

                }
                catch (Exception e)
                {
                    Modding.Logger.LogError("Ошибка при открытии файла: " + e.Message);
                }

                int seed = (int)(lastUnixTime ^ curentPlayTime);

                customCanvas = new CustomCanvas(new NumberInCanvas(seed), new LoadingSprite(lastString));
                DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}|"));

                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{curentPlayTime}|{self.TargetSceneName}|"));
                writer?.Flush();



            }
            else if (isPlayChalange)
            {
                if (currentPanteon.list == null && lastScene.Contains("GG_Boss_Door") || lastScene.Contains("GG_Vengefly_V"))
                {
                    if (self.TargetSceneName == Panteons.P1[0])
                        currentPanteon = ("P1",Panteons.P1.ToList());
                    if (self.TargetSceneName == Panteons.P2[0])
                        currentPanteon = ("P2", Panteons.P2.ToList());
                    if (self.TargetSceneName == Panteons.P3[0])
                        currentPanteon = ("P3", Panteons.P3.ToList());
                    if (self.TargetSceneName == Panteons.P4[0])
                        currentPanteon = ("P4", Panteons.P4.ToList());
                    if (self.TargetSceneName == Panteons.P5[1])
                        currentPanteon = ("P5", Panteons.P5.ToList());
                }
                else
                {
                    Modding.Logger.Log(self.TargetSceneName);
                    if (currentPanteon.list.IndexOf(self.TargetSceneName) == -1 || (currentPanteon.list.IndexOf(lastScene) != -1 && !(currentPanteon.list[currentPanteon.list.IndexOf(lastScene) + 1] == self.TargetSceneName)))
                    {
                        Close();
                    }
                    if (lastScene == "GG_Spa")
                    {
                        currentPanteon.list.Remove(lastScene);
                    }


                }

                StartLoad();
                DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}|"));

                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}|{{sprite}}"));
                writer?.Flush();

            }
            lastScene = self.TargetSceneName;
            orig(self);
            //infoBoss.Clear();
        }

        private void CleanupOldLogFiles()
        {
            try
            {
                DirectoryInfo directory = new DirectoryInfo(dllDir);
                FileInfo[] logFiles = directory.GetFiles($"KeyLog*.log")
                                              .OrderBy(f => f.CreationTimeUtc)
                                              .ToArray();

                int filesToDelete = logFiles.Length - 10;
                if (filesToDelete > 0)
                {
                    for (int i = 0; i < filesToDelete; i++)
                    {
                        try
                        {
                            File.Delete(logFiles[i].FullName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting old log file {logFiles[i].Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up old log files: {ex.Message}");
            }
        }
        private void Close()
        {
            if (writer != null)
            {
                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("\n------------------------DAMAGE INV------------------------\n"));

                foreach (string log in DamageAnfInv)
                {
                    writer?.WriteLine(log);
                    writer?.Flush();

                }
                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("\n\n"));
                DamageAnfInv = new();

                endMods = ModsChecking.ParsingMods(Mods, modsDir);
                foreach (string log in endMods)
                {
                    writer?.WriteLine(log);
                    writer?.Flush();

                }
                writer.Write(lastString);
                writer.Flush();
                writer.Close();
                writer = null;

                string dataTimeNow = DateTimeOffset.FromUnixTimeMilliseconds(lastUnixTime).ToString("dd-MM-yyyy HH-mm-ss");
                string newPath = Path.Combine(dllDir, $"{currentPanteon.name} ({dataTimeNow}).log");
                Modding.Logger.Log(currentNameLog);
                if (File.Exists(currentNameLog))
                    File.Move(currentNameLog, newPath);
                CleanupOldLogFiles();
                Modding.Logger.Log("Логгер клавиатуры остановлен.");
            }
            startUnixTime = 0;
            isPlayChalange = false;
            customCanvas?.DestroyCanvas();
            currentPanteon = (null,null);
        }

        float lastFps = 0f;
        private void CheckPressedKey(On.GameManager.orig_Update orig, GameManager self)
        {

            orig(self);
            if (!isPlayChalange) return;
            if (GameManager.instance.gameState == GlobalEnums.GameState.CUTSCENE && lastScene == "GG_Radiance")
                Close();


            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds() - startUnixTime);
            customCanvas?.UpdateTime(dateTimeOffset.ToString("HH:mm:ss"));

            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(keyCode) || Input.GetKeyUp(keyCode))
                {
                    string keyStatus = Input.GetKeyDown(keyCode) ? "+" : "-";

                    long unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();

                    float fps = Time.unscaledDeltaTime == 0 ? lastFps : 1f / Time.unscaledDeltaTime;
                    lastFps = fps;
                    string logEntry = $"+{unixTime - lastUnixTime}|{keyCode}|{keyStatus}|{{number}}|{fps.ToString("F0")}|";
                    customCanvas?.UpdateWatermark(keyCode);
                    try
                    {
                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog(logEntry));
                        writer?.Flush();
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError("Ошибка записи в файл: " + e.Message);
                    }
                }
            }
        }


    }
}