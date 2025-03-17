using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Common;
using Colossal.PSI.Environment;
using Game;
using Colossal.Serialization.Entities;
using Colossal.UI;
using Game.Modding;
using Game.Prefabs;
using Game.PSI;
using Game.SceneFlow;
using Game.UI.Localization;
using Game.UI.Menu;
using UnityEngine;
using Hash128 = Colossal.Hash128;
using StreamReader = System.IO.StreamReader;

namespace AssetPacksManager
{

    public partial class AssetPackLoaderSystem : GameSystemBase
    {
        private static PrefabSystem _prefabSystem;
        private static NotificationUISystem _notificationUISystem;

        // Each mod has a dict entry that contains the missing cid prefabs
        private static readonly string[] SupportedThumbnailExtensions = { ".png", ".svg", ".jpg" };
        private static readonly string ThumbnailDir = EnvPath.kUserDataPath + "/ModsData/AssetPacksManager/thumbnails";
        private static ApmLogger Logger;
        public static AssetPackLoaderSystem Instance;
        private static MonoComponent _monoComponent;
        private readonly GameObject _monoObject = new();
        public static bool AssetsLoaded;
        private static string LoadedAssetPacksText { get; set; } = "";
        private static string LocalAssetsText { get; set; } = "";
        private static NotificationUISystem.NotificationInfo _adaptiveAssetsNotification;
        private static readonly List<AssetPack> AssetPacks = new();
        private static readonly List<string> LocalAssets = new();
        private static int _eaiAssets;
        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            Enabled = false;
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _notificationUISystem = World.GetOrCreateSystemManaged<NotificationUISystem>();
            _monoComponent = _monoObject.AddComponent<MonoComponent>();
            Logger = ApmLogger.Instance;

            // VERY old CustomAssets folder migration, shouldn't be needed anymore, but just in case
            CheckForMigration();

            _notificationUISystem.AddOrUpdateNotification(
                $"APM-NoLoad",
                title: "Asset Packs Manager not loaded",
                text: "Click here to load Asset Packs Manager.",
                progressState: ProgressState.Indeterminate,
                progress: 0,
                thumbnail: "coui://apm/game_crash_warning.svg",
                onClicked: Initialize
            );

            if (!Directory.Exists(ThumbnailDir))
                Directory.CreateDirectory(ThumbnailDir);
            UIManager.defaultUISystem.AddHostLocation("customassets", ThumbnailDir, false);

            if (Setting.Instance.ShowWarningForLocalAssets)
            {
                FindLocalAssets($"{EnvPath.kUserDataPath}");
                if (LocalAssets.Count != 0)
                {
                    NotificationSystem.Pop("APM-local", getHideDelay(), "Local Assets Found",
                        $"Found {LocalAssets.Count} local assets in the user folder. These are loaded automatically.");
                }
            }

            GameManager.instance.RegisterUpdater(Initialize);
        }

        private static float getHideDelay()
        {
            if (Setting.Instance.AutoHideNotifications)
                return 30f;
            return 100000f;
        }

        /// <summary>
        /// First time initialization of the AssetPackLoaderSystem, loads asset packs if enabled
        /// </summary>
        private void Initialize()
        {
            _notificationUISystem.RemoveNotification("APM-NoLoad");
            Logger.Info("Initializing Asset Pack Loader System");
            if (Setting.Instance.EnableAssetPackLoadingOnStartup)
            {
                Logger.Info("Loading Asset Packs on Startup");
                LoadAssetPacks();
            }
            else
            {
                Logger.Info("Asset Pack Loading on Startup is disabled");
            }
        }

