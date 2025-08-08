using GlobalEnums;
using Modding;
using On;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public ReplayLogger() : base(ModInfo.Name) { }
        public override string GetVersion() => ModInfo.Version;

        //public override List<(string, string)> GetPreloadNames()
        //{
        //    return new List<(string, string)>
        //{
        //    ("GG_Vengefly_V", "Boss Holder/Vengefly King"),
        //    ("GG_Vengefly", "Boss Holder/Vengefly King"),
        //    ("GG_Gruz_Mother_V", "Boss Holder/Gruz Mother"),
        //    ("GG_Gruz_Mother", "Boss Holder/Gruz Mother"),
        //    ("GG_False_Knight", "Boss Holder/False Knight"),
        //    ("GG_Mega_Moss_Charger", "Boss Holder/Mega Moss Charger"),
        //    ("GG_Hornet_1", "Boss Holder/Hornet Boss 1"),
        //    ("GG_Ghost_Gorb_V", "Boss Holder/Ghost Gorb"),
        //    ("GG_Ghost_Gorb", "Boss Holder/Ghost Gorb"),
        //    ("GG_Dung_Defender", "Boss Holder/Dung Defender"),
        //    ("GG_Mage_Knight_V", "Boss Holder/Soul Warrior"),
        //    ("GG_Mage_Knight", "Boss Holder/Soul Warrior"),
        //    ("GG_Brooding_Mawlek_V", "Boss Holder/Brooding Mawlek"),
        //    ("GG_Brooding_Mawlek", "Boss Holder/Brooding Mawlek"),
        //    ("GG_Nailmasters", "Boss Holder/Nailmasters"),
        //    ("GG_Ghost_Xero_V", "Boss Holder/Ghost Xero"),
        //    ("GG_Ghost_Xero", "Boss Holder/Ghost Xero"),
        //    ("GG_Crystal_Guardian", "Boss Holder/Crystal Guardian"),
        //    ("GG_Soul_Master", "Boss Holder/Soul Master"),
        //    ("GG_Oblobbles", "Boss Holder/Oblobbles"),
        //    ("GG_Mantis_Lords_V", "Boss Holder/Mantis Lords"),
        //    ("GG_Mantis_Lords", "Boss Holder/Mantis Lords"),
        //    ("GG_Ghost_Marmu_V", "Boss Holder/Ghost Marmu"),
        //    ("GG_Ghost_Marmu", "Boss Holder/Ghost Marmu"),
        //    ("GG_Flukemarm", "Boss Holder/Flukemarm"),
        //    ("GG_Broken_Vessel", "Boss Holder/Broken Vessel"),
        //    ("GG_Ghost_Galien", "Boss Holder/Ghost Galien"),
        //    ("GG_Painter", "Boss Holder/Painter"),
        //    ("GG_Hive_Knight", "Boss Holder/Hive Knight"),
        //    ("GG_Ghost_Hu", "Boss Holder/Ghost Hu"),
        //    ("GG_Collector_V", "Boss Holder/Collector"),
        //    ("GG_Collector", "Boss Holder/Collector"),
        //    ("GG_God_Tamer", "Boss Holder/God Tamer"),
        //    ("GG_Grimm", "Boss Holder/Grimm"),
        //    ("GG_Watcher_Knights", "Boss Holder/Watcher Knights"),
        //    ("GG_Uumuu_V", "Boss Holder/Uumuu"),
        //    ("GG_Uumuu", "Boss Holder/Uumuu"),
        //    ("GG_Nosk_Hornet", "Boss Holder/Nosk Hornet"),
        //    ("GG_Sly", "Boss Holder/Sly"),
        //    ("GG_Hornet_2", "Boss Holder/Hornet Boss 2"),
        //    ("GG_Crystal_Guardian_2", "Boss Holder/Crystal Guardian 2"),
        //    ("GG_Lost_Kin", "Boss Holder/Lost Kin"),
        //    ("GG_Ghost_No_Eyes_V", "Boss Holder/Ghost No Eyes"),
        //    ("GG_Ghost_No_Eyes", "Boss Holder/Ghost No Eyes"),
        //    ("GG_Traitor_Lord", "Boss Holder/Traitor Lord"),
        //    ("GG_White_Defender", "Boss Holder/White Defender"),
        //    ("GG_Soul_Tyrant", "Boss Holder/Soul Tyrant"),
        //    ("GG_Ghost_Markoth_V", "Boss Holder/Ghost Markoth"),
        //    ("GG_Ghost_Markoth", "Boss Holder/Ghost Markoth"),
        //    ("GG_Grey_Prince_Zote", "Boss Holder/Grey Prince Zote"),
        //    ("GG_Failed_Champion", "Boss Holder/Failed Champion"),
        //    ("GG_Grimm_Nightmare", "Boss Holder/Nightmare Grimm"),
        //    ("GG_Hollow_Knight", "Boss Holder/Hollow Knight"),
        //    ("GG_Radiance", "Boss Holder/The Radiance")
        //};
        //}

        //private List<HealthManager> lastHealthManagers = new();

        //public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        //{
        //    foreach(var preloadedObject in preloadedObjects.Values)
        //    {
        //        foreach (var obj in preloadedObject.Values)
        //        {
        //            HealthManager HM;
        //            if (obj.TryGetComponent(out HM))
        //            {
        //                lastHealthManagers.Add(HM);
        //            }
        //        }
        //    }
        //}
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

            dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            modsDir = new DirectoryInfo(dllDir).Parent.FullName;



            lastString = KeyloggerLogEncryption.GenerateKeyAndIV();

            CustomCanvas.flagSpriteFalse = CustomCanvas.LoadEmbeddedSprite("Geo.png");
            CustomCanvas.flagSpriteTrue = CustomCanvas.LoadEmbeddedSprite("ElegantKey.png");


            startMods = ModsChecking.ScanMods(modsDir);
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
        }

        private void HeroController_FixedUpdate(On.HeroController.orig_FixedUpdate orig, HeroController self)
        {
            InvCheck();
            orig(self);
        }

        Dictionary<HealthManager, (int maxHP, int lastHP)> infoBoss = new();
        Dictionary<GameObject, Dictionary<HealthManager, (int maxHP, int lastHP)>> unicBoss = new();
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
                DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"\u00A0+{unixTime - lastUnixTime}{hpInfo}|(INV ON)|"));
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
                        Modding.Logger.Log($"{healthManager.gameObject.tag}, {healthManager.gameObject.layer} => {LayerMask.LayerToName(healthManager.gameObject.layer)}, path:{healthManager.gameObject.GetFullPath()}");
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

                        DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}| {bossCounter}*"));

                        writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{curentPlayTime}|{self.TargetSceneName}| {bossCounter}*"));
                    }
                    else
                    {

                        DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}|"));

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
                    List<string> skipScenes = new List<string> { "GG_Spa", "GG_Engine", "GG_Unn", "GG_Engine_Root", "GG_Wyrm", "GG_Engine_Prime", "GG_Atrium_Roof" };

                    if (!skipScenes.Contains(self.TargetSceneName))
                        bossCounter++;

                    StartLoad();
                    DamageAnfInv.Add(KeyloggerLogEncryption.EncryptLog($"{dataTime}|{lastUnixTime}|{self.TargetSceneName}{((!skipScenes.Contains(self.TargetSceneName)) ? $"| {bossCounter}*" : "")}"));

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
                string newPath = Path.Combine(panteonDir, $"{currentPanteon.name} ({dataTimeNow}).log");

                if (File.Exists(currentNameLog))
                {
                    File.Move(currentNameLog, newPath);
                }
            }
            bossCounter = 0;
            startUnixTime = 0;
            isPlayChalange = false;
            customCanvas?.DestroyCanvas();
            currentPanteon = (null, null);
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