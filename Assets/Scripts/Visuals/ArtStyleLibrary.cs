using System;
using Lightbringer.Units;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    public enum VisualId
    {
        Hero, Swordsman, Archer, Shieldbearer, Spearman, Priest, Mage, Knight, Dragon,
        EnemyRaider, EnemyArcher, EnemyStronghold
    }

    // A prop (staff, weapon) parented to a named bone of an override model so it follows animation.
    [Serializable]
    public sealed class PropAttachment
    {
        public GameObject prefab;
        public string boneName = "RightHand";
        public Vector3 localPosition;
        public Vector3 localEuler;
        public Vector3 localScale = Vector3.one;
        [Tooltip("Keep the prop mostly upright on top of empty-hand animation clips.")]
        public bool stabilize = true;
    }

    // A finished model replaces the procedural silhouette for one VisualId.
    // The prefab pivot is expected at the feet (ground contact), facing +Z.
    [Serializable]
    public sealed class VisualOverride
    {
        public VisualId id;
        public GameObject prefab;
        public Vector3 localOffset;
        public Vector3 localEuler;
        [Min(0.01f)] public float scale = 1f;
        [Header("Legacy animation clips (optional)")]
        public AnimationClip idleClip;
        public AnimationClip moveClip;
        public AnimationClip actionClip;
        [Tooltip("Ground speed (m/s) at which the move clip plays at normal speed.")]
        [Min(0.1f)] public float moveClipSpeed = 4f;

        [Header("Props on bones (optional)")]
        public PropAttachment[] attachments = new PropAttachment[0];

        public bool HasAnimation => idleClip != null || moveClip != null || actionClip != null;
    }

    // Single source for the Art Pass look. Colours bake into procedural silhouettes;
    // overrides swap in real models one VisualId at a time without touching gameplay code.
    [CreateAssetMenu(menuName = "Lightbringer/Art Style Library", fileName = "ArtStyleLibrary")]
    public sealed class ArtStyleLibrary : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Lightbringer/Toon with baked vertex palette and aura reaction on.")]
        public Material alliedMaterial;
        [Tooltip("Lightbringer/Toon with baked vertex palette, no aura reaction.")]
        public Material enemyMaterial;
        public Material groundMaterial;
        public Material auraRunesMaterial;
        public Material auraMotesMaterial;
        [Tooltip("Lightbringer/Lightning, used by hero strike spells.")]
        public Material lightningMaterial;

        [Header("Allied palette (character sheet v1)")]
        public Color ivory = new Color(0.93f, 0.91f, 0.86f);
        public Color royalBlue = new Color(0.2f, 0.33f, 0.68f);
        public Color gold = new Color(0.86f, 0.68f, 0.3f);
        public Color steel = new Color(0.66f, 0.7f, 0.77f);
        public Color leather = new Color(0.24f, 0.17f, 0.14f);
        public Color skin = new Color(0.96f, 0.81f, 0.7f);
        public Color hair = new Color(0.17f, 0.12f, 0.1f);
        public Color lightGlow = new Color(0.62f, 0.8f, 1f);
        public Color holyGlow = new Color(1f, 0.86f, 0.52f);
        public Color dragonScale = new Color(0.92f, 0.94f, 0.98f);
        public Color dragonMembrane = new Color(0.66f, 0.76f, 0.96f);

        [Header("Corrupted enemy palette")]
        public Color corruptBody = new Color(0.16f, 0.12f, 0.2f);
        public Color corruptArmor = new Color(0.3f, 0.2f, 0.34f);
        public Color bone = new Color(0.8f, 0.75f, 0.64f);
        public Color corruptGlow = new Color(1f, 0.22f, 0.35f);
        public Color corruptStone = new Color(0.25f, 0.22f, 0.28f);

        [Header("Battlefield lighting")]
        public Color sunColor = new Color(1f, 0.95f, 0.86f);
        [Min(0f)] public float sunIntensity = 1.35f;
        public Color ambientSky = new Color(0.62f, 0.7f, 0.85f);
        public Color ambientEquator = new Color(0.5f, 0.52f, 0.55f);
        public Color ambientGround = new Color(0.3f, 0.28f, 0.26f);
        public Color fogColor = new Color(0.72f, 0.76f, 0.84f);
        [Min(0f)] public float fogStart = 45f;
        [Min(0f)] public float fogEnd = 160f;
        [Tooltip("Optional global post-processing (bloom, tonemapping, grading).")]
        public VolumeProfile postProcessing;

        [Header("Aura")]
        [ColorUsage(false, true)] public Color auraColor = new Color(0.55f, 0.75f, 1.6f);

        [Header("Model overrides (Art Pass replacements)")]
        public VisualOverride[] overrides = new VisualOverride[0];

        public bool HasMaterials => alliedMaterial != null && enemyMaterial != null;

        public VisualOverride FindOverride(VisualId id)
        {
            if (overrides == null) return null;
            foreach (VisualOverride entry in overrides)
                if (entry != null && entry.id == id && entry.prefab != null) return entry;
            return null;
        }

        public static VisualId ForUnit(UnitKind kind) => (VisualId)((int)VisualId.Swordsman + (int)kind);
        public static bool IsEnemy(VisualId id) => id >= VisualId.EnemyRaider;

        private void OnValidate() => SilhouetteFactory.ClearCache();
    }
}
