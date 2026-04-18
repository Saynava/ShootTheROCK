#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ShootTheRockSceneImporter
{
    [MenuItem("Tools/Shoot the ROCK/Import Runtime Prototype Into Scene")]
    public static void ImportRuntimePrototypeIntoScene()
    {
        Debug.LogWarning(
            "Shoot the ROCK importer is deprecated in the simplified scene-authored workflow. " +
            "Place and move CannonRoot, GroundFloor, RockWall, and the Camera directly in the scene. " +
            "Play mode now uses existing scene transforms instead of rebuilding them through the importer.");
    }
}
#endif
