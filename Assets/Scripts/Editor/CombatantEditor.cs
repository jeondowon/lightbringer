using Lightbringer.Combat;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    [CustomEditor(typeof(Combatant))]
    public sealed class CombatantEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector())
                ((Combatant)target).RefreshTeamColor();
            if (Application.isPlaying)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField("Current Health", ((Combatant)target).CurrentHealth);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
