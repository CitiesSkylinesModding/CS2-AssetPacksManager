using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Common;
using Colossal.PSI.Environment;
using Game;
using Colossal.Serialization.Entities;
using Colossal.UI;
using Game.Prefabs;
using Game.PSI;
using Game.SceneFlow;
using Game.UI.Menu;
using Unity.Entities;
using UnityEngine;
using StreamReader = System.IO.StreamReader;

namespace AssetPacksManager;

public partial class AssetPackLoaderSystem : GameSystemBase
{
    private static PrefabSystem _prefabSystem;
    private static NotificationUISystem _notificationUISystem;

    // Each mod has a dict entry that contains the missing cid prefabs
    private static readonly Dictionary<string, List<string>> MissingCids = new();
    private static readonly string[] SupportedThumbnailExtensions = { ".png", ".svg" };
    private static readonly string ThumbnailDir = EnvPath.kUserDataPath + "/ModsData/AssetPacksManager/thumbnails";
    private static KLogger Logger;
    public static AssetPackLoaderSystem Instance;
    private static MonoComponent _monoComponent;
    private readonly GameObject _monoObject = new();
    private static bool _assetsLoaded = false;
    private static DateTime _assetLoadStartTime;
    protected override void OnCreate()
    {
        base.OnCreate();
        Instance = this;
        Enabled = false;
        _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        _notificationUISystem = World.GetOrCreateSystemManaged <NotificationUISystem>();
        _monoComponent = _monoObject.AddComponent<MonoComponent>();
        Logger = KLogger.Instance;

        var migrationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CustomAssets_backup");
        // TODO: Legacy deletion of custom assets
        try
        {
            string customAssetsDir = $"{EnvPath.kUserDataPath}/CustomAssets";
            if (Directory.Exists(customAssetsDir))
            {
                Directory.Move(customAssetsDir, migrationPath);
                NotificationSystem.Push("APM-legacy", "Custom Assets folder migrated, restart game", "The Custom Assets is no longer being used and has been moved to Desktop. Please restart the game");
            }
        }
        catch (Exception x)
        {
            NotificationSystem.Push("APM-legacy", "Error migrating Custom Assets folder", $"Please delete {migrationPath} manually.");
            Logger.Error("Error moving Custom Assets folder: " + x.Message);
        }

        if (!Directory.Exists(ThumbnailDir))
            Directory.CreateDirectory(ThumbnailDir);
        UIManager.defaultUISystem.AddHostLocation("customassets", ThumbnailDir, false);

        if (Setting.instance.ShowWarningForLocalAssets)
        {
            int localAssets = FindLocalAssets($"{EnvPath.kLocalModsPath}");
            if (localAssets > 0)
            {
                NotificationSystem.Pop("APM-local", 30f, "Local Assets Found", $"Found {localAssets} local assets in the user folder. These are loaded automatically.");
            }
        }
    }


    protected override void OnUpdate()
    {

    }

    protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
    {
        base.OnGameLoadingComplete(purpose, mode);
        if (mode == GameMode.MainMenu)
        {
            if (!_assetsLoaded)
            {
                _monoComponent.StartCoroutine(CollectAssets());
                _assetsLoaded = true;
            }
        }
    }

    public static void DeleteModsWithMissingCid()
    {
        int successfulDeletions = 0;
        foreach(string key in MissingCids.Keys)
        {
            try
            {
                string path = $"{EnvPath.kUserDataPath}/.cache/Mods/mods_subscribed/{key}";
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    Logger.Info($"Deleted mod cache for {key}");
                    successfulDeletions++;
                }
            }
            catch (Exception x)
            {
                Logger.Error($"Error deleting mod cache for mod {{key}}: {x.Message}");
            }
        }
        NotificationSystem.Push("APM-delete", "Deleted Mods", $"Deleted {successfulDeletions}/{MissingCids.Count} mods with missing CID. Click to close game.", onClicked: CloseGame);
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

    public static void OpenLogFile()
    {
        Logger.OpenLogFile();
    }
    private static IEnumerator CollectAssets()
    {
        if (!Setting.instance.EnableLocalAssetPacks && !Setting.instance.EnableSubscribedAssetPacks)
        {
            NotificationSystem.Pop("APM-status", 30f, "Asset Packs Disabled",
                "Both local and subscribed asset packs are disabled. No assets will be loaded.");
            yield break;
        }
        _assetLoadStartTime = DateTime.Now;
        var notificationInfo = _notificationUISystem.AddOrUpdateNotification(
            $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(CollectAssets)}",
            title: "Step 1/3: Collecting Asset Packs",
            progressState: ProgressState.Indeterminate,
            thumbnail: "coui://apm/notify_icon.png",
            progress: 0);
        int currentIndex = 0;
        int packsFound = 0;

