using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DiscoveryRadius.Patches;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;

namespace DiscoveryRadius;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public class DiscoveryRadiusPlugin : BaseUnityPlugin
{
    public const string ModName = "DiscoveryRadius";
    public const string ModVersion = "1.0.1";
    private const string ModAuthor = "FixItFelix";
    private const string ModGuid = ModAuthor + "." + ModName;

    private readonly Harmony _harmony = new(ModGuid);

    [UsedImplicitly] public static readonly ManualLogSource ModTemplateLogger =
        BepInEx.Logging.Logger.CreateLogSource(ModName);

    private static readonly ConfigSync ConfigSync = new(ModGuid)
        { DisplayName = ModName, CurrentVersion = ModVersion };

    private static ConfigEntry<bool> _configLocked = null!;

    public static ConfigEntry<float> LandExploreRadius = null!;
    public static ConfigEntry<float> SeaExploreRadius = null!;

    public static ConfigEntry<float> AltitudeRadiusMultiplier = null!;
    public static ConfigEntry<float> ForestRadiusMultiplier = null!;
    public static ConfigEntry<float> DaylightRadiusMultiplier = null!;
    public static ConfigEntry<float> WeatherRadiusMultiplier = null!;

    public static ConfigEntry<bool> DisplayCurrentRadiusValue = null!;
    public static ConfigEntry<bool> DisplayVariables = null!;
    public static ConfigEntry<bool> DisplayDebug = null!;

