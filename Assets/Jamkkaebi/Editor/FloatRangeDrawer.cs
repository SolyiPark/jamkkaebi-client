using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="FloatRange"/>를 Inspector 한 줄에 "Min [ ] Max [ ]" 형태로 그린다.
/// </summary>
[CustomPropertyDrawer(typeof(FloatRange))]
public class FloatRangeDrawer : PropertyDrawer
{
    private const float LabelWidth = 30f;
    private const float Gap = 6f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        // 앞쪽에 필드 이름을 쓰고, 남은 칸을 반씩 나눠 Min·Max를 그린다
        Rect content = EditorGUI.PrefixLabel(position, label);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;   // 들여쓰기가 남은 칸에도 적용되어 칸이 밀리는 것을 막는다

        float half = (content.width - Gap) * 0.5f;
        DrawField(new Rect(content.x, content.y, half, content.height), "Min", property.FindPropertyRelative("_min"));
        DrawField(new Rect(content.x + half + Gap, content.y, half, content.height), "Max", property.FindPropertyRelative("_max"));

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    private static void DrawField(Rect rect, string name, SerializedProperty value)
    {
        EditorGUI.LabelField(new Rect(rect.x, rect.y, LabelWidth, rect.height), name);
        EditorGUI.PropertyField(new Rect(rect.x + LabelWidth, rect.y, rect.width - LabelWidth, rect.height), value, GUIContent.none);
    }
}
