using System.Linq;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Builds the female hero's staff from code (see WeaponMeshes.HeroStaff) as a prefab and attaches it to the
    // hero's right hand. Request file: Docs/Validation/HeroStaff.request.
    internal static class HeroStaffSetup
    {
        private const string Folder = ArtModelImport.FemaleHeroPropsFolder + "/Code";
        private const string PrefabPath = ArtModelImport.FemaleHeroPropsFolder + "/HeroStaff_Code.prefab";

        static HeroStaffSetup() => EditorRequests.Register("Docs/Validation/HeroStaff.request", Build);

        [MenuItem("Lightbringer/Art/Build Code Hero Staff")]
        private static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(ArtModelImport.FemaleHeroPropsFolder, "Code");

            WeaponMeshes.HeroStaff("HeroStaff", out Mesh gold, out Mesh crystal, out Mesh orb, out Mesh shaft);
            gold = RigBuildUtility.SaveAsset(gold, Folder + "/HeroStaffGold.asset");
            crystal = RigBuildUtility.SaveAsset(crystal, Folder + "/HeroStaffCrystal.asset");
            orb = RigBuildUtility.SaveAsset(orb, Folder + "/HeroStaffOrb.asset");
            shaft = RigBuildUtility.SaveAsset(shaft, Folder + "/HeroStaffShaft.asset");

            // Engraved aged gold, dark lacquered wood and one solid faceted sapphire. Emission stays low on the shards
            // so their facets still shade.
            Material goldMaterial = WeaponMeshes.AgedGold(Folder, "HeroStaffGold");
            Material shaftMaterial = WeaponMeshes.WoodGrain(Folder, "HeroStaffShaft", new Color(.42f, .45f, .75f));
            shaftMaterial.SetFloat("_Smoothness", .62f);
            EditorUtility.SetDirty(shaftMaterial);
            Material crystalMaterial = RigBuildUtility.MakeGlowMaterial(Folder, "HeroStaffCrystal", new Color(.1f, .22f, 1f), .55f);
            crystalMaterial.SetFloat("_Smoothness", .95f);
            EditorUtility.SetDirty(crystalMaterial);
            Material orbMaterial = WeaponMeshes.Sapphire(Folder, "HeroStaffOrb");

            var root = new GameObject("HeroStaff");
            try
            {
                AddMesh(root.transform, "Gold", gold, goldMaterial);
                AddMesh(root.transform, "Crystal", crystal, crystalMaterial);
                AddMesh(root.transform, "Orb", orb, orbMaterial);
                AddMesh(root.transform, "Shaft", shaft, shaftMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            // The orb used to be a glass shell around a core; the core meshes and material are gone.
            AssetDatabase.DeleteAsset(Folder + "/HeroStaffCore.asset");
            AssetDatabase.DeleteAsset(Folder + "/HeroStaffCore.mat");

            ArtStyleLibrary library = AssetDatabase.LoadAssetAtPath<ArtStyleLibrary>(ArtStyleSetup.LibraryPath);
            VisualOverride hero = library != null ? library.FindOverride(VisualId.Hero) : null;
            if (hero == null)
            {
                Debug.LogWarning("Hero staff built at " + PrefabPath + " but not attached: run Assign Female Hero Model first.");
                return;
            }
            if (!ArtModelImport.AttachProp(library, hero, PrefabPath, "RightHand", 2.0f)) return;
            int triangles = new[] { gold, crystal, orb, shaft }.Sum(m => m.triangles.Length / 3);
            Debug.Log($"Hero staff built from code ({triangles} triangles) and attached to the right hand: {PrefabPath}");
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
