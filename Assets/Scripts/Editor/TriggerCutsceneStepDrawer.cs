#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TriggerCutscene.Step))]
public sealed class TriggerCutsceneStepDrawer : PropertyDrawer
{
    private const int TextAreaLineCount = 4;
    private static readonly string[] TargetFields = { "target" };
    private static readonly string[] DialogueFields = { "text" };
    private static readonly string[] MoveFields = { "axis", "coordinate", "speed" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing +
            EditorGUI.GetPropertyHeight(property.FindPropertyRelative("type"), true);
        foreach (string fieldName in GetVisibleFields(property))
        {
            height += EditorGUIUtility.standardVerticalSpacing +
                GetFieldHeight(property.FindPropertyRelative(fieldName));
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int originalIndent = EditorGUI.indentLevel;
        try
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawField(ref row, property.FindPropertyRelative("type"));
            foreach (string fieldName in GetVisibleFields(property))
            {
                DrawField(ref row, property.FindPropertyRelative(fieldName));
            }
        }
        finally
        {
            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }
    }

    private static string[] GetVisibleFields(SerializedProperty property)
    {
        var type = (TriggerCutscene.StepType)property.FindPropertyRelative("type").intValue;
        switch (type)
        {
            case TriggerCutscene.StepType.Dialogue:
                return DialogueFields;
            case TriggerCutscene.StepType.Move:
                return MoveFields;
            default:
                return TargetFields;
        }
    }

    private static void DrawField(ref Rect row, SerializedProperty property)
    {
        row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
        row.height = GetFieldHeight(property);
        if (property.name == "text")
        {
            DrawTextField(row, property);
            return;
        }
        EditorGUI.PropertyField(row, property, true);
    }

    private static float GetFieldHeight(SerializedProperty property)
    {
        if (property.name == "text")
        {
            return EditorGUIUtility.singleLineHeight * (TextAreaLineCount + 1) +
                EditorGUIUtility.standardVerticalSpacing;
        }
        return EditorGUI.GetPropertyHeight(property, true);
    }

    private static void DrawTextField(Rect position, SerializedProperty property)
    {
        GUIContent label = EditorGUI.BeginProperty(position, new GUIContent(property.displayName, property.tooltip), property);
        try
        {
            var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);
            var textRect = new Rect(position.x,
                labelRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width, EditorGUIUtility.singleLineHeight * TextAreaLineCount);
            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.TextArea(EditorGUI.IndentedRect(textRect), property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = text;
            }
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }
}
#endif
