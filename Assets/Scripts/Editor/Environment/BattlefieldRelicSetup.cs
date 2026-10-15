using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lightbringer.EditorTools
{
    // Fallen allied arms for the Act 2/3 battlefields: the unit weapon meshes (WeaponMeshes) rebuilt as centre-pivot
    // prefabs in tarnished, mud-dulled materials, listed in the Act Two profile's relics. ActTwoScenery poses them.
    [InitializeOnLoad]
    internal static class BattlefieldRelicSetup
    {
        private const string Output = "Docs/Diagnostics/BattlefieldRelicChecks.txt";
        private const string RequestPath = "Docs/Diagnostics/battlefield-relics.request";
        private const string Folder = "Assets/Art/Environment/ActTwo/Relics";
        private static readonly string LoadedScriptStamp = GreyboxScriptAutoRefresh.GetStamp();

        static BattlefieldRelicSetup() => EditorRequests.Register(RequestPath, () =>
        {
            if (LoadedScriptStamp != GreyboxScriptAutoRefresh.GetStamp())
            {
                File.WriteAllText(RequestPath, "Build after script refresh");
                AssetDatabase.Refresh();
                return;
            }
            Run();
        });

        // The allied longsword after a battle: same proportions, nicked along both edges.
        private static readonly WeaponMeshes.SwordSpec WornSword = new WeaponMeshes.SwordSpec
        {
            BaseY = .15f, TaperY = .575f, TipY = .754f, Half = .038f, RidgeBase = .0075f, RidgeTip = .0045f, ChipDepth = .005f,
            ChipRight = new[] { .22f, .36f, .49f }, ChipLeft = new[] { .27f, .41f },
            GuardY = .148f, GripY0 = -.12f, GripY1 = .12f, GripRadius = .0175f, GripPitch = .032f,
        };

        [MenuItem("Lightbringer/Art/Build Battlefield Relics")]
        public static void Run()
        {
            var report = new StringBuilder("Battlefield Relics | " + DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                var profile = AssetDatabase.LoadAssetAtPath<ActTwoEnvironmentProfile>("Assets/Art/Environment/ActTwo/ActTwoEnvironment.asset");
                if (profile == null) throw new InvalidOperationException("Missing ActTwoEnvironment.asset");
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Environment/ActTwo", "Relics");

                // Dulled by weather and mud: darker albedo, far less gloss than the units' polished kit.
                Material steel = Weather(WeaponMeshes.BrushedSteel(Folder, "RelicSteel"), new Color(.5f, .5f, .5f), .35f);
                Material gold = Weather(WeaponMeshes.AgedGold(Folder, "RelicGold"), new Color(.55f, .5f, .44f), .35f);
                Material enamel = Weather(WeaponMeshes.BlueEnamel(Folder, "RelicEnamel"), new Color(.5f, .52f, .58f), .3f);
                Material leather = WeaponMeshes.PebbledLeather(Folder, "RelicLeather", new Color(.3f, .21f, .15f));
                Material wood = WeaponMeshes.WoodGrain(Folder, "RelicWood", new Color(.5f, .4f, .32f));
                Material paintedWood = WeaponMeshes.WoodGrain(Folder, "RelicPaintedWood", new Color(.22f, .28f, .42f));

                var relics = new List<ActTwoScatterAsset>();
                void Add(string name, ActTwoScatterKind kind, Action<Transform> build)
                    => relics.Add(Save(name, kind, build, report));

                Add("RelicSword", ActTwoScatterKind.Sword, parent =>
                {
                    WeaponMeshes.Sword("RelicSword", WornSword, out Mesh blade, out Mesh hilt, out Mesh grip);
                    RigBuildUtility.MeshPart(parent, "Blade", blade, steel, Folder);
                    RigBuildUtility.MeshPart(parent, "Hilt", hilt, gold, Folder);
                    RigBuildUtility.MeshPart(parent, "Grip", grip, leather, Folder);
                });
                Add("RelicSpear", ActTwoScatterKind.Spear, parent =>
                {
                    WeaponMeshes.Spear("RelicSpear", -.74f, 1.30f, 1.62f, out Mesh shaft, out Mesh grip, out Mesh head, out Mesh trim);
                    RigBuildUtility.MeshPart(parent, "Shaft", shaft, wood, Folder);
                    RigBuildUtility.MeshPart(parent, "Grip", grip, leather, Folder);
                    RigBuildUtility.MeshPart(parent, "Head", head, steel, Folder);
                    RigBuildUtility.MeshPart(parent, "Trim", trim, gold, Folder);
                });
                Add("RelicMace", ActTwoScatterKind.Mace, parent =>
                {
                    WeaponMeshes.Mace("RelicMace", .56f, out Mesh grip, out Mesh head, out Mesh trim);
                    RigBuildUtility.MeshPart(parent, "Grip", grip, leather, Folder);
                    RigBuildUtility.MeshPart(parent, "Head", head, steel, Folder);
                    RigBuildUtility.MeshPart(parent, "Trim", trim, gold, Folder);
                });
                Add("RelicKiteShield", ActTwoScatterKind.Shield, parent =>
                {
                    WeaponMeshes.KiteShield("RelicKite", Vector2.zero, out Mesh face, out Mesh trim, out Mesh metal);
                    RigBuildUtility.MeshPart(parent, "Face", face, enamel, Folder);
                    RigBuildUtility.MeshPart(parent, "Gold", trim, gold, Folder);
                    RigBuildUtility.MeshPart(parent, "Steel", metal, steel, Folder);
                });
                Add("RelicTowerShield", ActTwoScatterKind.Shield, parent =>
                {
                    WeaponMeshes.TowerShield("RelicTower", Vector2.zero, out Mesh face, out Mesh trim, out Mesh metal);
                    RigBuildUtility.MeshPart(parent, "Face", face, enamel, Folder);
                    RigBuildUtility.MeshPart(parent, "Gold", trim, gold, Folder);
                    RigBuildUtility.MeshPart(parent, "Steel", metal, steel, Folder);
                });
                Add("RelicRoundShield", ActTwoScatterKind.Shield, parent =>
                {
                    WeaponMeshes.RoundShield("RelicRound", out Mesh planks, out Mesh iron);
                    RigBuildUtility.MeshPart(parent, "Planks", planks, paintedWood, Folder);
                    RigBuildUtility.MeshPart(parent, "Iron", iron, gold, Folder);
                });

                profile.relics = relics.ToArray();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                report.AppendLine("PASS: complete");
                Debug.Log("Battlefield relics complete. " + Output);
            }
            catch (Exception error) { report.AppendLine("FAIL: " + error); throw; }
            finally { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, report.ToString()); }
        }

        private static Material Weather(Material material, Color tint, float smoothness)
        {
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Builds the parts in weapon space, then centres them so the scenery can pose the relic about its middle.
        private static ActTwoScatterAsset Save(string name, ActTwoScatterKind kind, Action<Transform> build, StringBuilder report)
        {
            var root = new GameObject(name);
            try
            {
                var model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
                build(model);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("No renderers in " + name);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                // Long arms keep their flats on ±X so the scenery's flat pose lays them on a face (the spear head is
                // built flat on ±Z).
                if (kind != ActTwoScatterKind.Shield && bounds.size.z < bounds.size.x)
                {
                    model.localRotation = Quaternion.Euler(0, 90, 0);
                    bounds = new Bounds(Vector3.zero, new Vector3(bounds.size.z, bounds.size.y, bounds.size.x)) { center = model.localRotation * bounds.center };
                }
                model.localPosition = -bounds.center;
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
                var asset = new ActTwoScatterAsset
                {
                    kind = kind, prefab = prefab,
                    radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude,
                    length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z),
                };
                report.AppendLine($"PASS: {name} -> {kind}, size={bounds.size}, length={asset.length:F2}");
                return asset;
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
