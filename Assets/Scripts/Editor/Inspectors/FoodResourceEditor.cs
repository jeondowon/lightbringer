using Lightbringer.Resources;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    [CustomEditor(typeof(FoodResource))]
    public sealed class FoodResourceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (Application.isPlaying)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField("Current Food", ((FoodResource)target).CurrentFood);
            }
            else
                EditorGUILayout.HelpBox("Current Food is shown during Play. Each Play starts with Starting Food.", MessageType.Info);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
