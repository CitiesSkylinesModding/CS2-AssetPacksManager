using System;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;
using Colossal.Logging;
using Game.UI.Menu;

namespace AssetPacksManager
{
    [FileLocation($"ModsSettings/{nameof(AssetPacksManager)}/{nameof(AssetPacksManager)}")]
    [SettingsUIGroupOrder(kSettingsGroup, kActionsGroup, kMiscGroup, kLoadedPacks)]
    [SettingsUIShowGroupName(kSettingsGroup, kActionsGroup, kMiscGroup, kLoadedPacks)]
    public class Setting : ModSetting
    {

        public const string kMainSection = "Settings";
        public const string kPacksSection = "Packs";
        public const string kSettingsGroup = "Synchronization";
        public const string kActionsGroup = "Actions";
        public const string kMiscGroup = "Misc";
        public const string kLoadedPacks = "Loaded Asset Packs";
        public static Setting Instance;
        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISection(kMainSection, kSettingsGroup)]
        public bool EnableLocalAssetPacks { get; set; } = false;

        [SettingsUISection(kMainSection, kSettingsGroup)]
        public bool EnableSubscribedAssetPacks { get; set; } = true;

        [SettingsUISection(kMainSection, kSettingsGroup)]
        public bool EnableAssetPackLoadingOnStartup { get; set; } = true;

        [SettingsUISection(kMainSection, kSettingsGroup)]
        public bool AdaptiveAssetLoading { get; set; } = false;

        private bool AssetsLoadable()
        {
            return AssetPackLoaderSystem.AssetsLoaded;
        }

