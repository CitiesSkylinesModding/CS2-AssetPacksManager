using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;
using Colossal.UI;
using Game.Prefabs;
using Game.PSI;
using JetBrains.Annotations;
using UnityEngine;
using Hash128 = Colossal.Hash128;
using StreamReader = System.IO.StreamReader;

namespace AssetPacksManager
{
    public class Mod : IMod
    {
        public string ModPath { get; set; }

        public static KLogger Logger = new();

        public void OnLoad(UpdateSystem updateSystem)
        {
            KLogger.Init();

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                //if (!DisableLogging)
                //    Logger.Info($"Current mod asset at {asset.path}");
                ModPath = Path.GetDirectoryName(asset.path);
                UIManager.defaultUISystem.AddHostLocation("apm", Path.Combine(Path.GetDirectoryName(asset.path), "Resources"), false);
            }

            Setting setting = new (this);
            setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting));
            AssetDatabase.global.LoadSettings(nameof(AssetPacksManager), setting, new Setting(this));
            Setting.Instance = setting;
            Setting.Instance.UpdateLogLevel();
            Logger.Info(Setting.Instance.ToString());

            updateSystem.UpdateAt<AssetPackLoaderSystem>(SystemUpdatePhase.MainLoop);
        }

        public void OnDispose()
        {
            Logger.Debug(nameof(OnDispose));
            if (Setting.Instance != null)
            {
                Setting.Instance.UnregisterInOptionsUI();
                Setting.Instance = null;
            }
        }

    }
}