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

        // Styled battles already show the lanes as dirt roads, so lines stay quiet: a faint trace for every
        // Path and a softly pulsing gold line for the Path new troops will take.
        private bool styled;
        private static readonly Color StyledIdle = new Color(0.86f, 0.82f, 0.7f);
        private static readonly Color StyledSelected = new Color(1f, 0.82f, 0.38f);

        public void Configure(PrototypeBattle value, Shader shader, bool styledLook = false)
        {
            battle = value;
            styled = styledLook;
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
                for (int j = 0; j < line.positionCount; j++) line.SetPosition(j, battle.Paths[i].GetPosition(j) + Vector3.up * (styled ? 0.05f : 0.025f));
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                lines[i] = line;
            }
            ApplyLook();
        }

        private void ApplyLook()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            for (int i = 0; i < lines.Length; i++)
            {
                bool selected = battle.Summoner != null && battle.Summoner.SelectedPath == battle.Paths[i];
                if (!styled)
                {
                    lines[i].widthMultiplier = selected ? 0.22f : 0.07f;
                    continue;
                }
                lines[i].widthMultiplier = selected ? 0.14f + 0.04f * pulse : 0.04f;
                materials[i].SetColor("_BaseColor", selected ? Color.Lerp(StyledSelected, Color.white, 0.25f * pulse) : StyledIdle);
            }
        }

        // Per-front troop counts and damaged units (nearest first) for the HUD; refreshed four times a second.
        public int FrontCount => alliesPerPath != null ? alliesPerPath.Length : 0;
        public int AlliesOn(int front) => alliesPerPath[front];
        public int EnemiesOn(int front) => enemiesPerPath[front];
        public IReadOnlyList<Combatant> DamagedUnits => visible;
        // Every Combatant found by the last scan, including dead or destroyed ones; callers filter.
        public IReadOnlyList<Combatant> Units => units;

        private void Update()
        {
            if (battle == null || battle.Camera == null) return;
            ApplyLook();
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.25f;
            Scan();
        }

        public void Scan()
        {
            if (battle == null || battle.Camera == null) return;
            units = battle.Root.GetComponentsInChildren<Combatant>();
            System.Array.Clear(alliesPerPath, 0, alliesPerPath.Length);
            System.Array.Clear(enemiesPerPath, 0, enemiesPerPath.Length);
            visible.Clear();
            foreach (Combatant unit in units)
            {
                // Hero and both strongholds have their own HUD bars.
                if (!unit.IsAlive || unit == battle.Hero || unit == battle.Objective.EnemyBase || unit == battle.AlliedBase) continue;
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

        private void OnDestroy()
        {
            foreach (Material material in materials)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
