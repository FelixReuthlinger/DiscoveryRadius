using System;
using System.Collections.Generic;
using System.Linq;
using DiscoveryRadius.Patches;
using UnityEngine;

namespace DiscoveryRadius;

public static class DiscoveryLogic
{
    internal static float GetExploreRadius(Player player)
    {
        float baseRadius = NearPlayerInShip(player)
            ? DiscoveryRadiusPlugin.SeaExploreRadius.Value
            : DiscoveryRadiusPlugin.LandExploreRadius.Value;

        // default value will be discovery radius in a dungeon, building, etc.
        float result = DiscoveryRadiusPlugin.MinimumRadiusMin;
        string hudDetailsText = "";

        if (!player.InInterior())
        {
            // not in a dungeon, building, etc.

            // light will increase the sight range
            var radiusMultiplierLighting =
                GetEnvironmentalLight() * DiscoveryRadiusPlugin.DaylightRadiusMultiplier.Value;

            // fog and other particles will reduce the sight range
            var radiusMultiplierWeather =
                -1.0f * Mathf.Clamp(RenderSettings.fogDensity * 10.0f + GetParticlesInEnvironment(), 0.0f, 1.5f) *
                DiscoveryRadiusPlugin.WeatherRadiusMultiplier.Value;

            // altitude will increase the sight range
            var radiusMultiplierAltitude = GetPlayerAltitude(player) / 100.0f *
                                           DiscoveryRadiusPlugin.AltitudeRadiusMultiplier.Value;

            // biomes with many trees and being inside the forest will decrease radius
            var radiusMultiplierBiome = GetLocationModifier(player);

            var radiusTotalMultiplier = radiusMultiplierLighting +
                                        radiusMultiplierWeather +
                                        radiusMultiplierAltitude +
                                        radiusMultiplierBiome;

            result = Mathf.Clamp(baseRadius * radiusTotalMultiplier, DiscoveryRadiusPlugin.MinimumRadius.Value,
                DiscoveryRadiusPlugin.MaximumRadius.Value);

            hudDetailsText = $"\nTotal multiplier (sum): {radiusTotalMultiplier:0.00}" +
                             $"\nLighting multiplier: {radiusMultiplierLighting:0.0}" +
                             $"\nWeather multiplier: {radiusMultiplierWeather:0.0}" +
                             $"\nAltitude multiplier: {radiusMultiplierAltitude:0.0}" +
                             $"\nBiome multiplier: {radiusMultiplierBiome:0.0}";
        }

        if (DiscoveryRadiusPlugin.DisplayVariables.Value) DisplayHUD(player, baseRadius, result, hudDetailsText);

        HUDPatches.SetDiscoveryRadius(result);
        return result;
    }

    private static void DisplayHUD(Player player, float baseRadius, float result, string hudText)
    {
        if (HUDPatches.VariablesHudText != null)
            HUDPatches.VariablesHudText.text =
                $"Discovery radius variables" +
                $"\nRadius: {result:0.0}" +
                $"\nBase: {baseRadius:0.0}" +
                hudText;

        if (HUDPatches.DebugText != null)
        {
            float time = (long)ZNet.instance.GetTimeSeconds() % EnvMan.instance.m_dayLengthSec /
                         (float)EnvMan.instance.m_dayLengthSec;
            int hours = Mathf.FloorToInt(time * 24.0f);
            int minutes = Mathf.FloorToInt((time * 24.0f - hours) * 60.0f);

            HUDPatches.DebugText.text = "Discovery radius debug:" +
                                        $"\ntime={hours:00}:{minutes:00} ({time:0.000})" +
                                        $"\nplayer on ship={NearPlayerInShip(player)}" +
                                        $"\naltitude={GetPlayerAltitude(player):0.0}";
        }
    }

    /**
     * Depending on the biome the player is currently at, the sight range is decreased by trees blocking sight.
     */
    private static float GetLocationModifier(Player player)
    {
        float forestPenalty = 0.0f;
        switch (player.GetCurrentBiome())
        {
            case Heightmap.Biome.Swamp:
                forestPenalty = WorldGenerator.InForest(player.transform.position) ? -0.8f : 0.1f;
                break;
            case Heightmap.Biome.BlackForest:
                forestPenalty = WorldGenerator.InForest(player.transform.position) ? -0.6f : 0.2f;
                break;
            case Heightmap.Biome.Meadows:
                forestPenalty = WorldGenerator.InForest(player.transform.position) ? -0.4f : 0.3f;
                break;
        }

        return forestPenalty * DiscoveryRadiusPlugin.ForestRadiusMultiplier.Value;
    }

    /**
     * Player may not be the one piloting a boat, but should still get the sea radius if they are riding in one
     * that has a pilot.
     */
    private static bool NearPlayerInShip(Player player, float detectionRadius = 30.0f)
    {
        List<Player> players = new List<Player>();
        Player.GetPlayersInRange(player.transform.position, detectionRadius, players);
        return players.Any(p => p.IsAttachedToShip());
    }

    /**
     * Take the max of either directional or ambient light. Subtract 1 to turn this into a value we can add to our
     * multiplier.
     */
    private static float GetEnvironmentalLight()
    {
        return Mathf.Max(
            GetColorMagnitude(EnvMan.instance.m_dirLight.color * EnvMan.instance.m_dirLight.intensity),
            GetColorMagnitude(RenderSettings.ambientLight)) - 1.0f;
    }

    private static float GetColorMagnitude(Color color)
    {
        // Intentionally ignoring alpha here
        return Mathf.Sqrt(color.r * color.r + color.g * color.g + color.b * color.b);
    }

    private static float GetParticlesInEnvironment()
    {
        float result = 0.0f;
        foreach (GameObject particleSystem in EnvMan.instance.GetCurrentEnvironment().m_psystems)
        {
            if (particleSystem.name.IndexOf("mist", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result += 0.5f;
            }
            else if (particleSystem.name.IndexOf("storm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result += 0.3f;
            }
            else if (particleSystem.name.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result += 0.2f;
            }
            else if (particleSystem.name.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result += 0.7f;
            }
        }

        return result;
    }

    private static float GetPlayerAltitude(Player player)
    {
        return Mathf.Clamp(player.transform.position.y - ZoneSystem.instance.m_waterLevel, 0.0f,
            1000.0f);
    }
}