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
    public const string ModVersion = "1.0.0";
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

    private void Awake()
    {
        _configLocked = CreateConfig("1 - General", "Lock Configuration", true,
            "If 'true' and playing on a server, config can only be changed on server-side configuration, " +
            "clients cannot override");
        ConfigSync.AddLockingConfigEntry(_configLocked);

        LandExploreRadius = CreateConfig("2 - Exploration Radius", "Land exploration radius", 150.0f,
            "The radius around the player to uncover while travelling on land near sea level. " +
            "Higher values may cause performance issues. Max allowed is 2000. Default is 150.");
        SeaExploreRadius = CreateConfig("2 - Exploration Radius", "Sea exploration radius", 200.0f,
            "The radius around the player to uncover while travelling on a boat. " +
            "Higher values may cause performance issues. Max allowed is 2000. Default is 200.");

        AltitudeRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Altitude radius multiplier",
            0.5f,
            "Multiplier to apply to land exploration radius based on altitude. " +
            "For every 100 units above sea level (smooth scale), add this value multiplied by " +
            "LandExploreRadius to the total. For example, with a radius of 200 and a multiplier of " +
            "0.5, radius is 200 at sea level, 250 at 50 altitude, 300 at 100 altitude, 400 at 200 " +
            "altitude, etc. For reference, a typical mountain peak is around 170 altitude. " +
            "Accepted range 0-2. Set to 0 to disable. Default 0.5.");
        ForestRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Forest radius multiplier", 0.3f,
            "Multiplier to apply to land exploration radius when in a forest (black forest, " +
            "forested parts of meadows and plains). This value is multiplied by the base land exploration " +
            "radius and subtracted from the total. Accepted range 0-1. Set to 0 to disable. Default 0.3.");
        DaylightRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Forest radius multiplier", 0.2f,
            "Multiplier that influences how much daylight (directional and ambient light) affects " +
            "exploration radius. This value is multiplied by the base land or sea exploration radius and added " +
            "to the total. Accepted range 0-1. Set to 0 to disable. Default 0.2.");
        WeatherRadiusMultiplier = CreateConfig("3 - Exploration Radius Multipliers", "Weather radius multiplier", 0.3f,
            "Multiplier that influences how much the current weather affects exploration radius. " +
            "This value is multiplied by the base land or sea exploration radius and added to the total. " +
            "Accepted range 0-1. Set to 0 to disable. Default 0.3.");

        DisplayCurrentRadiusValue = CreateConfig("4- Miscellaneous", "Display current radius", false,
            "Enabling this will display the currently computed exploration radius in the bottom " +
            "left of the in-game Hud. Useful if you are trying to tweak config values and want to see the result.");
        DisplayVariables = CreateConfig("4- Miscellaneous", "Display current variables", false,
            "Enabling this will display on the Hud the values of various variables that go into " +
            "calculating the exploration radius. Mostly useful for debugging and tweaking the config.");

        AddFixConfigSettings();
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(typeof(HUDPatches));
        _harmony.PatchAll(typeof(MinimapPatches));
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
    }

    private void FixConfigBoundaries(object _, System.EventArgs __)
    {
        if (LandExploreRadius.Value < 0.0f) LandExploreRadius.Value = 0.0f;
        if (LandExploreRadius.Value > 2000.0f) LandExploreRadius.Value = 2000.0f;
        if (SeaExploreRadius.Value < 0.0f) SeaExploreRadius.Value = 0.0f;
        if (SeaExploreRadius.Value > 2000.0f) SeaExploreRadius.Value = 2000.0f;
        if (AltitudeRadiusMultiplier.Value < 0.0f) AltitudeRadiusMultiplier.Value = 0.0f;
        if (AltitudeRadiusMultiplier.Value > 2.0f) AltitudeRadiusMultiplier.Value = 2.0f;
        if (ForestRadiusMultiplier.Value < 0.0f) ForestRadiusMultiplier.Value = 0.0f;
        if (ForestRadiusMultiplier.Value > 1.0f) ForestRadiusMultiplier.Value = 1.0f;
        if (DaylightRadiusMultiplier.Value < 0.0f) DaylightRadiusMultiplier.Value = 0.0f;
        if (DaylightRadiusMultiplier.Value > 1.0f) DaylightRadiusMultiplier.Value = 1.0f;
        if (WeatherRadiusMultiplier.Value < 0.0f) WeatherRadiusMultiplier.Value = 0.0f;
        if (WeatherRadiusMultiplier.Value > 1.0f) WeatherRadiusMultiplier.Value = 1.0f;
    }
    
    private void DisplayRadiusValueSettingChanged(object _, System.EventArgs __)
    {
        HUDPatches.RadiusHudText.gameObject.SetActive(DisplayCurrentRadiusValue.Value);
        if (!DisplayCurrentRadiusValue.Value)
        {
            HUDPatches.RadiusHudText.text = string.Empty;
        }
    }

    private void DisplayVariablesValueSettingChanged(object _, System.EventArgs __)
    {
        HUDPatches.VariablesHudText.gameObject.SetActive(DisplayVariables.Value);
        if (!DisplayVariables.Value)
        {
            HUDPatches.VariablesHudText.text = string.Empty;
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