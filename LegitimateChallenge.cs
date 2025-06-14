using Modding;
using On;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;
using HutongGames.PlayMaker;

namespace LegitimateChallenge
{
    public class LegitimateChallenge : Mod
    {
        internal static LegitimateChallenge Instance;

        public LegitimateChallenge() : base(ModInfo.Name) { }
        public override void Initialize()
        {
            base.Initialize();
            Instance = this;
            On.SceneLoad.Begin += OpenFile;
            On.GameManager.Update += CheckPressedKey;
            On.GameManager.QuitGame += Close;
        }


        private void OpenFile(On.SceneLoad.orig_Begin orig, SceneLoad self)
        {
            lastUnixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();

            if (writer != null)
            {

                writer?.WriteLine($"time: {lastUnixTime} | scene: {self.TargetSceneName}"); // Используем оператор ?, чтобы избежать ошибки, если writer null
                writer?.Flush(); // Важно для немедленной записи
                orig(self);

                return;
            }

            // Определяем путь к файлу (в папке PersistentDataPath для надежности)
            filePath = Path.Combine(Application.persistentDataPath, fileName);
            //File.Create(filePath);

            // Открываем файл для записи
            try
            {

                writer = new StreamWriter(filePath, false); // true для добавления в файл, если он уже существует
                writer?.WriteLine($"time: {lastUnixTime} | scene: {self.TargetSceneName}"); // Используем оператор ?, чтобы избежать ошибки, если writer null
                writer?.Flush(); // Важно для немедленной записи

            }
            catch (Exception e)
            {
                Modding.Logger.LogError("Ошибка при открытии файла: " + e.Message);
            }

            Modding.Logger.Log("Логгер клавиатуры запущен. Запись в: " + filePath);
            orig(self);

        }

        private IEnumerator Close(On.GameManager.orig_QuitGame orig, GameManager self)
        {
            if (writer != null)
            {

                writer.Close();
                Modding.Logger.Log("Логгер клавиатуры остановлен.");
            }
            return orig(self);

        }


        public FsmGameObject spawnPoint ;

        public FsmVector3 position;

        public FsmInt spawnMin;

        public FsmInt spawnMax;

        public FsmFloat speedMin;

        public FsmFloat speedMax;

        public FsmFloat angleMin;

        public FsmFloat angleMax;

        public FsmColor colorOverride;

        private void CheckPressedKey(On.GameManager.orig_Update orig, GameManager self)
        {
            orig(self);

            if (Input.GetKeyDown(KeyCode.V))
            {
                if (GlobalPrefabDefaults.Instance)
                {
                    Vector3 value = position.Value;
                    if ((bool)spawnPoint.Value)
                    {
                        value += spawnPoint.Value.transform.position;
                    }
                    GlobalPrefabDefaults.Instance.SpawnBlood(value, (short)spawnMin.Value, (short)spawnMax.Value, speedMin.Value, speedMax.Value, angleMin.Value, angleMax.Value, colorOverride.IsNone ? ((Color?)null) : new Color?(colorOverride.Value));
                }


            }

            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(keyCode) || Input.GetKeyUp(keyCode)) // Отслеживаем нажатие и отпускание клавиш
                {
                    string keyStatus = Input.GetKeyDown(keyCode) ? "+" : "-"; // Определяем статус клавиши

                    long unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
                    // Форматируем строку лога
                    string logEntry = $"+{unixTime - lastUnixTime} | {keyCode} | {keyStatus}";

                    // Записываем в файл
                    try
                    {
                        writer?.WriteLine(logEntry); // Используем оператор ?, чтобы избежать ошибки, если writer null
                        writer?.Flush(); // Важно для немедленной записи
                    }
                    catch (Exception e)
                    {
                        Modding.Logger.LogError("Ошибка записи в файл: " + e.Message);
                    }
                }
            }
        }


        public string fileName = "KeyLog.log";

        private string filePath;
        private StreamWriter writer;

        private long lastUnixTime;
    }
}