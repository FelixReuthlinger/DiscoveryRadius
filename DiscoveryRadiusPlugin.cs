using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DiscoveryRadius.Patches;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace DiscoveryRadius;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public class DiscoveryRadiusPlugin : BaseUnityPlugin
{
    public const string ModName = "DiscoveryRadius";
    public const string ModVersion = "1.1.0";
    private const string ModAuthor = "FixItFelix";
    private const string ModGuid = ModAuthor + "." + ModName;

    private readonly Harmony _harmony = new(ModGuid);

    [UsedImplicitly] public static readonly ManualLogSource ModTemplateLogger =
        BepInEx.Logging.Logger.CreateLogSource(ModGuid);

    private static readonly ConfigSync ConfigSync = new(ModGuid)
        { DisplayName = ModName, CurrentVersion = ModVersion };

    private static ConfigEntry<bool> _configLocked = null!;

    public static ConfigEntry<float> MinimumRadius = null!;
    internal static readonly float MinimumRadiusMin = 10.0f;
    private static readonly float MinimumRadiusMax = 200.0f;
    private static readonly float MinimumRadiusDefault = 35.0f;
    public static ConfigEntry<float> MaximumRadius = null!;
    private static readonly float MaximumRadiusMin = 200.0f;
    private static readonly float MaximumRadiusMax = 1000.0f;
    private static readonly float MaximumRadiusDefault = 1000.0f;
    public static ConfigEntry<float> LandExploreRadius = null!;
    private static readonly float LandExploreRadiusMin = 0.0f;
    private static readonly float LandExploreRadiusMax = 1000.0f;
    private static readonly float LandExploreRadiusDefault = 150.0f;
    public static ConfigEntry<float> SeaExploreRadius = null!;
    private static readonly float SeaExploreRadiusMin = 0.0f;
    private static readonly float SeaExploreRadiusMax = 1000.0f;
    private static readonly float SeaExploreRadiusDefault = 200.0f;


    public static ConfigEntry<float> AltitudeRadiusMultiplier = null!;
    private static readonly float AltitudeRadiusMultiplierMin = 0.0f;
    private static readonly float AltitudeRadiusMultiplierMax = 2.0f;
    private static readonly float AltitudeRadiusMultiplierDefault = 1.5f;
    public static ConfigEntry<float> ForestRadiusMultiplier = null!;
    private static readonly float ForestRadiusMultiplierMin = 0.0f;
    private static readonly float ForestRadiusMultiplierMax = 1.0f;
    private static readonly float ForestRadiusMultiplierDefault = 1.0f;
    public static ConfigEntry<float> DaylightRadiusMultiplier = null!;
    private static readonly float DaylightRadiusMultiplierMin = 0.0f;
    private static readonly float DaylightRadiusMultiplierMax = 1.0f;
    private static readonly float DaylightRadiusMultiplierDefault = 0.3f;
    public static ConfigEntry<float> WeatherRadiusMultiplier = null!;
    private static readonly float WeatherRadiusMultiplierMin = 0.0f;
    private static readonly float WeatherRadiusMultiplierMax = 1.0f;
    private static readonly float WeatherRadiusMultiplierDefault = 0.3f;

    public static ConfigEntry<bool> DisplayCurrentRadiusValue = null!;
    public static ConfigEntry<bool> DisplayVariables = null!;
    public static ConfigEntry<bool> DisplayDebug = null!;

    private void Awake()
    {
        string generalGroup = "1 - General";
        _configLocked = CreateConfig(generalGroup, "Lock Configuration", true,
            "If 'true' and playing on a server, config can only be changed on server-side configuration, " +
            "clients cannot override");
        ConfigSync.AddLockingConfigEntry(_configLocked);

        string explorationRadiusGroup = "2 - Exploration Radius";
        MinimumRadius = CreateConfig(explorationRadiusGroup, "Minimum exploration radius", MinimumRadiusDefault,
            "The absolute minimum exploration Radius. However you set all the other values, this value will " +
            "ALWAYS be the minimum radius for exploration. " +
            $"Accepted range {MinimumRadiusMin} to {MinimumRadiusMax}. Default is {MinimumRadiusDefault}.");
        MaximumRadius = CreateConfig(explorationRadiusGroup, "Maximum exploration radius", MaximumRadiusDefault,
            "The absolute maximum exploration Radius. However you set all the other values, this value will " +
            "ALWAYS be the maximum radius for exploration. " +
            $"Accepted range {MaximumRadiusMin} to {MaximumRadiusMax}. Default is {MaximumRadiusDefault}.");
        LandExploreRadius = CreateConfig(explorationRadiusGroup, "Land exploration radius", LandExploreRadiusDefault,
            "The radius around the player to uncover while travelling on land near sea level. " +
            "Higher values may cause performance issues. " +
            $"Accepted range {LandExploreRadiusMin} to {LandExploreRadiusMax}. Default is {LandExploreRadiusDefault}.");
        SeaExploreRadius = CreateConfig(explorationRadiusGroup, "Sea exploration radius", SeaExploreRadiusDefault,
            "The radius around the player to uncover while travelling on a boat. " +
            "Higher values may cause performance issues. " +
            $"Accepted range {SeaExploreRadiusMin} to {SeaExploreRadiusMax}. Default is {SeaExploreRadiusDefault}.");


        string explorationRadiusMultipliersGroup = "3 - Exploration Radius Multipliers";
        AltitudeRadiusMultiplier = CreateConfig(explorationRadiusMultipliersGroup, "Altitude radius multiplier",
            AltitudeRadiusMultiplierDefault,
            "Multiplier (float) to apply to land exploration radius based on altitude. " +
            "For every 100 units above sea level (smooth scale), add this value multiplied by " +
            "Land exploration radius to the total. " +
            $"Accepted range {AltitudeRadiusMultiplierMin} to {AltitudeRadiusMultiplierMax}. Set to 0 to disable. " +
            $"Default {AltitudeRadiusMultiplierDefault}.");
        ForestRadiusMultiplier = CreateConfig(explorationRadiusMultipliersGroup, "Forest radius multiplier",
            ForestRadiusMultiplierDefault,
            "Multiplier (float) to apply to land exploration radius when in a forest (black forest, " +
            "forested parts of meadows or swamp). This value is multiplied by the base land exploration " +
            "radius and subtracted from the total. " +
            $"Accepted range {ForestRadiusMultiplierMin} to {ForestRadiusMultiplierMax}. Set to 0 to disable. " +
            $"Default {ForestRadiusMultiplierDefault}.");
        DaylightRadiusMultiplier = CreateConfig(explorationRadiusMultipliersGroup, "Daylight radius multiplier",
            DaylightRadiusMultiplierDefault,
            "Multiplier (float) that influences how much daylight (directional and ambient light) affects " +
            "exploration radius. This value is multiplied by the base land or sea exploration radius and added " +
            $"to the total. Accepted range {DaylightRadiusMultiplierMin} to {DaylightRadiusMultiplierMax}. " +
            $"Set to 0 to disable. Default {DaylightRadiusMultiplierDefault}.");
        WeatherRadiusMultiplier = CreateConfig(explorationRadiusMultipliersGroup, "Weather radius multiplier",
            WeatherRadiusMultiplierDefault,
            "Multiplier (float) that influences how much the current weather affects exploration radius. " +
            "This value is multiplied by the base land or sea exploration radius and added to the total. " +
            $"Accepted range {WeatherRadiusMultiplierMin} to {WeatherRadiusMultiplierMax}. Set to 0 to disable. " +
            $"Default {WeatherRadiusMultiplierDefault}.");

        string miscGroup = "4 - Miscellaneous";
        DisplayCurrentRadiusValue = CreateConfig(miscGroup, "Display current radius", false,
            "Enabling this will display the currently computed exploration radius in the bottom " +
            "left of the in-game Hud. Useful if you are trying to tweak config values and want to see the result.");
        DisplayVariables = CreateConfig(miscGroup, "Display current variables", false,
            "Enabling this will display on the Hud the values of various variables that go into " +
            "calculating the exploration radius. Mostly useful for debugging and tweaking the config.");
        DisplayDebug = CreateConfig(miscGroup, "Display debug text", false,
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
        MinimumRadius.Value = Mathf.Clamp(MinimumRadius.Value, MinimumRadiusMin, MinimumRadiusMax);
        MaximumRadius.Value = Mathf.Clamp(MaximumRadius.Value, MaximumRadiusMin, MaximumRadiusMax);
        LandExploreRadius.Value = Mathf.Clamp(LandExploreRadius.Value, LandExploreRadiusMin, LandExploreRadiusMax);
        SeaExploreRadius.Value = Mathf.Clamp(SeaExploreRadius.Value, SeaExploreRadiusMin, SeaExploreRadiusMax);
        AltitudeRadiusMultiplier.Value = Mathf.Clamp(AltitudeRadiusMultiplier.Value, AltitudeRadiusMultiplierMin,
            AltitudeRadiusMultiplierMax);
        ForestRadiusMultiplier.Value = Mathf.Clamp(ForestRadiusMultiplier.Value, ForestRadiusMultiplierMin,
            ForestRadiusMultiplierMax);
        DaylightRadiusMultiplier.Value = Mathf.Clamp(DaylightRadiusMultiplier.Value, DaylightRadiusMultiplierMin,
            DaylightRadiusMultiplierMax);
        WeatherRadiusMultiplier.Value = Mathf.Clamp(WeatherRadiusMultiplier.Value, WeatherRadiusMultiplierMin,
            WeatherRadiusMultiplierMax);
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