        [SettingsUISection(kMainSection, kSettingsGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(AssetsLoadable))]
        public bool LoadAssetPacks
        {
            set
            {
                if (value)
                {
                    AssetPackLoaderSystem.Instance.LoadAssetPacks();
                }
            }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kMainSection, kActionsGroup)]
        public bool DeleteModsCache
        {
            set { AssetPackLoaderSystem.DeleteModsCache(); }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kMainSection, kActionsGroup)]
        public bool DeleteModsWithMissingCid
        {
            set
            {
                AssetPackLoaderSystem.DeleteModsWithMissingCid();
                AssetPackLoaderSystem.CloseGame();
            }
        }

        [SettingsUIButton]
        [SettingsUISection(kMainSection, kActionsGroup)]
        public bool OpenLogFile
        {
            set { AssetPackLoaderSystem.OpenLogFile(); }
        }

        [SettingsUISlider(min=0, max=100000, step=1000, unit = "ms")]
        [SettingsUISection(kMainSection, kMiscGroup)]
        public int LogCooldownTicks { get; set; }

        private LogLevel _loggingLevel;

        [SettingsUISection(kMainSection, kMiscGroup)]
        public LogLevel LoggingLevel
        {
            get { return _loggingLevel; }
            set
            {
                _loggingLevel = value;
                switch (value)
                {
                    case LogLevel.Debug:
                        KLogger.Logger.effectivenessLevel = Level.Debug;
                        break;
                    case LogLevel.Info:
                        KLogger.Logger.effectivenessLevel = Level.Info;
                        break;
                    case LogLevel.Warning:
                        KLogger.Logger.effectivenessLevel = Level.Warn;
                        break;
                    case LogLevel.Error:
                        KLogger.Logger.effectivenessLevel = Level.Error;
                        break;
                    case LogLevel.Critical:
                        KLogger.Logger.effectivenessLevel = Level.Critical;
                        break;
                }
                /*KLogger.Instance.Debug("Debug");
                KLogger.Instance.Info("Info");
                KLogger.Instance.Warn("Warning");
                KLogger.Instance.Error("Error");
                KLogger.Instance.Critical("Critical");*/
            }
        }

        public void UpdateLogLevel()
        {
            switch (_loggingLevel)
            {
                case LogLevel.Debug:
                    KLogger.Logger.effectivenessLevel = Level.Debug;
                    break;
                case LogLevel.Info:
                    KLogger.Logger.effectivenessLevel = Level.Info;
                    break;
                case LogLevel.Warning:
                    KLogger.Logger.effectivenessLevel = Level.Warn;
                    break;
                case LogLevel.Error:
                    KLogger.Logger.effectivenessLevel = Level.Error;
                    break;
                case LogLevel.Critical:
                    KLogger.Logger.effectivenessLevel = Level.Critical;
                    break;
            }
        }

        [SettingsUISection(kMainSection, kMiscGroup)]
        public bool AutoHideNotifications { get; set; } = true;

        [SettingsUISection(kMainSection, kMiscGroup)]
        public bool ShowWarningForLocalAssets { get; set; } = true;

        public static int LoadedAssetPacksTextVersion { get; set; }

        //[SettingsUIValueVersion(typeof(Setting), nameof(LoadedAssetPacksTextVersion))]
        [SettingsUIDisplayName(typeof(AssetPackLoaderSystem), nameof(AssetPackLoaderSystem.GetLoadedAssetPacksText))]
        [SettingsUISection(kPacksSection, kLoadedPacks)]
        [SettingsUIMultilineText]
        public string LoadedAssetPacksText => "";

        public override void SetDefaults()
        {
            EnableLocalAssetPacks = false;
            EnableSubscribedAssetPacks = true;
            LoggingLevel = LogLevel.Info;
            AutoHideNotifications = true;
            ShowWarningForLocalAssets = true;
            EnableAssetPackLoadingOnStartup = true;
            LogCooldownTicks = 0;
        }

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        public override string ToString()
        {
            string text = "\n=====APM Settings=====";
            text += $"\nEnableLocalAssetPacks: {EnableLocalAssetPacks}";
            text += $"\nEnableSubscribedAssetPacks: {EnableSubscribedAssetPacks}";
            text += $"\nEnableAssetPackLoadingOnStartup: {EnableAssetPackLoadingOnStartup}";
            text += $"\nAdaptiveAssetLoading: {AdaptiveAssetLoading}";
            text += $"\nLoggingLevel: {LoggingLevel}";
            text += $"\nActualLoggingLevel: {KLogger.Logger.effectivenessLevel.name}";
            text += $"\nAutoHideNotifications: {AutoHideNotifications}";
            text += $"\nShowWarningForLocalAssets: {ShowWarningForLocalAssets}";
            text += $"\nLogCooldownTicks: {LogCooldownTicks}";
            text += "\n======================";
            return text;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                {m_Setting.GetSettingsLocaleID(), "Asset Packs Manager"},
                { m_Setting.GetOptionTabLocaleID(Setting.kMainSection), "Settings" },
                { m_Setting.GetOptionTabLocaleID(Setting.kPacksSection), "Asset Packs" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kSettingsGroup), "Synchronization" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup), "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMiscGroup), "Miscellaneous" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLoadedPacks), "Loaded Asset Packs" },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableLocalAssetPacks)), "Enable Local Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableLocalAssetPacks)),
                    $"Enables the import of locally installed mods (Mods in the user/Mods folder). These will already be loaded by the game (without icons). Activating this option may cause duplicate assets"
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableSubscribedAssetPacks)), "Enable Subscribed Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableSubscribedAssetPacks)),
                    $"Enables the import of subscribed asset packs."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableAssetPackLoadingOnStartup)), "Enable Asset Pack Loading on Startup"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableAssetPackLoadingOnStartup)),
                    $"Enables the loading of asset packs on startup. Turning this setting off will prevent the loading of asset packs on startup. You will have to load them manually."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.AdaptiveAssetLoading)), "Adaptive Asset Loading (Enable when you have double custom assets, disable when you are missing assets)"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AdaptiveAssetLoading)),
                    $"Enables the loading of assets adaptively. Only assets that have not been loaded by the integrated PDX Asset Loader will be loaded, which may significantly reduce load times (up to 99%). Disable this option if you experience Black Screens, Crashes, Low FPS, missing assets or other issues."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.LoadAssetPacks)), "Load Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.LoadAssetPacks)),
                    $"Loads the asset packs. This will load the asset packs that are enabled in the settings."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.LoggingLevel)), "Logging Level" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.LoggingLevel)),
                    $"Choose the amount of information that is logged. Cascading, Level Warning includes Error and Critical, for example."
                },

                { m_Setting.GetEnumValueLocaleID(Setting.LogLevel.Debug), "Debug" },
                { m_Setting.GetEnumValueLocaleID(Setting.LogLevel.Info), "Info" },
                { m_Setting.GetEnumValueLocaleID(Setting.LogLevel.Warning), "Warning" },
                { m_Setting.GetEnumValueLocaleID(Setting.LogLevel.Error), "Error" },
                { m_Setting.GetEnumValueLocaleID(Setting.LogLevel.Critical), "Critical" },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.AutoHideNotifications)), "Auto Hide Notifications"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AutoHideNotifications)),
                    $"Automatically hides APM Notifications after 30 seconds."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowWarningForLocalAssets)), "Show Warning for Local Assets"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowWarningForLocalAssets)),
                    $"Displays a warning when APM detects local assets in the user folder. This is to prevent accidental loading of local assets."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.LogCooldownTicks)), "Log Cooldown (10000 = 1ms)"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.LogCooldownTicks)),
                    $"Sets the minimum time between log entries. This is to prevent the NullReferenceExceptions caused by the logger."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteModsCache)), "Delete Mods Cache"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteModsCache)),
                    $"Sometimes helps the issue of missing CID-Files. Deletes the cache of downloaded PDX Mods. This will close the game immediately. It will not change your playset, but will require to re-download all mods on the next startup. This might take a few minutes depending on the amount of subscribed mods."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteModsCache)),
                    $"**WARNING. This will close your game!** Are you sure you want to delete the mods cache? This cannot be undone."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteModsWithMissingCid)), "Delete Mods with missing CID-Files"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteModsWithMissingCid)),
                    $"A less aggressive version of the Delete Mods Cache option. This will only delete mods that are missing the CID-File. This will close the game immediately. It will not change your playset, but will require to re-download all affected mods on the next startup."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteModsWithMissingCid)),
                    $"**WARNING. This will close your game!** Are you sure you want to delete the affected mods cache? This cannot be undone."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenLogFile)), "Open Log File"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenLogFile)),
                    $"Opens the log file of the mod in the default text editor. Log contains details about assets that failed to load."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.LoadedAssetPacksText)), "Loaded Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.LoadedAssetPacksText)),
                    $"Displays the loaded asset packs. This is a read-only field."
                },
            };
        }


        public void Unload()
        {
        }
    }
}