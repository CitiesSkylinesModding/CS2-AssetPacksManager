﻿using System;
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
    [SettingsUIGroupOrder(kAssetPackLoadingGroup, kNotificationsGroup, kMiscGroup, kLoadedPacks, kLocalAssets)]
    [SettingsUIShowGroupName(kAssetPackLoadingGroup, kNotificationsGroup, kLoggingGroup, kMiscGroup, kLoadedPacks)]
    public class Setting : ModSetting
    {

        public const string kMainSection = "Settings";
        public const string kPacksSection = "Packs";
        public const string kLocalAssetsSection = "Local Assets";
        public const string kAssetPackLoadingGroup = "Asset Pack Loading";
        public const string kNotificationsGroup = "Actions";
        public const string kLoggingGroup = "Logging";
        public const string kMiscGroup = "Misc";
        public const string kLoadedPacks = "Loaded Asset Packs";
        public const string kLocalAssets = "Local Assets";
        public static Setting Instance;
        public Setting(IMod mod) : base(mod)
        {

        }
        
        [SettingsUISection(kMainSection, kAssetPackLoadingGroup)]
        public bool EnableAssetPackLoadingOnStartup { get; set; } = true;

        private bool AssetsLoadable()
        {
            return AssetPackLoaderSystem.AssetsLoaded;
        }

        [SettingsUISection(kMainSection, kAssetPackLoadingGroup)]
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
        [SettingsUISection(kMainSection, kMiscGroup)]
        public bool DeleteCachedAssetPacks
        {
            set
            {
                AssetPackLoaderSystem.DeleteCachedAssetPacks();
                AssetPackLoaderSystem.CloseGame();
            }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kMainSection, kMiscGroup)]
        public bool DeleteModsWithMissingCid
        {
            set
            {
                AssetPackLoaderSystem.DeleteModsWithMissingCid();
                AssetPackLoaderSystem.CloseGame();
            }
        }

        [SettingsUISection(kMainSection, kMiscGroup)]
        public bool DisableTelemetry { get; set; } = false;

        [SettingsUIButton]
        [SettingsUISection(kMainSection, kLoggingGroup)]
        public bool OpenLogFile
        {
            set { AssetPackLoaderSystem.OpenLogFile(); }
        }

        private LogLevel _loggingLevel = LogLevel.Warning;

        [SettingsUISection(kMainSection, kLoggingGroup)]
        public LogLevel LoggingLevel
        {
            get { return _loggingLevel; }
            set
            {
                _loggingLevel = value;
                switch (value)
                {
                    case LogLevel.Debug:
                        ApmLogger.Logger.effectivenessLevel = Level.Debug;
                        break;
                    case LogLevel.Info:
                        ApmLogger.Logger.effectivenessLevel = Level.Info;
                        break;
                    case LogLevel.Warning:
                        ApmLogger.Logger.effectivenessLevel = Level.Warn;
                        break;
                    case LogLevel.Error:
                        ApmLogger.Logger.effectivenessLevel = Level.Error;
                        break;
                    case LogLevel.Critical:
                        ApmLogger.Logger.effectivenessLevel = Level.Critical;
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
                    ApmLogger.Logger.effectivenessLevel = Level.Debug;
                    break;
                case LogLevel.Info:
                    ApmLogger.Logger.effectivenessLevel = Level.Info;
                    break;
                case LogLevel.Warning:
                    ApmLogger.Logger.effectivenessLevel = Level.Warn;
                    break;
                case LogLevel.Error:
                    ApmLogger.Logger.effectivenessLevel = Level.Error;
                    break;
                case LogLevel.Critical:
                    ApmLogger.Logger.effectivenessLevel = Level.Critical;
                    break;
            }
        }

        [SettingsUISection(kMainSection, kNotificationsGroup)]
        public bool AutoHideNotifications { get; set; } = true;
        
        [SettingsUISection(kMainSection, kNotificationsGroup)]
        public bool DisableLoadingNotification { get; set; } = true;

        [SettingsUISection(kMainSection, kNotificationsGroup)]
        public bool ShowWarningForLocalAssets { get; set; } = true;

        public static int LoadedAssetPacksTextVersion { get; set; }

        [SettingsUIDisplayName(typeof(AssetPackLoaderSystem), nameof(AssetPackLoaderSystem.GetLoadedAssetPacksText))]
        [SettingsUISection(kPacksSection, kLoadedPacks)]
        [SettingsUIMultilineText]
        public string LoadedAssetPacksText => "";
        
        public static int LocalAssetsTextVersion { get; set; }

        [SettingsUIDisplayName(typeof(AssetPackLoaderSystem), nameof(AssetPackLoaderSystem.GetLocalAssetsText))]
        [SettingsUISection(kLocalAssetsSection, kLocalAssets)]
        [SettingsUIMultilineText]
        public string LocalAssetsText => "";

        public override void SetDefaults()
        {
            LoggingLevel = LogLevel.Warning;
            AutoHideNotifications = true;
            ShowWarningForLocalAssets = true;
            EnableAssetPackLoadingOnStartup = true;
            DisableTelemetry = false;
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
            text += $"\nEnableAssetPackLoadingOnStartup: {EnableAssetPackLoadingOnStartup}";
            text += $"\nLoggingLevel: {LoggingLevel}";
            text += $"\nActualLoggingLevel: {ApmLogger.Logger.effectivenessLevel.name}";
            text += $"\nAutoHideNotifications: {AutoHideNotifications}";
            text += $"\nShowWarningForLocalAssets: {ShowWarningForLocalAssets}";
            text += $"\nDisableTelemetry: {DisableTelemetry}";
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
                { m_Setting.GetOptionTabLocaleID(Setting.kLocalAssetsSection), "Local Assets" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kAssetPackLoadingGroup), "Asset Pack Loading" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kNotificationsGroup), "Notifications" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLoggingGroup), "Logging" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMiscGroup), "Miscellaneous" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLoadedPacks), "Loaded Asset Packs" },
                
                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableLoadingNotification)), "Disable Loading Notification"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableLoadingNotification)),
                    $"Disables status notifaction while loading packs (improves loading times)."
                },
                
                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableAssetPackLoadingOnStartup)), "Enable Asset Pack Loading on Startup"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableAssetPackLoadingOnStartup)),
                    $"Enables the loading of asset packs on startup. Turning this setting off will prevent the loading of asset packs on startup. You will have to load them manually."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableTelemetry)), "Disable telemetry (APM only collects data about the number of asset packs and the adaptive loading setting, no personal data)"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableTelemetry)),
                    $"Disables telemetry. APM only collects data about the number of asset packs and the adaptive loading setting. If a majority of users have adaptive loading enabled, the feature will be enabled by default in future versions."
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

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteCachedAssetPacks)), "Delete Cached Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteCachedAssetPacks)),
                    $"Sometimes helps the issue of missing CID-Files. Deletes the cache of downloaded PDX Mods. This will close the game immediately. It will not change your playset, but will require to re-download all mods on the next startup. This might take a few minutes depending on the amount of subscribed mods."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteCachedAssetPacks)),
                    $"**WARNING. This will close your game!** Are you sure you want to delete the asset packs cache? This cannot be undone."
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
                
                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.LocalAssetsText)), "Local Assets"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.LocalAssetsText)),
                    $"Displays the loaded local assets. This is a read-only field."
                },
            };
        }


        public void Unload()
        {
        }
    }
}