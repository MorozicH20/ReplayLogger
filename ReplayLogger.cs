using GlobalEnums;
using IL;
using Modding;
using On;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
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
        private int bossCounter;

        private List<string> startMods;
        private List<string> endMods;

        private List<string> DamageAnfInv;

        private List<string> InvWarn;

        private Dictionary<string, HashSet<int>> damageValuesByOwner = new Dictionary<string, HashSet<int>>();


        private Dictionary<string, HashSet<float>> multiplierValuesByOwner = new Dictionary<string, HashSet<float>>();

        private List<string> ChangeDamageOrMultiplier;


        public ReplayLogger() : base(ModInfo.Name) { }
        public override string GetVersion() => ModInfo.Version;

        public override void Initialize()
        {
            Instance = this;
            On.SceneLoad.Begin += OpenFile;
            On.GameManager.Update += CheckPressedKey;
            ModHooks.ApplicationQuitHook += Close;
            On.QuitToMenu.Start += QuitToMenu_Start;
            On.BossSceneController.Update += BossSceneController_Update;
            On.HeroController.FixedUpdate += HeroController_FixedUpdate;
            On.SceneLoad.RecordEndTime += SceneLoad_RecordEndTime;

            ModHooks.HitInstanceHook += ModHooks_HitInstanceHook;
            
            On.BossSequenceController.FinishLastBossScene += BossSequenceController_FinishLastBossScene;

            dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            modsDir = new DirectoryInfo(dllDir).Parent.FullName;



            lastString = KeyloggerLogEncryption.GenerateKeyAndIV();

            CustomCanvas.flagSpriteFalse = CustomCanvas.LoadEmbeddedSprite("Geo.png");
            CustomCanvas.flagSpriteTrue = CustomCanvas.LoadEmbeddedSprite("ElegantKey.png");

            startMods = ModsChecking.ScanMods(modsDir);
            endMods = new();
            DamageAnfInv = new();
            InvWarn = new();
            ChangeDamageOrMultiplier = new();
        }

        string isСhallengeСompleted = "-";
        private void BossSequenceController_FinishLastBossScene(On.BossSequenceController.orig_FinishLastBossScene orig, BossSceneController self)
        {
            isСhallengeСompleted = "+";
            orig(self);
        }



        private HitInstance ModHooks_HitInstanceHook(HutongGames.PlayMaker.Fsm owner, HitInstance hit)
        {
            long unixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string ownerName = owner.GameObject.GetFullPath();

            if (owner == null || owner.GameObject == null)
            {
                return hit;
            }

            if (!damageValuesByOwner.ContainsKey(ownerName))
            {
                damageValuesByOwner[ownerName] = new HashSet<int>();
            }

            if (!multiplierValuesByOwner.ContainsKey(ownerName))
            {
                multiplierValuesByOwner[ownerName] = new HashSet<float>();
            }

            bool newDamage = damageValuesByOwner[ownerName].Add(hit.DamageDealt);
            if (newDamage)
            {
                ChangeDamageOrMultiplier.Add($"Add NEW unique damage: {ownerName}-{lastScene}/{unixTime - lastUnixTime} #{hit.DamageDealt}");
            }

            bool newMultiplier = multiplierValuesByOwner[ownerName].Add(hit.Multiplier);
            if (newMultiplier)
            {

                ChangeDamageOrMultiplier.Add($"Add NEW unique multiplier: {ownerName}-{lastScene}/{unixTime - lastUnixTime} #{hit.Multiplier}");

            }

            return hit;


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

            var bossList = infoBoss.GetKeysWithUniqueGameObject().Values;


            if (shouldBeInvincible && !isInvincible)
            {
                isInvincible = true;
                invTimer = 0f;

                long unixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                string hpInfo = "";
                foreach (var kvp in bossList)
                {
                    hpInfo += $"|{infoBoss[kvp].lastHP}/{infoBoss[kvp].maxHP}";
                }
                DamageAnfInv.Add($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|(INV ON)|");
            }

            if (!shouldBeInvincible && isInvincible)
            {
                isInvincible = false;
                long unixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                string hpInfo = "";
                foreach (var kvp in bossList)
                {
                    hpInfo += $"|{infoBoss[kvp].lastHP}/{infoBoss[kvp].maxHP}";

                }
                DamageAnfInv.Add($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|(INV OFF, {invTimer.ToString("F3")})|");
                if (invTimer > 2.6f)
                {
                    string warning = $"|{lastScene}|+{unixTime - lastUnixTime}{hpInfo}|(INV OFF, {invTimer.ToString("F3")})";

                    InvWarn.Add(warning);
                }
                invTimer = 0f;
            }

            if (isInvincible)
                invTimer += Time.fixedDeltaTime;
        }

        bool isChange;

        public void EnemyUpdate()
        {
            if (!isPlayChalange) return;

            List<HealthManager> healthManagers = new();

            float searchRadius = 100f;
            LayerMask enemyLayer = 1 << (int)PhysLayers.ENEMIES;

            Collider2D[] colliders = Physics2D.OverlapBoxAll(HeroController.instance.transform.position, Vector2.one * searchRadius, enemyLayer);

            foreach (Collider2D collider in colliders)
            {
                GameObject enemyObject = collider.gameObject;

                if (enemyObject.activeInHierarchy)
                {
                    HealthManager healthManager = enemyObject.GetComponent<HealthManager>();

                    if (healthManager != null)
                    {
                        healthManagers.Add(healthManager);
                    }

                }
            }

            if (healthManagers != null || healthManagers.Count > 0)
            {

                foreach (HealthManager healthManager in healthManagers.ToList())
                {
                    if (healthManager != null && healthManager.hp > 0 && !infoBoss.ContainsKey(healthManager))
                    {
                        infoBoss.Add(healthManager, (healthManager.hp, 0));

                    }


                }

            }


            long unixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string hpInfo = "";

            var bossKeys = infoBoss.GetKeysWithUniqueGameObject().Values;
            foreach (var boss in infoBoss.Keys)
            {
                if (!bossKeys.Contains(boss) && !boss.isDead) continue;
                if (boss.hp != infoBoss[boss].lastHP)
                {
                    infoBoss[boss] = (infoBoss[boss].maxHP, boss.hp);
                    isChange = true;
                }

                hpInfo += $"|{infoBoss[boss].lastHP}/{infoBoss[boss].maxHP}";
            }

            if (isChange)
            {
                DamageAnfInv.Add($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|");
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
            try
            {
                var dataTimeNow = DateTimeOffset.Now;
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
                        string listCharms = "\nEquipped charms => ";
                        foreach (int numCharm in PlayerData.instance?.equippedCharms)
                        {
                            listCharms += (Charm)numCharm + ", ";
                        }
                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog(listCharms + (BossSequenceController.BoundCharms ? " => BOUND CHARMS" : "") + '\n'));


                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError("Ошибка при открытии файла: " + e.Message);
                    }

                    int seed = (int)(lastUnixTime ^ curentPlayTime);

                    customCanvas = new CustomCanvas(new NumberInCanvas(seed), new LoadingSprite(lastString));

                    if (self.TargetSceneName.Contains("GG_Vengefly_V") && lastScene == "GG_Atrium_Roof")
                    {
                        currentPanteon = ("P5", Panteons.P5.ToList());
                        bossCounter++;

                        DamageAnfInv.Add($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}| {bossCounter}*");

                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{curentPlayTime}|{self.TargetSceneName}| {bossCounter}*"));
                    }
                    else
                    {

                        DamageAnfInv.Add($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}|");

                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{curentPlayTime}|{self.TargetSceneName}|"));
                    }
                    writer?.Flush();



                }
                else if (isPlayChalange)
                {
                    if (currentPanteon.list == null && lastScene.Contains("GG_Boss_Door"))
                    {
                        if (self.TargetSceneName == Panteons.P1[0])
                            currentPanteon = ("P1", Panteons.P1.ToList());
                        if (self.TargetSceneName == Panteons.P2[0])
                            currentPanteon = ("P2", Panteons.P2.ToList());
                        if (self.TargetSceneName == Panteons.P3[0])
                            currentPanteon = ("P3", Panteons.P3.ToList());
                        if (self.TargetSceneName == Panteons.P4[0])
                            currentPanteon = ("P4", Panteons.P4.ToList());
                    }
                    else
                    {

                        int targetIndex = currentPanteon.list.IndexOf((self.TargetSceneName));
                        int lastSceneIndex = currentPanteon.list.IndexOf(lastScene);


                        if (targetIndex == -1 || (lastSceneIndex != -1 && !(IsValidNextScene(currentPanteon.list, lastSceneIndex, self.TargetSceneName))))
                        {
                            Close();
                        }
                        if (lastScene == "GG_Spa")
                        {
                            currentPanteon.list?.Remove(lastScene);
                        }


                    }
                    List<string> skipScenes = new List<string> { "GG_Spa", "GG_Engine", "GG_Unn", "GG_Engine_Root", "GG_Wyrm", "GG_Engine_Prime", "GG_Atrium", "GG_Atrium_Roof" };

                    if (!skipScenes.Contains(self.TargetSceneName))
                        bossCounter++;

                    StartLoad();
                    DamageAnfInv.Add($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}{((!skipScenes.Contains(self.TargetSceneName)) ? $"| {bossCounter}*" : "")}");

                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}|{{sprite}}{self.TargetSceneName}{((!skipScenes.Contains(self.TargetSceneName)) ? $"| {bossCounter}*" : "")}"));
                    writer?.Flush();

                }
            }
            catch (Exception e)
            {
                Modding.Logger.Log(e.Message);
            }
            lastScene = self.TargetSceneName;
            orig(self);
        }
        private bool IsValidNextScene(List<string> panteonList, int lastSceneIndex, string targetSceneName)
        {
            int nextIndex = lastSceneIndex + 1;

            if (nextIndex >= panteonList.Count) return false;

            string expectedNextScene = panteonList[nextIndex];

            if (expectedNextScene != targetSceneName)
            {
                nextIndex++;
                expectedNextScene = panteonList[nextIndex];

            }

            return expectedNextScene == targetSceneName;
        }

        public static string ConvertUnixTimeToDateTimeString(long unixTimeMilliseconds)
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

            string dateTimeString = dateTimeOffset.ToString("dd.MM.yyyy HH:mm:ss.fff");

            return dateTimeString;
        }
        public static string ConvertUnixTimeToTimeString(long unixTimeMilliseconds)
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

            string dateTimeString = dateTimeOffset.ToString("HH:mm:ss.fff");

            return dateTimeString;
        }
        private void Close()
        {
            try
            {
                if (writer != null)
                {
                    long EndTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    writer.WriteLine(KeyloggerLogEncryption.EncryptLog($"StartTime: {ConvertUnixTimeToDateTimeString(startUnixTime)}, EndTime: {ConvertUnixTimeToDateTimeString(EndTime)}, TimeInPlay: {ConvertUnixTimeToDateTimeString(EndTime - startUnixTime)}"));

                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("\n------------------------DAMAGE INV------------------------\n"));

                    foreach (string log in DamageAnfInv)
                    {
                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog(log));
                        writer?.Flush();

                    }
                    DamageAnfInv = new();

                    writer.WriteLine(KeyloggerLogEncryption.EncryptLog($"StartTime: {ConvertUnixTimeToDateTimeString(startUnixTime)}, EndTime: {ConvertUnixTimeToDateTimeString(EndTime)}, TimeInPlay: {ConvertUnixTimeToTimeString(EndTime - startUnixTime)}"));
                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("\n\n"));

                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("Warnings:"));
                    foreach (string warning in InvWarn)
                    {
                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog(warning));
                    }

                    InvWarn = new();

                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("\n\n"));

                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("DamageChange:"));

                    foreach (var entry in SortLogsByObjectName(ChangeDamageOrMultiplier))
                    {
                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{entry.Key}:"));
                        foreach (string log in entry.Value)
                        {
                            writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"  {log}"));
                        }
                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog("\n"));


                    }
                    damageValuesByOwner = new Dictionary<string, HashSet<int>>();

                    multiplierValuesByOwner = new Dictionary<string, HashSet<float>>();

                    ChangeDamageOrMultiplier = new();
                    writer?.WriteLine(KeyloggerLogEncryption.EncryptLog('\n' + isСhallengeСompleted + '\n'));

                    endMods = ModsChecking.ScanMods(modsDir);
                    foreach (string log in endMods)
                    {
                        writer?.WriteLine(log);
                        writer?.Flush();

                    }

                    writer.Write(lastString);
                    writer.Flush();
                    writer.Close();
                    writer = null;


                    string panteonDir = Path.Combine(dllDir, currentPanteon.name);
                    if (!Directory.Exists(panteonDir))
                    {
                        Directory.CreateDirectory(panteonDir);
                    }


                    string dataTimeNow = DateTimeOffset.FromUnixTimeMilliseconds(lastUnixTime).ToLocalTime().ToString("dd-MM-yyyy HH-mm-ss");
                    string newPath = Path.Combine(panteonDir, $"{isСhallengeСompleted}{currentPanteon.name} ({dataTimeNow}).log");

                    if (File.Exists(currentNameLog))
                    {
                        File.Move(currentNameLog, newPath);
                    }
                }
                isСhallengeСompleted = "-";
                bossCounter = 0;
                startUnixTime = 0;
                isPlayChalange = false;
                customCanvas?.DestroyCanvas();
                currentPanteon = (null, null);
            }
            catch(Exception ex)
            {
                Modding.Logger.Log(ex.Message);
            }
        }

        private static string ExtractObjectName(string log)
        {
            string pattern = @"^Add NEW unique (?:damage|multiplier): (.*?)-";
            Match match = Regex.Match(log, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            return null;
        }

        public static Dictionary<string, List<string>> SortLogsByObjectName(List<string> logs)
        {
            Dictionary<string, List<string>> sortedLogs = new Dictionary<string, List<string>>();

            foreach (string log in logs)
            {
                string objectName = ExtractObjectName(log);

                if (objectName != null)
                {
                    if (sortedLogs.ContainsKey(objectName))
                    {
                        sortedLogs[objectName].Add(log);
                    }
                    else
                    {
                        sortedLogs[objectName] = new List<string> { log };
                    }
                }
            }

            return sortedLogs;
        }


        float lastFps = 0f;
        private void CheckPressedKey(On.GameManager.orig_Update orig, GameManager self)
        {

            orig(self);
            if (!isPlayChalange) return;
            if (GameManager.instance.gameState == GlobalEnums.GameState.CUTSCENE && lastScene == "GG_Radiance")
                Close();


            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Now.ToUnixTimeMilliseconds() - startUnixTime);
            customCanvas?.UpdateTime(dateTimeOffset.ToString("HH:mm:ss"));

            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(keyCode) || Input.GetKeyUp(keyCode))
                {
                    string keyStatus = Input.GetKeyDown(keyCode) ? "+" : "-";

                    long unixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

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