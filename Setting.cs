using System;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace AssetImporter
{
    [FileLocation(nameof(AssetImporter))]
    [SettingsUIGroupOrder(kSettingsGroup, kActionsGroup)]
    [SettingsUIShowGroupName(kSettingsGroup, kActionsGroup)]
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
        public bool DeleteUnusedFiles { get; set; }

        [SettingsUISection(kSection, kMiscGroup)]
        public bool EnableVerboseLogging { get; set; }

        [SettingsUISection(kSection, kMiscGroup)]
        public bool AutoHideNotifications { get; set; }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kActionsGroup)]
        public bool DeleteImportedAssets
        {
            set { Mod.DeleteImportedAssets(); }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kActionsGroup)]
        public bool SyncAssets
        {
            set { Mod.SyncAssets(); }
        }

        public override void SetDefaults()
        {
            HiddenSetting = true;
            EnableLocalAssetPacks = true;
            EnableSubscribedAssetPacks = true;
            DeleteUnusedFiles = true;
            EnableVerboseLogging = false;
            AutoHideNotifications = true;
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
                {m_Setting.GetSettingsLocaleID(), nameof(AssetImporter)},
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kSettingsGroup), "Synchronization" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup), "Actions" },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableLocalAssetPacks)), "Enable Local Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableLocalAssetPacks)),
                    $"Enables the import of locally installed mods (Mods in the user/Mods folder)."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableSubscribedAssetPacks)), "Enable Subscribed Asset Packs"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableSubscribedAssetPacks)),
                    $"Enables the import of subscribed asset packs."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteUnusedFiles)), "Delete unused files"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteUnusedFiles)),
                    $"Deletes all unused files in the CustomAssets folder."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableVerboseLogging)), "Enable Verbose Logging"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableVerboseLogging)),
                    $"Enables additional log messages for debugging purposes."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.AutoHideNotifications)), "Auto Hide Notifications"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AutoHideNotifications)),
                    $"Automatically hides Asset Importer Notifications after 30 seconds."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteImportedAssets)), "Delete imported assets"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteImportedAssets)),
                    $"Deletes all imported asset packs from your game directory. Please manually sync assets to re-import them. You will need to restart the game after syncing assets. If you don't choose to sync manually, you will have to restart the game **TWICE**."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteImportedAssets)),
                    $"Are you sure you want to delete all imported asset packs? \nPlease manually sync assets to re-import them. You will need to restart the game after syncing assets. If you don't choose to sync manually, you will have to restart the game **TWICE**."
                },

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.SyncAssets)), "Re-Import subscribed assets"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.SyncAssets)),
                    $"Copies all the downloaded asset packs to your game directory again. This will overwrite any existing assets"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.SyncAssets)),
                    $"Are you sure you want to re-import all asset packs?"
                },
            };
        }

        public void Unload()
        {
        }
    }
}