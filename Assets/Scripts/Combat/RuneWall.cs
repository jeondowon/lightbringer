using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Combat
{
    // Rune Wall: a barrier the hero raises for a few seconds. Enemies within its taunt radius attack it instead of
    // anything else (status-immune bosses ignore it). It has no collider, so troops walk through it and it blocks
    // neither movement nor line of sight.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class RuneWall : MonoBehaviour
    {
        public const float Width = 4f, Height = 2.5f, Thickness = 0.6f;
        // Height of the wall's centre above the ground: the troops' centre, so melee reach measures as for a unit.
        private const float CentreHeight = 0.8f;
        private static readonly List<RuneWall> raised = new List<RuneWall>();
        private Combatant health;
        private float remaining, tauntRadius;
        public Combatant Health => health;

        public static RuneWall Raise(Vector3 ground, Vector3 facing, Faction faction, float maximumHealth, float duration,
            float taunt, Transform parent)
        {
            GameObject root = new GameObject("Rune Wall");
            root.transform.SetParent(parent, false);
            Vector3 flat = Vector3.ProjectOnPlane(facing, Vector3.up);
            root.transform.SetPositionAndRotation(ground + Vector3.up * CentreHeight,
                Quaternion.LookRotation(flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward));
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Wall";
            Collider solid = block.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(solid); else DestroyImmediate(solid);
            block.transform.SetParent(root.transform, false);
            block.transform.localPosition = Vector3.up * (Height * 0.5f - CentreHeight);
            block.transform.localScale = new Vector3(Width, Height, Thickness);
            Combatant combatant = root.AddComponent<Combatant>();
            combatant.Configure(faction, maximumHealth);
            RuneWall wall = root.AddComponent<RuneWall>();
            wall.health = combatant;
            wall.remaining = duration;
            wall.tauntRadius = taunt;
            // Registered here rather than in OnEnable so edit-mode validation sees it too; destroyed walls are pruned.
            raised.Add(wall);
            return wall;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (delta <= 0f || health == null || !health.IsAlive) return;
            remaining -= delta;
            if (remaining > 0f) return;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        // The nearest standing wall of the other side whose taunt reaches `attacker`, or null.
        public static Combatant TauntFor(Combatant attacker)
        {
            if (attacker == null || attacker.StatusImmune) return null;
            Vector3 position = attacker.transform.position;
            Combatant nearest = null;
            float best = float.PositiveInfinity;
            for (int i = raised.Count - 1; i >= 0; i--)
            {
                RuneWall wall = raised[i];
                if (wall == null) { raised.RemoveAt(i); continue; }
                if (wall.health == null || !wall.health.IsAlive || wall.health.Faction == attacker.Faction
                    || wall.gameObject.scene != attacker.gameObject.scene) continue;
                float distance = Vector3.ProjectOnPlane(wall.transform.position - position, Vector3.up).sqrMagnitude;
                if (distance <= wall.tauntRadius * wall.tauntRadius && distance < best)
                {
                    best = distance;
                    nearest = wall.health;
                }
            }
            return nearest;
        }
    }
}