        Dictionary<string, List<FileInfo>> modAssets = new();
        var assetFinderStartTime = DateTime.Now;

        foreach (var modInfo in GameManager.instance.modManager)
        {
            var assemblyName = modInfo.name.Split(',')[0];
            Logger.Debug($"Checking mod {assemblyName}");
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
                //var modTime = DateTime.Now;
                var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                if (assetDir.Exists)
                {
                    notificationInfo.progressState = ProgressState.Progressing;
                    notificationInfo.progress = (int)(currentIndex / (float)GameManager.instance.modManager.Count() * 100);
                    notificationInfo.text = $"Collecting: {modInfo.asset.name}";
                    var localModsPath = EnvPath.kLocalModsPath.Replace("/", "\\");
                    if (modDir.Contains(localModsPath) && !Setting.instance.EnableLocalAssetPacks)
                    {
                        Logger.Debug($"Skipping local mod {assemblyName} (" + modInfo.name + ")");
                        continue;
                    }
                    if (!Setting.instance.EnableSubscribedAssetPacks)
                        continue;

                    if (!modAssets.ContainsKey(mod.Name))
                        modAssets.Add(mod.Name, new List<FileInfo>());

                    Logger.Debug($"Copying assets from {mod.Name} (" + modInfo.name + ")");
                    var assetsFromMod = GetPrefabsFromDirectoryRecursively(assetDir.FullName, mod.Name);
                    Logger.Debug($"Found {assetsFromMod.Count} assets from mod {modInfo.name}");
                    modAssets[mod.Name].AddRange(assetsFromMod);
                    packsFound++;
                }
                //var modTimeEnd = DateTime.Now - modTime;
                //Logger.Info("Mod Time: " + modTimeEnd.TotalMilliseconds + "ms");
            }
            else
            {
                Logger.Debug($"Skipping disabled mod {modInfo.name} (" + modInfo.name + ")");
            }

