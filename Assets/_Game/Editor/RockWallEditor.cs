using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RockWall))]
public class RockWallEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Authoring Tools", EditorStyles.boldLabel);

        RockWall rockWall = (RockWall)target;
        Vector2 bottomLeft = rockWall.GetBottomLeftAnchorWorld();
        EditorGUILayout.HelpBox(
            $"Bottom-left anchor stays fixed when applying the massive preset.\nCurrent anchor: ({bottomLeft.x:0.###}, {bottomLeft.y:0.###})",
            MessageType.Info);

        if (GUILayout.Button("Apply Massive Wall Preset"))
        {
            Undo.RecordObject(rockWall, "Apply Massive Wall Preset");
            Undo.RecordObject(rockWall.transform, "Apply Massive Wall Preset");
            rockWall.ApplyMassiveWallPresetKeepingBottomLeft();
            EditorUtility.SetDirty(rockWall);
            EditorUtility.SetDirty(rockWall.transform);
            EditorSceneManager.MarkSceneDirty(rockWall.gameObject.scene);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Rebuild Wall From Authoring"))
        {
            Undo.RecordObject(rockWall, "Rebuild Wall From Authoring");
            rockWall.RebuildWallFromAuthoringContext();
            EditorUtility.SetDirty(rockWall);
            EditorSceneManager.MarkSceneDirty(rockWall.gameObject.scene);
            SceneView.RepaintAll();
        }
    }
}
