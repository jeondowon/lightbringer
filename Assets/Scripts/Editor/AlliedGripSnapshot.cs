using System.IO;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Close-ups of an allied unit's weapon hand (idle and attack poses) from the front, the outside and above.
    // Output: Docs/Diagnostics/AlliedGripSnapshot.png. Request: Docs/Diagnostics/alliedgrip.request.
    [InitializeOnLoad]
    internal static class AlliedGripSnapshot
    {
        private const string Request = "Docs/Diagnostics/alliedgrip.request";
        private const int Cell = 320;
        private static readonly VisualId[] Ids = { VisualId.Swordsman };

        static AlliedGripSnapshot() => EditorRequests.Register(Request, Capture);

        [MenuItem("Lightbringer/Art/Snapshot Allied Grips")]
        public static void Capture()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Cell, Cell, 24);
            var sheet = new Texture2D(Cell * 3, Cell * 2 * Ids.Length, TextureFormat.RGB24, false);
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional;
                light.intensity = 1.6f;
                light.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.enabled = false;
                camera.scene = scene;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.35f, .38f, .45f);
                camera.fieldOfView = 30f;
                for (int i = 0; i < Ids.Length; i++)
                {
                    VisualOverride entry = library.FindOverride(Ids[i]);
                    GameObject model = Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(model, scene);
                    for (int pose = 0; pose < 2; pose++)
                    {
                        AnimationClip clip = pose == 0 ? entry.idleClip : entry.actionClip;
                        clip.SampleAnimation(model, clip.length * (pose == 0 ? .3f : .5f));
                        Transform hand = null;
                        foreach (Transform t in model.GetComponentsInChildren<Transform>()) if (t.name == "RightHand") hand = t;
                        Vector3 centre = hand.position;
                        Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.up + Vector3.forward * .01f };
                        for (int view = 0; view < 3; view++)
                        {
                            camera.transform.position = centre + dirs[view] * 1.6f;
                            camera.transform.LookAt(centre);
                            camera.Render();
                            RenderTexture.active = target;
                            sheet.ReadPixels(new Rect(0, 0, Cell, Cell), view * Cell, (Ids.Length * 2 - 1 - (i * 2 + pose)) * Cell);
                            RenderTexture.active = null;
                        }
                    }
                    Object.DestroyImmediate(model);
                }
            }
            finally
            {
                RenderTexture.active = null;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
            }
            sheet.Apply();
            Directory.CreateDirectory("Docs/Diagnostics");
            File.WriteAllBytes("Docs/Diagnostics/AlliedGripSnapshot.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("Allied grip snapshot written");
        }
    }
}
