using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    [InitializeOnLoad]
    public static class ArcherFaceRepair
    {
        private const string Folder = "Assets/Art/Characters/Archer";
        private const string Source = Folder + "/Source/Tripo/Archer_Source.fbx";
        private const string ColorPath = Folder + "/Source/Tripo/Archer_Source.fbm/fantasy_armor_3d_model_basecolor.JPEG";
        private const string Output = Folder + "/FaceRepair";
        private const string Request = "Docs/Diagnostics/ArcherImport/Inspect.request";
        private const string RepairRequest = "Docs/Diagnostics/ArcherImport/Repair.request";

        static ArcherFaceRepair() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (File.Exists(Request)) { File.Delete(Request); Inspect(); }
                if (File.Exists(RepairRequest)) { File.Delete(RepairRequest); Repair(); }
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Lightbringer/Art/Repair Archer Face From Reference")]
        public static void Repair()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Output + "/Archer_Upright.asset");
            if (mesh == null) { Inspect(); mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Output + "/Archer_Upright.asset"); }
            Texture2D atlas = Load(ColorPath), reference = Load(Folder + "/Source/Archer_1_Front.png");
            try
            {
                Color[] original = atlas.GetPixels();
                Color[] painted = (Color[])original.Clone();
                float[] mask = new float[painted.Length];
                Vector3[] vertices = mesh.vertices;
                Vector2[] uv = mesh.uv;
                int[] triangles = mesh.triangles;
                const int visibilitySize=1024;
                float[] visible=Enumerable.Repeat(float.NegativeInfinity,visibilitySize*visibilitySize).ToArray();
                Func<Vector3,Vector2> project=p=>new Vector2((.22f-p.x)/.44f*visibilitySize,(p.y-1.25f)/.44f*visibilitySize);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                    if(Mathf.Max(a.y,b.y,c.y)<1.25f)continue;
                    Raster(project(a),project(b),project(c),visibilitySize,visibilitySize,(x,y,w)=> {
                        int index=y*visibilitySize+x;visible[index]=Mathf.Max(visible[index],a.z*w.x+b.z*w.y+c.z*w.z);
                    });
                }
                for (int i=0;i<triangles.Length;i+=3)
                {
                    int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];
                    Vector3 a=vertices[ia],b=vertices[ib],c=vertices[ic];
                    Vector3 centre=(a+b+c)/3f;
                    if (centre.y<1.435f || centre.y>1.585f || Mathf.Abs(centre.x)>.075f || centre.z<0) continue;
                    Vector2 ta=Vector2.Scale(uv[ia],new Vector2(atlas.width,atlas.height));
                    Vector2 tb=Vector2.Scale(uv[ib],new Vector2(atlas.width,atlas.height));
                    Vector2 tc=Vector2.Scale(uv[ic],new Vector2(atlas.width,atlas.height));
                    Raster(ta,tb,tc,atlas.width,atlas.height,(x,y,w)=> {
                        Vector3 p=a*w.x+b*w.y+c*w.z;
                        Vector2 screen=project(p);
                        int sx=Mathf.Clamp((int)screen.x,0,visibilitySize-1),sy=Mathf.Clamp((int)screen.y,0,visibilitySize-1);
                        if(p.z<visible[sy*visibilitySize+sx]-.002f)return;
                        float oval=Mathf.Pow(p.x/.060f,2)+Mathf.Pow((p.y-1.510f)/.075f,2);
                        float blend=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.72f,1f,Mathf.Sqrt(oval)));
                        if(blend<=0)return;
                        // Register chin and eye line to the existing illustration, not a regenerated face.
                        float imageX=506f-p.x*950f;
                        float imageY=211f-(p.y-1.443f)*720f;
                        Color face=reference.GetPixelBilinear(imageX/1024f,1f-imageY/1536f);
                        int index=y*atlas.width+x;
                        if(blend<mask[index])return;
                        painted[index]=Color.Lerp(original[index],face,blend);
                        mask[index]=blend;
                    });
                }
                int changed=mask.Count(v=>v>0);
                if(changed<100)throw new InvalidOperationException("No usable face projection found.");
                // Pad the painted UV islands to avoid original blurry texels bleeding through UV seams.
                for(int pass=0;pass<2;pass++)
                {
                    Color[] previous=(Color[])painted.Clone();float[] previousMask=(float[])mask.Clone();
                    for(int y=1;y<atlas.height-1;y++)for(int x=1;x<atlas.width-1;x++)
                    {
                        int index=y*atlas.width+x;
                        if(previousMask[index]>0)continue;
                        int best=index;
                        foreach(int delta in new[]{-1,1,-atlas.width,atlas.width})
                            if(previousMask[index+delta]>previousMask[best])best=index+delta;
                        if(best==index)continue;
                        painted[index]=previous[best];mask[index]=previousMask[best];
                    }
                }
                atlas.SetPixels(painted);atlas.Apply();
                string corrected=Output+"/Archer_BaseColor_FaceFixed.png";
                File.WriteAllBytes(corrected,atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(corrected,ImportAssetOptions.ForceSynchronousImport);
                var ti=(TextureImporter)AssetImporter.GetAtPath(corrected);
                ti.maxTextureSize=4096;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
                Render(mesh,atlas,"Docs/Diagnostics/ArcherImport/Face_After.png",0);
                Render(mesh,atlas,"Docs/Diagnostics/ArcherImport/Face_After_Side.png",55);
                var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Archer_FaceFixed.mat");
                if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Output+"/Archer_FaceFixed.mat"); }
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(corrected));
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.25f);
                RestoreSurfaceMaps(material,mask,atlas.width,atlas.height);
                var model=new GameObject("Archer_FaceFixed");
                try
                {
                    model.AddComponent<MeshFilter>().sharedMesh=mesh;
                    model.AddComponent<MeshRenderer>().sharedMaterial=material;
                    PrefabUtility.SaveAsPrefabAsset(model,Output+"/Archer_FaceFixed.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
                EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                File.WriteAllText("Docs/Diagnostics/ArcherImport/Repair.txt", $"Projected existing face reference onto {changed} atlas pixels. Original ZIP/FBX/textures unchanged. Geometry unchanged.\n");
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas);UnityEngine.Object.DestroyImmediate(reference); }
        }

        private static void RestoreSurfaceMaps(Material material,float[] mask,int width,int height)
        {
            string textures=Folder+"/Source/Tripo/Archer_Source.fbm/fantasy_armor_3d_model_";
            Texture2D normal=Load(textures+"normal.JPEG"),metal=Load(textures+"metallic.JPEG"),rough=Load(textures+"roughness.JPEG");
            var packed=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            try
            {
                var normals=new Color[mask.Length];var surface=new Color[mask.Length];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    int i=y*width+x;float u=(x+.5f)/width,v=(y+.5f)/height;
                    normals[i]=Color.Lerp(normal.GetPixelBilinear(u,v),new Color(.5f,.5f,1f,1f),mask[i]);
                    surface[i]=new Color(metal.GetPixelBilinear(u,v).r*(1-mask[i]),0,0,Mathf.Lerp(1-rough.GetPixelBilinear(u,v).r,.25f,mask[i]));
                }
                normal.Reinitialize(width,height);normal.SetPixels(normals);normal.Apply();
                packed.SetPixels(surface);packed.Apply();
                string n=Output+"/Archer_Normal_FaceFixed.png",m=Output+"/Archer_MetallicSmoothness.png";
                File.WriteAllBytes(n,normal.EncodeToPNG());File.WriteAllBytes(m,packed.EncodeToPNG());
                foreach(string path in new[]{n,m})
                {
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=path==n?TextureImporterType.NormalMap:TextureImporterType.Default;
                    importer.sRGBTexture=false;importer.maxTextureSize=4096;importer.SaveAndReimport();
                }
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(n));material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(m));
                material.SetFloat("_Smoothness",1);material.SetFloat("_Metallic",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normal);UnityEngine.Object.DestroyImmediate(metal);
                UnityEngine.Object.DestroyImmediate(rough);UnityEngine.Object.DestroyImmediate(packed);
            }
        }

        [MenuItem("Lightbringer/Art/Inspect Archer Face")]
        public static void Inspect()
        {
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) throw new InvalidOperationException("Archer FBX import failed.");
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder(Folder, "FaceRepair");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var probe = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(probe, scene);
            Texture2D color = Load(ColorPath);
            try
            {
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(-90, 0, 0));
                probe.transform.localScale = Vector3.one;
                Renderer[] renderers = probe.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = 1.65f / bounds.size.y;
                probe.transform.localScale = Vector3.one * scale;
                probe.transform.position = -scale * new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var mesh = new Mesh { name = "Archer_Upright", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(probe.GetComponentsInChildren<MeshFilter>().Select(f => new CombineInstance
                { mesh = f.sharedMesh, transform = f.transform.localToWorldMatrix }).ToArray(), true, true);
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(Output + "/Archer_Upright.asset");
                if (existing == null) AssetDatabase.CreateAsset(mesh, Output + "/Archer_Upright.asset");
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                Render(mesh, color, "Docs/Diagnostics/ArcherImport/Face_Before.png", 0);
                Render(mesh, color, "Docs/Diagnostics/ArcherImport/Face_Before_Side.png", 55);
                AssetDatabase.SaveAssets();
                File.WriteAllText("Docs/Diagnostics/ArcherImport/Inspect.txt", $"Imported {mesh.vertexCount} vertices; {mesh.triangles.Length/3} triangles; bounds {mesh.bounds}.\n");
            }
            finally { UnityEngine.Object.DestroyImmediate(color); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Texture2D Load(string path)
        {
            var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
            return texture;
        }

        // CPU orthographic texture projection: stable before/after views without changing the open scene.
        internal static void Render(Mesh mesh, Texture2D texture, string path, float yaw, int shadowStart = int.MaxValue)
        {
            const int size = 640;
            var pixels = Enumerable.Repeat(new Color32(65,70,80,255), size*size).ToArray();
            var depth = Enumerable.Repeat(float.NegativeInfinity, size*size).ToArray();
            Quaternion rotation = Quaternion.Euler(0,yaw,0);
            Vector3[] points = mesh.vertices.Select(v => rotation * v).ToArray();
            Vector2[] uv = mesh.uv; int[] triangles = mesh.triangles;
            for (int i=0;i<triangles.Length;i+=3)
            {
                int ia=triangles[i], ib=triangles[i+1], ic=triangles[i+2];
                Vector3 a=points[ia],b=points[ib],c=points[ic];
                if (Mathf.Max(a.y,b.y,c.y)<1.25f) continue;
                Vector2 pa=new Vector2((.22f-a.x)/.44f*size,(a.y-1.25f)/.44f*size);
                Vector2 pb=new Vector2((.22f-b.x)/.44f*size,(b.y-1.25f)/.44f*size);
                Vector2 pc=new Vector2((.22f-c.x)/.44f*size,(c.y-1.25f)/.44f*size);
                bool shadow = i >= shadowStart;
                Raster(pa,pb,pc,size,size,(x,y,w)=> {
                    float z=a.z*w.x+b.z*w.y+c.z*w.z; int index=y*size+x;
                    if(z<=depth[index])return;
                    depth[index]=z;
                    Vector2 coord=uv[ia]*w.x+uv[ib]*w.y+uv[ic]*w.z;
                    pixels[index]=shadow ? new Color(.018f,.023f,.035f,1) : texture.GetPixelBilinear(coord.x,coord.y);
                });
            }
            var image=new Texture2D(size,size,TextureFormat.RGBA32,false);
            image.SetPixels32(pixels);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }

        private static void Raster(Vector2 a,Vector2 b,Vector2 c,int width,int height,Action<int,int,Vector3> pixel)
        {
            float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
            if(Mathf.Abs(denominator)<1e-8f)return;
            int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)),0,width-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)),0,width-1);
            int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)),0,height-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)),0,height-1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                float u=((b.y-c.y)*(x+.5f-c.x)+(c.x-b.x)*(y+.5f-c.y))/denominator;
                float v=((c.y-a.y)*(x+.5f-c.x)+(a.x-c.x)*(y+.5f-c.y))/denominator;
                if(u>=0&&v>=0&&u+v<=1)pixel(x,y,new Vector3(u,v,1-u-v));
            }
        }
    }
}
