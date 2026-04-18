#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ShootTheRockSceneLayout))]
public class ShootTheRockSceneLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.HelpBox(
            "Scale RockWall in the scene, then press Bake RockWall Scale Into Data. The bake now uses a safe uniform scale, so the wall grows by more pixels instead of turning into a stretched pancake. If the authoring data gets weird, use Reset Wall Defaults.",
            MessageType.Info);

        ShootTheRockSceneLayout layout = (ShootTheRockSceneLayout)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Wall Preview", GUILayout.Height(28f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Refresh Shoot the ROCK Preview");
                layout.RefreshPreview();
                MarkSceneDirty();
            }

            if (GUILayout.Button("Bake RockWall Scale Into Data", GUILayout.Height(28f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Bake RockWall Scale Into Data");
                layout.BakeRockWallScaleIntoWallData();
                MarkSceneDirty();
            }
        }

        if (GUILayout.Button("Reset Wall Defaults", GUILayout.Height(24f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Reset Shoot the ROCK Wall Defaults");
            layout.ResetWallAuthoringDefaults();
            MarkSceneDirty();
        }
    }

    private static void MarkSceneDirty()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
#endif