        /// <summary>
        /// If player still has the old CustomAssets folder, move it to the desktop and notify the player.
        /// This is to prevent the game from loading these assets by itself.
        /// </summary>
        private void CheckForMigration()
        {
            try
            {
                string customAssetsDir = $"{EnvPath.kUserDataPath}/CustomAssets";
                if (Directory.Exists(customAssetsDir))
                {
                    var migrationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "CustomAssets_backup");
                    Directory.Move(customAssetsDir, migrationPath);
                    NotificationSystem.Push("APM-legacy", "Custom Assets folder migrated, restart game",
                        "The Custom Assets is no longer being used and has been moved to Desktop. Please restart the game");
                    Logger.Error("Closing game due to APM-legacy migration");
                    CloseGame();
                }
            }
            catch (Exception x)
            {
                var migrationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "CustomAssets_backup");
                NotificationSystem.Push("APM-legacy", "Error migrating Custom Assets folder",
                    $"Please delete {migrationPath} manually.");
                Logger.Error("Error moving Custom Assets folder: " + x.Message);
            }
        }

        public static LocalizedString GetLoadedAssetPacksText()
        {
            return LocalizedString.IdWithFallback("APM-LoadedAssetPacks", LoadedAssetPacksText);
        }
        
        public static LocalizedString GetLocalAssetsText()
        {
            return LocalizedString.IdWithFallback("APM-LocalAssets", LocalAssetsText);
        }

        public void LoadAssetPacks()
        {
            if (AssetsLoaded)
            {
                Logger.Info("Assets are already loaded, skipping...");
                return;
            }
            _monoComponent.StartCoroutine(CollectAssets());
            AssetsLoaded = true;
        }

        public static void DeleteModsWithMissingCid()
        {
            int successfulDeletions = 0;
            foreach (var pack in AssetPacks)
            {
                if (pack.MissingCids.Count == 0)
                    continue;
                foreach (string key in pack.MissingCids)
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
            }


            NotificationSystem.Push("APM-delete", "Deleted Mods",
                $"Deleted {successfulDeletions} mods with missing CID. Click to close game.",
                onClicked: CloseGame);
        }

        private static void FindLocalAssets(string currentDir)
        {
            var currentDirectory = new DirectoryInfo(currentDir);
            if (currentDirectory.Name.StartsWith("."))
                return;
            if (!Directory.Exists(currentDir))
            {
                return;
            }

            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                FindLocalAssets(dir);
            }

            foreach (var file in Directory.GetFiles(currentDir))
            {
                if (file.EndsWith(".Prefab"))
                {
                    string localPath = file.Replace(EnvPath.kUserDataPath, "").Replace("\\", "/");
                    if (localPath.Contains("ModsData/ExtraAssetsImporter"))
                        _eaiAssets++;
                    else
                        LocalAssets.Add(localPath);
                }
            }
        }

        public static void OpenLogFile()
        {
            Logger.OpenLogFile();
        }

        private static IEnumerator CollectAssets()
        {
            var notificationInfo = _notificationUISystem.AddOrUpdateNotification(
                $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(CollectAssets)}",
                title: "Collecting Asset Packs",
                progressState: ProgressState.Indeterminate,
                thumbnail: "coui://apm/notify_icon.png",
                progress: 0);
            int currentIndex = 0;
            int packsFound = 0;
            int assetCount = 0;

            var assetFinderStartTime = DateTime.Now;

            foreach (var modInfo in GameManager.instance.modManager)
            {
                var assemblyName = modInfo.name.Split(',')[0];
                Logger.Debug($"Checking mod {assemblyName}");
                var modDir = Path.GetDirectoryName(modInfo.asset.path);
                var modId = $"{modInfo.asset.subPath.Split('/')[1].Split('_')[0]}";
                var mod = new DirectoryInfo(modDir);
                if (modDir == null)
                    continue;
                if (assemblyName == "CustomAssetPack")
                {
                    Logger.Warn($"Mod {modInfo.asset.name} is using default name");

                    NotificationSystem.Push(Guid.NewGuid().ToString(), title: $"Mod {mod.Name} is using default name",
                        text:
                        $"Please contact the developer of this mod to change the assembly name to something unique");
                }

                if (modInfo.asset.isEnabled)
                {
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        if (!Setting.Instance.DisableLoadingNotification)
                        {
                            notificationInfo = _notificationUISystem.AddOrUpdateNotification(
                                $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(CollectAssets)}",
                                title: "Collecting Asset Packs",
                                text: $"Collecting: {modInfo.asset.name}",
                                progressState: ProgressState.Progressing,
                                thumbnail: "coui://apm/notify_icon.png",
                                progress: (int)(currentIndex / (float)GameManager.instance.modManager.Count() * 100));
                        }

                        AssetPack currentPack = new AssetPack()
                        {
                            Path = modDir,
                            AssetPath = assetDir.FullName,
                            Name = assemblyName,
                        };

                        if (int.TryParse(modId, out int id))
                        {
                            currentPack.ID = id;
                        }

                        var localModsPath = Path.Combine(EnvPath.kUserDataPath, "Mods").Replace("/", "\\");
                        if (modDir.Contains(localModsPath))
                        {
                            currentPack.Type = AssetPackType.Local;
                        }
                        Logger.Debug($"Collecting assets from {currentPack.Name} (" + currentPack.ID + ")");
                        assetCount += currentPack.AddAssetFiles(GetPrefabsFromDirectoryRecursively(currentPack.AssetPath, currentPack));
                        AssetPacks.Add(currentPack);
                        Logger.Debug($"Found {currentPack.AssetFiles.Count} assets from mod {currentPack.Name}");
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
            
            SkyveInterface.CheckPlaysetStatus(AssetPacks);
            foreach(AssetPack pack in AssetPacks)
            {
                if (pack.Stability == PackageStability.Broken)
                {
                    ShowAssetPackWarning(pack);
                }
            }

            WriteLoadedPacksText();            
            Setting.LoadedAssetPacksTextVersion++;
            WriteLocalAssetsText();
            Setting.LocalAssetsTextVersion++;
            
            _notificationUISystem.RemoveNotification(
                identifier: notificationInfo.id,
                delay: 30f,
                text: $"Asset Pack collection complete. Found {packsFound} Asset Packs",
                progressState: ProgressState.Complete,
                progress: 100
            );

            var assetFinderEndTime = DateTime.Now - assetFinderStartTime;
            Logger.Info("Asset Collection Time: " + assetFinderEndTime.TotalMilliseconds + "ms");

            foreach (var pack in AssetPacks)
            {
                if (pack.MissingCids.Count == 0)
                    continue;
                NotificationSystem.Pop($"MissingCID_{pack.ID}", getHideDelay(), title: $"Missing CID for {pack.MissingCids.Count} prefabs",
                    text: $"{pack.Name} has {pack.MissingCids.Count} missing CIDs. Click to delete cache",
                    onClicked: DeleteModsWithMissingCid);
            }
        }

        private static string ConvertCamelCaseToSpaces(string input)
        {
            input = input.Replace("_", "");
            return Regex.Replace(input, "(?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z])", " $1");
        }

        private static void ShowAssetPackWarning(AssetPack pack)
        {
            Logger.Warn($"Broken asset pack detected: {pack.Name} ({pack.ID}). No support will be provided unless this pack is removed from the playset.");
            _notificationUISystem.AddOrUpdateNotification(
                $"Broken_{pack.ID}",
                title: "Broken Asset Pack",
                text: $"{pack.Name} is {pack.Stability}, no support will be provided",
                progressState: ProgressState.Warning,
                onClicked:() => _notificationUISystem.RemoveNotification($"Broken_{pack.ID}"));

        }

        private static void WriteLoadedPacksText()
        {
            List<AssetPack> sortedPacks = new(AssetPacks);

            sortedPacks.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            foreach (var pack in sortedPacks)
            {
                string id = "";
                if (pack.Type == AssetPackType.Local)
                {
                    id = "Local";
                    if (pack.ID != 0)
                        id += " " + pack.ID;
                }
                else
                {
                    if (pack.ID != 0)
                        id = pack.ID.ToString();
                    else
                        id = "Local";
                }
                LoadedAssetPacksText += $"[{pack.Stability}] {ConvertCamelCaseToSpaces(pack.Name)} ({id}) ({pack.AssetFiles.Count} Assets)\n";
            }
            Logger.Info($"Loaded Asset Packs: \n{LoadedAssetPacksText}");
        }

        private static void WriteLocalAssetsText()
        {
            LocalAssets.Sort();
            LocalAssetsText = "";
            if (_eaiAssets > 0)
                LocalAssetsText += $"{_eaiAssets} Assets found in Extra Asset Importer folder, these are handled by EAI\n";
            foreach (var pack in LocalAssets)
            {
                LocalAssetsText += $"{pack}\n";
            }
            Logger.Info($"Local Assets Text: \n{LocalAssetsText}");
        }

        /// <summary>
        /// Checks if the asset has a CID file. If not, it will try to restore it from the backup
        /// Creates a backup file if it doesn't exist.
        ///
        /// This is only needed because PDX Mods deleted CID files when disabling a mod while ingame.
        /// </summary>
        /// <param name="file">Asset file to be checked</param>
        /// <param name="pack">Current pack</param>
        /// <returns></returns>
        private static bool CheckAsset(FileInfo file, AssetPack pack)
        {
            //AnalyzeAsset(file, modName);
            if (!File.Exists(file.FullName + ".cid"))
            {
                if (File.Exists(file.FullName + ".cid.backup"))
                {
                    File.Copy(file.FullName + ".cid.backup", file.FullName + ".cid");
                    Logger.Info($"Restored CID for {file.FullName}");
                    return true;
                }

                Logger.Warn($"Asset has no CID: {file.FullName}. No CID Backup was found");
                pack.MissingCids.Add(file.Name);
                return false;
            }

            // Back up CID
            if (!File.Exists(file.FullName + ".cid.backup"))
                File.Copy(file.FullName + ".cid", file.FullName + ".cid.backup", true);
            return true;
        }

        private static readonly List<string> AdditionalCidChecks = [".Geometry", ".Surface", ".Texture"];
        private static List<FileInfo> GetPrefabsFromDirectoryRecursively(string directory, AssetPack currentPack)
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
                    if (CheckAsset(file, currentPack))
                        files.Add(file);
                }
                else if (AdditionalCidChecks.Contains(file.Extension))
                {
                    CheckAsset(file, currentPack);
                }

                if (SupportedThumbnailExtensions.Contains(file.Extension))
                {
                    CopyThumbnail(file, currentPack.Name);
                }
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                files.AddRange(GetPrefabsFromDirectoryRecursively(subDir.FullName, currentPack));
            }

            return files;
        }

        private static void CopyThumbnail(FileInfo file, string modName)
        {
            try
            {
                var fullPath = file.FullName.Replace('\\', '/');
                // Get all after /assets/ to get the relative path
                var relativePath = fullPath.Substring(fullPath.IndexOf("/assets/", StringComparison.Ordinal) + 8);
                FileInfo target = new FileInfo(Path.Combine(ThumbnailDir, relativePath));
                if (target.Directory != null && !target.Directory.Exists)
                    target.Directory.Create();
                File.Copy(file.FullName, target.FullName, true);
            }
            catch (Exception e)
            {
                Logger.Error($"Error copying thumbnail for {file.Name}: {e.Message}. Details: {file.FullName} should have been copied to mod name {modName}");
            }
        }

        public static void CloseGame()
        {
            Logger.Info("Closing Game...");
            Application.Quit(0);
        }

        public static void DeleteCachedAssetPacks()
        {
            var foldersToDelete = new[]
            {
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

            var modsSubscribed = new DirectoryInfo(Path.Combine(EnvPath.kUserDataPath, ".cache", "Mods", "mods_subscribed"));
            foreach (DirectoryInfo di in modsSubscribed.GetDirectories())
            {
                Logger.Debug("Looking for assets folder in " + di.FullName);
                foreach (DirectoryInfo subfolder in di.GetDirectories())
                {
                    if (subfolder.Name == "assets")
                    {
                        Logger.Debug($"Found assets folder in {di.FullName}");
                        Directory.Delete(di.FullName, true);
                        Logger.Info($"Deleted folder: {di.FullName}");
                        break;
                        /*if (FindLocalAssets(di.FullName) > 0)
                        {
                            Directory.Delete(di.FullName, true);
                            Logger.Info($"Deleted folder: {di.FullName}");
                        }*/
                    }
                }
            }
        }

        protected override void OnUpdate()
        {

        }
    }
}