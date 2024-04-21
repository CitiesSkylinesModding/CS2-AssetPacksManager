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

        public void OnLoad(UpdateSystem updateSystem)
        {
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                ModPath = Path.GetDirectoryName(asset.path);
            }

            /*Logger.Info("kCacheDataPath: " + EnvPath.kCacheDataPath);
            Logger.Info("kAssetDataPath: " + EnvPath.kAssetDataPath);
            Logger.Info("kGameDataPath: " + EnvPath.kGameDataPath);
            Logger.Info("kStreamingDataPath: " + EnvPath.kStreamingDataPath);
            Logger.Info("kTempDataPath: " + EnvPath.kTempDataPath);
            Logger.Info("kCachePathName: " + EnvPath.kCachePathName);
            Logger.Info("kLocalModsPath: " + EnvPath.kLocalModsPath);
            Logger.Info("kVTDataPath: " + EnvPath.kVTDataPath);
            Logger.Info("kVTPathName: " + EnvPath.kVTPathName);
            Logger.Info("kVTSubPath: " + EnvPath.kVTSubPath);*/
            //prefabSystem = updateSystem.World.GetOrCreateSystemManaged<PrefabSystem>();
            //var dir = "C:/Users/" + Environment.UserName + "/Desktop/assets";
            //LoadFromDirectory(dir);
            //CopyDirectoryToInstalled(dir);
            //Logger.Info("Loaded Directory: " + dir);

            Setting setting = new (this);
            setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting));
            AssetDatabase.global.LoadSettings(nameof(AssetImporter), setting, new Setting(this));
            setting.HiddenSetting = false;
            Setting.instance = setting;

            UIManager.defaultUISystem.AddHostLocation("customassets", $"{EnvPath.kUserDataPath}/CustomAssets");


            //Colossal.IO.AssetDatabase.AssetDatabase.databases
            //SyncAssets();
        }

        public static void SyncAssets()
        {
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
            List<FileInfo> expectedAssets = new();

            foreach (var modInfo in GameManager.instance.modManager)
            {
                if (modInfo.asset.isEnabled)
                {
                    var modDir = Path.GetDirectoryName(modInfo.asset.path);
                    if (modDir == null)
                        continue;
                    if (modDir.Contains($"{EnvPath.kLocalModsPath}/Mods") && !Setting.instance.EnableLocalAssetPacks)
                    {
                        Logger.Info($"Skipping local mod {modInfo.name}");
                        continue;
                    }

                    var mod = new DirectoryInfo(modDir);
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        Logger.Info($"Copying assets from {mod.Name}");
                        expectedAssets.AddRange(CollectAssetsRecursively(assetDir.FullName));
                    }
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
            var assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";
            int changedFiles = 0;
            List<string> checkedFiles = new();

            foreach (var file in expectedFiles)
            {
                var targetFilePath = Path.Combine(assetPath, file.Name);
                if (!File.Exists(targetFilePath))
                {
                    file.CopyTo(targetFilePath);
                    Logger.Info($"Added file: {targetFilePath}");
                    changedFiles++;
                    checkedFiles.Add(targetFilePath);
                }
                else
                {
                    // Check if file is different
                    using StreamReader updatedReader = new StreamReader(file.FullName);
                    var updatedContent = updatedReader.ReadToEnd();
                    updatedReader.Close();
                    using StreamReader existingReader = new StreamReader(targetFilePath);
                    var existingContent = existingReader.ReadToEnd();
                    existingReader.Close();
                    if (updatedContent != existingContent)
                    {
                        file.CopyTo(targetFilePath, true);
                        Logger.Info($"Updated file: {targetFilePath}");
                        changedFiles++;
                    }
                    checkedFiles.Add(targetFilePath);
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

        public static void DeleteImportedAssets()
        {
            var assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";
            if (Directory.Exists(assetPath))
            {
                Directory.Delete(assetPath, true);
                Logger.Info("Deleted CustomAssets directory");
            }
        }

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