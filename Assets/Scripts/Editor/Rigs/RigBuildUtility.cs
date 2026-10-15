using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.EditorTools
{
    // Helpers shared by the generated prototype rigs (Swordsman, Archer, Spearman, Dragon).
    internal static class RigBuildUtility
    {
        public static int Triangles(Mesh mesh)
        {
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }

        // Creates the asset, or overwrites an existing one in place so references to it stay valid.
        public static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            // Meshes go through the Mesh API: CopySerialized leaves renderers already using the mesh on
            // its old GPU data (they stop drawing or draw the previous shape), and deleting and recreating the
            // asset leaves loaded prefabs with a missing mesh until the Editor restarts.
            if (value is Mesh from && existing is Mesh to) CopyMesh(from, to);
            else EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // Vertex streams (skin weights included), index buffer, sub-meshes, mesh LOD ranges, bind poses and
        // bounds, copied through the Mesh API so every renderer using `to` picks the new data up. No blend shapes.
        private static void CopyMesh(Mesh from, Mesh to)
        {
            to.Clear();
            using (Mesh.MeshDataArray source = Mesh.AcquireReadOnlyMeshData(from))
            {
                Mesh.MeshData s = source[0];
                Mesh.MeshDataArray target = Mesh.AllocateWritableMeshData(1);
                Mesh.MeshData d = target[0];
                d.SetVertexBufferParams(s.vertexCount, from.GetVertexAttributes());
                for (int stream = 0; stream < s.vertexBufferCount; stream++)
                    d.GetVertexData<byte>(stream).CopyFrom(s.GetVertexData<byte>(stream));
                if (s.indexFormat == IndexFormat.UInt16)
                {
                    var indices = s.GetIndexData<ushort>();
                    d.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                    d.GetIndexData<ushort>().CopyFrom(indices);
                }
                else
                {
                    var indices = s.GetIndexData<uint>();
                    d.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                    d.GetIndexData<uint>().CopyFrom(indices);
                }
                d.subMeshCount = s.subMeshCount;
                const MeshUpdateFlags keep = MeshUpdateFlags.DontValidateIndices;
                for (int i = 0; i < s.subMeshCount; i++) d.SetSubMesh(i, s.GetSubMesh(i), keep);
                Mesh.ApplyAndDisposeWritableMeshData(target, to, keep);
            }
            to.bindposes = from.bindposes;
            to.RecalculateBounds(); // Copied sub-mesh bounds can be stale, which culls the mesh while it is on screen.
            if (from.lodCount > 1)
            {
                to.lodCount = from.lodCount;
                var ranges = new List<MeshLodRange>();
                for (int i = 0; i < from.subMeshCount; i++)
                {
                    ranges.Clear();
                    from.GetLods(ranges, i);
                    to.SetLods(ranges, i, MeshUpdateFlags.Default);
                }
                to.lodSelectionCurve = from.lodSelectionCurve;
            }
        }

        public static Material MakeMaterial(string folder, string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            return SaveAsset(material, folder + "/" + name + ".mat");
        }

        // Emissive URP Lit material for glowing prop parts (staff crystals, holy orbs).
        public static Material MakeGlowMaterial(string folder, string name, Color color, float intensity)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", .8f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return SaveAsset(material, folder + "/" + name + ".mat");
        }

        // Mesh asset (saved in `folder`) on a child renderer of `parent`.
        public static Transform MeshPart(Transform parent, string name, Mesh mesh, Material material, string folder)
        {
            mesh = SaveAsset(mesh, folder + "/" + mesh.name + ".asset");
            var part = new GameObject(name).transform;
            part.SetParent(parent, false);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        // Collider-free primitive prop part.
        public static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        // Centroid of the vertices on one side (sign = +1 right, -1 left) beyond minX within a height band.
        public static Vector3 Landmark(Vector3[] vertices, int sign, float minY, float maxY, float minX, out int[] indices)
        {
            var matches = new List<int>();
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                if (p.x * sign < minX || p.y < minY || p.y > maxY) continue;
                sum += p; matches.Add(i);
            }
            if (matches.Count == 0) throw new InvalidOperationException($"Cannot locate arm landmark (y {minY}-{maxY}, |x| > {minX}).");
            indices = matches.ToArray();
            return sum / matches.Count;
        }
    }
}
