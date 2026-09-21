using MobilePrototype;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TabHost))]
public class TabHostEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var host = (TabHost)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Edit MainTabs for tab count, scene paths, artwork and per-tab background policies. Structural edits apply on next Play. Policy edits apply while playing.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !host.IsReady || host.IsBusy))
            if (GUILayout.Button("Restart Current Scene Now")) host.RestartCurrent();
        if (GUILayout.Button("Select Tab Catalog")) Selection.activeObject = host.catalog;
    }
}
