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
            "ShootTheRockSceneRoot supports two isolated modes: Prototype-Wall and Motherload. " +
            "Motherload mode disables prototype-only objects (floor/wall/goal/prototype overlays) and uses chunk streaming + camera follow. " +
            "Use the context menu to switch mode if needed, or set sceneMode in the component manually.",
            MessageType.Info);
    }
}
#endif
