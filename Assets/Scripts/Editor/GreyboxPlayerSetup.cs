using System;
using System.Linq;
using Lightbringer.CameraSystem;
using Lightbringer.Player;
using Lightbringer.Resources;
using Lightbringer.Units;
using Lightbringer.Pathing;
using Lightbringer.Combat;
using Lightbringer.Aura;
using Lightbringer.Core;
using Lightbringer.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    public static class GreyboxPlayerSetup
    {
        private const string MenuPath = "Lightbringer/Setup Greybox Player";
        private const string RootName = "Lightbringer Greybox";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem(MenuPath, true)]
        private static bool CanSetup()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling
                && PrefabStageUtility.GetCurrentPrefabStage() == null
                && !SceneManager.GetActiveScene().GetRootGameObjects().Any(root =>
                    root.GetComponentInChildren<CampaignSession>(true) != null);
        }

        [MenuItem(MenuPath)]
        private static void Setup() => TrySetup();

        public static bool TrySetup()
        {
            if (!CanSetup())
                return false;

            Scene scene = SceneManager.GetActiveScene();
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (!scene.IsValid() || !scene.isLoaded || actions == null
                || actions.FindAction("Player/Move", false) == null
                || actions.FindAction("Player/Look", false) == null)
            {
                Debug.LogError("Greybox setup needs a loaded scene and Player/Move + Player/Look in " + InputPath);
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            GameObject[] matchingRoots = roots.Where(item => item.name == RootName).ToArray();
            UnityEngine.Camera[] cameras = roots.SelectMany(item =>
                item.GetComponentsInChildren<UnityEngine.Camera>(true)).ToArray();
            UnityEngine.Camera[] mainCameras = cameras.Where(item => item.CompareTag("MainCamera")).ToArray();
            if (matchingRoots.Length > 1 || mainCameras.Length > 1
                || (mainCameras.Length == 0 && cameras.Length > 1))
            {
                Debug.LogError("Greybox setup found ambiguous roots or cameras. Keep one Greybox root and identify one MainCamera.");
                return false;
            }

            GameObject existingRoot = matchingRoots.FirstOrDefault();
            if (existingRoot != null && !IsValidExistingRoot(existingRoot))
            {
                Debug.LogError("The Lightbringer Greybox root contains conflicting objects. Rename the unrelated root before setup.");
                return false;
            }

            UnityEngine.Camera camera = mainCameras.FirstOrDefault() ?? cameras.FirstOrDefault();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Greybox Player");
            try
            {
                GameObject root = existingRoot ?? CreateObject(RootName, scene);
                if (root.transform.Find("Ground") == null)
                {
                    GameObject ground = CreatePrimitive("Ground", PrimitiveType.Plane, root.transform);
                    ground.transform.localScale = new Vector3(10f, 1f, 10f);
                }

                Transform player = root.transform.Find("Player");
                bool createdPlayer = player == null;
                if (createdPlayer)
                {
                    player = CreatePrimitive("Player", PrimitiveType.Capsule, root.transform).transform;
                    player.localPosition = new Vector3(0f, 1.1f, 0f);
                    // The CharacterController is the only player collider.
                    Undo.DestroyObjectImmediate(player.GetComponent<CapsuleCollider>());
                    CharacterController controller = Undo.AddComponent<CharacterController>(player.gameObject);
                    controller.height = 2f;
                    controller.radius = 0.5f;
                    controller.center = Vector3.zero;
                    controller.stepOffset = 0.3f;
                    controller.skinWidth = 0.05f;
                    controller.minMoveDistance = 0f;
                    Undo.AddComponent<PlayerMovement>(player.gameObject);

                    GameObject facing = CreatePrimitive("Facing Marker", PrimitiveType.Cube, player);
                    Undo.DestroyObjectImmediate(facing.GetComponent<BoxCollider>());
                    facing.transform.localPosition = new Vector3(0f, 0.35f, 0.5f);
                    facing.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f);
                }

                if (camera == null)
                {
                    GameObject cameraObject = CreateObject("Main Camera", scene);
                    camera = Undo.AddComponent<UnityEngine.Camera>(cameraObject);
                    bool hasListener = roots.Any(item => item.GetComponentInChildren<AudioListener>(true) != null);
                    if (!hasListener)
                        Undo.AddComponent<AudioListener>(cameraObject);
                }
                if (!camera.CompareTag("MainCamera"))
                {
                    Undo.RecordObject(camera.gameObject, "Tag Main Camera");
                    camera.tag = "MainCamera";
                }

                ThirdPersonCamera follow = camera.GetComponent<ThirdPersonCamera>();
                bool createdFollow = follow == null;
                if (createdFollow)
                    follow = Undo.AddComponent<ThirdPersonCamera>(camera.gameObject);
                SetReference(follow, "target", player);
                SetReference(follow, "inputActions", actions);
                PlayerMovement movement = player.GetComponent<PlayerMovement>();
                SetReference(movement, "cameraTransform", camera.transform);
                SetReference(movement, "inputActions", actions);
                if (createdFollow)
                {
                    Undo.RecordObject(camera.transform, "Frame Greybox Player");
                    Quaternion rotation = Quaternion.Euler(30f, 0f, 0f);
                    camera.transform.SetPositionAndRotation(
                        player.position + Vector3.up - rotation * Vector3.forward * 9f, rotation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
                }

                bool hasSun = roots.SelectMany(item => item.GetComponentsInChildren<Light>(true))
                    .Any(light => light.type == LightType.Directional);
                if (!hasSun)
                {
                    GameObject sun = CreateObject("Directional Light", scene);
                    sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    Light light = Undo.AddComponent<Light>(sun);
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    light.shadows = LightShadows.Soft;
                }

                SetupSummoning(root, player);
                SetupAura(player.gameObject);
                SetupEnemies(root);
                SetupHeroCombat(player.gameObject, camera);
                SetupObjective(root, player, follow);
                SetupPathSelection(root, player.GetComponent<UnitSummoner>());
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = player.gameObject;
                Debug.Log("Greybox ready. Save, then Play: WASD / mouse look / F summons a soldier. Destroy the enemy base to win; Food and base HP appear in the HUD. Escape releases the cursor.", player);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static bool IsValidExistingRoot(GameObject root)
        {
            Transform[] children = root.transform.Cast<Transform>().ToArray();
            if (children.Count(item => item.name == "Player") > 1
                || children.Count(item => item.name == "Ground") > 1
                || children.Count(item => item.name == "Soldier Template") > 1
                || children.Count(item => item.name == "Path 1") > 1
                || children.Count(item => item.name == "Enemies") > 1
                || children.Count(item => item.name == "Enemy Base") > 1
                || children.Count(item => item.name == "Soldiers") > 1)
                return false;
            Transform template = root.transform.Find("Soldier Template");
            if (template != null && ((template.GetComponent<CapsuleCollider>() == null
                && template.GetComponent<CharacterController>() == null)
                || template.gameObject.activeSelf))
                return false;
            Transform path = root.transform.Find("Path 1");
            if (path != null && path.GetComponent<WaypointPath>() == null)
                return false;
            Transform player = root.transform.Find("Player");
            Transform ground = root.transform.Find("Ground");
            return (player == null || (player.GetComponent<PlayerMovement>() != null
                    && player.GetComponent<CharacterController>() != null))
                && (ground == null || ground.GetComponent<MeshCollider>() != null);
        }

        private static void SetupSummoning(GameObject root, Transform player)
        {
            FoodResource food = player.GetComponent<FoodResource>();
            if (food == null)
                food = Undo.AddComponent<FoodResource>(player.gameObject);
            UnitSummoner summoner = player.GetComponent<UnitSummoner>();
            if (summoner == null)
                summoner = Undo.AddComponent<UnitSummoner>(player.gameObject);

            Transform soldiers = root.transform.Find("Soldiers");
            if (soldiers == null)
            {
                GameObject container = CreateObject("Soldiers", root.scene);
                Undo.SetTransformParent(container.transform, root.transform, "Parent Soldiers");
                soldiers = container.transform;
            }
            Transform template = root.transform.Find("Soldier Template");
            if (template == null)
            {
                GameObject item = CreatePrimitive("Soldier Template", PrimitiveType.Capsule, root.transform);
                // Keep transform scale at one; resize the mesh and collider together via a visual child.
                MeshFilter meshFilter = item.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = item.GetComponent<MeshRenderer>();
                GameObject visual = CreatePrimitive("Visual", PrimitiveType.Capsule, item.transform);
                Undo.DestroyObjectImmediate(visual.GetComponent<CapsuleCollider>());
                visual.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
                Undo.DestroyObjectImmediate(meshFilter);
                Undo.DestroyObjectImmediate(meshRenderer);
                CapsuleCollider capsule = item.GetComponent<CapsuleCollider>();
                capsule.height = 1.6f;
                capsule.radius = 0.35f;
                item.SetActive(false);
                template = item.transform;
            }
            SetReference(summoner, "food", food);
            CharacterController controller = template.GetComponent<CharacterController>();
            if (controller == null)
            {
                CapsuleCollider oldCollider = template.GetComponent<CapsuleCollider>();
                float height = oldCollider.height;
                float radius = oldCollider.radius;
                Vector3 center = oldCollider.center;
                Undo.DestroyObjectImmediate(oldCollider);
                controller = Undo.AddComponent<CharacterController>(template.gameObject);
                controller.height = height;
                controller.radius = radius;
                controller.center = center;
                controller.skinWidth = 0.03f;
                controller.stepOffset = 0.2f;
                controller.minMoveDistance = 0f;
            }
            if (template.GetComponent<UnitPathFollower>() == null)
                Undo.AddComponent<UnitPathFollower>(template.gameObject);
            EnsureCombat(template.gameObject, Faction.Allied);
            SetReference(summoner, "soldierTemplate", controller);
            SetReference(summoner, "soldiersParent", soldiers);
            SerializedObject serializedSummoner = new SerializedObject(summoner);
            if (serializedSummoner.FindProperty("selectedPath").objectReferenceValue == null)
                SetReference(summoner, "selectedPath", SetupPath(root));
        }

        private static void SetupEnemies(GameObject root)
        {
            Transform container = root.transform.Find("Enemies");
            if (container == null)
            {
                GameObject item = CreateObject("Enemies", root.scene);
                Undo.SetTransformParent(item.transform, root.transform, "Parent Enemies");
                container = item.transform;
            }
            Vector3[] positions = { new Vector3(0f, 0.85f, 12f), new Vector3(3f, 0.85f, 22f) };
            for (int i = 0; i < positions.Length; i++)
            {
                string name = "Greybox Enemy " + (i + 1);
                Transform existing = container.Find(name);
                if (existing != null)
                {
                    if (existing.GetComponent<UnitCombat>() == null || existing.GetComponent<Combatant>() == null)
                        throw new InvalidOperationException("Conflicting object named " + name + ". Rename it before setup.");
                    existing.GetComponent<Combatant>().RefreshTeamColor();
                    continue;
                }
                GameObject enemy = CreateObject(name, root.scene);
                Undo.SetTransformParent(enemy.transform, container, "Parent Enemy");
                enemy.transform.position = root.transform.TransformPoint(positions[i]);
                CharacterController controller = Undo.AddComponent<CharacterController>(enemy);
                controller.height = 1.6f;
                controller.radius = 0.35f;
                controller.skinWidth = 0.03f;
                controller.stepOffset = 0.2f;
                controller.minMoveDistance = 0f;
                GameObject visual = CreatePrimitive("Visual", PrimitiveType.Capsule, enemy.transform);
                Undo.DestroyObjectImmediate(visual.GetComponent<CapsuleCollider>());
                visual.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
                Undo.AddComponent<UnitPathFollower>(enemy);
                EnsureCombat(enemy, Faction.Enemy);
            }
        }

        private static void SetupAura(GameObject player)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("The Greybox aura needs the installed URP Unlit shader.");
            if (player.GetComponent<HeroAura>() == null)
                Undo.AddComponent<HeroAura>(player);
            AuraRangeVisual visual = player.GetComponent<AuraRangeVisual>();
            if (visual == null)
                visual = Undo.AddComponent<AuraRangeVisual>(player);
            SetReference(visual, "ringShader", shader);
        }

        private static void SetupObjective(GameObject root, Transform player, ThirdPersonCamera camera)
        {
            Transform existing = root.transform.Find("Enemy Base");
            Combatant health;
            if (existing == null)
            {
                UnitSummoner summoner = player.GetComponent<UnitSummoner>();
                WaypointPath path = (WaypointPath)new SerializedObject(summoner)
                    .FindProperty("selectedPath").objectReferenceValue;
                if (path == null || !path.IsValid)
                    throw new InvalidOperationException("The enemy base needs a valid selected Path.");
                GameObject baseObject = CreatePrimitive("Enemy Base", PrimitiveType.Cube, root.transform);
                baseObject.transform.position = path.GetPosition(path.Count - 1) + Vector3.up * 2f;
                baseObject.transform.localScale = new Vector3(4f, 4f, 4f);
                health = Undo.AddComponent<Combatant>(baseObject);
                SerializedObject settings = new SerializedObject(health);
                settings.FindProperty("faction").enumValueIndex = (int)Faction.Enemy;
                settings.FindProperty("maximumHealth").floatValue = 200f;
                settings.FindProperty("attackSurface").objectReferenceValue = baseObject.GetComponent<BoxCollider>();
                settings.ApplyModifiedProperties();
            }
            else
            {
                health = existing.GetComponent<Combatant>();
                BoxCollider box = existing.GetComponent<BoxCollider>();
                if (health == null || health.Faction != Faction.Enemy || box == null
                    || !box.enabled || box.isTrigger || !existing.gameObject.activeSelf
                    || existing.GetComponent<UnitCombat>() != null || existing.GetComponent<UnitPathFollower>() != null)
                    throw new InvalidOperationException("Conflicting Enemy Base object. Use an active enemy Combatant and a solid BoxCollider without unit movement/combat.");
                SetReference(health, "attackSurface", box);
            }
            health.RefreshTeamColor();
            StageObjective objective = root.GetComponent<StageObjective>();
            if (objective == null)
                objective = Undo.AddComponent<StageObjective>(root);
            SetReference(objective, "enemyBase", health);
            SetReference(objective, "battlefieldRoot", root.transform);
            SetReference(objective, "battleCamera", camera);
            SetReference(objective, "hero", player.GetComponent<Combatant>());
            GreyboxHUD hud = root.GetComponent<GreyboxHUD>();
            if (hud == null)
                hud = Undo.AddComponent<GreyboxHUD>(root);
            SetReference(hud, "objective", objective);
            SetReference(hud, "food", player.GetComponent<FoodResource>());
            SetReference(hud, "summoner", player.GetComponent<UnitSummoner>());
            SetReference(hud, "hero", player.GetComponent<Combatant>());
            SetReference(hud, "mana", player.GetComponent<ManaResource>());
            SetReference(hud, "abilities", player.GetComponent<HeroAbilities>());
        }

        private static void SetupHeroCombat(GameObject player, UnityEngine.Camera camera)
        {
            if (player.GetComponent<Combatant>() == null)
            {
                Combatant health = Undo.AddComponent<Combatant>(player);
                Undo.RecordObject(health, "Configure Hero Health");
                health.Configure(Faction.Allied, 120f);
            }
            if (player.GetComponent<ManaResource>() == null) Undo.AddComponent<ManaResource>(player);
            HeroAbilities abilities = player.GetComponent<HeroAbilities>();
            if (abilities == null) abilities = Undo.AddComponent<HeroAbilities>(player);
            SetReference(abilities, "aimCamera", camera);
        }

        private static void SetupPathSelection(GameObject root, UnitSummoner summoner)
        {
            WaypointPath first = root.transform.Find("Path 1")?.GetComponent<WaypointPath>();
            if (first == null || !first.IsValid) return;
            Transform second = root.transform.Find("Path 2");
            if (second == null)
            {
                GameObject pathObject = CreateObject("Path 2", root.scene);
                Undo.SetTransformParent(pathObject.transform, root.transform, "Parent second Path");
                WaypointPath path = Undo.AddComponent<WaypointPath>(pathObject);
                Vector3 end = first.GetPosition(first.Count - 1);
                Vector3[] positions = { root.transform.TransformPoint(new Vector3(12, 0, 6)),
                    root.transform.TransformPoint(new Vector3(12, 0, 20)), end };
                Transform[] points = new Transform[positions.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    GameObject point = CreateObject("Waypoint " + (i + 1), root.scene);
                    Undo.SetTransformParent(point.transform, pathObject.transform, "Parent Waypoint");
                    point.transform.position = positions[i];
                    points[i] = point.transform;
                }
                Undo.RecordObject(path, "Configure second Path");
                path.Configure(points);
                second = pathObject.transform;
            }
            if (second.GetComponent<WaypointPath>() == null)
                throw new InvalidOperationException("Path 2 must contain WaypointPath.");
            SerializedObject settings = new SerializedObject(summoner);
            SerializedProperty paths = settings.FindProperty("availablePaths");
            if (paths.arraySize == 0)
            {
                paths.arraySize = 2;
                paths.GetArrayElementAtIndex(0).objectReferenceValue = first;
                paths.GetArrayElementAtIndex(1).objectReferenceValue = second.GetComponent<WaypointPath>();
                settings.ApplyModifiedProperties();
            }
        }

        private static void EnsureCombat(GameObject unit, Faction faction)
        {
            Combatant health = unit.GetComponent<Combatant>();
            if (health == null)
            {
                health = Undo.AddComponent<Combatant>(unit);
                SerializedObject settings = new SerializedObject(health);
                settings.FindProperty("faction").enumValueIndex = (int)faction;
                settings.ApplyModifiedProperties();
            }
            if (unit.GetComponent<UnitCombat>() == null)
            {
                UnitCombat combat = Undo.AddComponent<UnitCombat>(unit);
                if (faction == Faction.Enemy)
                {
                    SerializedObject settings = new SerializedObject(combat);
                    settings.FindProperty("attackDamage").floatValue = 4f;
                    settings.FindProperty("attackInterval").floatValue = 1.2f;
                    settings.ApplyModifiedProperties();
                }
            }
            health.RefreshTeamColor();
        }

        private static WaypointPath SetupPath(GameObject root)
        {
            Transform existing = root.transform.Find("Path 1");
            if (existing != null)
                return existing.GetComponent<WaypointPath>();
            GameObject pathObject = CreateObject("Path 1", root.scene);
            Undo.SetTransformParent(pathObject.transform, root.transform, "Parent Path");
            WaypointPath path = Undo.AddComponent<WaypointPath>(pathObject);
            Vector3[] points = { new Vector3(0f, 0f, 6f), new Vector3(0f, 0f, 16f), new Vector3(6f, 0f, 28f) };
            SerializedObject serialized = new SerializedObject(path);
            SerializedProperty waypoints = serialized.FindProperty("waypoints");
            waypoints.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                GameObject waypoint = CreateObject("Waypoint " + (i + 1), root.scene);
                Undo.SetTransformParent(waypoint.transform, pathObject.transform, "Parent Waypoint");
                waypoint.transform.localPosition = points[i];
                GameObject marker = CreatePrimitive("Marker", PrimitiveType.Sphere, waypoint.transform);
                Undo.DestroyObjectImmediate(marker.GetComponent<SphereCollider>());
                marker.transform.localPosition = Vector3.up * 0.2f;
                marker.transform.localScale = Vector3.one * 0.4f;
                waypoints.GetArrayElementAtIndex(i).objectReferenceValue = waypoint.transform;
            }
            serialized.ApplyModifiedProperties();
            return path;
        }

        private static GameObject CreateObject(string name, Scene scene)
        {
            GameObject item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, scene);
            Undo.RegisterCreatedObjectUndo(item, "Create " + name);
            return item;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
            item.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(item, "Create " + name);
            return item;
        }

        private static void SetReference(UnityEngine.Object component, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property.objectReferenceValue == value)
                return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
