using System;
using UnityEngine;

namespace Lightbringer.Combat
{
    // Grow only when saturated, then reuse. Never silently discard crowded-battle targets.
    public sealed class PhysicsQueryBuffer
    {
        public Collider[] Items { get; private set; }
        public int Count { get; private set; }
        public PhysicsQueryBuffer(int capacity = 32) => Items = new Collider[Mathf.Max(4, capacity)];
        public int Overlap(Vector3 centre, float radius, int mask = -1)
        {
            while (true)
            {
                Count = Physics.OverlapSphereNonAlloc(centre, radius, Items, mask, QueryTriggerInteraction.Ignore);
                if (Count < Items.Length) return Count;
                Items = new Collider[Items.Length * 2];
            }
        }
    }
}
