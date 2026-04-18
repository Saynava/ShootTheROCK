#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShootTheRockSceneRoot))]
public class ShootTheRockSceneRootEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "ShootTheRockSceneRoot is now legacy. The simplified workflow uses normal scene transforms as the source of truth. " +
            "Move the camera, cannon, floor, and RockWall directly in the scene. Resize the wall from the RockWall component, not from importer/root tooling.",
            MessageType.Warning);
    }
}
#endif
