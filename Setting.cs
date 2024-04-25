using System;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

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

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kActionsGroup)]
        public bool DeleteModsCache
        {
            set { Mod.DeleteModsCache(); }
        }

        [SettingsUISection(kSection, kMiscGroup)]
        public bool EnableVerboseLogging { get; set; }

        [SettingsUISection(kSection, kMiscGroup)]
        public bool AutoHideNotifications { get; set; }

        public override void SetDefaults()
        {
            HiddenSetting = true;
            EnableLocalAssetPacks = false;
            EnableSubscribedAssetPacks = true;
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
                {m_Setting.GetSettingsLocaleID(), "Asset Packs Manager"},
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kSettingsGroup), "Synchronization" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup), "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMiscGroup), "Miscellaneous" },

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

                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteModsCache)), "Delete Mods Cache"},
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteModsCache)),
                    $"Sometimes helps the issue of missing CID-Files. Deletes the cache of downloaded PDX Mods. This will close the game immediately. It will not change your playset, but will require to re-download all mods on the next startup. This might take a few minutes depending on the amount of subscribed mods."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteModsCache)),
                    $"**WARNING. This will close your game!** Are you sure you want to delete the mods cache? This cannot be undone."
                },
            };
        }

        public void Unload()
        {
        }
    }
}