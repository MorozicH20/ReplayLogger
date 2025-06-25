using HutongGames.PlayMaker;
using IL;
using Modding;
using On;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static UnityEngine.Networking.UnityWebRequest;
using UObject = UnityEngine.Object;

namespace LegitimateChallenge
{
    public class LegitimateChallenge : Mod
    {
        internal static LegitimateChallenge Instance;

        internal CustomCanvas customCanvas;

        public string fileName = "KeyLog.log";


        private string filePath;
        private StreamWriter writer;
        private string lastString;
        private string lastScene;
        private List<string> currentPanteon;

        private long lastUnixTime;
        private long startUnixTime;

        private bool isPlayChalange = false;
        public LegitimateChallenge() : base(ModInfo.Name) { }
        public override string GetVersion() => ModInfo.Version;

        public override void Initialize()
        {
            base.Initialize();
            Instance = this;
            On.SceneLoad.Begin += OpenFile;
            On.GameManager.Update += CheckPressedKey;
            ModHooks.ApplicationQuitHook += Close;
            On.QuitToMenu.Start += QuitToMenu_Start;

            filePath = Path.Combine(Application.persistentDataPath, fileName);

            lastString = KeyloggerLogEncryption.GenerateKeyAndIV();

            CustomCanvas.flagSpriteFalse = CustomCanvas.LoadEmbeddedSprite("Geo.png");
            CustomCanvas.flagSpriteTrue = CustomCanvas.LoadEmbeddedSprite("ElegantKey.png");

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

        private void OpenFile(On.SceneLoad.orig_Begin orig, SceneLoad self)
        {
            lastUnixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
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
                    writer = new StreamWriter(filePath, false);
                }
                catch (Exception e)
                {
                    Modding.Logger.LogError("Ошибка при открытии файла: " + e.Message);
                }

                int seed = (int)(lastUnixTime ^ curentPlayTime);

                customCanvas = new CustomCanvas(new NumberInCanvas(seed), new LoadingSprite(lastString));

                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"time: {lastUnixTime} | playTime: {curentPlayTime} | scene: {self.TargetSceneName}"));
                writer?.Flush();

            }
            else if (isPlayChalange)
            {
                if (currentPanteon == null && lastScene.Contains("GG_Boss_Door") || lastScene.Contains("GG_Vengefly_V"))
                {
                    if (self.TargetSceneName == Panteons.P1[0])
                        currentPanteon = Panteons.P1.ToList();
                    if (self.TargetSceneName == Panteons.P2[0])
                        currentPanteon = Panteons.P2.ToList();
                    if (self.TargetSceneName == Panteons.P3[0])
                        currentPanteon = Panteons.P3.ToList();
                    if (self.TargetSceneName == Panteons.P4[0])
                        currentPanteon = Panteons.P4.ToList();
                    if (self.TargetSceneName == Panteons.P5[1])
                        currentPanteon = Panteons.P5.ToList();
                }
                else
                {

                    if (currentPanteon.IndexOf(lastScene) != -1 && !(currentPanteon[currentPanteon.IndexOf(lastScene) + 1] == self.TargetSceneName))
                    {

                        Close();
                    }
                    if (lastScene == "GG_Spa")
                    {
                        currentPanteon.Remove(lastScene);
                    }
                }

                StartLoad();
                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"time: {lastUnixTime} | scene: {self.TargetSceneName}"));
                writer?.Flush();

            }
            lastScene = self.TargetSceneName;
            orig(self);
        }


        private void Close()
        {
            if (writer != null)
            {
                writer.Write(lastString);
                writer.Close();
                writer = null;
                Modding.Logger.Log("Логгер клавиатуры остановлен.");
            }
            startUnixTime = 0;
            isPlayChalange = false;
            customCanvas?.DestroyCanvas();
            currentPanteon = null;
        }

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
                    string logEntry = $"+{unixTime - lastUnixTime} | {keyCode} | {keyStatus}";
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