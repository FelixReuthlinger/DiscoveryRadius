using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoveryRadius.Patches
{
    [HarmonyPatch(typeof(Hud))]
    public static class HUDPatches
    {
        public static Text? RadiusHudText;
        public static Text? VariablesHudText;
        public static Text? DebugText;

        [HarmonyPatch("Awake"), Harmony, HarmonyPostfix]
        // ReSharper disable once InconsistentNaming
        private static void AwakePostfix(Hud __instance)
        {
            GameObject radiusTextObject = new GameObject("DiscoveryRadiusText");
            RadiusHudText = SetUpTextObject(radiusTextObject);
            if (radiusTextObject.TryGetComponent(out RectTransform radiusTransform))
            {
                radiusTransform.SetParent(__instance.m_rootObject.transform);
                radiusTransform.pivot = radiusTransform.anchorMin = radiusTransform.anchorMax = new Vector2(0.0f, 0.0f);
                radiusTransform.offsetMin = new Vector2(10.0f, 5.0f);
                radiusTransform.offsetMax = new Vector2(210.0f, 165.0f);
            }
            radiusTextObject.SetActive(DiscoveryRadiusPlugin.DisplayCurrentRadiusValue.Value);

            GameObject variablesTextObject = new GameObject("DiscoveryVariableText");
            VariablesHudText = SetUpTextObject(variablesTextObject);
            if (variablesTextObject.TryGetComponent(out RectTransform variablesTransform))
            {
                variablesTransform.SetParent(__instance.m_rootObject.transform);
                variablesTransform.pivot =
                    variablesTransform.anchorMin = variablesTransform.anchorMax = new Vector2(0.0f, 0.0f);
                variablesTransform.offsetMin = new Vector2(240.0f, 5.0f);
                variablesTransform.offsetMax = new Vector2(440.0f, 165.0f);
            }
            variablesTextObject.SetActive(DiscoveryRadiusPlugin.DisplayVariables.Value);

            GameObject debugTextObject = new GameObject("DiscoveryDebugText");
            DebugText = SetUpTextObject(debugTextObject);
            if (debugTextObject.TryGetComponent(out RectTransform debugTransform))
            {
                debugTransform.SetParent(__instance.m_rootObject.transform);
                debugTransform.pivot = debugTransform.anchorMin = debugTransform.anchorMax = new Vector2(0.5f, 1.0f);
                debugTransform.offsetMin = new Vector2(-300.0f, -500.0f);
                debugTransform.offsetMax = new Vector2(300.0f, -100.0f);
            }
            debugTextObject.SetActive(DiscoveryRadiusPlugin.DisplayVariables.Value);
        }

        private static Text SetUpTextObject(GameObject textObject)
        {
            textObject.AddComponent<CanvasRenderer>();
            textObject.transform.localPosition = Vector3.zero;

            Text result = textObject.AddComponent<Text>();
            result.raycastTarget = false;
            result.font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Helvetica", "Arial" }, 12);
            result.fontStyle = FontStyle.Bold;
            result.color = Color.white;
            result.fontSize = 12;
            result.alignment = TextAnchor.UpperLeft;

            Outline debugTextOutline = textObject.AddComponent<Outline>();
            debugTextOutline.effectColor = Color.black;

            return result;
        }

        public static void SetDiscoveryRadius(float radius)
        {
            if (DiscoveryRadiusPlugin.DisplayCurrentRadiusValue.Value && RadiusHudText != null)
                RadiusHudText.text = $"Discovery radius: radius={radius:0.0}";
        }
    }
}