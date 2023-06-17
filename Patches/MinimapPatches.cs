using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace DiscoveryRadius.Patches;

[HarmonyPatch(typeof(Minimap))]
public static class MinimapPatches
{
    private enum TranspilerState
    {
        Searching,
        Checking,
        Finishing
    }

    [HarmonyPatch("UpdateExplore"), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UpdateExplore_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        TranspilerState state = TranspilerState.Searching;

        CodeInstruction previous = null;

        foreach (CodeInstruction instruction in instructions)
        {
            switch (state)
            {
                case TranspilerState.Searching:
                    if (instruction.opcode == OpCodes.Ldarg_0)
                    {
                        previous = instruction;
                        state = TranspilerState.Checking;
                    }
                    else
                    {
                        yield return instruction;
                    }

                    break;
                case TranspilerState.Checking:
                    if (instruction.opcode == OpCodes.Ldfld &&
                        ((FieldInfo)instruction.operand).Name == nameof(Minimap.m_exploreRadius))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_2); // player
                        yield return new CodeInstruction(OpCodes.Call,
                            typeof(MinimapPatches).GetMethod(nameof(GetExploreRadius),
                                BindingFlags.Static | BindingFlags.NonPublic));
                        state = TranspilerState.Finishing;
                    }
                    else
                    {
                        yield return previous;
                        yield return instruction;
                        state = TranspilerState.Searching;
                    }

                    previous = null;
                    break;
                case TranspilerState.Finishing:
                    yield return instruction;
                    break;
            }
        }
    }

    private static float GetExploreRadius(Player player)
    {
        float result;

        if (player.InInterior())
        {
            // In a dungeon. Dungeons are way up high and we dont want to reveal a huge section of the map when entering one.
            // We actually want to reduce the radius since it doesnt make sense to be able to explore the map while in a dungeon
            result = Mathf.Max(DiscoveryRadiusPlugin.LandExploreRadius.Value * 0.2f, 10.0f);

            HUDPatches.RadiusHudText.text = $"Pathfinder: radius={result:0.0}";

            return result;
        }

        float baseRadius;
        float multiplier = 1.0f;

        // Player may not be the one piloting a boat, but should still get the sea radius if they are riding in one that has a pilot.
        // A longship is about 20 units long. 19 is about as far as you could possibly get from a pilot and still be on the boat.
        List<Player> players = new List<Player>();
        Player.GetPlayersInRange(player.transform.position, 21.0f, players);
        if (players.Any(p => p.IsAttachedToShip()))
        {
            baseRadius = DiscoveryRadiusPlugin.SeaExploreRadius.Value;
        }
        else
        {
            baseRadius = DiscoveryRadiusPlugin.LandExploreRadius.Value;
        }

        // Take the higher of directional or ambient light, subtract 1 to turn this into a value we can add to our multiplier
        float light =
            Mathf.Max(GetColorMagnitude(EnvMan.instance.m_dirLight.color * EnvMan.instance.m_dirLight.intensity),
                GetColorMagnitude(RenderSettings.ambientLight));
        multiplier += (light - 1.0f) * DiscoveryRadiusPlugin.DaylightRadiusMultiplier.Value;

        // Account for weather
        float particles = 0.0f;
        foreach (GameObject particleSystem in EnvMan.instance.GetCurrentEnvironment().m_psystems)
        {
            // Certain particle systems heavily obstruct view
            if (particleSystem.name.Equals("Mist", StringComparison.InvariantCultureIgnoreCase))
            {
                particles += 0.5f;
            }

            if (particleSystem.name.Equals("SnowStorm", StringComparison.InvariantCultureIgnoreCase))
            {
                // Snow storm lowers visibility during the day more than at night
                particles += 0.7f * light;
            }
        }

        // Fog density range seems to be 0.001 to 0.15 based on environment data. Multiply by 10 to get a more meaningful range.
        float fog = Mathf.Clamp(RenderSettings.fogDensity * 10.0f + particles, 0.0f, 1.5f);
        multiplier -= fog * DiscoveryRadiusPlugin.WeatherRadiusMultiplier.Value;

        // Sea level = 30, tallest mountains (not including the rare super mountains) seem to be around 220. Stop adding altitude bonus after 400
        float altitude = Mathf.Clamp(player.transform.position.y - ZoneSystem.instance.m_waterLevel, 0.0f, 400.0f);
        float adjustedAltitude = altitude / 100.0f * Mathf.Max(0.05f, 1.0f - particles);
        multiplier += adjustedAltitude * DiscoveryRadiusPlugin.AltitudeRadiusMultiplier.Value;

        // Make adjustments based on biome
        float location = GetLocationModifier(player, adjustedAltitude);
        multiplier += location;

        if (multiplier > 5.0f) multiplier = 5.0f;
        if (multiplier < 0.2f) multiplier = 0.2f;


        {
            Light dirLight = EnvMan.instance.m_dirLight;

            float time = (float)((long)ZNet.instance.GetTimeSeconds() % EnvMan.instance.m_dayLengthSec) /
                         (float)EnvMan.instance.m_dayLengthSec;
            int hours = Mathf.FloorToInt(time * 24.0f);
            int minutes = Mathf.FloorToInt((time * 24.0f - hours) * 60.0f);

            HUDPatches.DebugTextBuilder.Clear();

            HUDPatches.DebugTextBuilder.AppendLine(
                $"env={EnvMan.instance.GetCurrentEnvironment().m_name} ({Localization.instance.Localize(string.Concat("$biome_", EnvMan.instance.GetCurrentBiome().ToString().ToLower()))})");
            HUDPatches.DebugTextBuilder.AppendLine($"time={hours:00}:{minutes:00} ({time:0.000})");
            HUDPatches.DebugTextBuilder.AppendLine(
                $"altitude={player.transform.position.y - ZoneSystem.instance.m_waterLevel:0.00}");
            HUDPatches.DebugTextBuilder.AppendLine(
                $"light={light:0.000} (dir={GetColorMagnitude(EnvMan.instance.m_dirLight.color * EnvMan.instance.m_dirLight.intensity):0.000}, amb={GetColorMagnitude(RenderSettings.ambientLight):0.000})");
            HUDPatches.DebugTextBuilder.AppendLine($"fog={fog:0.000} (raw={RenderSettings.fogDensity:0.0000})");
            HUDPatches.DebugTextBuilder.AppendLine($"particle={particles:0.000}");
            HUDPatches.DebugTextBuilder.AppendLine(
                $"radius={baseRadius * multiplier:0.0} ({baseRadius:0.0} * {multiplier:0.000})");

            HUDPatches.DebugText.text = HUDPatches.DebugTextBuilder.ToString();
        }


        result = Mathf.Clamp(baseRadius * multiplier, 20.0f, 2000.0f);

        if (DiscoveryRadiusPlugin.DisplayVariables.Value)
        {
            const string fmt = "+0.000;-0.000;0.000";
            HUDPatches.VariablesHudText.text =
                $"Pathfinder Variables" +
                $"\nRadius: {result:0.0}" +
                $"\nBase: {baseRadius:0.#}" +
                $"\nMultiplier: {multiplier:0.000}\n" +
                $"\nLight: {((light - 1.0f) * DiscoveryRadiusPlugin.DaylightRadiusMultiplier.Value).ToString(fmt)}" +
                $"\nWeather: {(-fog * DiscoveryRadiusPlugin.AltitudeRadiusMultiplier.Value).ToString(fmt)}" +
                $"\nAltitude: {(adjustedAltitude * DiscoveryRadiusPlugin.AltitudeRadiusMultiplier.Value).ToString(fmt)}" +
                $"\nLocation: {location.ToString(fmt)}";
        }

        if (DiscoveryRadiusPlugin.DisplayCurrentRadiusValue.Value)
        {
            HUDPatches.RadiusHudText.text = $"Pathfinder: radius={result:0.0}";
        }

        return result;
    }

    private static float GetColorMagnitude(Color color)
    {
        // Intentionally ignoring alpha here
        return Mathf.Sqrt(color.r * color.r + color.g * color.g + color.b * color.b);
    }

    private static float GetLocationModifier(Player player, float altitude)
    {
        // Forest thresholds based on logic found in MiniMap.GetMaskColor

        float forestPenalty =
            DiscoveryRadiusPlugin.ForestRadiusMultiplier.Value + altitude *
            DiscoveryRadiusPlugin.AltitudeRadiusMultiplier.Value * DiscoveryRadiusPlugin.ForestRadiusMultiplier.Value;
        switch (player.GetCurrentBiome())
        {
            case Heightmap.Biome.BlackForest:
                // Small extra penalty to account for high daylight values in black forest
                return -forestPenalty - 0.25f * DiscoveryRadiusPlugin.DaylightRadiusMultiplier.Value;
            case Heightmap.Biome.Meadows:
                return WorldGenerator.InForest(player.transform.position) ? -forestPenalty : 0.0f;
            case Heightmap.Biome.Plains:
                // Small extra bonus to account for low daylight values in plains
                return (WorldGenerator.GetForestFactor(player.transform.position) < 0.8f ? -forestPenalty : 0.0f) +
                       0.1f * DiscoveryRadiusPlugin.DaylightRadiusMultiplier.Value;
            default:
                return 0.0f;
        }
    }
}