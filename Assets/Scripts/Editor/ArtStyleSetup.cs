using Lightbringer.Core;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Creates the Art Pass style assets once (never overwriting tuned values) and assigns them to the
    // campaign scene. Delete an asset to regenerate it with defaults.
    public static class ArtStyleSetup
    {
        public const string Folder = "Assets/Art";
        public const string MaterialFolder = "Assets/Art/Materials";
        public const string LibraryPath = "Assets/Art/ArtStyleLibrary.asset";
        public const string ToonShader = "Lightbringer/Toon";
        public const string RuneShader = "Lightbringer/Aura Runes";
        public const string GlowShader = "Lightbringer/Glow Particle";
        public const string LightningShader = "Lightbringer/Lightning";
        public const string SkyShader = "Lightbringer/Sky Gradient";
        public const string TerrainShader = "Lightbringer/Terrain";
        public const string GrassShader = "Lightbringer/Grass";

        [MenuItem("Lightbringer/Art/Build Art Style Assets")]
        private static void BuildMenu()
        {
            ArtStyleLibrary library = EnsureLibrary();
            if (library == null) return;
            bool assigned = AssignToCampaignScene(library);
            Selection.activeObject = library;
            Debug.Log(assigned
                ? "Art style assets ready and assigned to " + CampaignSceneSetup.ScenePath + "."
                : "Art style assets ready. Open " + CampaignSceneSetup.ScenePath + " and run this again to assign them.");
        }

        [MenuItem("Lightbringer/Art/Build Art Style Assets", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        public static ArtStyleLibrary EnsureLibrary()
        {
            Shader toon = Shader.Find(ToonShader), runes = Shader.Find(RuneShader), glow = Shader.Find(GlowShader);
            if (toon == null || runes == null || glow == null)
            {
                Debug.LogError("Lightbringer art shaders are missing or failed to compile. Check Assets/Art/Shaders in the Console.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(Folder, "Materials");
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ArtStyleLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.alliedMaterial = library.alliedMaterial != null ? library.alliedMaterial : LoadOrCreate("LB_Allied", toon, ConfigureAllied);
            library.enemyMaterial = library.enemyMaterial != null ? library.enemyMaterial : LoadOrCreate("LB_Enemy", toon, ConfigureEnemy);
            library.groundMaterial = library.groundMaterial != null ? library.groundMaterial : LoadOrCreate("LB_Ground", toon, ConfigureGround);
            library.auraRunesMaterial = library.auraRunesMaterial != null ? library.auraRunesMaterial : LoadOrCreate("LB_AuraRunes", runes, m => m.SetColor("_Color", library.auraColor));
            library.auraMotesMaterial = library.auraMotesMaterial != null ? library.auraMotesMaterial : LoadOrCreate("LB_AuraMotes", glow, m => m.SetColor("_Color", new Color(0.7f, 0.85f, 1.5f)));
            Shader lightning = Shader.Find(LightningShader);
            if (library.lightningMaterial == null && lightning != null) library.lightningMaterial = LoadOrCreate("LB_Lightning", lightning, null);
            Shader terrain = Shader.Find(TerrainShader), grass = Shader.Find(GrassShader);
            if (library.terrainMaterial == null && terrain != null) library.terrainMaterial = LoadOrCreate("LB_EnvTerrain", terrain, null);
            // Earlier builds made the terrain material with the toon shader; move it to the terrain shader.
            if (library.terrainMaterial != null && terrain != null && library.terrainMaterial.shader != terrain)
            {
                library.terrainMaterial.shader = terrain;
                EditorUtility.SetDirty(library.terrainMaterial);
            }
            if (library.grassMaterial == null && grass != null) library.grassMaterial = LoadOrCreate("LB_Grass", grass, null);
            if (library.propsMaterial == null) library.propsMaterial = LoadOrCreate("LB_EnvProps", toon, ConfigureProps);
            Shader sky = Shader.Find(SkyShader);
            if (library.skyMaterial == null && sky != null) library.skyMaterial = LoadOrCreate("LB_Sky", sky, null);
            // v2 grading: more contrast and saturation, cool shadows / warm highlights, gentler bloom. Replaces only
            // the original v1 default; a profile assigned by hand is left alone.
            string v1 = Folder + "/LB_BattlePostProcess.asset";
            if (library.postProcessing == null || AssetDatabase.GetAssetPath(library.postProcessing) == v1)
                library.postProcessing = LoadOrCreateGradedProfile(Folder + "/LB_BattlePostProcess_v2.asset");
            // One-time value updates for libraries created by earlier Art Pass steps (tuned values are kept after).
            if (library.styleVersion < 2)
            {
                library.fogStart = 80f;
                library.fogEnd = 260f;
                library.styleVersion = 2;
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        // In-memory copy for validation; nothing is written to the project.
        // First run only: create the style and assign it. Afterwards the scene field is left alone,
        // so clearing it deliberately keeps the greybox capsules for comparison.
        public static void EnsureFirstRunStyle()
        {
            bool existed = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(LibraryPath) != null;
            // Always fill materials added in later Art Pass steps (never overwrites tuned ones).
            ArtStyleLibrary library = EnsureLibrary();
            if (!existed && library != null) AssignToCampaignScene(library);
        }

        public static ArtStyleLibrary CreateTransientLibrary()
        {
            Shader toon = Shader.Find(ToonShader), runes = Shader.Find(RuneShader), glow = Shader.Find(GlowShader);
            if (toon == null || runes == null || glow == null) return null;
            ArtStyleLibrary library = ScriptableObject.CreateInstance<ArtStyleLibrary>();
            library.alliedMaterial = Create(toon, ConfigureAllied);
            library.enemyMaterial = Create(toon, ConfigureEnemy);
            library.groundMaterial = Create(toon, ConfigureGround);
            library.auraRunesMaterial = Create(runes, m => m.SetColor("_Color", library.auraColor));
            library.auraMotesMaterial = Create(glow, null);
            Shader lightning = Shader.Find(LightningShader);
            if (lightning != null) library.lightningMaterial = Create(lightning, null);
            Shader terrain = Shader.Find(TerrainShader), grass = Shader.Find(GrassShader);
            if (terrain != null) library.terrainMaterial = Create(terrain, null);
            if (grass != null) library.grassMaterial = Create(grass, null);
            library.propsMaterial = Create(toon, ConfigureProps);
            Shader sky = Shader.Find(SkyShader);
            if (sky != null) library.skyMaterial = Create(sky, null);
            return library;
        }

        public static void DestroyTransientLibrary(ArtStyleLibrary library)
        {
            if (library == null) return;
            Object.DestroyImmediate(library.alliedMaterial);
            Object.DestroyImmediate(library.enemyMaterial);
            Object.DestroyImmediate(library.groundMaterial);
            Object.DestroyImmediate(library.auraRunesMaterial);
            Object.DestroyImmediate(library.auraMotesMaterial);
            if (library.lightningMaterial != null) Object.DestroyImmediate(library.lightningMaterial);
            if (library.terrainMaterial != null) Object.DestroyImmediate(library.terrainMaterial);
            if (library.grassMaterial != null) Object.DestroyImmediate(library.grassMaterial);
            if (library.propsMaterial != null) Object.DestroyImmediate(library.propsMaterial);
            if (library.skyMaterial != null) Object.DestroyImmediate(library.skyMaterial);
            Object.DestroyImmediate(library);
            SilhouetteFactory.ClearCache();
        }

        private static void ConfigureAllied(Material material)
        {
            material.SetFloat("_UseVertexColor", 1f);
            material.SetFloat("_AuraReact", 1f);
            material.SetColor("_ShadowColor", new Color(0.58f, 0.62f, 0.84f));
            material.SetColor("_OutlineColor", new Color(0.07f, 0.08f, 0.16f));
            material.SetFloat("_OutlineWidth", 0.018f);
            material.SetFloat("_RimStrength", 0.35f);
        }

        private static void ConfigureEnemy(Material material)
        {
            material.SetFloat("_UseVertexColor", 1f);
            material.SetFloat("_AuraReact", 0f);
            material.SetColor("_ShadowColor", new Color(0.42f, 0.36f, 0.55f));
            material.SetColor("_OutlineColor", new Color(0.12f, 0.02f, 0.05f));
            material.SetColor("_RimColor", new Color(1f, 0.45f, 0.6f));
            material.SetFloat("_OutlineWidth", 0.02f);
            material.SetFloat("_RimStrength", 0.3f);
        }

        private static void ConfigureProps(Material material)
        {
            material.SetFloat("_UseVertexColor", 1f);
            material.SetFloat("_AuraReact", 0f);
            material.SetColor("_ShadowColor", new Color(0.58f, 0.64f, 0.82f));
            material.SetColor("_OutlineColor", new Color(0.1f, 0.12f, 0.14f));
            material.SetFloat("_OutlineWidth", 0.035f);
            material.SetFloat("_RimStrength", 0.15f);
            material.SetFloat("_RampSoftness", 0.15f);
            material.SetFloat("_WindSway", 0.12f);
        }

        private static void ConfigureGround(Material material)
        {
            material.SetColor("_BaseColor", new Color(0.47f, 0.57f, 0.4f));
            material.SetColor("_ShadowColor", new Color(0.6f, 0.66f, 0.82f));
            material.SetFloat("_UseVertexColor", 0f);
            material.SetFloat("_OutlineWidth", 0f);
            material.SetFloat("_RimStrength", 0f);
            material.SetFloat("_RampSoftness", 0.3f);
        }

        private static Material Create(Shader shader, System.Action<Material> configure)
        {
            Material material = new Material(shader);
            configure?.Invoke(material);
            return material;
        }

        private static Material LoadOrCreate(string name, Shader shader, System.Action<Material> configure)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = Create(shader, configure);
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static VolumeProfile LoadOrCreateGradedProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.6f);
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments grading = profile.Add<ColorAdjustments>();
            grading.postExposure.Override(0.05f);
            grading.contrast.Override(18f);
            grading.saturation.Override(14f);
            SplitToning split = profile.Add<SplitToning>();
            split.shadows.Override(new Color(0.4f, 0.5f, 0.82f));
            split.highlights.Override(new Color(1f, 0.86f, 0.66f));
            split.balance.Override(-10f);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.22f);
            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile LoadOrCreateProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.65f);
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments grading = profile.Add<ColorAdjustments>();
            grading.postExposure.Override(0.1f);
            grading.contrast.Override(6f);
            grading.saturation.Override(8f);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.18f);
            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // Sets the style on every CampaignSession in the campaign scene and saves it.
        public static bool AssignToCampaignScene(ArtStyleLibrary library)
        {
            if (library == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSceneSetup.ScenePath) == null) return false;
            Scene scene = SceneManager.GetSceneByPath(CampaignSceneSetup.ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(CampaignSceneSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (CampaignSession session in root.GetComponentsInChildren<CampaignSession>(true))
                    {
                        if (session.ArtStyle == library) continue;
                        Undo.RecordObject(session, "Assign Art Style");
                        session.ConfigureArt(library);
                        EditorUtility.SetDirty(session);
                        changed = true;
                    }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                return true;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
