using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoveryRadius.Patches;

[HarmonyPatch(typeof(Hud))]
public static class HUDPatches
{
    public static StringBuilder DebugTextBuilder;
    
    public static Text RadiusHudText;
    public static Text VariablesHudText;
    public static Text DebugText;
    
    [HarmonyPatch("Awake"), Harmony, HarmonyPostfix]
    private static void Awake_Postfix(Hud __instance)
    {
        {
            GameObject textObject = new GameObject("Pathfinder_RadiusText");
            textObject.AddComponent<CanvasRenderer>();
            textObject.transform.localPosition = Vector3.zero;

            RectTransform transform = textObject.AddComponent<RectTransform>();
            transform.SetParent(__instance.m_rootObject.transform);
            transform.pivot = transform.anchorMin = transform.anchorMax = new Vector2(0.0f, 0.0f);
            transform.offsetMin = new Vector2(10.0f, 5.0f);
            transform.offsetMax = new Vector2(210.0f, 165.0f);

            RadiusHudText = textObject.AddComponent<Text>();
            RadiusHudText.raycastTarget = false;
            RadiusHudText.font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica", "Arial" }, 12);
            RadiusHudText.fontStyle = FontStyle.Bold;
            RadiusHudText.color = Color.white;
            RadiusHudText.fontSize = 12;
            RadiusHudText.alignment = TextAnchor.LowerLeft;

            Outline textOutline = textObject.AddComponent<Outline>();
            textOutline.effectColor = Color.black;

            textObject.SetActive(DiscoveryRadiusPlugin.DisplayCurrentRadiusValue.Value);
        }

        {
            GameObject textObject = new GameObject("Pathfinder_VariableText");
            textObject.AddComponent<CanvasRenderer>();
            textObject.transform.localPosition = Vector3.zero;

            RectTransform transform = textObject.AddComponent<RectTransform>();
            transform.SetParent(__instance.m_rootObject.transform);
            transform.pivot = transform.anchorMin = transform.anchorMax = new Vector2(0.0f, 0.0f);
            transform.offsetMin = new Vector2(240.0f, 5.0f);
            transform.offsetMax = new Vector2(440.0f, 165.0f);

            VariablesHudText = textObject.AddComponent<Text>();
            VariablesHudText.raycastTarget = false;
            VariablesHudText.font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica", "Arial" }, 12);
            VariablesHudText.fontStyle = FontStyle.Bold;
            VariablesHudText.color = Color.white;
            VariablesHudText.fontSize = 12;
            VariablesHudText.alignment = TextAnchor.LowerLeft;

            Outline textOutline = textObject.AddComponent<Outline>();
            textOutline.effectColor = Color.black;

            textObject.SetActive(DiscoveryRadiusPlugin.DisplayVariables.Value);
        }
    }
    
    [HarmonyPatch("Awake"), Harmony, HarmonyPostfix]
    private static void Awake_Postfix(Hud __instance)
    {
        GameObject debugTextObject = new GameObject("DebugText");
        debugTextObject.AddComponent<CanvasRenderer>();
        debugTextObject.transform.localPosition = Vector3.zero;

        RectTransform transform = debugTextObject.AddComponent<RectTransform>();
        transform.SetParent(__instance.m_rootObject.transform);
        transform.pivot = transform.anchorMin = transform.anchorMax = new Vector2(0.5f, 1.0f);
        transform.offsetMin = new Vector2(-300.0f, -500.0f);
        transform.offsetMax = new Vector2(300.0f, -100.0f);

        DebugText = debugTextObject.AddComponent<Text>();
        DebugText.raycastTarget = false;
        DebugText.font = Font.CreateDynamicFontFromOSFont("Courier New", 14);
        DebugText.fontStyle = FontStyle.Bold;
        DebugText.color = Color.white;
        DebugText.fontSize = 14;
        DebugText.alignment = TextAnchor.UpperLeft;

        Outline debugTextOutline = debugTextObject.AddComponent<Outline>();
        debugTextOutline.effectColor = Color.black;
    }
}
