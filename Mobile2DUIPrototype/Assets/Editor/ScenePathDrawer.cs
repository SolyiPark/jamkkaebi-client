using MobilePrototype;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ScenePathAttribute))]
public class ScenePathDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
        EditorGUI.BeginChangeCheck();
        var selected = (SceneAsset)EditorGUI.ObjectField(position,label,scene,typeof(SceneAsset),false);
        if (EditorGUI.EndChangeCheck()) property.stringValue = selected ? AssetDatabase.GetAssetPath(selected) : "";
        EditorGUI.EndProperty();
    }
}
