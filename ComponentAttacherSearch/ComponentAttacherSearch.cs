using System;
using System.Collections.Generic;
using HarmonyLib;
using NeosModLoader;

namespace ComponentAttacherSearch
{
    public partial class ComponentAttacherSearch : NeosMod
    {
        internal static ModConfiguration Config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<HashSet<string>> ExcludedCategoriesKey = new("ExcludedCategories", "Exclude specific categories. Discarded while loading.", internalAccessOnly: true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> SearchRefreshDelay = new("SearchRefreshDelay", "Time in ms to wait after search input change before refreshing the results. 0 to always refresh.", () => 750, valueValidator: value => value >= 0);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> UserExcludedCategories = new("UserExcludedCategories", "Exclude specific categories by path (case sensitive). Separate entries by semicolon. Search will work inside them anyways.", () => "/LogiX/Actions; /LogiX/Cast; /LogiX/Math; /LogiX/Operators");

        private static readonly char[] UserExclusionSeparator = new[] { ';' };
        private static string lastUserExcludedCategories = "";
        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosComponentAttacherSearch";
        public override string Name => "ComponentAttacherSearch";
        public override string Version => "1.1.0";
        private static HashSet<string> ExcludedCategories => Config.GetValue(ExcludedCategoriesKey);

        public override void OnEngineInit()
        {
            var harmony = new Harmony($"{Author}.{Name}");

            Config = GetConfiguration();

            Config.Set(ExcludedCategoriesKey, new HashSet<string>());
            updateExcludedCategories();

            Config.OnThisConfigurationChanged += Config_OnThisConfigurationChanged;
            Config.Save(true);

            harmony.PatchAll();
        }

        private void Config_OnThisConfigurationChanged(ConfigurationChangedEvent configurationChangedEvent)
        {
            if (configurationChangedEvent.Key == UserExcludedCategories)
                updateExcludedCategories();
        }

        private void updateExcludedCategories()
        {
            var previousValues = lastUserExcludedCategories.Split(UserExclusionSeparator, StringSplitOptions.RemoveEmptyEntries);
            var newValues = Config.GetValue(UserExcludedCategories).Split(UserExclusionSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var value in previousValues)
                ExcludedCategories.Remove(value.Trim());

            foreach (var value in newValues)
                ExcludedCategories.Add(value.Trim());

            lastUserExcludedCategories = Config.GetValue(UserExcludedCategories);
        }
    }
}