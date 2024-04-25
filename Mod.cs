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
using Colossal.Json;
using Colossal.PSI.Environment;
using Colossal.UI;
using Game.Debug;
using Game.Prefabs;
using Game.PSI;
using Game.Simulation;
using JetBrains.Annotations;
using Unity.Entities;
using UnityEngine;
using Hash128 = Colossal.Hash128;
using StreamReader = System.IO.StreamReader;

namespace AssetPacksManager
{
    public class Mod : IMod
    {
        public static readonly ILog Logger = LogManager.GetLogger($"{nameof(AssetPacksManager)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        [CanBeNull] public string ModPath { get; set; }

        private static PrefabSystem prefabSystem;

        private static string assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";

        // Each mod has a dict entry that contains the missing cid prefabs
        private static Dictionary<string, List<string>> missingCids = new();
        
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
            AssetDatabase.global.LoadSettings(nameof(AssetPacksManager), setting, new Setting(this));
            setting.HiddenSetting = false;
            Setting.instance = setting;

            prefabSystem = updateSystem.World.GetOrCreateSystemManaged<PrefabSystem>();

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

            DatabaseExperiment();
            return;
            SyncAssets();

            foreach(string key in missingCids.Keys)
            {
                NotificationSystem.Pop(key, 300f, title:$"Missing CID for {missingCids[key].Count} prefabs", text: $"The mod {key.Split(',')[0]} is missing CID for {missingCids[key].Count} prefabs.");
            }
        }

        private static void Log(string message, bool alwaysLog = false)
        {
            if (Setting.instance.EnableVerboseLogging || alwaysLog)
                Logger.Info(message);
        }

        private static void DatabaseExperiment()
        {
            /*var name = AssetDataPath.Create("Mods/SmallFireHouse01", "SmallFireHouse01");
            new PrefabAsset();
            var prefab = new GameObject("SmallFireHouse01");
            PrefabAssetExtensions.AddAsset(AssetDatabase.game, name, prefab);*/

            foreach (PrefabAsset p in AssetDatabase.global.GetAssets<PrefabAsset>())
            {
                Logger.Info("I Name: " + p.name);
                Logger.Info("I Path: " + p.path);
                Logger.Info("I SubPath: " + p.subPath);
            }

            var relativePath = "Mods/CustomAssets/SmallFireHouse01";
            var fileName = "SmallFireHouse01";

            var path = AssetDataPath.Create(relativePath, fileName);
            Logger.Info("Subpath: " + path.subPath);
            Logger.Info("Ext: " + path.extension);
            Logger.Info("AssetName: " + path.assetName);
            Logger.Info("ToPath: " + path.ToPath(new FileSystemDataSource.PathEscapePolicy()));
            Logger.Info("ToFileName: " + path.ToFilename(new FileSystemDataSource.PathEscapePolicy()));
            var cidFilename = Path.Combine(EnvPath.kGameDataPath, "StreamingAssets", relativePath, (fileName + ".Prefab.cid"));
            using StreamReader sr = new StreamReader(cidFilename);
            var guid = new Guid(sr.ReadToEnd());
            sr.Close();
            AssetDatabase.game.AddAsset<PrefabAsset>(path, guid);
            Logger.Info("Prefab added successfully");

            foreach (PrefabAsset prefabAsset in AssetDatabase.game.GetAssets<PrefabAsset>())
            {
                try
                {
                    Logger.Info("I Name: " + prefabAsset.name);
                    Logger.Info("I Path: " + prefabAsset.path);
                    Logger.Info("I SubPath: " + prefabAsset.subPath);
                    Logger.Info("AssetName: " + path.assetName);
                    PrefabBase prefabBase = prefabAsset.Load() as PrefabBase;
                    Logger.Info("Loaded Prefab");
                    prefabSystem.AddPrefab(prefabBase, null, null, null);
                    Logger.Info("Added to Prefab System");
                }
                catch (DirectoryNotFoundException e)
                {
                    Logger.Error("Message: " + e.Message);
                    Logger.Error("Stack: " + e.StackTrace);
                    if (e.InnerException != null)
                    {
                        Logger.Error("Inner: " + e.InnerException.Message);
                        Logger.Error("InnerStack: " + e.InnerException.StackTrace);
                        Logger.Error(e.ToJSONString());
                    }
                }
            }
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


        private static void DumpAssets(string name, string path)
        {
            var x = AssetDatabase.game.AllAssets().GetEnumerator();
            string s = "";
            while (x.MoveNext())
            {
                s += x.Current?.name + " " + x.Current?.database.name + "\n";
            }

            // Create file
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.Write(s);
            }
        }

