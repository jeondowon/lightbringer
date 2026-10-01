using System.Linq;
using Lightbringer.Aura;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateArtStyle()
        {
            Shader toon = Shader.Find(ArtStyleSetup.ToonShader);
            Shader runes = Shader.Find(ArtStyleSetup.RuneShader);
            Shader glow = Shader.Find(ArtStyleSetup.GlowShader);
            Check(toon != null && toon.isSupported && runes != null && runes.isSupported && glow != null && glow.isSupported,
                "Art Pass toon, aura rune and glow shaders compile for URP");
            Check(FacesOutward(ProceduralMeshes.Box) && FacesOutward(ProceduralMeshes.Sphere) && FacesOutward(ProceduralMeshes.Frustum(0.5f))
                && FacesOutward(ProceduralMeshes.Frustum(0f, 6))
                && FacesOutward(ProceduralMeshes.Lumpy(0)) && FacesOutward(ProceduralMeshes.Faceted(0)),
                "Procedural building blocks have outward-facing triangles for culling and outlines");

            ArtStyleLibrary style = ArtStyleSetup.CreateTransientLibrary();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = new GameObject("Art style validation host");
            LightingSnapshot lighting = LightingSnapshot.Capture();
            GameObject overrideModel = null;
            try
            {
                foreach (VisualId id in System.Enum.GetValues(typeof(VisualId)))
                {
                    Silhouette silhouette = SilhouetteFactory.Get(style, id);
                    Bounds bounds = silhouette.Body.bounds;
                    bool large = id == VisualId.Dragon || id == VisualId.EnemyStronghold || id == VisualId.AlliedStronghold || id == VisualId.Knight;
                    Check(silhouette.Body.vertexCount > 0 && silhouette.Body.colors.Length == silhouette.Body.vertexCount
                        && bounds.min.y > -0.1f && bounds.max.y > 1.2f && (large || bounds.max.y < 2.8f)
                        && silhouette.Body.vertexCount < 20000,
                        $"{id} silhouette bakes a single palette mesh standing on the ground at a readable height");
                }
                Check(SilhouetteFactory.Get(style, VisualId.Dragon).Parts.Length == 2
                    && SilhouetteFactory.Get(style, VisualId.Dragon).Body.bounds.size.z > SilhouetteFactory.Get(style, VisualId.Knight).Body.bounds.size.z * 1.8f,
                    "Dragon is a winged final-tier silhouette far larger than the Knight");

                CampaignSession session = host.AddComponent<CampaignSession>();
                CampaignProgress profile = new CampaignProgress();
                for (int stage = 1; stage < CampaignProgress.StageCount; stage++) profile.CompleteStage(stage);
                session.InitializeForValidation(profile);
                session.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),
                    material, Shader.Find("Universal Render Pipeline/Unlit"));
                session.ConfigureArt(style);
                session.SelectStage(8);
                Check(session.StartBattle(), "Styled campaign battle starts with the Art Style Library");
                PrototypeBattle battle = session.Battle;
                Invoke(battle.Objective, "OnEnable");

                Transform heroVisual = battle.Hero.transform.Find(UnitAppearance.StyledName);
                Check(heroVisual != null && !battle.Hero.GetComponent<MeshRenderer>().enabled && !battle.Hero.UseTeamTint
                    && battle.Hero.GetComponent<CharacterController>().height == 2f,
                    "Hero capsule is replaced by the styled commander without changing its controller");
                Check(battle.Hero.GetComponent<AuraRuneVisual>() != null && battle.Hero.GetComponent<AuraRangeVisual>() == null,
                    "Styled battle uses the rune aura instead of the greybox ring");
                AuraRuneVisual rune = battle.Hero.GetComponent<AuraRuneVisual>();
                HeroAura aura = battle.Hero.GetComponent<HeroAura>();
                Invoke(rune, "OnEnable");
                Invoke(rune, "LateUpdate");
                Vector4 sphere = Shader.GetGlobalVector("_LB_AuraSphere");
                Check(rune.Disc != null && Mathf.Approximately(rune.Disc.transform.localScale.x, aura.Radius * 2f)
                    && Mathf.Approximately(sphere.w, aura.Radius) && rune.Disc.transform.parent == battle.Root.transform,
                    "Rune circle and ally glow follow the real aura radius");
                aura.Configure(aura.Radius + 1.2f, aura.AttackBonus);
                Invoke(rune, "LateUpdate");
                Check(Mathf.Approximately(rune.Disc.transform.localScale.x, aura.Radius * 2f)
                    && Mathf.Approximately(Shader.GetGlobalVector("_LB_AuraSphere").w, aura.Radius),
                    "Aura Size growth enlarges the visible rune circle immediately");
                Invoke(rune, "OnDisable");
                Check(Shader.GetGlobalVector("_LB_AuraSphere").w == 0f, "Disabling the aura clears the ally glow");

                Check(battle.Hero.GetComponent<HeroSpellVfx>() != null, "Styled hero turns offensive spells into lightning strikes");
                LightningStrikeVfx strike = LightningStrikeVfx.Spawn(style, new Vector3(0f, 0f, 5f), 3f, 1.6f, battle.Root.transform);
                Check(strike != null && strike.MainBolt.GetPosition(0).y > 10f
                    && Vector3.Distance(strike.MainBolt.GetPosition(strike.MainBolt.positionCount - 1), new Vector3(0f, 0f, 5f)) < 0.01f
                    && strike.BranchCount == 4 && strike.GetComponentsInChildren<Collider>(true).Length == 0,
                    "Area spell strike falls from the sky onto the impact point with branches and no colliders");
                strike.Tick(2f);
                Check(strike == null, "Lightning strike removes itself after its short life");
                Combatant stronghold = battle.Objective.EnemyBase;
                Transform fortress = stronghold.transform.Find(UnitAppearance.StyledName);
                BoxCollider surface = stronghold.GetComponent<BoxCollider>();
                Check(fortress != null && Vector3.Distance(fortress.lossyScale, Vector3.one) < 0.001f
                    && surface.enabled && !stronghold.GetComponent<MeshRenderer>().enabled
                    && fortress.GetComponentsInChildren<Collider>(true).Length == 0,
                    "Enemy stronghold gets a world-scale fortress while keeping its attack surface");

                BattlefieldEnvironment environment = battle.Root.GetComponentInChildren<BattlefieldEnvironment>(true);
                Check(environment != null && environment.Terrain != null && !battle.Root.transform.Find("Ground").gameObject.activeSelf,
                    "Styled battle replaces the greybox plane with the environment terrain");
                bool flat = true;
                foreach (Lightbringer.Pathing.WaypointPath lane in battle.Paths)
                    for (int w = 0; w < lane.Count; w++)
                    {
                        Vector3 point = lane.GetPosition(w);
                        if (point.z > 36f) continue; // final waypoint sits inside the enemy stronghold
                        flat &= environment.Terrain.Raycast(new Ray(point + Vector3.up * 5f, Vector3.down), out RaycastHit hit, 10f)
                            && Mathf.Abs(hit.point.y - point.y) < 0.01f;
                    }
                Check(flat, "Terrain stays flat at ground level along every Path, so movement is unchanged");
                Mesh props = environment.transform.Find("Props (Near)").GetComponent<MeshFilter>().sharedMesh;
                Check(props.vertices.Where(v => v.y < 2.5f).All(v => environment.LaneDistance(new Vector2(v.x, v.z)) > 2.5f),
                    "Trees, rocks and ruins keep the lanes clear");
                bool grassOffRoads = environment.GrassChunks > 20;
                foreach (MeshFilter chunk in environment.GetComponentsInChildren<MeshFilter>())
                    if (chunk.name.StartsWith("Grass "))
                        grassOffRoads &= chunk.sharedMesh.vertices.Where(v => Mathf.Abs(v.y) < 0.01f)
                            .All(v => environment.LaneDistance(new Vector2(v.x, v.z)) > 1.8f);
                Check(grassOffRoads, "Grass covers the field in culled chunks and leaves the dirt roads bare");
                FoodResource food = battle.Hero.GetComponent<FoodResource>();
                CharacterController template = battle.Root.GetComponentsInChildren<CharacterController>(true).First(c => c.name == "Unit Template");
                Combatant spawned = null;
                battle.Summoner.Summoned += unit => spawned = unit;
                for (int i = 0; i < UnitCatalog.Count; i++)
                {
                    Invoke(food, "GenerateFood", 100f);
                    Physics.SyncTransforms();
                    Check(battle.Summoner.TrySelectUnit(i) && battle.Summoner.TrySummon(), "Styled " + UnitCatalog.Names[i] + " can be deployed");
                    Transform visual = spawned.transform.Find(UnitAppearance.StyledName);
                    CharacterController body = spawned.GetComponent<CharacterController>();
                    Check(visual != null && spawned.transform.Find(UnitAppearance.GreyboxName) == null
                        && visual.GetComponentsInChildren<Collider>(true).Length == 0 && !spawned.UseTeamTint
                        && visual.GetComponent<MeshRenderer>().sharedMaterial == style.alliedMaterial
                        && body.height == template.height && body.radius == template.radius,
                        UnitCatalog.Names[i] + " shows its styled silhouette without changing collision");
                    spawned.transform.position = new Vector3(-35 + i * 4, 0.85f, -25);
                    Physics.SyncTransforms();
                }
                Check(spawned.transform.Find(UnitAppearance.StyledName).GetComponentsInChildren<MeshRenderer>().Length == 3,
                    "Dragon deploys with two animated wing parts");

                Physics.SyncTransforms();
                battle.Waves.Tick(0.1f);
                Combatant[] enemies = battle.Root.GetComponentsInChildren<Combatant>()
                    .Where(x => x.Faction == Faction.Enemy && x != stronghold).ToArray();
                Check(enemies.Length > 0 && enemies.All(e => e.transform.Find(UnitAppearance.StyledName) != null
                    && e.transform.Find(UnitAppearance.StyledName).GetComponent<MeshRenderer>().sharedMaterial == style.enemyMaterial),
                    "Enemy waves spawn as corrupted silhouettes with the enemy material");
                Check(enemies.Any(e => e.name == "Enemy Archer"), "Late-stage waves include the distinct enemy archer");

                // A finished model replaces one troop without touching gameplay code; its colliders are stripped.
                overrideModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                overrideModel.name = "Override Model";
                style.overrides = new[] { new VisualOverride { id = VisualId.Swordsman, prefab = overrideModel, scale = 1.5f } };
                Invoke(food, "GenerateFood", 100f);
                Physics.SyncTransforms();
                Check(battle.Summoner.TrySelectUnit(0) && battle.Summoner.TrySummon(), "Swordsman with a model override can be deployed");
                Transform replaced = spawned.transform.Find(UnitAppearance.StyledName + "/Override Model");
                Check(replaced != null && replaced.localScale == Vector3.one * 1.5f
                    && replaced.GetComponentsInChildren<Collider>(true).All(c => c == null || !c.enabled),
                    "Model override swaps in the assigned prefab with gameplay colliders removed");
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (overrideModel != null) Object.DestroyImmediate(overrideModel);
                Object.DestroyImmediate(material);
                ArtStyleSetup.DestroyTransientLibrary(style);
                Shader.SetGlobalVector("_LB_AuraSphere", Vector4.zero);
                lighting.Restore();
                Physics.SyncTransforms();
            }
        }

        private static bool FacesOutward(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.sqrMagnitude < 1e-10f) continue;
                Vector3 normal = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                if (Vector3.Dot(face, normal) <= 0f) return false;
            }
            return true;
        }

        // Styled battles change scene lighting; validation must leave the open scene untouched.
        private struct LightingSnapshot
        {
            private AmbientMode ambientMode;
            private Color sky, equator, ground, ambientLight, fogColor;
            private bool fog;
            private FogMode fogMode;
            private float fogStart, fogEnd, fogDensity;

            public static LightingSnapshot Capture() => new LightingSnapshot
            {
                ambientMode = RenderSettings.ambientMode, sky = RenderSettings.ambientSkyColor,
                equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
                ambientLight = RenderSettings.ambientLight, fog = RenderSettings.fog, fogMode = RenderSettings.fogMode,
                fogColor = RenderSettings.fogColor, fogStart = RenderSettings.fogStartDistance,
                fogEnd = RenderSettings.fogEndDistance, fogDensity = RenderSettings.fogDensity
            };

            public void Restore()
            {
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientLight = ambientLight; RenderSettings.fog = fog; RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor; RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd; RenderSettings.fogDensity = fogDensity;
            }
        }
    }
}
