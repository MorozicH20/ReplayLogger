using HutongGames.PlayMaker;
using IL;
using Modding;
using On;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
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

        private long lastUnixTime;
        private long startUnixTime;

        private bool inPlayChalange = false;
        public LegitimateChallenge() : base(ModInfo.Name) { }
        public override string GetVersion() => ModInfo.Version;

        public override void Initialize()
        {
            base.Initialize();
            Instance = this;
            On.SceneLoad.Begin += OpenFile;
            ModHooks.BeforeSceneLoadHook += StartLoad;
            On.GameManager.Update += CheckPressedKey;
            ModHooks.ApplicationQuitHook += Close;

            filePath = Path.Combine(Application.persistentDataPath, fileName);

            lastString = KeyloggerLogEncryption.GenerateKeyAndIV();
        }

        private string StartLoad(string arg)
        {
            customCanvas?.StartUpdateSprite();
            return arg;
        }

        private void OpenFile(On.SceneLoad.orig_Begin orig, SceneLoad self)
        {
            Modding.Logger.Log(self.TargetSceneName);
            lastUnixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
            if (inPlayChalange && self.TargetSceneName.Contains("GG_Atrium"))
            {
                startUnixTime = 0;
                inPlayChalange = false;
                customCanvas?.DestroyCanvas();
            }
            if (self.TargetSceneName.Contains("GG_Boss_Door") || self.TargetSceneName.Contains("GG_End_Sequence") || (self.TargetSceneName.Contains("GG_Vengefly_V") && lastScene == "GG_Atrium_Roof"))
            {
                startUnixTime = lastUnixTime;
                inPlayChalange = true;
                Close();

                try
                {
                    writer = new StreamWriter(filePath, false);
                }
                catch (Exception e)
                {
                    Modding.Logger.LogError("Ошибка при открытии файла: " + e.Message);
                }
                Modding.Logger.Log("Логгер клавиатуры запущен. Запись в: " + filePath);

                int seed = (int)(lastUnixTime ^ (int)(PlayerData.instance.playTime * 100));

                customCanvas = new CustomCanvas(new NumberInCanvas(seed), new LoadingSprite(lastString));

                writer?.WriteLine(KeyloggerLogEncryption.EncryptLog($"time: {lastUnixTime} | playTime: {PlayerData.instance.playTime * 100} | scene: {self.TargetSceneName}"));
                writer?.Flush();

            }
            else
            {
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


        }

        private void CheckPressedKey(On.GameManager.orig_Update orig, GameManager self)
        {
            orig(self);
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