        public static void SyncAssets()
        {
            missingCids.Clear();
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
                var assemblyName = modInfo.name.Split(',')[0];
                var modDir = Path.GetDirectoryName(modInfo.asset.path);
                var mod = new DirectoryInfo(modDir);
                if (modDir == null)
                    continue;
                if (assemblyName == "CustomAssetPack")
                {
                    Logger.Warn($"Mod {modInfo.asset.name} is using default name");

                    NotificationSystem.Push(Guid.NewGuid().ToString(), title:$"Mod {mod.Name} is using default name", text:$"Please contact the developer of this mod to change the assembly name to something unique");
                }
                if (modInfo.asset.isEnabled)
                {
                    if (modDir.Contains($"{EnvPath.kLocalModsPath}/Mods") && !Setting.instance.EnableLocalAssetPacks)
                    {
                        Log($"Skipping local mod {assemblyName} (" + modInfo.name + ")");
                        continue;
                    }
                    if (!Setting.instance.EnableSubscribedAssetPacks)
                        continue;
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        Log($"Copying assets from {mod.Name} (" + modInfo.name + ")");

                        expectedAssets.AddRange(CollectAssetsRecursively(assetDir.FullName, modInfo.name));
                    }
                }
                else
                {
                    Log($"Skipping disabled mod {modInfo.name} (" + modInfo.name + ")");
                }
            }

            return expectedAssets;
        }

        private static List<FileInfo> CollectAssetsRecursively(string directory, string modName)
        {
            List<FileInfo> files = new();
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
                Logger.Error($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Extension == ".Prefab")
                {
                    if (!File.Exists(file.FullName + ".cid"))
                    {
                        Logger.Warn("Prefab has no CID: " + file.FullName);
                        if (missingCids.ContainsKey(modName))
                        {
                            missingCids[modName].Add(file.Name);
                        }
                        else
                        {
                            missingCids.Add(modName, new List<string> {file.Name});
                        }
                        continue;
                    }
                }
                files.Add(file);
            }
            foreach (DirectoryInfo subDir in dirs)
            {
                files.AddRange(CollectAssetsRecursively(subDir.FullName, modName));
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

            if (Setting.instance.DeleteUnusedFiles)
            {
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
            float delay = 30f;
            NotificationSystem.Pop("asset-packs-manager");
            if (Setting.instance.AutoHideNotifications)
                delay = 10000f;
            NotificationSystem.Pop("asset-packs-manager", delay, title:"Deleted imported assets", text:"Imported assets have been deleted. Click here to sync", onClicked:() => SyncAssets());
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

            float delay = 30f;
            NotificationSystem.Pop("asset-packs-manager");
            if (Setting.instance.AutoHideNotifications)
                delay = 10000f;
            NotificationSystem.Pop("asset-packs-manager", delay, title:$"Asset Importer ({createdFiles} created, {updatedFiles} updated, {deletedFiles} deleted)", text: $"Custom Assets have been changed. Restart the game to apply changes");
            //GameManager.instance.modManager.RequireRestart();
            //Log("Mod Manager init: " + GameManager.instance.modManager.isInitialized + " Restart: " + GameManager.instance.modManager.restartRequired, true);
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

        public static void DeleteModsCache()
        {
            var foldersToDelete = new[] {
                Path.Combine(EnvPath.kUserDataPath, ".cache", "Mods", "mods_subscribed"),
                Path.Combine(EnvPath.kUserDataPath, ".cache", "Mods", "mods_unmanaged"),
                Path.Combine(EnvPath.kUserDataPath, ".cache", "Mods", "mods_workInProgress")
            };

            Logger.Info("Deleting Mods Cache");
            foreach (var folder in foldersToDelete)
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                    Logger.Info($"Deleted folder: {folder}");
                }
            }

            Logger.Info("Closing Game...");
            Application.Quit(0);
        }
    }
}