            currentIndex++;
            yield return null;
        }

        _notificationUISystem.RemoveNotification(
            identifier: notificationInfo.id,
            delay: 1f,
            text: $"Asset Pack collection complete. Found {packsFound} Asset Packs",
            progressState: ProgressState.Complete,
            progress: 100
            );

        var assetFinderEndTime = DateTime.Now - assetFinderStartTime;
        Logger.Info("Asset Collection Time: " + assetFinderEndTime.TotalMilliseconds + "ms");
        Logger.Debug("All mod prefabs have been collected. Adding to database now.");
        _monoComponent.StartCoroutine(PrepareAssets(modAssets));

        //SendAssetNotification();
        foreach(string key in MissingCids.Keys)
        {
            NotificationSystem.Pop(key, 300f, title:$"Missing CID for {MissingCids[key].Count} prefabs", text: $"{key.Split(',')[0]} has {MissingCids[key].Count} missing CIDs. Click to delete cache", onClicked: DeleteModsWithMissingCid);
        }
    }


    private static int loaded;
    private static int notLoaded;
    private static IEnumerator PrepareAssets(Dictionary<string, List<FileInfo>> modAssets)
    {
        var notificationInfo = _notificationUISystem.AddOrUpdateNotification(
            $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(PrepareAssets)}",
            title: "Step 2/3: Preparing Assets",
            progressState: ProgressState.Indeterminate,
            thumbnail: "coui://apm/notify_icon.png",
            progress: 0);
        int currentIndex = 0;

        loaded = 0;
        notLoaded = 0;
        var assetDatabaseStartTime = DateTime.Now;
        foreach(var mod in modAssets)
        {
            notificationInfo.progressState = ProgressState.Progressing;
            notificationInfo.progress = (int)(currentIndex / (float)modAssets.Count() * 100);
            notificationInfo.text = $"Preparing Pack: {currentIndex}/{modAssets.Count()}";
            foreach (var file in mod.Value)
            {
                try
                {
                    Logger.Debug("Loading File: " + file.FullName);

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
                    Logger.Debug("Prefab added to database successfully");
                }
                catch (Exception e)
                {
                    Logger.Warn($"Asset {file} could not be loaded: {e.Message}");
                }
            }
            currentIndex++;
            yield return null;
        }
        var assetDatabaseEndTime = DateTime.Now - assetDatabaseStartTime;
        Logger.Info("Asset Database Time: " + assetDatabaseEndTime.TotalMilliseconds + "ms");
        _notificationUISystem.RemoveNotification(
            identifier: notificationInfo.id,
            delay: 1f,
            text: $"Asset Preparing complete.",
            progressState: ProgressState.Complete,
            progress: 100
        );

        _monoComponent.StartCoroutine(LoadAssets());
    }

    private static IEnumerator LoadAssets()
    {
        var notificationInfo = _notificationUISystem.AddOrUpdateNotification(
            $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(LoadAssets)}",
            title: "Step 3/3: Loading Assets",
            progressState: ProgressState.Indeterminate,
            thumbnail: "coui://apm/notify_icon.png",
            progress: 0);


        int currentIndex = 0;
        var prefabSystemStartTime = DateTime.Now;
        var allPrefabs = AssetDatabase.user.GetAssets<PrefabAsset>();
        var prefabAssets = allPrefabs as PrefabAsset[] ?? allPrefabs.ToArray();
        foreach (PrefabAsset prefabAsset in prefabAssets)
        {
            try
            {
                notificationInfo.progressState = ProgressState.Progressing;
                notificationInfo.progress = (int)(currentIndex / (float)prefabAssets.Count() * 100);
                notificationInfo.text = $"Loading: {prefabAsset.name}";
                Logger.Debug("Asset Name: " + prefabAsset.name);
                Logger.Debug("Asset Path: " + prefabAsset.path);
                // Logger.Info("I SubPath: " + prefabAsset.subPath);
                PrefabBase prefabBase = prefabAsset.Load() as PrefabBase;
                Logger.Debug("Loaded Prefab");
                _prefabSystem.AddPrefab(prefabBase, null, null, null);
                Logger.Debug($"Added {prefabAsset.name} to Prefab System");
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
            currentIndex++;
            yield return null;
        }
        var prefabSystemEndTime = DateTime.Now - prefabSystemStartTime;
        Logger.Info("Prefab System Time: " + prefabSystemEndTime.TotalMilliseconds + "ms");
        _notificationUISystem.RemoveNotification(
            identifier: notificationInfo.id,
            delay: 30f,
            text: $"Asset Loading complete. {loaded} assets loaded, {notLoaded} failed to load.",
            progressState: ProgressState.Complete,
            progress: 100
        );
        var totalAssetTime = DateTime.Now - _assetLoadStartTime;
        KLogger.Logger.Critical("Asset Time: " + totalAssetTime);
    }

    /// <summary>
    /// Checks if the prefab has a CID file. If not, it will try to restore it from the backup
    /// Creates a backup file if it doesn't exist.
    ///
    /// This is only needed because PDX Mods deleted CID files when disabling a mod while ingame.
    /// </summary>
    /// <param name="file">Prefab file to be checked</param>
    /// <param name="modName">Current mod directory</param>
    /// <returns></returns>
    private static bool CheckPrefab(FileInfo file, string modName)
    {
        if (!File.Exists(file.FullName + ".cid"))
        {
            if (File.Exists(file.FullName + ".cid.backup"))
            {
                File.Copy(file.FullName + ".cid.backup", file.FullName + ".cid");
                Logger.Info($"Restored CID for {file.FullName}");
                return true;
            }
            Logger.Warn($"Prefab has no CID: {file.FullName}. No CID Backup was found");
            if (MissingCids.ContainsKey(modName))
            {
                MissingCids[modName].Add(file.Name);
            }
            else
            {
                MissingCids.Add(modName, new List<string> {file.Name});
            }
            return false;
        }
        // Back up CID
        if (!File.Exists(file.FullName + ".cid.backup"))
            File.Copy(file.FullName + ".cid", file.FullName + ".cid.backup", true);
        return true;
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
                if (CheckPrefab(file, modName))
                    files.Add(file);
            }

            if (SupportedThumbnailExtensions.Contains(file.Extension))
            {
                CopyThumbnail(file, modName);
            }
        }
        foreach (DirectoryInfo subDir in dirs)
        {
            files.AddRange(GetPrefabsFromDirectoryRecursively(subDir.FullName, modName));
        }
        return files;
    }

    private static void CopyThumbnail(FileInfo file, string modName)
    {
        try
        {
            var fullPath = file.FullName.Replace('\\', '/');
            // get everything of the path after modName
            var relativePath =
                fullPath.Substring(fullPath.IndexOf(modName, StringComparison.Ordinal) + modName.Length + 1);
            // Remove "assets"
            relativePath = relativePath.Substring(relativePath.IndexOf("/", StringComparison.Ordinal) + 1);
            FileInfo target = new FileInfo(Path.Combine(ThumbnailDir, relativePath));
            if (!target.Directory.Exists)
                target.Directory.Create();
            File.Copy(file.FullName, target.FullName, true);
        }
        catch (Exception e)
        {
            Logger.Error($"Error copying thumbnail for {file.Name}: " + e.Message);
        }
    }

    public static void CloseGame()
    {
        Logger.Info("Closing Game...");
        Application.Quit(0);
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
    }

}