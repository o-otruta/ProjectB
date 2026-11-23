using UnityEditor;
using UnityEngine;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    [CustomEditor(typeof(ResponsiveRect))]
    [CanEditMultipleObjects]
    public class ResponsiveRectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (targets.Length == 1)
            {
                var rect = (ResponsiveRect)target;
                EditorGUILayout.LabelField("Last applied", rect.AppliedMode.ToString(), EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture Portrait")) Capture(LayoutMode.Portrait);
                if (GUILayout.Button("Capture Landscape")) Capture(LayoutMode.Landscape);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Portrait")) Apply(LayoutMode.Portrait);
                if (GUILayout.Button("Apply Landscape")) Apply(LayoutMode.Landscape);
            }
            EditorGUILayout.HelpBox("Capture stores the current RectTransform as the preset of that mode. Use ProjectB/UI/Preview Portrait | Landscape to switch the whole scene.", MessageType.None);
        }

        private void Capture(LayoutMode mode)
        {
            foreach (Object t in targets)
            {
                Undo.RecordObject(t, $"Capture {mode} Preset");
                ((ResponsiveRect)t).CapturePreset(mode);
                EditorUtility.SetDirty(t);
            }
        }

        private void Apply(LayoutMode mode)
        {
            foreach (Object t in targets)
            {
                var rect = (ResponsiveRect)t;
                Undo.RecordObjects(new Object[] { rect, rect.transform }, $"Apply {mode} Preset");
                rect.ApplyLayoutMode(mode);
                EditorUtility.SetDirty(rect);
            }
        }
    }

    [CustomEditor(typeof(AdaptiveLayoutGroup))]
    [CanEditMultipleObjects]
    public class AdaptiveLayoutGroupEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Padding", "portraitSettings", "landscapeSettings", "m_ReverseArrangement",
            "m_ChildControlWidth", "m_ChildControlHeight", "m_ChildScaleWidth", "m_ChildScaleHeight",
            "m_ChildForceExpandWidth", "m_ChildForceExpandHeight"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in DrawnProperties)
            {
                SerializedProperty property = serializedObject.FindProperty(name);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (targets.Length == 1)
            {
                EditorGUILayout.LabelField("Last applied", ((AdaptiveLayoutGroup)target).AppliedMode.ToString(), EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Portrait")) Apply(LayoutMode.Portrait);
                if (GUILayout.Button("Apply Landscape")) Apply(LayoutMode.Landscape);
            }
        }

        private void Apply(LayoutMode mode)
        {
            foreach (Object t in targets)
            {
                Undo.RecordObject(t, $"Apply {mode} Layout");
                ((AdaptiveLayoutGroup)t).ApplyLayoutMode(mode);
                EditorUtility.SetDirty(t);
            }
        }
    }
}
