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

        private static string assetPath = $"{EnvPath.kGameDataPath}/StreamingAssets/Mods/CustomAssets";

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

            UIManager.defaultUISystem.AddHostLocation("customassets", assetPath);
            Logger.Info("Added custom assets COUI location");


            Logger.Info("Assets.");
            var path1= AssetDataPath.Create("Mods/CustomAssets/SmallFireHouse01", "SmallFireHouse01");
            using StreamReader sr1 = new StreamReader("D:/Games/steamapps/common/Cities Skylines II/Cities2_Data/StreamingAssets/Mods/CustomAssets/SmallFireHouse01/SmallFireHouse01.Prefab.cid");
            var guid1 = new Guid(sr1.ReadToEnd());
            AssetDatabase.game.AddAsset<PrefabAsset>(path1, Guid.NewGuid());

            var path3= AssetDataPath.Create("Mods/SmallFireHouse01", "SmallFireHouse01.Prefab");
            AssetDatabase.game.AddAsset<PrefabAsset>(path3, Guid.NewGuid());

            var path2= AssetDataPath.Create("Mods/CustomAssets/Mixed Residential Fire Station", "Mixed Residential Fire Station");
            using StreamReader sr2 = new StreamReader("D:/Games/steamapps/common/Cities Skylines II/Cities2_Data/StreamingAssets/Mods/CustomAssets/Mixed Residential Fire Station/Mixed Residential Fire Station.Prefab.cid");
            var guid2 = new Guid(sr2.ReadToEnd());
            AssetDatabase.game.AddAsset<PrefabAsset>(path2, guid2);
            Logger.Info("Assets.");

            DumpAssets("Test");


            //var path1 = AssetDataPath.Create("Mods/SmallFireHouse01", "SmallFireHouse01");
            //AssetDatabase.game.AddAsset<PrefabAsset>(path1, Guid.NewGuid());
            // Maybe Prefab instead of PrefabAsset

            //AssetDatabase.user.AddAsset(path);
            //SyncAssets();
        }

        public static int ix = 0;

        private static void TryAddPrefab(string targetFilePath)
        {
            return;
            Logger.Info("TryAddPrefab: " + targetFilePath);
            if (targetFilePath.EndsWith(".Prefab"))
            {
                var relativePath = @"Mods\CustomAssets" + targetFilePath.Replace(assetPath, "");
                var fileNameInFolder = Path.GetFileNameWithoutExtension(relativePath);
                var pathWithoutFileName = relativePath.Replace(fileNameInFolder + ".Prefab", "");
                pathWithoutFileName = pathWithoutFileName.Replace(" ", "_");


                // TODO Remove spaces from prefab name and see if that was the issue
                Logger.Info("Try Adding Prefab with path: " + pathWithoutFileName);
                Logger.Info("Try Adding Prefab with name: " + fileNameInFolder);
                var path = AssetDataPath.Create(pathWithoutFileName, fileNameInFolder);
                var cidFilename = targetFilePath + ".cid";
                using StreamReader sr = new StreamReader(cidFilename);
                var guid = new Guid(sr.ReadToEnd());
                sr.Close();
                AssetDatabase.game.AddAsset<PrefabAsset>(path, guid);
                Logger.Info("Prefab added successfully");
            }

            DumpAssets("Dump" + ix);
            ix++;
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
            Logger.Info("Starting Asset Sync");
            Logger.Info("Asset Path: " + assetPath);
            if (!Directory.Exists(assetPath))
            {
                Logger.Info("Creating CustomAssets directory");
                Directory.CreateDirectory(assetPath);
            }

            var expectedFiles = CollectExpectedAssets();
            Logger.Info("Expected files: " + expectedFiles.Count);
            var changedFiles = ApplySync(expectedFiles);
            if (changedFiles > 0)
            {
                SendAssetChangedNotification(changedFiles);
            }
        }

        public static List<FileInfo> CollectExpectedAssets()
        {
            Logger.Info("Collecing expected assets");
            List<FileInfo> expectedAssets = new();

            foreach (var modInfo in GameManager.instance.modManager)
            {
                Logger.Info("Checking mod: " + modInfo.name);
                if (modInfo.asset.isEnabled)
                {
                    var modDir = Path.GetDirectoryName(modInfo.asset.path);
                    if (modDir == null)
                        continue;
                    if (modDir.Contains($"{EnvPath.kLocalModsPath}/Mods") && !Setting.instance.EnableLocalAssetPacks)
                    {
                        Logger.Info($"Skipping local mod {modInfo.name} (" + modInfo.assemblyFullName + ")");
                        continue;
                    }
                    if (!Setting.instance.EnableSubscribedAssetPacks)
                        continue;

                    var mod = new DirectoryInfo(modDir);
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        Logger.Info($"Copying assets from {mod.Name} (" + modInfo.name + ")");
                        expectedAssets.AddRange(CollectAssetsRecursively(assetDir.FullName));
                    }
                }
                else
                {
                    Logger.Info($"Skipping disabled mod {modInfo.name} (" + modInfo.name + ")");
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

        public static int ApplySync(List<FileInfo> expectedFiles)
        {
            Logger.Info("Applying sync");
            int changedFiles = 0;
            List<string> checkedFiles = new();

            foreach (var file in expectedFiles)
            {
                Logger.Info("Syncing file: " + file.FullName);
                var targetFilePath = file.FullName.Split([@"\assets\"], StringSplitOptions.None)[1];
                targetFilePath = Path.Combine(assetPath, targetFilePath);
                Logger.Info("Target: " + targetFilePath);
                if (!File.Exists(targetFilePath))
                {
                    Logger.Info("File not existing, copying...");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                    file.CopyTo(targetFilePath);
                    Logger.Info($"Added file: {targetFilePath}");
                    changedFiles++;
                    checkedFiles.Add(targetFilePath);
                    TryAddPrefab(targetFilePath);
                }
                else
                {
                    Logger.Info("File existing, verifying...");
                    // Check if file is different
                    using StreamReader updatedReader = new StreamReader(file.FullName);
                    var updatedContent = updatedReader.ReadToEnd();
                    updatedReader.Close();
                    using StreamReader existingReader = new StreamReader(targetFilePath);
                    var existingContent = existingReader.ReadToEnd();
                    existingReader.Close();
                    Logger.Info("Comparing content...");
                    if (updatedContent != existingContent)
                    {
                        Logger.Info("Content is different, updating...");
                        file.CopyTo(targetFilePath, true);
                        Logger.Info($"Updated file: {targetFilePath}");
                        changedFiles++;
                    }
                    else
                    {
                        Logger.Info("Content is the same, skipping...");
                    }
                    checkedFiles.Add(targetFilePath);
                    TryAddPrefab(targetFilePath);
                }
            }

            foreach (string file in Directory.EnumerateFiles(assetPath, "*.*", SearchOption.AllDirectories))
            {
                if (expectedFiles.All(f => f.FullName != file) && !checkedFiles.Contains(file))
                {
                    File.Delete(file);
                    Logger.Info($"Deleted file: {file}");
                    changedFiles++;
                }
            }

            return changedFiles;
        }

        /*public static void DeleteImportedAssets()
        {
            var assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";
            if (Directory.Exists(assetPath))
            {
                Directory.Delete(assetPath, true);
                Logger.Info("Deleted CustomAssets directory");
            }
        }*/

        private static async void SendAssetChangedNotification(int assetsChanged)
        {
            Logger.Info("Assets have been changed. Waiting for mod manager initialization to show warning");
            //Logger.Info("Mod Manager init: " + GameManager.instance.modManager.isInitialized + " Restart: " + GameManager.instance.modManager.restartRequired);

            // Delay by 100 ms, because we have to wait for the mod manager to initialize
            while (!GameManager.instance.modManager.isInitialized)
            {
                await Task.Delay(100);
            }

            NotificationSystem.Push("asset-importer", "Asset Importer",$"{assetsChanged} custom assets have been updated. Restart the game to apply changes");
            //GameManager.instance.modManager.RequireRestart();
            Logger.Info("Mod Manager init: " + GameManager.instance.modManager.isInitialized + " Restart: " + GameManager.instance.modManager.restartRequired);
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
            if (Setting.instance != null)
            {
                Setting.instance.UnregisterInOptionsUI();
                Setting.instance = null;
            }
        }
    }
}