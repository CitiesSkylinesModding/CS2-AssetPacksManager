using System;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;
using Colossal.Logging;

namespace AssetPacksManager
{
    [FileLocation(nameof(AssetPacksManager))]
    [SettingsUIGroupOrder(kSettingsGroup, kActionsGroup, kMiscGroup)]
    [SettingsUIShowGroupName(kSettingsGroup, kActionsGroup, kMiscGroup)]
    public class Setting : ModSetting
    {

        public const string kSection = "Main";
        public const string kSettingsGroup = "Synchronization";
        public const string kActionsGroup = "Actions";
        public const string kMiscGroup = "Misc";
        public static Setting instance;
        public Setting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUIHidden]
        public bool HiddenSetting { get; set; }


        [SettingsUISection(kSection, kSettingsGroup)]
        public bool EnableLocalAssetPacks { get; set; }

        [SettingsUISection(kSection, kSettingsGroup)]
        public bool EnableSubscribedAssetPacks { get; set; }

        [SettingsUISection(kSection, kSettingsGroup)]
        public bool LoadIconLocationInBackground { get; set; }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kActionsGroup)]
        public bool DeleteModsCache
        {
            set { Mod.DeleteModsCache(); }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kActionsGroup)]
        public bool DeleteModsWithMissingCid
        {
            set
            {
                Mod.DeleteModsWithMissingCid();
                Mod.CloseGame();
            }
        }

        [SettingsUIButton]
        [SettingsUISection(kSection, kActionsGroup)]
        public bool OpenLogFIle
        {
            set { Mod.OpenLogFile(); }
        }

        [SettingsUISlider(min=0, max=100000, step=1000, unit = "ms")]
        [SettingsUISection(kSection, kMiscGroup)]
        public int LogCooldownTicks { get; set; }

        private LogLevel _loggingLevel;

        [SettingsUISection(kSection, kMiscGroup)]
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


        [SettingsUISection(kSection, kMiscGroup)]
        public bool AutoHideNotifications { get; set; }

        [SettingsUISection(kSection, kMiscGroup)]
        public bool ShowWarningForLocalAssets { get; set; }

        public override void SetDefaults()
        {
            HiddenSetting = true;
            EnableLocalAssetPacks = false;
            EnableSubscribedAssetPacks = true;
            LoadIconLocationInBackground = true;
            LoggingLevel = LogLevel.Info;
            AutoHideNotifications = true;
            ShowWarningForLocalAssets = true;
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
            string text = "=====APM Settings=====";
            text += $"\nEnableLocalAssetPacks: {EnableLocalAssetPacks}";
            text += $"\nEnableSubscribedAssetPacks: {EnableSubscribedAssetPacks}";
            text += $"\nLoadIconLocationInBackground: {LoadIconLocationInBackground}";
            text += $"\nLoggingLevel: {LoggingLevel}";
            text += $"\nAutoHideNotifications: {AutoHideNotifications}";
            text += $"\nShowWarningForLocalAssets: {ShowWarningForLocalAssets}";
            text += $"\nLogCooldownTicks: {LogCooldownTicks}";
            text += "======================";
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
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kSettingsGroup), "Synchronization" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup), "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMiscGroup), "Miscellaneous" },

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

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.LoadIconLocationInBackground)), "Load Icon Location in Background"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.LoadIconLocationInBackground)),
                    $"Improves asset pack loading times drastically, but sometimes causes missing icons ingame. Turn off if you have issues with missing asset icons."
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
                    $"Displays a warning when APM detects local assets in the user/Mods folder. This is to prevent accidental loading of local assets."
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
                    $"A less agressive version of the Delete Mods Cache option. This will only delete mods that are missing the CID-File. This will close the game immediately. It will not change your playset, but will require to re-download all affected mods on the next startup."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteModsWithMissingCid)),
                    $"**WARNING. This will close your game!** Are you sure you want to delete the affected mods cache? This cannot be undone."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenLogFIle)), "Open Log File"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenLogFIle)),
                    $"Opens the log file of the mod in the default text editor. Log contains details about assets that failed to load."
                },
            };
        }

        public void Unload()
        {
        }
    }
}