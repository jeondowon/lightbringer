using System.Collections.Generic;
using System.IO;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // One row per allied unit: its attack pose seen from the side and from the front, then each of its weapons
    // alone (front and side, orthographic). Output: Docs/Diagnostics/AlliedWeaponSnapshot.png.
    // Request: Docs/Diagnostics/alliedweapons.request.
    [InitializeOnLoad]
    internal static class AlliedWeaponSnapshot
    {
        private const string Request = "Docs/Diagnostics/alliedweapons.request";
        private const int W = 256, H = 384, Columns = 6;
        private static readonly VisualId[] Ids =
            { VisualId.Spearman, VisualId.Shieldbearer, VisualId.Knight, VisualId.Archer, VisualId.Mage, VisualId.Priest };
        private static readonly string[] WeaponNames = { "Spear", "Shield", "Mace", "Lance", "Bow", "Staff", "Quiver" };

        static AlliedWeaponSnapshot() => EditorRequests.Register(Request, Capture);

        [MenuItem("Lightbringer/Art/Snapshot Allied Weapons")]
        public static void Capture()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(W, H, 24);
            var sheet = new Texture2D(W * Columns, H * Ids.Length, TextureFormat.RGB24, false);
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
                camera.orthographic = true;
                for (int row = 0; row < Ids.Length; row++)
                {
                    VisualOverride entry = library.FindOverride(Ids[row]);
                    GameObject model = Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(model, scene);
                    model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(entry.localEuler));
                    AnimationClip clip = entry.actionClip != null ? entry.actionClip : entry.idleClip;
                    clip.SampleAnimation(model, clip.length * .5f);
                    int column = 0;
                    Shot(model, camera, target, sheet, row, column++, Vector3.right);
                    Shot(model, camera, target, sheet, row, column++, Ids[row] == VisualId.Shieldbearer || Ids[row] == VisualId.Knight ? Vector3.back : Vector3.forward);
                    foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                    {
                        if (column > Columns - 2 || System.Array.IndexOf(WeaponNames, part.name) < 0) continue;
                        GameObject copy = Object.Instantiate(part.gameObject);
                        SceneManager.MoveGameObjectToScene(copy, scene);
                        copy.transform.SetPositionAndRotation(new Vector3(40f, 0f, 0f), Quaternion.identity);
                        copy.transform.localScale = Vector3.one;
                        Shot(copy, camera, target, sheet, row, column++, Vector3.forward, true);
                        Shot(copy, camera, target, sheet, row, column++, Vector3.right, true);
                        Object.DestroyImmediate(copy);
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
            File.WriteAllBytes("Docs/Diagnostics/AlliedWeaponSnapshot.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("Allied weapon snapshot written");
        }

        private static void Shot(GameObject subject, Camera camera, RenderTexture target, Texture2D sheet, int row, int column, Vector3 from, bool isolated = false)
        {
            var bounds = new Bounds(subject.transform.position, Vector3.zero);
            foreach (Renderer r in subject.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float horizontal = from == Vector3.right ? bounds.extents.z : bounds.extents.x;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, horizontal * H / W) * 1.1f;
            camera.farClipPlane = isolated ? 6f : 1000f;
            camera.transform.SetPositionAndRotation(bounds.center + from * 3f, Quaternion.LookRotation(-from, Vector3.up));
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, W, H), column * W, (Ids.Length - 1 - row) * H);
            RenderTexture.active = null;
        }
    }
}
