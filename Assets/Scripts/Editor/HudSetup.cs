using Lightbringer.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Lightbringer.EditorTools
{
    // Creates the UI Toolkit PanelSettings for the HUD (once) and assigns HUD assets to the campaign scene.
    [InitializeOnLoad]
    public static class HudSetup
    {
        public const string ThemePath = "Assets/UI/LightbringerTheme.tss";

        static HudSetup() => EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EnsureAssets();
        };

        [MenuItem("Lightbringer/UI/Build HUD Assets")]
        private static void Build()
        {
            if (EnsureAssets() != null) AssignToOpenScenes();
        }

        public static PanelSettings EnsureAssets()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(CampaignHUD.PanelSettingsPath);
            if (settings != null) return settings;
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) { Debug.LogError("Missing " + ThemePath); return null; }
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            // Desktop HUD authored at 1080p; scales with the window, never with DPI tricks.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 10;
            AssetDatabase.CreateAsset(settings, CampaignHUD.PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        // Fills empty HUD asset fields on every CampaignHUD in the open scenes and saves those scenes.
        public static void AssignToOpenScenes()
        {
            PanelSettings settings = EnsureAssets();
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(CampaignHUD.StyleSheetPath);
            if (settings == null || style == null) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (CampaignHUD hud in root.GetComponentsInChildren<CampaignHUD>(true))
                    {
                        SerializedObject serialized = new SerializedObject(hud);
                        SerializedProperty panel = serialized.FindProperty("panelSettings");
                        SerializedProperty sheet = serialized.FindProperty("styleSheet");
                        if (panel.objectReferenceValue != null && sheet.objectReferenceValue != null) continue;
                        panel.objectReferenceValue = settings;
                        sheet.objectReferenceValue = style;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                if (changed && !string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
    }
}
