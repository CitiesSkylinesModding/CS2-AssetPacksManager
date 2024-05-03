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
        private static readonly string logFileName = $"{nameof(AssetPacksManager)}.{nameof(Mod)}";
        public static readonly ILog Logger = LogManager.GetLogger(logFileName)
            .SetShowsErrorsInUI(false);

        [CanBeNull] public string ModPath { get; set; }

        private static PrefabSystem prefabSystem;
        public static bool DisableLogging = false;

        //private static string assetPath = $"{EnvPath.kUserDataPath}/CustomAssets";

        // Each mod has a dict entry that contains the missing cid prefabs
        private static Dictionary<string, List<string>> missingCids = new();
        
        public void OnLoad(UpdateSystem updateSystem)
        {

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                if (!DisableLogging)
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

            /*if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }
            Log("Added custom assets COUI location");
            */

            // TODO: Legacy deletion of custom assets
            try
            {
                string customAssetsDir = $"{EnvPath.kUserDataPath}/CustomAssets";
                if (Directory.Exists(customAssetsDir))
                {
                    Directory.Move(customAssetsDir,  "C:/Users/" + Environment.UserName + "/Desktop/CustomAssets_backup");
                    NotificationSystem.Push("APM-legacy", "Custom Assets folder moved, restart game", "The Custom Assets is no longer being used and has been moved to Desktop. You may need to restart the game");
                }
            }
            catch (Exception x)
            {

            }

            LoadModAssetsInForeground();
            AddHostLocations();
            if (Setting.instance.ShowWarningForLocalAssets)
            {
                int localAssets = FindLocalAssets($"{EnvPath.kLocalModsPath}");
                if (localAssets > 0)
                {
                    NotificationSystem.Pop("APM-local", 30f, "Local Assets Found", $"Found {localAssets} local assets in the user folder. These are loaded automatically.");
                }
            }
        }

        private static void AddHostLocations()
        {
            var hostLocationBefore = DateTime.Now;
            foreach (var dir in hostLocationDirs)
            {
                UIManager.defaultUISystem.AddHostLocation("customassets", dir);
            }
            var hostLocationAfter = DateTime.Now - hostLocationBefore;
            Logger.Info("Host Location Time: " + hostLocationAfter.TotalMilliseconds + "ms");
        }

        private static int FindLocalAssets(string currentDir)
        {
            int localAssets = 0;
            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                if (dir.Contains(".cache"))
                    continue;
                else
                    localAssets += FindLocalAssets(dir);
            }
            foreach (var file in Directory.GetFiles(currentDir))
            {
                if (file.EndsWith(".Prefab"))
                {
                    localAssets++;
                }
            }
            return localAssets;
        }

        private static DateTime assetStartTime;

        private static void LoadModAssetsInForeground()
        {
            assetStartTime = DateTime.Now;
            LoadModAssets();
            var totalAssetLoadTime = DateTime.Now - assetStartTime;
            Logger.Info("Total Asset Load Time: " + totalAssetLoadTime.TotalMilliseconds + "ms");
            SendAssetNotification();
            foreach(string key in missingCids.Keys)
            {
                NotificationSystem.Pop(key, 300f, title:$"Missing CID for {missingCids[key].Count} prefabs", text: $"{key.Split(',')[0]} is missing CID for {missingCids[key].Count} prefabs. Delete mods cache or contact Asset Creator");
            }
        }

        public static void OpenLogFile()
        {
            System.Diagnostics.Process.Start($"{EnvPath.kUserDataPath}/Logs/{logFileName}.log");
        }

        private static int loaded;
        private static int notLoaded;
        private static void LoadAssets(Dictionary<string, List<FileInfo>> modAssets)
        {
            loaded = 0;
            notLoaded = 0;
            var assetDatabaseStartTime = DateTime.Now;
            foreach(var mod in modAssets)
            {
                foreach (var file in mod.Value)
                {
                    try
                    {
                        if (file == null)
                        {
                            Logger.Error($"File is null for mod {mod.Key}. This should never happen.");
                        }
                        if (!DisableLogging)
                            Logger.Info("Loading File: " + file.FullName);

                        var absolutePath = file.FullName;
                        //var absolutePath = "C:/Users/Konsi/AppData/LocalLow/Colossal Order/Cities Skylines II/.cache/Mods/mods_subscribed/79063_6/assets/DansPack/Rural Welfare Office/Rural Welfare Office.Prefab";
                        //var relativePath = ".cache/Mods/mods_subscribed/79063_6/assets/DansPack/Rural Welfare Office/";

                        // Replace backslashes with forward slashes
                        absolutePath = absolutePath.Replace('\\', '/');
                        // get relative path from absolute path
                        var relativePath = absolutePath.Replace(EnvPath.kUserDataPath + "/", "");
                        // Remove content after last / from relative path
                        relativePath = relativePath.Substring(0, relativePath.LastIndexOf('/'));

                        //var fileName = "SmallFireHouse01";
                        var fileName = Path.GetFileNameWithoutExtension(file.FullName);

                        var path = AssetDataPath.Create(relativePath, fileName);

                        var cidFilename = EnvPath.kUserDataPath + "\\" + relativePath + "\\" + fileName + ".Prefab.cid";
                        using StreamReader sr = new StreamReader(cidFilename);
                        var guid = sr.ReadToEnd();
                        sr.Close();
                        AssetDatabase.user.AddAsset<PrefabAsset>(path, guid);
                        Log("Prefab added to database successfully");
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Asset {file} could not be loaded: {e.Message}");
                    }
                }
            }
            var assetDatabaseEndTime = DateTime.Now - assetDatabaseStartTime;
            Logger.Info("Asset Database Time: " + assetDatabaseEndTime.TotalMilliseconds + "ms");

            var prefabSystemStartTime = DateTime.Now;
            foreach (PrefabAsset prefabAsset in AssetDatabase.user.GetAssets<PrefabAsset>())
            {
                try
                {
                    Log("Asset Name: " + prefabAsset.name);
                    Log("Asset Path: " + prefabAsset.path);
                    // Logger.Info("I SubPath: " + prefabAsset.subPath);
                    PrefabBase prefabBase = prefabAsset.Load() as PrefabBase;
                    Log("Loaded Prefab");
                    prefabSystem.AddPrefab(prefabBase, null, null, null);
                    Log($"Added {prefabAsset.name} to Prefab System");
                    loaded++;
                }
                catch (Exception e)
                {
                    notLoaded++;
                    Logger.Info($"Please see AssetPacksManager Log for details. Asset {prefabAsset.name} could not be added to Database: {e.Message}Path: {prefabAsset.path}\nUnique Name: {prefabAsset.uniqueName}\nCID: {prefabAsset.guid}\nSubPath: {prefabAsset.subPath}");
                    /*if (e.InnerException != null)
                    {
                        Logger.Error("Inner: " + e.InnerException.Message);
                        Logger.Error("InnerStack: " + e.InnerException.StackTrace);
                        Logger.Error(e.ToJSONString());
                    }*/
                }
            }
            var prefabSystemEndTime = DateTime.Now - prefabSystemStartTime;
            Logger.Info("Prefab System Time: " + prefabSystemEndTime.TotalMilliseconds + "ms");
        }

        private static List<FileInfo> GetPrefabsFromDirectoryRecursively(string directory, string modName)
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
                    files.Add(file);
                }
            }
            foreach (DirectoryInfo subDir in dirs)
            {
                files.AddRange(GetPrefabsFromDirectoryRecursively(subDir.FullName, modName));
            }
            return files;
        }
        private static List<string> hostLocationDirs = new();
        private static void LoadModAssets()
        {
            Dictionary<string, List<FileInfo>> modAssets = new();
            var assetFinderStartTime = DateTime.Now;
            foreach (var modInfo in GameManager.instance.modManager)
            {
                var assemblyName = modInfo.name.Split(',')[0];
                Log($"Checking mod {assemblyName}");
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
                    var modTime = DateTime.Now;
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        hostLocationDirs.Add(assetDir.FullName);
                        var localModsPath = EnvPath.kLocalModsPath.Replace("/", "\\");
                        if (modDir.Contains(localModsPath) && !Setting.instance.EnableLocalAssetPacks)
                        {
                            Log($"Skipping local mod {assemblyName} (" + modInfo.name + ")");
                            continue;
                        }
                        if (!Setting.instance.EnableSubscribedAssetPacks)
                            continue;

                        if (!modAssets.ContainsKey(mod.Name))
                            modAssets.Add(mod.Name, new List<FileInfo>());

                        Log($"Copying assets from {mod.Name} (" + modInfo.name + ")");
                        var assetsFromMod = GetPrefabsFromDirectoryRecursively(assetDir.FullName, mod.Name);
                        if (!DisableLogging)
                            Logger.Info($"Found {assetsFromMod.Count} assets from mod {modInfo.name}");
                        modAssets[mod.Name].AddRange(assetsFromMod);
                    }
                    var modTimeEnd = DateTime.Now - modTime;
                    Logger.Info("Mod Time: " + modTimeEnd.TotalMilliseconds + "ms");
                }
                else
                {
                    Log($"Skipping disabled mod {modInfo.name} (" + modInfo.name + ")");
                }
            }

            var assetFinderEndTime = DateTime.Now - assetFinderStartTime;
            Logger.Info("Asset Finder Time: " + assetFinderEndTime.TotalMilliseconds + "ms");
            Log("All mod prefabs have been collected. Adding to database now.");
            LoadAssets(modAssets);
        }

        private static void Log(string message, bool alwaysLog = false)
        {
            if (Setting.instance.EnableVerboseLogging || alwaysLog)
                Logger.Info(message);
        }

        private static async void SendAssetNotification()
        {
            // Delay by 100 ms, because we have to wait for the mod manager to initialize
            while (!GameManager.instance.modManager.isInitialized)
            {
                await Task.Delay(100);
            }

            float delay = 30f;
            NotificationSystem.Pop("asset-packs-manager");
            if (!Setting.instance.AutoHideNotifications)
                delay = 10000f;
            var text = "All custom assets have been loaded successfully. Loaded: " + loaded;
            if (notLoaded > 0)
                text = "Some assets could not be loaded. Loaded: " + loaded + ". Not Loaded: " + notLoaded;
            NotificationSystem.Pop("asset-packs-manager", delay, title:$"Asset Packs Manager", text: text);
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