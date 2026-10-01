using System.Collections.Generic;
using System.IO;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Renders contact sheets of the hero override (model + scale + props, built exactly as in battle)
    // sampled across its idle / move / action clips, from the front and the side.
    // Output: Docs/ArtPass/Preview/HeroAnimation_Front.png and HeroAnimation_Side.png.
    // Also runs automatically when Docs/ArtPass/Preview/capture.request exists (the file is then removed).
    [InitializeOnLoad]
    public static class ArtAnimationPreview
    {
        private const int Columns = 6;
        private const int TileWidth = 320, TileHeight = 400;
        // Far from the origin so objects in the open scene (e.g. the greybox player) stay out of frame.
        private static readonly Vector3 Stage = new Vector3(5000f, 0f, 0f);
        private static double nextProbe;

        static ArtAnimationPreview() => EditorApplication.update += ProbeRequest;

        private static string PreviewFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "ArtPass", "Preview");

        private static void ProbeRequest()
        {
            if (EditorApplication.timeSinceStartup < nextProbe || Application.isPlaying || EditorApplication.isPlaying
                || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextProbe = EditorApplication.timeSinceStartup + 2d;
            string request = Path.Combine(PreviewFolder, "capture.request");
            if (!File.Exists(request)) return;
            File.Delete(request);
            Capture();
        }

        [MenuItem("Lightbringer/Art/Capture Hero Animation Preview")]
        public static void Capture()
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before capturing the hero animation preview."); return; }
            ArtStyleLibrary library = ArtStyleSetup.EnsureLibrary();
            VisualOverride hero = library != null ? library.FindOverride(VisualId.Hero) : null;
            if (hero == null) { Debug.LogError("Preview: no Hero override in the Art Style Library."); return; }
            var clips = new List<(string label, AnimationClip clip)>();
            if (hero.idleClip != null) clips.Add(("Idle", hero.idleClip));
            if (hero.moveClip != null) clips.Add(("Move", hero.moveClip));
            if (hero.actionClip != null) clips.Add(("Action", hero.actionClip));
            if (clips.Count == 0) clips.Add(("Bind pose", null));

            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject owner = new GameObject("Preview Hero");
                owner.transform.position = Stage;
                Transform visual = UnitAppearance.Attach(owner, VisualId.Hero, library, 0f);
                if (visual == null || visual.childCount == 0) { Debug.LogError("Preview: hero override could not be built."); return; }
                GameObject model = visual.GetChild(0).gameObject;
                foreach (MonoBehaviour behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;

                GameObject sun = new GameObject("Preview Sun");
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.localScale = Vector3.one * 2f;
                ground.transform.position = Stage;

                Camera camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.82f, 0.85f, 0.9f);
                camera.fieldOfView = 30f;
                camera.targetTexture = target;

                Directory.CreateDirectory(PreviewFolder);
                RenderSheet(camera, model, clips, Stage + new Vector3(0f, 1.15f, 7.2f), "HeroAnimation_Front.png");
                RenderSheet(camera, model, clips, Stage + new Vector3(7.2f, 1.15f, 0f), "HeroAnimation_Side.png");
                RenderStrike(camera, model, hero, library);
                Debug.Log($"Hero animation preview written to {PreviewFolder} ({clips.Count} clip row(s) x {Columns} frames).");
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void RenderSheet(Camera camera, GameObject model, List<(string label, AnimationClip clip)> clips,
            Vector3 cameraPosition, string fileName)
        {
            camera.transform.position = cameraPosition;
            camera.transform.LookAt(Stage + new Vector3(0f, 1.05f, 0f));
            Texture2D sheet = new Texture2D(TileWidth * Columns, TileHeight * clips.Count, TextureFormat.RGB24, false);
            Texture2D tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGB24, false);
            for (int row = 0; row < clips.Count; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    AnimationClip clip = clips[row].clip;
                    if (clip != null) clip.SampleAnimation(model, clip.length * column / Columns);
                    // Same post-animation staff correction as in play (LateUpdate does not run in edit mode).
                    foreach (PropStabilizer prop in model.GetComponentsInChildren<PropStabilizer>(true))
                    {
                        prop.SetCasting(clips[row].label == "Action");
                        prop.Apply(10f);
                    }
                    camera.Render();
                    RenderTexture.active = camera.targetTexture;
                    tile.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), 0, 0);
                    tile.Apply();
                    RenderTexture.active = null;
                    // Rows top to bottom in clip order.
                    sheet.SetPixels(column * TileWidth, (clips.Count - 1 - row) * TileHeight, TileWidth, TileHeight, tile.GetPixels());
                }
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(PreviewFolder, fileName), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(tile);
        }

        // One row: the hero mid-cast and a lightning strike 5 m ahead, sampled across the strike's life.
        private static void RenderStrike(Camera camera, GameObject model, VisualOverride hero, ArtStyleLibrary library)
        {
            if (library.lightningMaterial == null) { Debug.LogWarning("Preview: no lightning material."); return; }
            if (hero.actionClip != null) hero.actionClip.SampleAnimation(model, hero.actionClip.length * 0.35f);
            foreach (PropStabilizer prop in model.GetComponentsInChildren<PropStabilizer>(true)) { prop.SetCasting(true); prop.Apply(10f); }
            Transform holder = new GameObject("Preview Strikes").transform;
            float fov = camera.fieldOfView;
            camera.fieldOfView = 55f;
            camera.transform.position = Stage + new Vector3(11f, 4.5f, -5f);
            camera.transform.LookAt(Stage + new Vector3(0f, 4.5f, 3f));
            float[] ages = { 0.02f, 0.06f, 0.12f, 0.2f, 0.3f, 0.42f };
            Texture2D sheet = new Texture2D(TileWidth * ages.Length, TileHeight, TextureFormat.RGB24, false);
            Texture2D tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < ages.Length; i++)
                {
                    LightningStrikeVfx strike = LightningStrikeVfx.Spawn(library, Stage + new Vector3(0f, 0f, 5f), 3f, 1.6f, holder);
                    strike.Tick(ages[i]);
                    foreach (ParticleSystem system in strike.GetComponentsInChildren<ParticleSystem>(true))
                        system.Simulate(ages[i], true, true);
                    camera.Render();
                    RenderTexture.active = camera.targetTexture;
                    tile.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), 0, 0);
                    tile.Apply();
                    RenderTexture.active = null;
                    sheet.SetPixels(i * TileWidth, 0, TileWidth, TileHeight, tile.GetPixels());
                    Object.DestroyImmediate(strike.gameObject);
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(PreviewFolder, "HeroCast_Strike.png"), sheet.EncodeToPNG());
            }
            finally
            {
                camera.fieldOfView = fov;
                Object.DestroyImmediate(holder.gameObject);
                Object.DestroyImmediate(sheet);
                Object.DestroyImmediate(tile);
            }
        }
    }
}
