#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShootTheRockSceneSync
{
    [MenuItem("Tools/Shoot the ROCK/Restore Saved State")]
    public static void RestoreSavedStateMenu()
    {
        RestoreSavedState();
    }

    [MenuItem("Tools/Shoot the ROCK/Save Current Checkpoint State")]
    public static void SaveCurrentCheckpointStateMenu()
    {
        SaveCurrentCheckpointState();
    }

    public static void SaveCurrentCheckpointState()
    {
        int removedMissingScripts = ShootTheRockSceneCleanup.CleanupMissingScriptsInActiveScene();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        ShootTheRockSceneStateSnapshot.SaveCurrentSceneStateSnapshot();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (removedMissingScripts > 0)
            Debug.Log($"Shoot the ROCK checkpoint state saved. Removed {removedMissingScripts} missing script component(s) first.");
        else
            Debug.Log("Shoot the ROCK checkpoint state saved.");
    }

    public static void RestoreSavedState()
    {
        if (!ShootTheRockSceneStateSnapshot.RestoreSavedSceneState())
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shoot the ROCK saved state restored from latest checkpoint snapshot.");
    }
}
#endif
