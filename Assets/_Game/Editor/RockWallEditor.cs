using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(RockWall))]
public class RockWallEditor : Editor
{
    private const string PaintModePrefKey = "ShootTheRock.RockWall.PaintMode";
    private const string BrushRadiusPrefKey = "ShootTheRock.RockWall.BrushRadius";
    private const string BrushHardnessPrefKey = "ShootTheRock.RockWall.BrushHardness";

    private static bool isBrushStrokeActive;

    private static bool PaintModeEnabled
    {
        get => EditorPrefs.GetBool(PaintModePrefKey, false);
        set => EditorPrefs.SetBool(PaintModePrefKey, value);
    }

    private static float BrushRadius
    {
        get => EditorPrefs.GetFloat(BrushRadiusPrefKey, 1.35f);
        set => EditorPrefs.SetFloat(BrushRadiusPrefKey, Mathf.Max(0.1f, value));
    }

    private static float BrushHardness
    {
        get => EditorPrefs.GetFloat(BrushHardnessPrefKey, 0.72f);
        set => EditorPrefs.SetFloat(BrushHardnessPrefKey, Mathf.Clamp01(value));
    }

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

        DrawAuthoringMapControls(rockWall);
        DrawBrushControls(rockWall);

        if (GUILayout.Button("Apply Massive Wall Preset"))
        {
            Undo.RecordObject(rockWall, "Apply Massive Wall Preset");
            Undo.RecordObject(rockWall.transform, "Apply Massive Wall Preset");
            rockWall.ApplyMassiveWallPresetKeepingBottomLeft();
            EditorUtility.SetDirty(rockWall);
            EditorUtility.SetDirty(rockWall.transform);
            MarkSceneDirty(rockWall.gameObject.scene);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Rebuild Wall From Authoring"))
        {
            Undo.RecordObject(rockWall, "Rebuild Wall From Authoring");
            rockWall.RebuildWallFromAuthoringContext();
            EditorUtility.SetDirty(rockWall);
            MarkSceneDirty(rockWall.gameObject.scene);
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI()
    {
        RockWall rockWall = (RockWall)target;
        if (!PaintModeEnabled)
            return;

        Event currentEvent = Event.current;
        if (currentEvent == null)
            return;

        bool canPaint = rockWall.AuthoringMap != null && !rockWall.HasAuthoringMapResolutionMismatch();
        if (!TryGetBrushWorldPoint(rockWall, currentEvent.mousePosition, out Vector3 worldPoint))
            return;

        DrawBrushPreview(worldPoint, canPaint, currentEvent.shift);

        if (!canPaint)
            return;

        if (currentEvent.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (currentEvent.alt)
            return;

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            PaintModeEnabled = false;
            isBrushStrokeActive = false;
            currentEvent.Use();
            SceneView.RepaintAll();
            Repaint();
            return;
        }

        if (currentEvent.button != 0)
        {
            if (currentEvent.type == EventType.MouseUp)
                isBrushStrokeActive = false;
            return;
        }

        if (currentEvent.type == EventType.MouseDown)
        {
            isBrushStrokeActive = true;
            ApplyBrushStroke(rockWall, worldPoint, currentEvent.shift);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && isBrushStrokeActive)
        {
            ApplyBrushStroke(rockWall, worldPoint, currentEvent.shift);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp)
        {
            isBrushStrokeActive = false;
            currentEvent.Use();
        }
    }

    private void DrawAuthoringMapControls(RockWall rockWall)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Brush Authoring Foundation", EditorStyles.boldLabel);

        Vector2Int expectedResolution = rockWall.GetExpectedAuthoringMapResolution();
        RockWallAuthoringMap authoringMap = rockWall.AuthoringMap;

        if (authoringMap == null)
        {
            EditorGUILayout.HelpBox(
                $"No authoring map assigned. Expected resolution for the current wall is {expectedResolution.x} x {expectedResolution.y}.",
                MessageType.Warning);
        }
        else
        {
            bool hasMismatch = rockWall.HasAuthoringMapResolutionMismatch();
            EditorGUILayout.HelpBox(
                $"Authoring map resolution: {authoringMap.Width} x {authoringMap.Height}. Expected: {expectedResolution.x} x {expectedResolution.y}.",
                hasMismatch ? MessageType.Warning : MessageType.Info);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(authoringMap == null ? "Create Authoring Map Asset" : "Recreate Authoring Map"))
            {
                RockWallAuthoringMap resolvedMap = authoringMap != null ? authoringMap : CreateAuthoringMapAsset(rockWall);
                Undo.RecordObject(rockWall, authoringMap == null ? "Assign RockWall Authoring Map" : "Recreate RockWall Authoring Map");
                rockWall.AssignAuthoringMap(resolvedMap);
                Undo.RecordObject(resolvedMap, authoringMap == null ? "Create RockWall Authoring Map" : "Recreate RockWall Authoring Map");
                rockWall.RecreateAuthoringMapToCurrentResolution();
                rockWall.RebuildWallFromAuthoringContext();
                EditorUtility.SetDirty(resolvedMap);
                EditorUtility.SetDirty(rockWall);
                MarkSceneDirty(rockWall.gameObject.scene);
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(rockWall.AuthoringMap == null))
            {
                if (GUILayout.Button("Capture Current Wall Into Map"))
                {
                    Undo.RecordObject(rockWall.AuthoringMap, "Capture Current Wall Into Authoring Map");
                    rockWall.CaptureCurrentWallIntoAuthoringMap();
                    EditorUtility.SetDirty(rockWall.AuthoringMap);
                    EditorUtility.SetDirty(rockWall);
                    MarkSceneDirty(rockWall.gameObject.scene);
                    SceneView.RepaintAll();
                }
            }
        }
    }