    private void Awake()
    {
        _configLocked = CreateConfig("1 - General", "Lock Configuration", true,
            "If 'true' and playing on a server, config can only be changed on server-side configuration, " +
            "clients cannot override");
        ConfigSync.AddLockingConfigEntry(_configLocked);

        LandExploreRadius = CreateConfig("2 - Exploration Radius", "Land exploration radius", 150.0f,
            "The radius around the player to uncover while travelling on land near sea level. " +
            "Higher values may cause performance issues. Max allowed is 1000. Default is 150.");
        SeaExploreRadius = CreateConfig("2 - Exploration Radius", "Sea exploration radius", 200.0f,
            "The radius around the player to uncover while travelling on a boat. " +
            "Higher values may cause performance issues. Max allowed is 1000. Default is 200.");

        AltitudeRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Altitude radius multiplier",
            1.5f,
            "Multiplier to apply to land exploration radius based on altitude. " +
            "For every 100 units above sea level (smooth scale), add this value multiplied by " +
            "Land exploration radius to the total. Accepted range 0.0 to 2.0. Set to 0 to disable. Default 1.5.");
        ForestRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Forest radius multiplier", 1.0f,
            "Multiplier to apply to land exploration radius when in a forest (black forest, " +
            "forested parts of meadows or swamp). This value is multiplied by the base land exploration " +
            "radius and subtracted from the total. Accepted range 0.0 to 1.0. Set to 0 to disable. Default 1.0.");
        DaylightRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Daylight radius multiplier", 0.3f,
            "Multiplier that influences how much daylight (directional and ambient light) affects " +
            "exploration radius. This value is multiplied by the base land or sea exploration radius and added " +
            "to the total. Accepted range 0-1. Set to 0 to disable. Default 0.3.");
        WeatherRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Weather radius multiplier", 0.3f,
            "Multiplier that influences how much the current weather affects exploration radius. " +
            "This value is multiplied by the base land or sea exploration radius and added to the total. " +
            "Accepted range 0-1. Set to 0 to disable. Default 0.3.");

        DisplayCurrentRadiusValue = CreateConfig("4 - Miscellaneous", "Display current radius", false,
            "Enabling this will display the currently computed exploration radius in the bottom " +
            "left of the in-game Hud. Useful if you are trying to tweak config values and want to see the result.");
        DisplayVariables = CreateConfig("4 - Miscellaneous", "Display current variables", false,
            "Enabling this will display on the Hud the values of various variables that go into " +
            "calculating the exploration radius. Mostly useful for debugging and tweaking the config.");
        DisplayDebug = CreateConfig("4 - Miscellaneous", "Display debug text", false,
            "Debugging use only, would not recommend to enable for players.");

        AddFixConfigSettings();
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
    }

    private void AddFixConfigSettings()
    {
        LandExploreRadius.SettingChanged += FixConfigBoundaries;
        SeaExploreRadius.SettingChanged += FixConfigBoundaries;
        AltitudeRadiusMultiplier.SettingChanged += FixConfigBoundaries;
        ForestRadiusMultiplier.SettingChanged += FixConfigBoundaries;
        DaylightRadiusMultiplier.SettingChanged += FixConfigBoundaries;
        WeatherRadiusMultiplier.SettingChanged += FixConfigBoundaries;
        DisplayCurrentRadiusValue.SettingChanged += DisplayRadiusValueSettingChanged;
        DisplayVariables.SettingChanged += DisplayVariablesValueSettingChanged;
        DisplayDebug.SettingChanged += DisplayDebugSettingChanged;
    }

    private void FixConfigBoundaries(object _, EventArgs __)
    {
        if (LandExploreRadius.Value < 0.0f) LandExploreRadius.Value = 0.0f;
        if (LandExploreRadius.Value > 1000.0f) LandExploreRadius.Value = 1000.0f;
        if (SeaExploreRadius.Value < 0.0f) SeaExploreRadius.Value = 0.0f;
        if (SeaExploreRadius.Value > 1000.0f) SeaExploreRadius.Value = 1000.0f;
        if (AltitudeRadiusMultiplier.Value < 0.0f) AltitudeRadiusMultiplier.Value = 0.0f;
        if (AltitudeRadiusMultiplier.Value > 2.0f) AltitudeRadiusMultiplier.Value = 2.0f;
        if (ForestRadiusMultiplier.Value < 0.0f) ForestRadiusMultiplier.Value = 0.0f;
        if (ForestRadiusMultiplier.Value > 1.0f) ForestRadiusMultiplier.Value = 1.0f;
        if (DaylightRadiusMultiplier.Value < 0.0f) DaylightRadiusMultiplier.Value = 0.0f;
        if (DaylightRadiusMultiplier.Value > 1.0f) DaylightRadiusMultiplier.Value = 1.0f;
        if (WeatherRadiusMultiplier.Value < 0.0f) WeatherRadiusMultiplier.Value = 0.0f;
        if (WeatherRadiusMultiplier.Value > 1.0f) WeatherRadiusMultiplier.Value = 1.0f;
    }
    
    private void DisplayRadiusValueSettingChanged(object _, EventArgs __)
    {
        if (HUDPatches.RadiusHudText != null)
        {
            HUDPatches.RadiusHudText.gameObject.SetActive(DisplayCurrentRadiusValue.Value);
            if (!DisplayCurrentRadiusValue.Value)
            {
                HUDPatches.RadiusHudText.text = string.Empty;
            }
        }
    }

    private void DisplayVariablesValueSettingChanged(object _, EventArgs __)
    {
        if (HUDPatches.VariablesHudText != null)
        {
            HUDPatches.VariablesHudText.gameObject.SetActive(DisplayVariables.Value);
            if (!DisplayVariables.Value)
            {
                HUDPatches.VariablesHudText.text = string.Empty;
            }
        }
    }
    
    private void DisplayDebugSettingChanged(object _, EventArgs __)
    {
        if (HUDPatches.DebugText != null)
        {
            HUDPatches.DebugText.gameObject.SetActive(DisplayDebug.Value);
            if (!DisplayDebug.Value)
            {
                HUDPatches.DebugText.text = string.Empty;
            }
        }
    }

    private void OnDestroy()
    {
        _harmony.UnpatchSelf();
    }

    private ConfigEntry<T> CreateConfig<T>(string group, string parameterName, T value,
        ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigEntry<T> configEntry = Config.Bind(group, parameterName, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> CreateConfig<T>(string group, string parameterName, T value, string description,
        bool synchronizedSetting = true) => CreateConfig(group, parameterName, value,
        new ConfigDescription(description), synchronizedSetting);
}