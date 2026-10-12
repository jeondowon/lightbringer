using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Renders each enemy override prefab offscreen through the Editor's live mesh objects (front and side,
    // idle pose) and reports how much of each view it covers. A model that is valid on disk but drawn from
    // stale GPU data shows up here as missing or wrong. Output: Docs/Diagnostics/EnemyRigSnapshot.png/.txt.
    [InitializeOnLoad]
    internal static class EnemyRigSnapshot
    {
        private const string Request = "Docs/Diagnostics/enemyrigsnapshot.request";
        private const string Output = "Docs/Diagnostics/EnemyRigSnapshot";
        private const int Cell = 256;
        private const string WeaponOutput = "Docs/Diagnostics/EnemyWeaponSnapshot.png";
        private const int WeaponWidth = 256, WeaponHeight = 512;
        private static readonly string[] WeaponNames = { "Sword", "Shield", "Dagger", "Club", "Staff", "Greatsword" };
        private static readonly VisualId[] Ids =
            { VisualId.EnemyRaider, VisualId.EnemySwarm, VisualId.EnemyBrute, VisualId.EnemyShaman, VisualId.EnemyBoss, VisualId.EnemyArcher };

        static EnemyRigSnapshot() => EditorRequests.Register(Request, Capture);

        [MenuItem("Lightbringer/Art/Snapshot Enemy Rigs")]
        public static void Capture()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath)
                ?? throw new InvalidOperationException("Art style library is missing.");
            var sheet = new Texture2D(Cell * Ids.Length, Cell * 2, TextureFormat.RGB24, false);
            var report = new StringBuilder("Enemy rig snapshot | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                + "\nColumns: " + string.Join(", ", Ids) + ". Top row: camera on +Z (the face if the model faces +Z). Bottom row: camera on +X.\n");
            Color background = new Color(.35f, .38f, .45f);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Cell, Cell, 24);
            var weaponTarget = new RenderTexture(WeaponWidth, WeaponHeight, 24);
            var weapons = new List<Color[]>();
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
                camera.backgroundColor = background;
                camera.fieldOfView = 30f;

                for (int i = 0; i < Ids.Length; i++)
                {
                    VisualOverride entry = library.FindOverride(Ids[i]);
                    if (entry == null || entry.prefab == null) { report.AppendLine($"{Ids[i]}: no override"); continue; }
                    GameObject model = UnityEngine.Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(model, scene);
                    model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(entry.localEuler));
                    if (entry.idleClip != null) entry.idleClip.SampleAnimation(model, entry.idleClip.length * .3f);
                    var line = new StringBuilder($"{Ids[i]}:");
                    string crowdPath = $"Assets/Art/Characters/{Ids[i]}/Rigged/{Ids[i]}_Skinned_Crowd.asset";
                    Mesh atPath = AssetDatabase.LoadAssetAtPath<Mesh>(crowdPath);
                    line.Append($" asset at path {(atPath != null ? atPath.vertexCount + " verts" : "null")} (type {AssetDatabase.GetMainAssetTypeAtPath(crowdPath)?.Name ?? "none"});");
                    foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        line.Append($" skin '{skin.name}' mesh {(skin.sharedMesh != null ? skin.sharedMesh.name + " " + skin.sharedMesh.vertexCount + " verts" : "MISSING")},"
                            + $" enabled {skin.enabled}, bounds centre {skin.bounds.center}, size {skin.bounds.size};");
                    for (int view = 0; view < 2; view++)
                    {
                        camera.transform.position = view == 0 ? new Vector3(0f, .95f, 5f) : new Vector3(5f, .95f, 0f);
                        camera.transform.LookAt(new Vector3(0f, .95f, 0f));
                        camera.Render();
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, Cell, Cell), i * Cell, (1 - view) * Cell);
                        RenderTexture.active = null;
                        int covered = 0;
                        Color[] pixels = sheet.GetPixels(i * Cell, (1 - view) * Cell, Cell, Cell);
                        foreach (Color c in pixels)
                            if (Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > .06f) covered++;
                        line.Append($" {(view == 0 ? "front" : "side")} coverage {100f * covered / pixels.Length:0.0}%;");
                    }
                    report.AppendLine(line.ToString());
                    foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                        if (Array.IndexOf(WeaponNames, part.name) >= 0)
                        {
                            weapons.Add(RenderWeapon(part, scene, camera, weaponTarget, false));
                            weapons.Add(RenderWeapon(part, scene, camera, weaponTarget, true));
                            report.AppendLine($"  weapon {part.name}: columns {weapons.Count - 2} (from +Z, the forward side) and {weapons.Count - 1} (from +X)");
                        }
                    UnityEngine.Object.DestroyImmediate(model);
                }
            }
            finally
            {
                RenderTexture.active = null;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                weaponTarget.Release();
                UnityEngine.Object.DestroyImmediate(weaponTarget);
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllBytes(Output + ".png", sheet.EncodeToPNG());
            File.WriteAllText(Output + ".txt", report.ToString());
            UnityEngine.Object.DestroyImmediate(sheet);
            if (weapons.Count > 0)
            {
                var weaponSheet = new Texture2D(WeaponWidth * weapons.Count, WeaponHeight, TextureFormat.RGB24, false);
                for (int i = 0; i < weapons.Count; i++) weaponSheet.SetPixels(i * WeaponWidth, 0, WeaponWidth, WeaponHeight, weapons[i]);
                weaponSheet.Apply();
                File.WriteAllBytes(WeaponOutput, weaponSheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(weaponSheet);
            }
            Debug.Log("Enemy rig snapshot written to " + Output + ".png/.txt");
        }

        // The weapon alone in its own space (grip at the origin, +Y up), seen from +Z (forward) or +X with an orthographic camera.
        private static Color[] RenderWeapon(Transform weapon, Scene scene, Camera camera, RenderTexture target, bool side)
        {
            GameObject copy = UnityEngine.Object.Instantiate(weapon.gameObject);
            SceneManager.MoveGameObjectToScene(copy, scene);
            copy.transform.SetPositionAndRotation(new Vector3(30f, 0f, 0f), Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            Bounds bounds = new Bounds(copy.transform.position, Vector3.zero);
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(r.bounds);
            }
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.orthographic = true;
            camera.farClipPlane = 6f; // Only the weapon copy, not the model standing at the origin behind it.
            camera.orthographicSize = Mathf.Max(bounds.extents.y, (side ? bounds.extents.z : bounds.extents.x) * WeaponHeight / WeaponWidth) * 1.08f;
            camera.transform.SetPositionAndRotation(bounds.center + (side ? Vector3.right : Vector3.forward) * 3f, Quaternion.Euler(0f, side ? -90f : 180f, 0f));
            camera.Render();
            var cell = new Texture2D(WeaponWidth, WeaponHeight, TextureFormat.RGB24, false);
            RenderTexture.active = target;
            cell.ReadPixels(new Rect(0, 0, WeaponWidth, WeaponHeight), 0, 0);
            RenderTexture.active = null;
            Color[] pixels = cell.GetPixels();
            UnityEngine.Object.DestroyImmediate(cell);
            camera.orthographic = false;
            camera.farClipPlane = 1000f;
            camera.targetTexture = previous;
            UnityEngine.Object.DestroyImmediate(copy);
            return pixels;
        }
    }
}
