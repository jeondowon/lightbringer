using System;
using System.IO;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Contact sheet of the Warlord's generated clips for reviewing the motion: idle and run from the side,
    // run and cleave from the front three-quarter view, eight evenly spaced phases each (the cleave also shows
    // its contact phase). A ground plane shows whether the planted foot stays on it.
    // Output: Docs/Diagnostics/WarlordMotionStrip.png.
    [InitializeOnLoad]
    internal static class WarlordMotionStrip
    {
        private const string Request = "Docs/Diagnostics/warlordmotion.request";
        private const string Output = "Docs/Diagnostics/WarlordMotionStrip.png";
        private const int Cell = 256, Columns = 8;

        static WarlordMotionStrip() => EditorRequests.Register(Request, Capture);

        [MenuItem("Lightbringer/Art/Snapshot Warlord Motion")]
        public static void Capture()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath)
                ?? throw new InvalidOperationException("Art style library is missing.");
            VisualOverride entry = library.FindOverride(VisualId.EnemyBoss);
            if (entry == null || entry.prefab == null) throw new InvalidOperationException("The Warlord has no rig override.");
            var rows = new (AnimationClip clip, Vector3 camera)[]
            {
                (entry.idleClip, new Vector3(3.6f, 1f, 0f)),
                (entry.moveClip, new Vector3(3.6f, 1f, 0f)),
                (entry.moveClip, new Vector3(2.4f, 1.35f, 2.8f)),
                (entry.actionClip, new Vector3(2.4f, 1.35f, 2.8f)),
            };
            var sheet = new Texture2D(Cell * Columns, Cell * rows.Length, TextureFormat.RGB24, false);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Cell, Cell, 24);
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional;
                light.intensity = 1.6f;
                light.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.localScale = Vector3.one * .6f;
                var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                groundMaterial.color = new Color(.22f, .26f, .2f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                var camera = new GameObject("Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.enabled = false;
                camera.scene = scene;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.35f, .38f, .45f);
                camera.fieldOfView = 34f;

                GameObject model = UnityEngine.Object.Instantiate(entry.prefab);
                SceneManager.MoveGameObjectToScene(model, scene);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(entry.localEuler));
                // Preview scenes do not re-skin after SampleAnimation, so each pose is baked into a plain mesh.
                SkinnedMeshRenderer skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var posed = new Mesh();
                var baked = new GameObject("Posed");
                baked.transform.SetParent(skin.transform, false);
                baked.AddComponent<MeshFilter>().sharedMesh = posed;
                baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
                camera.Render(); // the first render of a fresh preview scene comes out empty
                for (int row = 0; row < rows.Length; row++)
                {
                    AnimationClip clip = rows[row].clip;
                    if (clip == null) continue;
                    camera.transform.position = rows[row].camera;
                    camera.transform.LookAt(new Vector3(0f, .85f, 0f));
                    bool action = clip == entry.actionClip;
                    for (int column = 0; column < Columns; column++)
                    {
                        // The cleave row replaces its fifth frame with the contact phase.
                        float phase = action && column == 4 ? EnemyCatalog.StrikePhase(EnemyKind.Boss) : column / (float)Columns;
                        clip.SampleAnimation(model, clip.length * phase);
                        skin.BakeMesh(posed, true);
                        camera.Render();
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, Cell, Cell), column * Cell, (rows.Length - 1 - row) * Cell);
                        RenderTexture.active = null;
                    }
                }
                UnityEngine.Object.DestroyImmediate(model);
                UnityEngine.Object.DestroyImmediate(posed);
                UnityEngine.Object.DestroyImmediate(groundMaterial);
            }
            finally
            {
                RenderTexture.active = null;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllBytes(Output, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("Warlord motion strip written to " + Output);
        }
    }
}
