using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly Dictionary<string, List<string>> MissingCids = new();
        private static readonly string[] SupportedThumbnailExtensions = { ".png", ".svg" };
        private static readonly string ThumbnailDir = EnvPath.kUserDataPath + "/ModsData/AssetPacksManager/thumbnails";
        private static KLogger Logger;
        public static AssetPackLoaderSystem Instance;
        private static MonoComponent _monoComponent;
        private readonly GameObject _monoObject = new();
        public static bool AssetsLoaded = false;
        private static DateTime _assetLoadStartTime;
        public static string LoadedAssetPacksText { get; set; } = "";
        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            Enabled = false;
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _notificationUISystem = World.GetOrCreateSystemManaged<NotificationUISystem>();
            _monoComponent = _monoObject.AddComponent<MonoComponent>();
            Logger = KLogger.Instance;

            var migrationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "CustomAssets_backup");

            var apmNotLoadedNotification = _notificationUISystem.AddOrUpdateNotification(
                $"APM-NoLoad",
                title: "Asset Packs Manager not loaded",
                text: "Click here to load Asset Packs Manager.",
                progressState: ProgressState.Indeterminate,
                progress: 0,
                thumbnail: "coui://apm/game_crash_warning.svg",
                onClicked: OnMainMenu
            );

            try
            {
                string customAssetsDir = $"{EnvPath.kUserDataPath}/CustomAssets";
                if (Directory.Exists(customAssetsDir))
                {
                    Directory.Move(customAssetsDir, migrationPath);
                    NotificationSystem.Push("APM-legacy", "Custom Assets folder migrated, restart game",
                        "The Custom Assets is no longer being used and has been moved to Desktop. Please restart the game");
                }
            }
            catch (Exception x)
            {
                NotificationSystem.Push("APM-legacy", "Error migrating Custom Assets folder",
                    $"Please delete {migrationPath} manually.");
                Logger.Error("Error moving Custom Assets folder: " + x.Message);
            }

            if (!Directory.Exists(ThumbnailDir))
                Directory.CreateDirectory(ThumbnailDir);
            UIManager.defaultUISystem.AddHostLocation("customassets", ThumbnailDir, false);

            if (Setting.Instance.ShowWarningForLocalAssets)
            {
                int localAssets = FindLocalAssets($"{EnvPath.kUserDataPath}");
                if (localAssets > 0)
                {
                    NotificationSystem.Pop("APM-local", 30f, "Local Assets Found",
                        $"Found {localAssets} local assets in the user folder. These are loaded automatically.");
                }
            }
        }

        public static LocalizedString GetLoadedAssetPacksText()
        {
            return LocalizedString.IdWithFallback("APM-LoadedAssetPacks", LoadedAssetPacksText);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.MainMenu)
            {
                OnMainMenu();
            }
        }

        private void OnMainMenu()
        {
            _notificationUISystem.RemoveNotification("APM-NoLoad");
            Logger.Info("Main Menu entered");
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
            foreach (string key in MissingCids.Keys)
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

            NotificationSystem.Push("APM-delete", "Deleted Mods",
                $"Deleted {successfulDeletions}/{MissingCids.Count} mods with missing CID. Click to close game.",
                onClicked: CloseGame);
        }

        private static int FindLocalAssets(string currentDir)
        {
            int localAssets = 0;
            if (!Directory.Exists(currentDir))
            {
                return 0;
            }

            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                if (dir.StartsWith("."))
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
            if (!Setting.Instance.EnableLocalAssetPacks && !Setting.Instance.EnableSubscribedAssetPacks)
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

                    NotificationSystem.Push(Guid.NewGuid().ToString(), title: $"Mod {mod.Name} is using default name",
                        text:
                        $"Please contact the developer of this mod to change the assembly name to something unique");
                }

                if (modInfo.asset.isEnabled)
                {
                    var assetDir = new DirectoryInfo(Path.Combine(modDir, "assets"));
                    if (assetDir.Exists)
                    {
                        notificationInfo = _notificationUISystem.AddOrUpdateNotification(
                            $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(CollectAssets)}",
                            title: "Step 1/3: Collecting Asset Packs",
                            text: $"Collecting: {modInfo.asset.name}",
                            progressState: ProgressState.Progressing,
                            thumbnail: "coui://apm/notify_icon.png",
                            progress: (int)(currentIndex / (float)GameManager.instance.modManager.Count() * 100));

                        var localModsPath = EnvPath.kLocalModsPath.Replace("/", "\\");
                        if (modDir.Contains(localModsPath) && !Setting.Instance.EnableLocalAssetPacks)
                        {
                            Logger.Debug($"Skipping local mod {assemblyName} (" + modInfo.name + ")");
                            continue;
                        }

                        if (!Setting.Instance.EnableSubscribedAssetPacks)
                            continue;

                        if (!modAssets.ContainsKey(mod.Name))
                            modAssets.Add(mod.Name, new List<FileInfo>());

                        Logger.Debug($"Copying assets from {mod.Name} (" + modInfo.name + ")");
                        var assetsFromMod = GetPrefabsFromDirectoryRecursively(assetDir.FullName, mod.Name);
                        Logger.Debug($"Found {assetsFromMod.Count} assets from mod {modInfo.name}");
                        modAssets[mod.Name].AddRange(assetsFromMod);
                        packsFound++;

                        LoadedPacks.Add(modInfo, assetsFromMod.Count);
                        Setting.LoadedAssetPacksTextVersion++;
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

            WriteLoadedPacks();

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

            foreach (string key in MissingCids.Keys)
            {
                NotificationSystem.Pop(key, 300f, title: $"Missing CID for {MissingCids[key].Count} prefabs",
                    text: $"{key.Split(',')[0]} has {MissingCids[key].Count} missing CIDs. Click to delete cache",
                    onClicked: DeleteModsWithMissingCid);
            }
        }

        private static Dictionary<ModManager.ModInfo, int> LoadedPacks = new();
        private static void WriteLoadedPacks()
        {
            List<ModManager.ModInfo> packs = new();
            foreach(var mod in LoadedPacks)
            {
                packs.Add(mod.Key);
            }
            packs.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            foreach (var modInfo in packs)
            {
                string modName = modInfo.name.Split(',')[0].Replace(" ", "_");
                string modId = "";
                string assetsByMod = "";

                try
                {
                    modId = $"[{modInfo.asset.subPath.Split('/')[1].Split('_')[0]}]";
                    assetsByMod = $"({LoadedPacks[modInfo].ToString()} Asset";
                    if(LoadedPacks[modInfo] != 1)
                        assetsByMod += "s";
                    assetsByMod += ")";
                }
                catch (Exception)
                {
                    // ignored
                }

                LoadedAssetPacksText += $"{modName} {modId} {assetsByMod}\n";
                //LoadedAssetPacksText += $"{modName} {modId} {assetsByMod}                                                                                               ----------------------------------------------------------------------------------------------- ";
            }
        }


        private static int loaded;
        private static int skipped;
        private static int autoLoaded;
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
            skipped = 0;
            autoLoaded = 0;
            notLoaded = 0;
            var assetDatabaseStartTime = DateTime.Now;
            foreach (var mod in modAssets)
            {
                notificationInfo = _notificationUISystem.AddOrUpdateNotification(
                    $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(PrepareAssets)}",
                    title: "Step 2/3: Preparing Assets",
                    text: $"Preparing Pack: {mod.Key}",
                    progressState: ProgressState.Progressing,
                    thumbnail: "coui://apm/notify_icon.png",
                    progress: (int)(currentIndex / (float)modAssets.Count() * 100));

                var modStartTime = DateTime.Now;

                foreach (var file in mod.Value)
                {
                    try
                    {
                        Logger.Debug("Loading File: " + file.FullName);

                        var absolutePath = file.FullName;

                        // Replace backslashes with forward slashes
                        absolutePath = absolutePath.Replace('\\', '/');
                        // get relative path from absolute path
                        var relativePath = absolutePath.Replace(EnvPath.kUserDataPath + "/", "");
                        // Remove content after last / from relative path
                        relativePath = relativePath.Substring(0, relativePath.LastIndexOf('/'));

                        var fileName = Path.GetFileNameWithoutExtension(file.FullName);

                        var path = AssetDataPath.Create(relativePath, fileName);

                        var cidFilename = EnvPath.kUserDataPath + "\\" + relativePath + "\\" + fileName + ".Prefab.cid";
                        using StreamReader sr = new StreamReader(cidFilename);
                        var guid = sr.ReadToEnd();
                        sr.Close();

                        // The game automatically loads assets from the PDX Mods folder in the AssetDatabase.PDX_MODS (dynamic) database
                        if (Setting.Instance.AdaptiveAssetLoading)
                        {
                            if (AssetDatabase.global.TryGetAsset(Hash128.Parse(guid), out var asset))
                            {
                                if (asset.state != LoadState.NotLoaded)
                                {
                                    // TODO: Find out why some assets are already loaded
                                    Logger.Warn("Asset already loaded: " + asset.name);
                                }
                                else
                                {
                                    autoLoaded++;
                                    Logger.Debug("Prefab asset already in database");
                                }
                            }
                        }
                        else
                        {
                            AssetDatabase.user.AddAsset<PrefabAsset>(path, guid);
                            Logger.Debug("Prefab added to database successfully");
                        }

                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Asset {file} could not be loaded: {e.Message}");
                    }
                }

                var modEndTime = DateTime.Now - modStartTime;
                Logger.Debug($"Mod Time for {mod.Key}: {modEndTime.TotalMilliseconds} ms (average {modAssets.Count() / modEndTime.TotalMilliseconds} ms per asset)");

                currentIndex++;
                yield return null;
            }

            //WriteAnalysisInfo();

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
            var allPrefabs = AssetDatabase.global.GetAssets<PrefabAsset>();
            var prefabAssets = allPrefabs as PrefabAsset[] ?? allPrefabs.ToArray();
            Dictionary<string, int> times = new();
            foreach (PrefabAsset prefabAsset in prefabAssets)
            {
                try
                {
                    var prefabStartTime = DateTime.Now;
                    notificationInfo = _notificationUISystem.AddOrUpdateNotification(
                        $"{nameof(AssetPacksManager)}.{nameof(AssetPackLoaderSystem)}.{nameof(LoadAssets)}",
                        title: "Step 3/3: Loading Assets",
                        text: $"Loading: {prefabAsset.name}",
                        progressState: ProgressState.Progressing,
                        thumbnail: "coui://apm/notify_icon.png",
                        progress: (int)(currentIndex / (float)prefabAssets.Count() * 100));
                    var notificationTime = DateTime.Now - prefabStartTime;
                    Logger.Debug("Asset Name: " + prefabAsset.name);
                    Logger.Debug("Asset Path: " + prefabAsset.path);
                    var prefabBaseTime = DateTime.Now;
                    PrefabBase prefabBase = prefabAsset.Load() as PrefabBase;
                    var prefabBaseEndTime = DateTime.Now - prefabBaseTime;
                    Logger.Debug("Loaded Prefab");
                    var prefabAddTime = DateTime.Now;
                    _prefabSystem.AddPrefab(prefabBase, null, null, null);
                    Logger.Debug($"Added {prefabAsset.name} to Prefab System");
                    var prefabAddEndTime = DateTime.Now - prefabAddTime;
                    //Logger.Debug($"Added {prefabAsset.name} to Prefab System");
                    loaded++;
                    var prefabEndTime = DateTime.Now - prefabStartTime;
                    Logger.Debug("Prefab Time: " + prefabEndTime.TotalMilliseconds + "ms");
                    Logger.Debug("Notification Time: " + notificationTime.TotalMilliseconds + "ms");
                    Logger.Debug("Prefab Base Time: " + prefabBaseEndTime.TotalMilliseconds + "ms");
                    Logger.Debug("Prefab Add Time: " + prefabAddEndTime.TotalMilliseconds + "ms");
                    if (times.ContainsKey(prefabAsset.name))
                    {
                        times[prefabAsset.name] += (int)prefabEndTime.TotalMilliseconds;
                    }
                    else
                    {
                        times.Add(prefabAsset.name, (int)prefabEndTime.TotalMilliseconds);
                    }
                }
                catch (Exception e)
                {
                    notLoaded++;
                    Logger.Info(
                        $"Please see AssetPacksManager Log for details. Asset {prefabAsset.name} could not be added to Database: {e.Message}Path: {prefabAsset.path}\nUnique Name: {prefabAsset.uniqueName}\nCID: {prefabAsset.guid}\nSubPath: {prefabAsset.subPath}");
                }

                currentIndex++;
                yield return null;
            }

            var prefabSystemEndTime = DateTime.Now - prefabSystemStartTime;
            Logger.Info("Prefab System Time: " + prefabSystemEndTime.TotalMilliseconds + "ms");
            Logger.Info($"Average: {prefabSystemEndTime.TotalMilliseconds / prefabAssets.Length}ms per asset");
            string minAsset = "", maxAsset = "";
            int min = Int32.MaxValue, max = -1;
            foreach(var time in times)
            {
                if (time.Value < min)
                {
                    min = time.Value;
                    minAsset = time.Key;
                }
                if (time.Value > max)
                {
                    max = time.Value;
                    maxAsset = time.Key;
                }
            }
            Logger.Info($"Min: {minAsset} - {min}ms");
            Logger.Info($"Max: {maxAsset} - {max}ms");
            using(StreamWriter sw = new StreamWriter(Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(AssetPacksManager), "AssetLoadTimes.txt")))
            {
                foreach (var time in times)
                {
                    sw.WriteLine($"{time.Key}: {time.Value}ms");
                }
            }

            int delay = 100000;
            if (Setting.Instance.AutoHideNotifications)
                delay = 30;

            string text = $"{loaded} assets loaded by APM.";
            if (skipped > 0)
            {
                text += $", {skipped} skipped";
            }
            if (notLoaded > 0)
            {
                text += $", {notLoaded} failed to load";
            }
            if (autoLoaded > 0)
            {
                text += $", {autoLoaded} loaded automatically";
            }

            _notificationUISystem.RemoveNotification(
                identifier: notificationInfo.id,
                delay: delay,
                text: text,
                progressState: ProgressState.Complete,
                progress: 100
            );
            var totalAssetTime = DateTime.Now - _assetLoadStartTime;
            KLogger.Logger.Info("Asset Time: " + totalAssetTime);
        }

        /// <summary>
        /// Checks if the asset has a CID file. If not, it will try to restore it from the backup
        /// Creates a backup file if it doesn't exist.
        ///
        /// This is only needed because PDX Mods deleted CID files when disabling a mod while ingame.
        /// </summary>
        /// <param name="file">Asset file to be checked</param>
        /// <param name="modName">Current mod directory</param>
        /// <returns></returns>
        private static bool CheckAsset(FileInfo file, string modName)
        {
            AnalyzeAsset(file, modName);
            if (!File.Exists(file.FullName + ".cid"))
            {
                if (File.Exists(file.FullName + ".cid.backup"))
                {
                    File.Copy(file.FullName + ".cid.backup", file.FullName + ".cid");
                    Logger.Info($"Restored CID for {file.FullName}");
                    return true;
                }

                Logger.Warn($"Asset has no CID: {file.FullName}. No CID Backup was found");
                if (MissingCids.ContainsKey(modName))
                {
                    MissingCids[modName].Add(file.Name);
                }
                else
                {
                    MissingCids.Add(modName, [file.Name]);
                }

                return false;
            }

            // Back up CID
            if (!File.Exists(file.FullName + ".cid.backup"))
                File.Copy(file.FullName + ".cid", file.FullName + ".cid.backup", true);
            return true;
        }

        private static readonly Dictionary<string, int> ObsoleteIdentifiers = new();
        private static void AnalyzeAsset(FileInfo file, string modName)
        {
            try
            {
                string prefabContent = File.ReadAllText(file.FullName);
                if (prefabContent.Contains("ObsoleteIdentifiers"))
                {
                    int startIndex = prefabContent.IndexOf("ObsoleteIdentifiers", StringComparison.Ordinal);
                    string obsoleteIdentifierWrapper = prefabContent.Substring(startIndex, 2000);
                    int nameStartIndex = obsoleteIdentifierWrapper.IndexOf("\"m_Name\": \"", StringComparison.Ordinal);
                    string obsoleteIdentifierCloseWrapper = obsoleteIdentifierWrapper.Substring(nameStartIndex);
                    int nameEndIndex = obsoleteIdentifierCloseWrapper.IndexOf("\",", StringComparison.Ordinal);
                    string obsoleteIdentifier = obsoleteIdentifierCloseWrapper.Substring(0, nameEndIndex);
                    if (obsoleteIdentifier.Contains("\"m_Name\": \""))
                    {
                        obsoleteIdentifier = obsoleteIdentifier.Replace("\"m_Name\": \"", "");
                    }
                    if (ObsoleteIdentifiers.ContainsKey(obsoleteIdentifier))
                    {
                        ObsoleteIdentifiers[obsoleteIdentifier]++;
                    }
                    else
                    {
                        ObsoleteIdentifiers.Add(obsoleteIdentifier, 1);
                    }
                }
            }
            catch (Exception e)
            {
                //Logger.Warn($"Error analyzing asset {file.Name}: {e.Message}");
            }
        }

        private static void WriteAnalysisInfo()
        {
            string fileName = EnvPath.kUserDataPath + "/ModsData/AssetPacksManager/ObsoleteIdentifiers.txt";
            using StreamWriter sw = new StreamWriter(fileName);
            foreach (var identifier in ObsoleteIdentifiers)
            {
                sw.WriteLine($"{identifier.Key}: {identifier.Value}");
            }
        }

        private static readonly List<string> AdditionalCidChecks = [".Geometry", ".Surface", ".Texture"];
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
                    if (CheckAsset(file, modName))
                        files.Add(file);
                }
                else if (AdditionalCidChecks.Contains(file.Extension))
                {
                    CheckAsset(file, modName);
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
            var foldersToDelete = new[]
            {
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

        protected override void OnUpdate()
        {

        }
    }
}