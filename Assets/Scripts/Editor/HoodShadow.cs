using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Lightbringer.EditorTools.RigBuildUtility;

namespace Lightbringer.EditorTools
{
    // The Mage and Priest Tripo exports were generated from art with the face hidden in the hood's shadow, and the
    // meshes came out with no face at all: the camera looks through the opening into the culled inside of the hood.
    // A dark unlit volume on the Head bone fills that cavity, shrink-wrapped to the hood's inner wall and capped a
    // little behind the opening, so the face reads as shadow (the Archer gets the same look by recolouring its face).
    [InitializeOnLoad]
    internal static class HoodShadow
    {
        public const string ObjectName = "HoodShadow";
        private const string Request = "Docs/Validation/HoodShadow.request";
        private const string ReportPath = "Docs/Validation/HoodShadowChecks.txt";
        private const int Rings = 32, Segments = 48;
        private const float Reach = .3f;     // rays that find nothing this close leave through the opening
        private const float Inset = .012f;   // gap kept between the volume and the hood's inner wall
        private const float Recess = .02f;   // how far the face cap sits behind the opening's rim
        private static readonly Color Shadow = new Color(.018f, .023f, .035f, 1f); // Archer HoodInterior

        static HoodShadow() => EditorRequests.Register(Request, PatchCasterPrefabs);

        // Adds the volume to the existing rig prefabs without rebuilding the rigs.
        [MenuItem("Lightbringer/Art/Add Hood Shadows to Mage and Priest")]
        public static void PatchCasterPrefabs()
        {
            var lines = new List<string>();
            foreach (string unit in new[] { "Mage", "Priest" })
            {
                string folder = $"Assets/Art/Characters/{unit}/Rigged";
                string path = $"{folder}/{unit}_Rigged.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SkinnedMeshRenderer skin = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == "Body")
                        ?? throw new InvalidOperationException(unit + " rig prefab has no Body skin.");
                    lines.Add(Attach(skin, folder, unit));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(ReportPath, $"PASS | {DateTime.UtcNow:O}\n" + string.Join("\n", lines) + "\n");
            Debug.Log("Hood shadows added to the Mage and Priest rigs.\n" + string.Join("\n", lines));
        }

        // Builds (or rebuilds) the shadow volume under the skin's Head bone; returns a one-line check report.
        public static string Attach(SkinnedMeshRenderer skin, string folder, string unit)
        {
            Mesh body = skin.sharedMesh;
            int headIndex = Array.FindIndex(skin.bones, b => b.name == "Head");
            if (headIndex < 0) throw new InvalidOperationException(unit + " rig has no Head bone.");
            Transform head = skin.bones[headIndex];
            for (int i = head.childCount - 1; i >= 0; i--)
                if (head.GetChild(i).name == ObjectName) UnityEngine.Object.DestroyImmediate(head.GetChild(i).gameObject);

            // Head-bone space at bind: the bone sits at the neck on the head's centre line with unit-aligned axes.
            Matrix4x4 toHead = body.bindposes[headIndex];
            Vector3[] vertices = body.vertices.Select(v => toHead.MultiplyPoint3x4(v)).ToArray();
            float top = vertices.Where(p => Mathf.Abs(p.x) < .15f && Mathf.Abs(p.z) < .2f && p.y > 0f && p.y < .5f).Max(p => p.y);
            var centre = new Vector3(0f, top * .45f, 0f);
            List<Vector3[]> triangles = NearbyTriangles(vertices, body.triangles, centre);

            int columns = Segments, count = (Rings + 1) * columns;
            var directions = new Vector3[count];
            var hits = new float[count];
            for (int r = 0; r <= Rings; r++)
            for (int s = 0; s < columns; s++)
            {
                float polar = Mathf.PI * r / Rings, azimuth = 2f * Mathf.PI * s / columns;
                Vector3 d = new Vector3(Mathf.Sin(polar) * Mathf.Sin(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Cos(azimuth));
                directions[r * columns + s] = d;
                hits[r * columns + s] = Cast(triangles, centre, d);
            }

            // The rim is every hit next to a ray that left through the front opening; the cap sits just behind it.
            var rim = new List<float>();
            int open = 0;
            for (int i = 0; i < count; i++)
            {
                bool front = directions[i].z > .2f;
                if (float.IsInfinity(hits[i])) { if (front) open++; continue; }
                if (front && Neighbours(i, columns).Any(n => float.IsInfinity(hits[n]))) rim.Add(hits[i]);
            }
            if (open < 20 || rim.Count < 10)
                throw new InvalidOperationException($"{unit}: no face opening found in the hood ({open} open rays, {rim.Count} rim hits).");
            float cap = Mathf.Clamp(rim.Average() - Recess, .05f, .16f);

            var radius = new float[count];
            for (int i = 0; i < count; i++) radius[i] = Mathf.Max(.03f, Mathf.Min(hits[i] - Inset, cap));
            // Pull isolated spikes in (never out) so the volume stays inside the hood between the sampled rays.
            for (int pass = 0; pass < 2; pass++)
            {
                var smoothed = new float[count];
                for (int i = 0; i < count; i++) smoothed[i] = Mathf.Min(radius[i], Neighbours(i, columns).Average(n => radius[n]) + .004f);
                radius = smoothed;
            }

            var mesh = new Mesh { name = unit + "_HoodShadow" };
            mesh.SetVertices(directions.Select((d, i) => d * radius[i]).ToList());
            var indices = new List<int>();
            for (int r = 0; r < Rings; r++)
            for (int s = 0; s < columns; s++)
            {
                int a = r * columns + s, b = r * columns + (s + 1) % columns, c = a + columns, d = b + columns;
                indices.AddRange(new[] { a, b, c, b, d, c });
            }
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveAsset(mesh, $"{folder}/{mesh.name}.asset");

            var volume = new GameObject(ObjectName);
            volume.transform.SetParent(head, false);
            volume.transform.localPosition = centre;
            volume.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = volume.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShadowMaterial(folder);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return $"{unit}: hood shadow {centre.y:F2} m above the neck, face cap {cap:F3} m from the centre; "
                + $"{open} open front rays, {rim.Count} rim hits, {triangles.Count} hood triangles.";
        }

        private static Material ShadowMaterial(string folder)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "HoodShadow" };
            material.SetColor("_BaseColor", Shadow);
            material.SetFloat("_Cull", 0f);
            return SaveAsset(material, folder + "/HoodShadow.mat");
        }

