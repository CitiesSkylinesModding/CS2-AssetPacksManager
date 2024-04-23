using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Environment;
using Colossal.UI;
using Game.Debug;
using Game.Prefabs;
using Game.PSI;
using Game.Simulation;
using JetBrains.Annotations;
using Unity.Entities;
using Hash128 = Colossal.Hash128;
using StreamReader = System.IO.StreamReader;

namespace AssetImporter
{
    public class Mod : IMod
    {
        public static readonly ILog Logger = LogManager.GetLogger($"{nameof(AssetImporter)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        [CanBeNull] public string ModPath { get; set; }

        private PrefabSystem prefabSystem;

        private static string assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";
        
        public void OnLoad(UpdateSystem updateSystem)
        {

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                ModPath = Path.GetDirectoryName(asset.path);
            }

            Setting setting = new (this);
            setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting));
            AssetDatabase.global.LoadSettings(nameof(AssetImporter), setting, new Setting(this));
            setting.HiddenSetting = false;
            Setting.instance = setting;

            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }
            UIManager.defaultUISystem.AddHostLocation("customassets", assetPath);
            Log("Added custom assets COUI location");


            //var path1 = AssetDataPath.Create("Mods/SmallFireHouse01", "SmallFireHouse01");
            //AssetDatabase.game.AddAsset<PrefabAsset>(path1, Guid.NewGuid());
            // Maybe Prefab instead of PrefabAsset

            //AssetDatabase.user.AddAsset(path);

            SyncAssets();
        }

        private static void Log(string message, bool alwaysLog = false)
        {
            if (Setting.instance.EnableVerboseLogging || alwaysLog)
                Logger.Info(message);
        }


        private static void TryAddPrefab(string targetFilePath)
        {
            return;
            Log("TryAddPrefab: " + targetFilePath);
            if (targetFilePath.EndsWith(".Prefab"))
            {
                var relativePath = @"Mods\CustomAssets" + targetFilePath.Replace(assetPath, "");
                var fileNameInFolder = Path.GetFileNameWithoutExtension(relativePath);
                var pathWithoutFileName = relativePath.Replace(fileNameInFolder + ".Prefab", "");
                pathWithoutFileName = pathWithoutFileName.Replace(" ", "_");


                Log("Try Adding Prefab with path: " + pathWithoutFileName);
                Log("Try Adding Prefab with name: " + fileNameInFolder);
                var path = AssetDataPath.Create(pathWithoutFileName, fileNameInFolder);
                var cidFilename = targetFilePath + ".cid";
                using StreamReader sr = new StreamReader(cidFilename);
                var guid = new Guid(sr.ReadToEnd());
                sr.Close();
                AssetDatabase.game.AddAsset<PrefabAsset>(path, guid);
                Log("Prefab added successfully");
            }
        }


        private static void DumpAssets(string name)
        {
            var x = AssetDatabase.game.AllAssets().GetEnumerator();
            string s = "";
            while (x.MoveNext())
            {
                s += x.Current?.name + " " + x.Current?.database.name + "\n";
            }

            var path = "C:/Users/Konsi/Documents/CS2-Modding/CS2-AssetImporter/AssetsDump" + name + ".txt";

            // Create file
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.Write(s);
            }
        }

        public static void SyncAssets()
        {
            Log("Starting Asset Sync", true);
            Log("Asset Path: " + assetPath);
            if (!Directory.Exists(assetPath))
            {
                Log("Creating CustomAssets directory");
                Directory.CreateDirectory(assetPath);
            }

            var expectedFiles = CollectExpectedAssets();
            Log("Expected files: " + expectedFiles.Count);
            var changedFiles = ApplySync(expectedFiles);
            if (changedFiles > 0)
            {
                SendAssetChangedNotification(changedFiles);
            }
        }

        public static List<FileInfo> CollectExpectedAssets()
        {
            Log("Collecting expected assets", true);
            List<FileInfo> expectedAssets = new();

            foreach (var modInfo in GameManager.instance.modManager)
            {
                Log("Checking mod: " + modInfo.name);
                if (modInfo.asset.isEnabled)
                {
                    var modDir = Path.GetDirectoryName(modInfo.asset.path);
                    if (modDir == null)
                        continue;
                    if (modDir.Contains($"{EnvPath.kLocalModsPath}/Mods") && !Setting.instance.EnableLocalAssetPacks)
                    {
                        Log($"Skipping local mod {modInfo.name} (" + modInfo.assemblyFullName + ")");
                        continue;
                    }
                    if (!Setting.instance.EnableSubscribedAssetPacks)
                        continue;

                    var mod = new DirectoryInfo(modDir);
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        Log($"Copying assets from {mod.Name} (" + modInfo.name + ")");
                        expectedAssets.AddRange(CollectAssetsRecursively(assetDir.FullName));
                    }
                }
                else
                {
                    Log($"Skipping disabled mod {modInfo.name} (" + modInfo.name + ")");
                }
            }

            return expectedAssets;
        }

        private static List<FileInfo> CollectAssetsRecursively(string directory)
        {
            List<FileInfo> files = new();
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
                Logger.Error($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (FileInfo file in dir.GetFiles())
            {
                files.Add(file);
            }
            foreach (DirectoryInfo subDir in dirs)
            {
                files.AddRange(CollectAssetsRecursively(subDir.FullName));
            }
            return files;
        }

        private static int createdFiles;
        private static int updatedFiles;
        private static int deletedFiles;

        public static int ApplySync(List<FileInfo> expectedFiles)
        {
            createdFiles = 0;
            updatedFiles = 0;
            deletedFiles = 0;

            Log("Applying sync", true);
            int changedFiles = 0;
            List<string> checkedFiles = new();

            foreach (var file in expectedFiles)
            {
                Log("Syncing file: " + file.FullName);
                var targetFilePath = file.FullName.Split([@"\assets\"], StringSplitOptions.None)[1];
                targetFilePath = Path.Combine(assetPath, targetFilePath);
                Log("Target: " + targetFilePath);
                if (!File.Exists(targetFilePath))
                {
                    Log("File not existing, copying...");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                    file.CopyTo(targetFilePath);
                    Log($"Added file: {targetFilePath}");
                    changedFiles++;
                    checkedFiles.Add(targetFilePath);
                    createdFiles++;
                    //TryAddPrefab(targetFilePath);
                }
                else
                {
                    Log("File existing, verifying...");
                    // Check if file is different
                    using StreamReader updatedReader = new StreamReader(file.FullName);
                    var updatedContent = updatedReader.ReadToEnd();
                    updatedReader.Close();
                    using StreamReader existingReader = new StreamReader(targetFilePath);
                    var existingContent = existingReader.ReadToEnd();
                    existingReader.Close();
                    Log("Comparing content...");
                    if (updatedContent != existingContent)
                    {
                        Log("Content is different, updating...");
                        file.CopyTo(targetFilePath, true);
                        Log($"Updated file: {targetFilePath}");
                        updatedFiles++;
                        changedFiles++;
                    }
                    else
                    {
                        Log("Content is the same, skipping...");
                    }
                    checkedFiles.Add(targetFilePath);
                    //TryAddPrefab(targetFilePath);
                }
            }

            foreach (string file in Directory.EnumerateFiles(assetPath, "*.*", SearchOption.AllDirectories))
            {
                if (expectedFiles.All(f => f.FullName != file) && !checkedFiles.Contains(file))
                {
                    File.Delete(file);
                    Log($"Deleted file: {file}");
                    changedFiles++;
                    deletedFiles++;
                }
            }

            return changedFiles;
        }

        public static void DeleteImportedAssets()
        {
            var assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";
            if (Directory.Exists(assetPath))
            {
                Directory.Delete(assetPath, true);
                Log("Deleted CustomAssets directory");
            }
            NotificationSystem.Pop("asset-importer");
            NotificationSystem.Push("asset-importer", "Deleted imported assets", "Imported assets have been deleted. Click here to sync", onClicked:() => SyncAssets());
            if (Setting.instance.AutoHideNotifications)
                NotificationSystem.Pop("asset-importer", 30f);
        }

        private static async void SendAssetChangedNotification(int assetsChanged)
        {
            Log("Assets have been changed. Waiting for mod manager initialization to show warning", true);
            //Log("Mod Manager init: " + GameManager.instance.modManager.isInitialized + " Restart: " + GameManager.instance.modManager.restartRequired);

            // Delay by 100 ms, because we have to wait for the mod manager to initialize
            while (!GameManager.instance.modManager.isInitialized)
            {
                await Task.Delay(100);
            }

            NotificationSystem.Pop("asset-importer");
            NotificationSystem.Push("asset-importer", $"Asset Importer ({createdFiles} created, {updatedFiles} updated, {deletedFiles} deleted)",$"Custom Assets have been changed. Restart the game to apply changes");
            if (Setting.instance.AutoHideNotifications)
                NotificationSystem.Pop("asset-importer", 30f);
            //GameManager.instance.modManager.RequireRestart();
            Log("Mod Manager init: " + GameManager.instance.modManager.isInitialized + " Restart: " + GameManager.instance.modManager.restartRequired, true);
        }

        public void OnDispose()
        {
            Log(nameof(OnDispose));
            if (Setting.instance != null)
            {
                Setting.instance.UnregisterInOptionsUI();
                Setting.instance = null;
            }
        }
    }
}