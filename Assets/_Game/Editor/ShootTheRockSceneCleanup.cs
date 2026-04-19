#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShootTheRockSceneCleanup
{
    [MenuItem("Tools/Shoot the ROCK/Cleanup Missing Scripts In Active Scene")]
    public static void CleanupMissingScriptsInActiveSceneMenu()
    {
        int removedCount = CleanupMissingScriptsInActiveScene();
        if (removedCount <= 0)
        {
            Debug.Log("Shoot the ROCK cleanup: no missing scripts found in active scene.");
            return;
        }

        Debug.Log($"Shoot the ROCK cleanup removed {removedCount} missing script component(s) from the active scene.");
    }

    public static int CleanupMissingScriptsInActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        int removedCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            removedCount += CleanupMissingScriptsRecursive(roots[i]);

        if (removedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return removedCount;
    }

    private static int CleanupMissingScriptsRecursive(GameObject gameObject)
    {
        if (gameObject == null)
            return 0;

        int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
        Transform transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
            removedCount += CleanupMissingScriptsRecursive(transform.GetChild(i).gameObject);

        return removedCount;
    }
}
#endif