    private void DrawBrushControls(RockWall rockWall)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Scene Brush", EditorStyles.boldLabel);

        bool canPaint = rockWall.AuthoringMap != null && !rockWall.HasAuthoringMapResolutionMismatch();
        using (new EditorGUI.DisabledScope(!canPaint))
        {
            bool paintMode = EditorGUILayout.Toggle("Paint Mode", PaintModeEnabled);
            if (paintMode != PaintModeEnabled)
            {
                PaintModeEnabled = paintMode;
                SceneView.RepaintAll();
            }

            float radius = EditorGUILayout.Slider("Brush Radius", BrushRadius, 0.1f, 8f);
            if (!Mathf.Approximately(radius, BrushRadius))
                BrushRadius = radius;

            float hardness = EditorGUILayout.Slider("Brush Hardness", BrushHardness, 0f, 1f);
            if (!Mathf.Approximately(hardness, BrushHardness))
                BrushHardness = hardness;
        }

        EditorGUILayout.HelpBox(
            canPaint
                ? "Paint Mode: LMB paints Rock, Shift + LMB erases to Empty, Esc exits paint mode."
                : "Brush painting needs a compatible authoring map first.",
            canPaint ? MessageType.Info : MessageType.Warning);
    }

    private void ApplyBrushStroke(RockWall rockWall, Vector3 worldPoint, bool erase)
    {
        string undoName = erase ? "Erase RockWall Authoring" : "Paint RockWall Authoring";
        Undo.RecordObject(rockWall, undoName);
        Undo.RecordObject(rockWall.AuthoringMap, undoName);

        byte materialId = erase ? RockWallAuthoringMap.EmptyMaterialId : RockWallAuthoringMap.RockMaterialId;
        bool changed = rockWall.PaintAuthoringCircle(worldPoint, BrushRadius, materialId, BrushHardness);
        if (!changed)
            return;

        EditorUtility.SetDirty(rockWall.AuthoringMap);
        EditorUtility.SetDirty(rockWall);
        MarkSceneDirty(rockWall.gameObject.scene);
        SceneView.RepaintAll();
    }

    private static bool TryGetBrushWorldPoint(RockWall rockWall, Vector2 guiPoint, out Vector3 worldPoint)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, rockWall.transform.position.z));
        if (plane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        worldPoint = default;
        return false;
    }

    private static void DrawBrushPreview(Vector3 worldPoint, bool canPaint, bool erase)
    {
        Color color = !canPaint
            ? new Color(1f, 0.4f, 0.2f, 0.8f)
            : erase
                ? new Color(1f, 0.35f, 0.35f, 0.9f)
                : new Color(0.25f, 1f, 0.45f, 0.9f);

        Handles.color = color;
        Handles.DrawWireDisc(worldPoint, Vector3.forward, BrushRadius);
        Handles.color = new Color(color.r, color.g, color.b, 0.18f);
        Handles.DrawSolidDisc(worldPoint, Vector3.forward, BrushRadius * Mathf.Lerp(0.18f, 0.94f, BrushHardness));
    }

    private static RockWallAuthoringMap CreateAuthoringMapAsset(RockWall rockWall)
    {
        const string generatedRoot = "Assets/_Game/Data/Wall/Generated";
        EnsureFolderExists("Assets/_Game", "Data");
        EnsureFolderExists("Assets/_Game/Data", "Wall");
        EnsureFolderExists("Assets/_Game/Data/Wall", "Generated");

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{generatedRoot}/{rockWall.name}_AuthoringMap.asset");
        RockWallAuthoringMap asset = ScriptableObject.CreateInstance<RockWallAuthoringMap>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Undo.RegisterCreatedObjectUndo(asset, "Create RockWall Authoring Map");
        return asset;
    }

    private static void EnsureFolderExists(string parentFolder, string childFolder)
    {
        string combinedPath = $"{parentFolder}/{childFolder}";
        if (AssetDatabase.IsValidFolder(combinedPath))
            return;

        AssetDatabase.CreateFolder(parentFolder, childFolder);
    }

    private static void MarkSceneDirty(Scene scene)
    {
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}
