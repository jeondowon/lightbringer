using Lightbringer.Aura;
using Lightbringer.CameraSystem;
using Lightbringer.Combat;
using Lightbringer.Pathing;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.Core
{
    public sealed class PrototypeBattle
    {
        public GameObject Root;
        public Combatant Hero;
        public UnitSummoner Summoner;
        public StageObjective Objective;
        public HeroAbilities Abilities;
        public EnemyWaveSpawner Waves;
        public UnityEngine.Camera Camera;
        public WaypointPath[] Paths;
    }

    public static class PrototypeBattleBuilder
    {
        public static PrototypeBattle Build(int stage, CampaignProgress progress, InputActionAsset input,
            Material material, Shader ringShader, Transform parent)
        {
            PrototypeBattle battle = new PrototypeBattle();
            battle.Root = new GameObject("Campaign Battlefield " + stage);
            battle.Root.SetActive(false);
            battle.Root.transform.SetParent(parent, false);
            Transform root = battle.Root.transform;
            GameObject ground = Primitive("Ground", PrimitiveType.Plane, root, material);
            ground.transform.localPosition = new Vector3(0, 0, 12);
            ground.transform.localScale = new Vector3(12, 1, 12);
            SetColor(ground, new Color(0.24f, 0.29f, 0.27f));
            GameObject sun = Child("Sun", root);
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            Light light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;

            GameObject hero = Primitive("Female Hero (Greybox)", PrimitiveType.Capsule, root, material, false);
            hero.transform.localPosition = new Vector3(0, 1.05f, -12);
            CharacterController controller = hero.AddComponent<CharacterController>();
            controller.height = 2; controller.radius = 0.5f; controller.skinWidth = 0.05f;
            battle.Hero = hero.AddComponent<Combatant>(); battle.Hero.Configure(Faction.Allied, 120);
            FoodResource food = hero.AddComponent<FoodResource>();
            hero.AddComponent<ManaResource>();
            hero.AddComponent<HeroAura>();
            hero.AddComponent<AuraRangeVisual>().Configure(ringShader);
            battle.Abilities = hero.AddComponent<HeroAbilities>();
            battle.Abilities.ConfigureLoadout(progress.loadout, progress.equipmentLevels);
            battle.Abilities.ApplyProgression(progress.ranks);
            GameObject cameraObject = Child("Battle Camera", root);
            battle.Camera = cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            ThirdPersonCamera follow = cameraObject.AddComponent<ThirdPersonCamera>();
            follow.Configure(hero.transform, input);
            hero.AddComponent<PlayerMovement>().Configure(cameraObject.transform, input);
            battle.Abilities.ConfigureCamera(battle.Camera);

            int lanes = stage <= 2 ? 1 : stage <= 5 ? 2 : 3;
            battle.Paths = new WaypointPath[lanes];
            WaypointPath[] enemyRoutes = new WaypointPath[lanes];
            for (int i = 0; i < lanes; i++)
            {
                float x = (i - (lanes - 1) * 0.5f) * 22f;
                Vector3[] points = { new Vector3(x, 0, -5), new Vector3(x, 0, 14), new Vector3(x * 0.5f, 0, 29), new Vector3(0, 0, 38) };
                battle.Paths[i] = Path("Path " + (i + 1), root, points);
                enemyRoutes[i] = Path("Enemy Route " + (i + 1), root, new[] {
                    new Vector3(x * 0.5f, 0, 29), new Vector3(x, 0, 14), new Vector3(x, 0, -5), new Vector3(0, 0, -12) });
            }
            CharacterController template = UnitTemplate(root, material);
            Transform soldiers = Child("Allied Units", root).transform;
            Transform enemies = Child("Enemy Units", root).transform;
            battle.Summoner = hero.AddComponent<UnitSummoner>();
            battle.Summoner.Configure(food, template, soldiers, battle.Paths, progress.UnlockedUnits);
            battle.Abilities.ApplyProgression(progress.ranks);
            GameObject objective = Primitive("Enemy Stronghold", PrimitiveType.Cube, root, material);
            objective.transform.localPosition = new Vector3(0, 2, 38);
            objective.transform.localScale = new Vector3(6, 4, 4);
            Combatant baseHealth = objective.AddComponent<Combatant>();
            baseHealth.Configure(Faction.Enemy, 150 + stage * 50);
            baseHealth.SetAttackSurface(objective.GetComponent<BoxCollider>());
            battle.Objective = battle.Root.AddComponent<StageObjective>();
            battle.Objective.Configure(baseHealth, battle.Hero, root, follow);
            battle.Waves = battle.Root.AddComponent<EnemyWaveSpawner>();
            battle.Waves.Configure(template, enemies, enemyRoutes, stage);
            battle.Root.AddComponent<Lightbringer.UI.BattlefieldReadability>().Configure(battle, ringShader);
            return battle;
        }

        public static CharacterController UnitTemplate(Transform parent, Material material)
        {
            GameObject item = Child("Unit Template", parent);
            item.SetActive(false);
            CharacterController controller = item.AddComponent<CharacterController>();
            controller.height = 1.6f; controller.radius = 0.35f; controller.skinWidth = 0.03f;
            controller.stepOffset = 0.2f; controller.minMoveDistance = 0;
            GameObject visual = Primitive("Visual", PrimitiveType.Capsule, item.transform, material, false);
            visual.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            item.AddComponent<UnitPathFollower>();
            item.AddComponent<Combatant>();
            item.AddComponent<UnitCombat>();
            return controller;
        }

        private static GameObject Child(string name, Transform parent)
        { GameObject item = new GameObject(name); item.transform.SetParent(parent, false); return item; }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Material material, bool solid = true)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name; item.transform.SetParent(parent, false);
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                Collider collider = item.GetComponent<Collider>(); collider.enabled = false;
                if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            }
            return item;
        }

        public static void SetColor(GameObject item, Color color)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            item.GetComponent<Renderer>().SetPropertyBlock(properties);
        }

        private static WaypointPath Path(string name, Transform parent, Vector3[] positions)
        {
            GameObject item = Child(name, parent);
            Transform[] points = new Transform[positions.Length];
            for (int i = 0; i < points.Length; i++)
            { points[i] = Child("Waypoint " + i, item.transform).transform; points[i].localPosition = positions[i]; }
            WaypointPath path = item.AddComponent<WaypointPath>(); path.Configure(points); return path;
        }
    }
}
