using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Lightbringer.Resources;
using Lightbringer.Units;
using Lightbringer.Pathing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Runs once after import in the existing Editor, when it is not in Play mode.
    // Uses an unsaved additive scene, closes it, and restores the original active scene.
    [InitializeOnLoad]
    public static partial class GreyboxValidation
    {
        private const string Revision = "Lightbringer.GreyboxValidation.EnemyRoster.v1";
        private const string LastCheckedScriptsKey = "Lightbringer.LastCheckedScripts";
        private static readonly List<string> Results = new List<string>();

        static GreyboxValidation()
        {
            EditorApplication.update += RunOnceWhenIdle;
        }

        private static void RunOnceWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
            EditorApplication.update -= RunOnceWhenIdle;
            if (SessionState.GetString(LastCheckedScriptsKey, "") == GreyboxScriptAutoRefresh.GetStamp())
                return;
            Run();
        }

        [MenuItem("Lightbringer/Validate Greybox Systems")]
        public static void Run() => RunChecks();

        public static bool RunChecks()
        {
            if (Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogWarning("Stop Play and leave Prefab mode before validating Greybox systems.");
                return false;
            }

            Results.Clear();
            SessionState.SetString(LastCheckedScriptsKey, GreyboxScriptAutoRefresh.GetStamp());
            Scene original = SceneManager.GetActiveScene();
            UnityEngine.Object[] originalSelection = Selection.objects;
            Undo.IncrementCurrentGroup();
            int validationUndoGroup = Undo.GetCurrentGroup();
            Scene temporary = default;
            bool passed = false;
            try
            {
                temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporary);
                ValidateFoodAndSummoning();
                ValidatePathMovement();
                ValidateCombat();
                ValidateAura();
                ValidateObjective();
                ValidateSelection();
                ValidateHeroSystems();
                ValidateProgression();
                ValidateCampaignIntegration();
                ValidateEnemyRoster();
                ValidateKnightCharge();
                ValidatePlaytestRecorder();
                ValidateArtStyle();
                ValidateCombatScale();
                passed = true;
            }
            catch (Exception exception)
            {
                Results.Add("FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                // Setup checks own their Undo groups; remove them without touching earlier user edits.
                Undo.RevertAllDownToGroup(validationUndoGroup);
                if (original.IsValid() && original.isLoaded)
                    SceneManager.SetActiveScene(original);
                if (temporary.IsValid())
                    EditorSceneManager.CloseScene(temporary, true);
                Selection.objects = originalSelection;
                Physics.SyncTransforms();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Validation"));
                Directory.CreateDirectory(directory);
                string report = (passed ? "PASS" : "FAIL") + " | " + Revision + " | "
                    + DateTime.UtcNow.ToString("O") + Environment.NewLine
                    + string.Join(Environment.NewLine, Results)
                    + Environment.NewLine + "Scope: in-Editor component/physics checks; manual Game-view input and visual checks remain.";
                File.WriteAllText(Path.Combine(directory, "GreyboxChecks.txt"), report);
                if (passed)
                    Debug.Log("Greybox validation PASS: " + Results.Count + " checks. See Docs/Validation/GreyboxChecks.txt.");
            }
            return passed;
        }

        private static void ValidateFoodAndSummoning()
        {
            // Far from the current prototype, to avoid user scene colliders without moving them.
            Vector3 origin = new Vector3(10000f, 0f, 10000f);
            GameObject hero = new GameObject("Validation Hero");
            hero.transform.position = origin + Vector3.up;
            FoodResource food = hero.AddComponent<FoodResource>();
            Invoke(food, "Awake");
            Check(food.CurrentFood == 0f, "Food starts at zero");
            Invoke(food, "GenerateFood", 2f);
            Check(food.CurrentFood == 10f, "Two seconds generates ten Food");
            Check(!food.TrySpend(11f) && food.CurrentFood == 10f, "Insufficient Food is not spent");
            Check(food.TrySpend(10f) && food.CurrentFood == 0f, "Exact balance can be spent");
            Check(!food.TrySpend(-1f) && !food.TrySpend(float.NaN)
                && !food.TrySpend(float.PositiveInfinity) && food.CurrentFood == 0f,
                "Invalid costs are rejected without changing balance");
            Invoke(food, "GenerateFood", 100f);
            Check(food.CurrentFood == 100f, "Generation stops at the maximum");
            Invoke(food, "GenerateFood", 0f);
            Check(food.CurrentFood == 100f, "Paused time generates no Food");
            food.enabled = false;
            Check(!food.TrySpend(1f), "Disabled resource rejects spending");
            food.enabled = true;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = origin;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            GameObject template = new GameObject("Validation Template");
            template.SetActive(false);
            CharacterController capsule = template.AddComponent<CharacterController>();
            capsule.height = 1.6f;
            capsule.radius = 0.35f;
            template.AddComponent<UnitPathFollower>();
            GameObject soldiers = new GameObject("Validation Soldiers");
            WaypointPath path = CreatePath(origin, new[] { new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 15f) });
            UnitSummoner summoner = hero.AddComponent<UnitSummoner>();
            SetReference(summoner, "food", food);
            SetReference(summoner, "soldierTemplate", capsule);
            SetReference(summoner, "soldiersParent", soldiers.transform);
            SetReference(summoner, "selectedPath", path);
            Check(summoner.TrySummon() && soldiers.transform.childCount == 1 && food.CurrentFood == 90f,
                "Successful summon creates one soldier and charges ten Food");
            Vector3 firstPosition = soldiers.transform.GetChild(0).position;
            Check(soldiers.transform.GetChild(0).GetComponent<UnitPathFollower>().AssignedPath == path,
                "Summoned soldier receives the selected Path before activation");
            Check(summoner.TrySummon() && soldiers.transform.childCount == 2 && food.CurrentFood == 80f,
                "Second summon creates exactly one more soldier");
            Check(Vector3.Distance(firstPosition, soldiers.transform.GetChild(1).position) >= 0.7f,
                "Consecutive soldiers do not overlap");
            SetReference(summoner, "selectedPath", null);
            Check(!summoner.TrySummon() && food.CurrentFood == 80f && soldiers.transform.childCount == 2,
                "Missing Path creates no soldier and spends no Food");
            SetReference(summoner, "selectedPath", path);

            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = origin + Vector3.up;
            blocker.transform.localScale = new Vector3(20f, 20f, 20f);
            Check(!summoner.TrySummon() && food.CurrentFood == 80f && soldiers.transform.childCount == 2,
                "Blocked placement neither spends Food nor creates a soldier");
            UnityEngine.Object.DestroyImmediate(blocker);
            UnityEngine.Object.DestroyImmediate(ground);
            Check(!summoner.TrySummon() && food.CurrentFood == 80f && soldiers.transform.childCount == 2,
                "Missing ground neither spends Food nor creates a soldier");
            Invoke(food, "Awake");
            Check(!summoner.TrySummon() && soldiers.transform.childCount == 2 && food.CurrentFood == 0f,
                "Insufficient Food creates no soldier");
        }

        private static void ValidatePathMovement()
        {
            Vector3 origin = new Vector3(20000f, 0f, 20000f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = origin;
            ground.transform.localScale = Vector3.one * 4f;
            WaypointPath path = CreatePath(origin, new[] { Vector3.zero, Vector3.forward * 4f, new Vector3(4f, 0f, 4f) });
            Check(path.IsValid && path.FindEntryWaypoint(origin - Vector3.forward * 2f) == 0,
                "Unit behind the Path enters at the first waypoint");
            Check(path.FindEntryWaypoint(origin + Vector3.forward * 2f) == 1,
                "Unit halfway along the Path enters in the forward direction");

            GameObject unit = new GameObject("Validation Moving Unit");
            unit.transform.position = origin + new Vector3(0f, 0.85f, -2f);
            CharacterController controller = unit.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            UnitPathFollower follower = unit.AddComponent<UnitPathFollower>();
            WaypointPath invalid = new GameObject("Invalid Path").AddComponent<WaypointPath>();
            Check(!follower.TryAssignPath(invalid), "Empty Path is rejected");
            Check(follower.TryAssignPath(path), "Valid Path can be assigned");
            WaypointPath other = CreatePath(origin, new[] { Vector3.right * 10f });
            Check(!follower.TryAssignPath(other) && follower.AssignedPath == path,
                "Existing unit keeps its original Path");
            Physics.SyncTransforms();
            Vector3 before = unit.transform.position;
            Invoke(follower, "Tick", 0f);
            Check(unit.transform.position == before, "Paused unit does not move");
            Invoke(follower, "Tick", 0.1f);
            float moved = unit.transform.position.z - before.z;
            Check(moved > 0.28f && moved < 0.32f, "Unit moves at the configured three metres per second");
            for (int i = 0; i < 200; i++)
                Invoke(follower, "Tick", 0.05f);
            Vector3 remaining = unit.transform.position - path.GetPosition(path.Count - 1);
            remaining.y = 0f;
            Check(follower.HasReachedEnd && remaining.magnitude <= 0.35f,
                "Unit traverses the bend and reaches the final waypoint");
            Check(controller.isGrounded, "Unit remains grounded during travel");
            Vector3 end = unit.transform.position;
            for (int i = 0; i < 20; i++)
                Invoke(follower, "Tick", 0.05f);
            Vector3 drift = unit.transform.position - end;
            drift.y = 0f;
            Check(drift.magnitude < 0.01f, "Unit stops horizontally after reaching the end");

            GameObject blockedUnit = new GameObject("Validation Blocked Unit");
            blockedUnit.transform.position = origin + new Vector3(-5f, 0.85f, -2f);
            CharacterController blockedController = blockedUnit.AddComponent<CharacterController>();
            blockedController.height = 1.6f;
            blockedController.radius = 0.35f;
            UnitPathFollower blockedFollower = blockedUnit.AddComponent<UnitPathFollower>();
            WaypointPath blockedPath = CreatePath(origin, new[] { new Vector3(-5f, 0f, 4f) });
            blockedFollower.TryAssignPath(blockedPath);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(-5f, 2f, 0f);
            wall.transform.localScale = new Vector3(4f, 4f, 1f);
            Physics.SyncTransforms();
            for (int i = 0; i < 100; i++)
                Invoke(blockedFollower, "Tick", 0.05f);
            Check(blockedUnit.transform.position.z < origin.z - 0.6f && !blockedFollower.HasReachedEnd,
                "Unit cannot pass through a blocking wall");
        }

        private static WaypointPath CreatePath(Vector3 origin, Vector3[] positions)
        {
            GameObject root = new GameObject("Validation Path");
            WaypointPath path = root.AddComponent<WaypointPath>();
            SerializedObject serialized = new SerializedObject(path);
            SerializedProperty points = serialized.FindProperty("waypoints");
            points.arraySize = positions.Length;
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject point = new GameObject("Waypoint " + i);
                point.transform.SetParent(root.transform);
                point.transform.position = origin + positions[i];
                points.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return path;
        }

        private static void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            Results.Add("PASS: " + name);
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(target, arguments);
        }

        private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
