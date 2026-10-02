using Lightbringer.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Swaps the greybox capsule for a styled visual. Lives on the unit template so clones carry the
    // style reference; the summoner/spawner picks the VisualId after choosing the troop.
    // Without a style library nothing changes, so greybox scenes and validations keep capsules.
    [DisallowMultipleComponent]
    public sealed class UnitAppearance : MonoBehaviour
    {
        public const string StyledName = "Styled Visual";
        public const string GreyboxName = "Visual";

        [SerializeField] private ArtStyleLibrary style;
        [Tooltip("Ground contact relative to the unit pivot (CharacterController bottom).")]
        [SerializeField] private float feetOffset = -0.8f;

        public ArtStyleLibrary Style => style;

        public void Configure(ArtStyleLibrary library, float feet)
        {
            style = library;
            feetOffset = feet;
        }

        public bool Apply(VisualId id) => Attach(gameObject, id, style, feetOffset) != null;

        public static Transform Attach(GameObject owner, VisualId id, ArtStyleLibrary style, float feetOffset)
        {
            if (owner == null || style == null || !style.HasMaterials) return null;
            Transform ownerTransform = owner.transform;
            Remove(ownerTransform.Find(StyledName));
            Remove(ownerTransform.Find(GreyboxName));
            if (owner.TryGetComponent(out MeshRenderer ownRenderer)) ownRenderer.enabled = false;
            if (owner.TryGetComponent(out Combatant health)) health.UseTeamTint = false;

            GameObject visual = new GameObject(StyledName);
            Transform root = visual.transform;
            root.SetParent(ownerTransform, false);
            // Author in world metres even under a scaled owner (the stronghold is a scaled cube).
            Vector3 scale = ownerTransform.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(scale.x, 0.0001f), 1f / Mathf.Max(scale.y, 0.0001f), 1f / Mathf.Max(scale.z, 0.0001f));
            root.localPosition = new Vector3(0f, feetOffset / Mathf.Max(scale.y, 0.0001f), 0f);

            VisualOverride replacement = style.FindOverride(id);
            if (replacement != null)
            {
                GameObject model = Object.Instantiate(replacement.prefab, root, false);
                model.name = replacement.prefab.name;
                model.transform.localPosition = replacement.localOffset;
                model.transform.localRotation = Quaternion.Euler(replacement.localEuler);
                model.transform.localScale = Vector3.one * replacement.scale;
                // Gameplay collision stays on the CharacterController / objective box only.
                AttachProps(model.transform, replacement);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Remove(collider);
                if (replacement.HasAnimation)
                    model.AddComponent<ModelAnimationDriver>().Configure(replacement);
                // Unrigged stand-in models still get stride bob and lean until real animation exists.
                else if (model.GetComponentInChildren<Animator>() == null)
                    visual.AddComponent<VisualMotion>().Configure(new Silhouette { BobHeight = 0.035f, BobRate = 9f, LeanDegrees = 5f }, null);
                return root;
            }

            Material material = ArtStyleLibrary.IsEnemy(id) ? style.enemyMaterial : style.alliedMaterial;
            Silhouette silhouette = SilhouetteFactory.Get(style, id);
            AddRenderer(visual, silhouette.Body, material);
            Transform[] parts = new Transform[silhouette.Parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject part = new GameObject(silhouette.Parts[i].Motion.ToString());
                part.transform.SetParent(root, false);
                part.transform.localPosition = silhouette.Parts[i].Pivot;
                AddRenderer(part, silhouette.Parts[i].Mesh, material);
                parts[i] = part.transform;
            }
            visual.AddComponent<VisualMotion>().Configure(silhouette, parts);
            return root;
        }

        private static void AttachProps(Transform model, VisualOverride source)
        {
            if (source.attachments == null) return;
            foreach (PropAttachment prop in source.attachments)
            {
                if (prop == null || prop.prefab == null) continue;
                Transform bone = FindDeep(model, prop.boneName);
                if (bone == null) { Debug.LogWarning($"Bone {prop.boneName} not found on {model.name}; prop skipped."); continue; }
                GameObject item = Object.Instantiate(prop.prefab, bone, false);
                item.name = prop.prefab.name;
                item.transform.localPosition = prop.localPosition;
                item.transform.localRotation = Quaternion.Euler(prop.localEuler);
                item.transform.localScale = prop.localScale;
                if (prop.stabilize) item.AddComponent<PropStabilizer>().Configure(bone, model);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void AddRenderer(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void Remove(Object target)
        {
            if (target == null) return;
            if (target is Transform transform) target = transform.gameObject;
            if (target is GameObject item) item.SetActive(false);
            if (target is Collider collider) collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(target); else Object.DestroyImmediate(target);
        }
    }
}