        private static List<Vector3[]> NearbyTriangles(Vector3[] vertices, int[] indices, Vector3 centre)
        {
            var result = new List<Vector3[]>();
            float reach = Reach * Reach;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                if ((a - centre).sqrMagnitude > reach && (b - centre).sqrMagnitude > reach && (c - centre).sqrMagnitude > reach) continue;
                result.Add(new[] { a, b - a, c - a });
            }
            return result;
        }

        // Nearest two-sided hit distance along the ray (Moller-Trumbore), or infinity past Reach.
        private static float Cast(List<Vector3[]> triangles, Vector3 origin, Vector3 direction)
        {
            float nearest = Reach;
            foreach (Vector3[] t in triangles)
            {
                Vector3 p = Vector3.Cross(direction, t[2]);
                float det = Vector3.Dot(t[1], p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1f / det;
                Vector3 s = origin - t[0];
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, t[1]);
                float v = Vector3.Dot(direction, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float distance = Vector3.Dot(t[2], q) * inv;
                if (distance > 0f && distance < nearest) nearest = distance;
            }
            return nearest < Reach ? nearest : float.PositiveInfinity;
        }

        private static IEnumerable<int> Neighbours(int index, int columns)
        {
            int r = index / columns, s = index % columns;
            yield return r * columns + (s + 1) % columns;
            yield return r * columns + (s + columns - 1) % columns;
            if (r > 0) yield return index - columns;
            if (r < Rings) yield return index + columns;
        }
    }
}
