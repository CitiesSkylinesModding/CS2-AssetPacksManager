using System;
using System.IO;
using System.Linq;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Colossal.IO.AssetDatabase;
using Game.Debug;
using Game.Simulation;
using JetBrains.Annotations;
using Unity.Entities;
using Hash128 = Colossal.Hash128;

namespace AssetImporter
{
    public class Mod : IMod
    {
        public static readonly ILog Logger = LogManager.GetLogger($"{nameof(AssetImporter)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        [CanBeNull] public string ModPath { get; set; }

        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                ModPath = Path.GetDirectoryName(asset.path);
            }

            //var dir = "C:/Users/" + Environment.UserName + "/AppData/LocalLow/Colossal Order/Cities Skylines II/CustomAssets";
            var dir = "C:/Users/" + Environment.UserName + "/Desktop/assets";
            //LoadFromDirectory(dir);
            //CopyDirectoryToInstalled(dir);
            //Logger.Info("Loaded Directory: " + dir);
            CopyFromSubscribedMods();
            
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(AssetImporter), m_Setting, new Setting(this));
        }

        // Not working yet
        private void LoadFromDirectory(string assetsDir)
        {
            Logger.Info("Assets before import: " + AssetDatabase.game.count);
            DumpAssets("1");

            var hash = Hash128.Parse(File.ReadAllText("C:/Users/Konsi/AppData/LocalLow/Colossal Order/Cities Skylines II/.cache/Mods/mods_subscribed/77463_4/assets/Circle Lot/CircleLot.Prefab.cid"));
            var path = ".cache/Mods/mods_subscribed/77463_4/assets/Circle Lot";

            var ret = AssetDatabase.user.AddAsset<PrefabAsset>(
                AssetDataPath.Create(path, "CircleLot"), hash);

            //AssetDataPath assetDataPath = AssetDataPath.Create("CustomAssets/CircleLot/CircleLot", "CircleLot");
            //PrefabAsset prefabAsset = new(

            Logger.Info("Added Prefab: " + ret.name + " with unique name " + ret.uniqueName);


            /*foreach (var file in new DirectoryInfo(assetsDir).GetFiles("*.Prefab"))
            {
                Logger.Info("Found " + file.FullName);

                //var prefabName = Path.GetFileNameWithoutExtension(file.FullName);

                //var hash = Hash128.Parse(File.ReadAllText(file.FullName + ".cid"));
                var hash = Hash128.Parse(File.ReadAllText(file.FullName + ".cid");
                var path = ".cache/Mods/mods_subscribed/77463_4/assets/Circle Lot";

                var ret = AssetDatabase.user.AddAsset<PrefabAsset>(
                    AssetDataPath.Create(path, "CircleLot"), hash);

                //AssetDataPath assetDataPath = AssetDataPath.Create("CustomAssets/CircleLot/CircleLot", "CircleLot");
                //PrefabAsset prefabAsset = new(

                Logger.Info("Added Prefab: " + ret.name + " with unique name " + ret.uniqueName);
                Logger.Info("Loaded " + file.FullName + " with hash " + hash);
            }*/
            DumpAssets("2");
            Logger.Info(AssetDatabase.user.AllAssets().Last().name);
            Logger.Info("Assets after import: " + AssetDatabase.user.count);
        }

        private void DumpAssets(string name)
        {
            var x = AssetDatabase.user.AllAssets().GetEnumerator();
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

        private void CopyDirectoryToInstalled(string assetsDir)
        {
            var streamingPath = "C:/Users/" + Environment.UserName + "/AppData/LocalLow/Colossal Order/Cities Skylines II/CustomAssets";
            foreach (var file in new DirectoryInfo(assetsDir).GetFiles("*.Prefab"))
            {
                Logger.Info("Copying " + file.FullName);
                File.Copy(file.FullName, Path.Combine(streamingPath, file.Name));
                File.Copy(file.FullName + ".cid", Path.Combine(streamingPath, file.Name + ".cid"));
                Logger.Info("Copied " + file.FullName);
            }
        }

        private void CopyFromSubscribedMods()
        {
            Logger.Info("Copying from subscribed mods.");
            var modsPath = "C:/Users/" + Environment.UserName + "/AppData/LocalLow/Colossal Order/Cities Skylines II/.cache/Mods/mods_subscribed";
            var streamingPath = "C:/Users/" + Environment.UserName + "/AppData/LocalLow/Colossal Order/Cities Skylines II/CustomAssets";
            foreach (var mod in new DirectoryInfo(modsPath).GetDirectories())
            {
                //foreach (var assetDir in new DirectoryInfo(mod.FullName).GetDirectories("assets_"))
                var assetDir = new DirectoryInfo(Path.Combine(mod.FullName, "assets"));
                if (assetDir.Exists)
                {
                    Logger.Info($"Copying assets from {mod.Name}");
                    //CopyDirectoryToInstalled(assetDir.FullName);
                    // Copy directory recursively
                    CopyDirectory(assetDir.FullName, streamingPath, true);
                }

            }
        }



        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            Logger.Info("Copying directory: " + sourceDir + " to " + destinationDir);
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                Logger.Error($"Source directory not found: {dir.FullName}");
            if (Directory.Exists(destinationDir))
                Directory.Delete(destinationDir);

            Directory.CreateDirectory(destinationDir);
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
                Logger.Info("Copied file: " + targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private void LoadFromInstalledMods()
        {
            foreach (var modInfo in GameManager.instance.modManager)
            {
                if (modInfo.asset.isEnabled)
                {
                    var modDir = Path.GetDirectoryName(modInfo.asset.path);
                    if (modDir == null)
                    {
                        continue;
                    }

                    var assetsDir = Path.Combine(modDir, "assets");
                    if (Directory.Exists(assetsDir))
                    {
                        Logger.Info($"Load \"{modInfo.name}\"'s assets.");
                        LoadFromDirectory(assetsDir);
                    }
                }
            }
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}