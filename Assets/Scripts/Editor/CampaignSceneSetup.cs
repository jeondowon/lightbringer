using Lightbringer.Core;
using Lightbringer.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    public static class CampaignSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/CampaignPrototype.unity";

        public static bool EnsureScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return true;
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Shader aura = Shader.Find("Universal Render Pipeline/Unlit");
            if (actions == null || shader == null || aura == null) return false;
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            const string materialPath = "Assets/Materials/PrototypeCampaign.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Prototype Campaign" };
                material.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Scene original = SceneManager.GetActiveScene();
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(temporary);
                GameObject root = new GameObject("Lightbringer Campaign");
                root.AddComponent<CampaignSession>().Configure(actions, material, aura);
                root.AddComponent<CampaignHUD>();
                return EditorSceneManager.SaveScene(temporary, ScenePath);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(temporary, true);
            }
        }

        [MenuItem("Lightbringer/Prepare and Play Campaign")]
        private static void PrepareAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !GreyboxValidation.RunChecks() || !EnsureScene()) return;
            ArtStyleSetup.EnsureFirstRunStyle();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }
    }
}
