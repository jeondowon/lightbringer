using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Core;
using UnityEngine;

namespace Lightbringer.UI
{
    public sealed class BattlefieldReadability : MonoBehaviour
    {
        private PrototypeBattle battle;
        private readonly List<Material> materials = new List<Material>();
        private LineRenderer[] lines;
        private Combatant[] units = new Combatant[0];
        private float nextScan;
        private readonly List<Combatant> visible = new List<Combatant>();
        private int[] alliesPerPath;
        private int[] enemiesPerPath;

        public void Configure(PrototypeBattle value, Shader shader)
        {
            battle = value;
            lines = new LineRenderer[battle.Paths.Length];
            alliesPerPath = new int[lines.Length]; enemiesPerPath = new int[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                GameObject item = new GameObject("Visible Path " + (i + 1));
                item.transform.SetParent(transform, false);
                LineRenderer line = item.AddComponent<LineRenderer>();
                Material material = new Material(shader);
                material.SetColor("_BaseColor", Color.HSVToRGB(i * 0.22f + 0.45f, 0.6f, 0.85f));
                materials.Add(material);
                line.sharedMaterial = material;
                line.positionCount = battle.Paths[i].Count;
                for (int j = 0; j < line.positionCount; j++) line.SetPosition(j, battle.Paths[i].GetPosition(j) + Vector3.up * 0.025f);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                lines[i] = line;
            }
        }

        private void Update()
        {
            if (battle == null || battle.Camera == null) return;
            for (int i = 0; i < lines.Length; i++)
                lines[i].widthMultiplier = battle.Summoner != null && battle.Summoner.SelectedPath == battle.Paths[i] ? 0.22f : 0.07f;
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.25f;
            units = battle.Root.GetComponentsInChildren<Combatant>();
            System.Array.Clear(alliesPerPath, 0, alliesPerPath.Length);
            System.Array.Clear(enemiesPerPath, 0, enemiesPerPath.Length);
            visible.Clear();
            foreach (Combatant unit in units)
            {
                if (!unit.IsAlive || unit == battle.Hero || unit == battle.Objective.EnemyBase) continue;
                var follower = unit.GetComponent<Units.UnitPathFollower>();
                if (follower != null && follower.AssignedPath != null)
                {
                    for (int i = 0; i < lines.Length; i++)
                        if (follower.AssignedPath == battle.Paths[i]) alliesPerPath[i]++;
                    if (unit.Faction == Faction.Enemy)
                    {
                        int nearest = 0;
                        float distance = float.PositiveInfinity;
                        for (int i = 0; i < lines.Length; i++)
                        {
                            float candidate = Mathf.Abs(unit.transform.position.x - battle.Paths[i].GetPosition(1).x);
                            if (candidate < distance) { distance = candidate; nearest = i; }
                        }
                        enemiesPerPath[nearest]++;
                    }
                }
                if (unit.CurrentHealth < unit.MaximumHealth) visible.Add(unit);
            }
            Vector3 cameraPosition = battle.Camera.transform.position;
            visible.Sort((a, b) => (a.transform.position - cameraPosition).sqrMagnitude.CompareTo((b.transform.position - cameraPosition).sqrMagnitude));
        }

        private void OnGUI()
        {
            if (battle == null || battle.Camera == null || alliesPerPath == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 230, 16, 215, 100), GUI.skin.box);
            GUILayout.Label("FRONTS | Allies / Enemies");
            for (int i = 0; i < alliesPerPath.Length; i++) GUILayout.Label($"Path {i + 1}: {alliesPerPath[i]} / {enemiesPerPath[i]}");
            GUILayout.EndArea();
            Color previous = GUI.color;
            for (int i = 0; i < Mathf.Min(48, visible.Count); i++)
            {
                Combatant unit = visible[i];
                if (unit == null || !unit.IsAlive) continue;
                Vector3 point = battle.Camera.WorldToScreenPoint(unit.transform.position + Vector3.up * 1.25f);
                if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                Rect box = new Rect(point.x - 20, Screen.height - point.y, 40, 5);
                GUI.color = Color.black; GUI.DrawTexture(box, Texture2D.whiteTexture);
                box.width *= unit.CurrentHealth / unit.MaximumHealth;
                GUI.color = unit.Faction == Faction.Allied ? Color.cyan : Color.red;
                GUI.DrawTexture(box, Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
