using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Helpers shared by the generated prototype rigs (Swordsman, Archer, Spearman, Dragon).
    internal static class RigBuildUtility
    {
        // Creates the asset, or overwrites an existing one in place so references to it stay valid.
        public static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static Material MakeMaterial(string folder, string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            return SaveAsset(material, folder + "/" + name + ".mat");
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
