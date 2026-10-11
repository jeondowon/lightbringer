using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Rendering cost of every Art Pass model override: vertices, triangles, draw calls (sub-meshes), bones,
    // blend shapes, texture memory and shaders, plus the import options that matter for crowds.
    // Output: Docs/Diagnostics/CharacterMeshes.txt. Also runs when Docs/Diagnostics/characters.request exists.
    [InitializeOnLoad]
    public static class CharacterMeshReport
    {
        private static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");

        static CharacterMeshReport() => EditorRequests.Register(Path.Combine(Folder, "characters.request"), Run);

        [MenuItem("Lightbringer/Diagnostics/Report Character Meshes")]
        public static void Run()
        {
            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            StringBuilder report = new StringBuilder("Character model cost (per instance; shadows and outline passes draw again)\n");
            if (library == null || library.overrides == null) { Debug.LogError("Art style library not found."); return; }
            foreach (VisualOverride entry in library.overrides)
            {
                if (entry == null || entry.prefab == null) continue;
                report.AppendLine($"\n[{entry.id}] {AssetDatabase.GetAssetPath(entry.prefab)}");
                Describe(entry.prefab, report);
                if (entry.attachments != null)
                    foreach (PropAttachment prop in entry.attachments)
                        if (prop != null && prop.prefab != null)
                        {
                            report.AppendLine($"  + prop {prop.prefab.name} on {prop.boneName}");
                            Describe(prop.prefab, report);
                        }
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "CharacterMeshes.txt"), report.ToString());
            Debug.Log("Character mesh report written to Docs/Diagnostics/CharacterMeshes.txt");
        }

        private static void Describe(GameObject prefab, StringBuilder report)
        {
            int vertices = 0, triangles = 0, drawCalls = 0, bones = 0, blendShapes = 0;
            HashSet<Texture> textures = new HashSet<Texture>();
            HashSet<string> shaders = new HashSet<string>();
            HashSet<Mesh> meshes = new HashSet<Mesh>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                meshes.Add(mesh);
                vertices += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
                drawCalls += mesh.subMeshCount;
                blendShapes += mesh.blendShapeCount;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    bones += skin.bones.Length;
                    report.AppendLine($"  skinned '{renderer.name}': quality {skin.quality}, offscreen update {skin.updateWhenOffscreen}, shadows {renderer.shadowCastingMode}");
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    shaders.Add(material.shader != null ? material.shader.name : "none");
                    foreach (int id in material.GetTexturePropertyNameIDs())
                        if (material.GetTexture(id) is Texture texture) textures.Add(texture);
                }
            }
            long textureBytes = textures.Sum(texture => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture));
            report.AppendLine($"  vertices {vertices:N0}, triangles {triangles:N0}, draw calls {drawCalls}, bones {bones}, blend shapes {blendShapes}");
            report.AppendLine($"  textures {textures.Count} ({textureBytes / (1024f * 1024f):0.0} MB): "
                + string.Join(", ", textures.Select(texture => $"{texture.name} {texture.width}x{texture.height}")));
            report.AppendLine("  shaders: " + string.Join(", ", shaders));
            foreach (Mesh mesh in meshes)
            {
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) is ModelImporter model)
                    report.AppendLine($"  import {Path.GetFileName(model.assetPath)}: compression {model.meshCompression}, read/write {model.isReadable}, "
                        + $"optimize mesh {model.optimizeMeshVertices}/{model.optimizeMeshPolygons}, optimize game objects {model.optimizeGameObjects}, "
                        + $"anim compression {model.animationCompression}, skin weights {model.skinWeights}");
            }
        }
    }
}
