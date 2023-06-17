using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace DiscoveryRadius.Patches
{
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
        private static IEnumerable<CodeInstruction?> UpdateExplore_Transpiler(
            IEnumerable<CodeInstruction?> instructions)
        {
            TranspilerState state = TranspilerState.Searching;

            CodeInstruction? previous = null;

            foreach (CodeInstruction? instruction in instructions)
            {
                switch (state)
                {
                    case TranspilerState.Searching:
                        if (instruction != null && instruction.opcode == OpCodes.Ldarg_0)
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
                        if (instruction != null &&
                            instruction.opcode == OpCodes.Ldfld &&
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
            float baseRadius = NearPlayerInShip(player)
                ? DiscoveryRadiusPlugin.SeaExploreRadius.Value
                : DiscoveryRadiusPlugin.LandExploreRadius.Value;

            float result;

            if (player.InInterior())
            {
                /*
                 * In a dungeon: dungeons are way up high and we dont want to reveal a huge section of the map when
                 * entering one. We actually want to reduce the radius since it doesnt make sense to be able to
                 * explore the map while in a dungeon.
                 */
                result = 10.0f;
            }
            else
            {
                // light will increase the sight range
                var radiusMultiplierLighting =
                    GetEnvironmentalLight() * DiscoveryRadiusPlugin.DaylightRadiusMultiplier.Value;

                // fog and other particles will reduce the sight range
                var radiusMultiplierSightRange =
                    -1.0f * Mathf.Clamp(RenderSettings.fogDensity * 10.0f + GetParticlesInEnvironment(), 0.0f, 1.5f) *
                    DiscoveryRadiusPlugin.WeatherRadiusMultiplier.Value;

                // altitude will increase the sight range
                var radiusMultiplierAltitude = GetPlayerAltitude(player) / 100.0f *
                                               DiscoveryRadiusPlugin.AltitudeRadiusMultiplier.Value;

                // biomes with many trees and being inside the forest will decrease radius
                var radiusMultiplierBiome = GetLocationModifier(player);

                var radiusTotalMultiplier = radiusMultiplierLighting +
                                            radiusMultiplierSightRange +
                                            radiusMultiplierAltitude +
                                            radiusMultiplierBiome;

                result = Mathf.Clamp(baseRadius * radiusTotalMultiplier, 20.0f, 2000.0f);

                if (DiscoveryRadiusPlugin.DisplayVariables.Value)
                {
                    if (HUDPatches.VariablesHudText != null)
                        HUDPatches.VariablesHudText.text =
                            $"Discovery radius variables" +
                            $"\nRadius: {result:0.0}" +
                            $"\nBase: {baseRadius:0.0}" +
                            $"\nTotal multiplier (sum): {radiusTotalMultiplier:0.00}" +
                            $"\nLighting multiplier: {radiusMultiplierLighting:0.0}" +
                            $"\nSight range multiplier: {radiusMultiplierSightRange:0.0}" +
                            $"\nAltitude multiplier: {radiusMultiplierAltitude:0.0}" +
                            $"\nBiome multiplier: {radiusMultiplierBiome}";
                }

                if (DiscoveryRadiusPlugin.DisplayDebug.Value && HUDPatches.DebugText != null)
                {
                    float time = (long)ZNet.instance.GetTimeSeconds() % EnvMan.instance.m_dayLengthSec /
                                 (float)EnvMan.instance.m_dayLengthSec;
                    int hours = Mathf.FloorToInt(time * 24.0f);
                    int minutes = Mathf.FloorToInt((time * 24.0f - hours) * 60.0f);

                    HUDPatches.DebugText.text = "Discovery radius debug:" +
                                                $"\ntime={hours:00}:{minutes:00} ({time:0.000})" +
                                                $"\nplayer on ship={NearPlayerInShip(player)}" +
                                                $"\naltitude={GetPlayerAltitude(player)}";
                }
            }

            HUDPatches.SetDiscoveryRadius(result);
            return result;
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
                    forestPenalty = 0.8f;
                    break;
                case Heightmap.Biome.BlackForest:
                    forestPenalty = WorldGenerator.InForest(player.transform.position) ? 0.6f : 0.3f;
                    break;
                case Heightmap.Biome.Meadows:
                    forestPenalty = WorldGenerator.InForest(player.transform.position) ? 0.4f : 0.2f;
                    break;
            }

            return -1.0f * forestPenalty * DiscoveryRadiusPlugin.ForestRadiusMultiplier.Value;
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
}