using Lightbringer.Combat;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    [CustomEditor(typeof(UnitCombat))]
    public sealed class UnitCombatEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (Application.isPlaying)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField("Effective Damage", ((UnitCombat)target).EffectiveDamage);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
