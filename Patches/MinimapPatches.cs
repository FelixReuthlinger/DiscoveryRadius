using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

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

    [HarmonyPatch(nameof(Minimap.UpdateExplore)), HarmonyTranspiler]
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
                            typeof(DiscoveryLogic).GetMethod(nameof(DiscoveryLogic.GetExploreRadius),
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
}