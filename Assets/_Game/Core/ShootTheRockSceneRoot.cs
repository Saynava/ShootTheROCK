using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShootTheRockSceneRoot : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private RockWallDefinition wallDefinition;

    [Header("Scene References")]
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private CameraFramingController cameraFramingController;
    [SerializeField] private Canvas moneyCanvas;
    [SerializeField] private Text moneyText;
    [SerializeField] private MoneyHud moneyHud;
    [SerializeField] private Transform floorTransform;
    [SerializeField] private BoxCollider2D floorCollider;
    [SerializeField] private Transform wallAnchor;
    [SerializeField] private RockWall rockWall;
    [SerializeField] private Transform cannonRoot;
    [SerializeField] private Transform cannonBase;
    [SerializeField] private Transform cannonBarrel;
    [SerializeField] private Transform firePoint;

    [Header("Editor Preview")]
    [SerializeField] private bool autoRefreshPreview = true;

    private RockWallDefinition runtimeFallbackDefinition;

    public Camera SceneCamera => sceneCamera;
    public MoneyHud MoneyHud => moneyHud;
    public RockWall RockWall => rockWall;
    public Transform CannonRoot => cannonRoot;
    public Transform FirePoint => firePoint;
    public RockWallDefinition EffectiveWallDefinition => GetEffectiveWallDefinition();

    public void EnsureSceneObjectsExist()
    {
        EnsureCamera();
        EnsureMoneyCanvas();
        EnsureFloor();
        EnsureWallAnchor();
        EnsureRockWall();
        EnsureCannon();
        EnsureCameraFramingController();
    }

    public void BuildPreview()
    {
        EnsureSceneObjectsExist();
        InitializeWall();
        EnsureCameraFramingController();
        if (cameraFramingController != null && sceneCamera != null && rockWall != null)
            cameraFramingController.Initialize(sceneCamera, rockWall, cannonRoot);
    }

    public void InitializeRuntime()
    {
        EnsureSceneObjectsExist();
        InitializeWall();
        InitializeCannonRuntime();
        EnsureCameraFramingController();
        if (cameraFramingController != null && sceneCamera != null && rockWall != null)
            cameraFramingController.Initialize(sceneCamera, rockWall, cannonRoot);
    }

    public Vector2 GetWallBottomLeftAnchor()
    {
        if (wallAnchor != null)
            return wallAnchor.position;

        return new Vector2(-1.25f, GetFloorTopY());
    }

    public void AssignWallDefinition(RockWallDefinition definition)
    {
        wallDefinition = definition;
    }

    public void ResetToDefinitionDefaults()
    {
        RockWallDefinition definition = GetEffectiveWallDefinition();
        definition.ResetToDefaults();
        BuildPreview();
    }

    private RockWallDefinition GetEffectiveWallDefinition()
    {
        if (wallDefinition != null)
            return wallDefinition;

        if (runtimeFallbackDefinition == null)
        {
            runtimeFallbackDefinition = ScriptableObject.CreateInstance<RockWallDefinition>();
            runtimeFallbackDefinition.hideFlags = HideFlags.DontSave;
            runtimeFallbackDefinition.ResetToDefaults();
        }

        return runtimeFallbackDefinition;
    }

    private void InitializeWall()
    {
        if (rockWall == null)
            return;

        rockWall.transform.localScale = Vector3.one;
        rockWall.Initialize(moneyHud, ShootTheRockPrototypeBootstrap.CreateUnlitMaterial(Color.white), GetWallBottomLeftAnchor(), GetEffectiveWallDefinition());
    }

    private void InitializeCannonRuntime()
    {
        if (cannonRoot == null || sceneCamera == null || rockWall == null)
            return;

        CannonAim aim = cannonRoot.GetComponent<CannonAim>();
        if (aim == null)
            aim = cannonRoot.gameObject.AddComponent<CannonAim>();
        aim.Initialize(sceneCamera);

        AutoShooter shooter = cannonRoot.GetComponent<AutoShooter>();
        if (shooter == null)
            shooter = cannonRoot.gameObject.AddComponent<AutoShooter>();

        Transform effectiveFirePoint = firePoint != null ? firePoint : cannonRoot.Find("FirePoint");
        shooter.Initialize(effectiveFirePoint, rockWall);
    }

    private void EnsureCamera()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
        }

        sceneCamera.orthographic = true;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;
        if (sceneCamera.transform.position.z > -1f)
            sceneCamera.transform.position = new Vector3(sceneCamera.transform.position.x, sceneCamera.transform.position.y, -10f);
    }

    private void EnsureCameraFramingController()
    {
        if (sceneCamera == null)
            return;

        if (cameraFramingController == null)
            cameraFramingController = sceneCamera.GetComponent<CameraFramingController>();
        if (cameraFramingController == null)
            cameraFramingController = sceneCamera.gameObject.AddComponent<CameraFramingController>();
    }

    private void EnsureMoneyCanvas()
    {
        if (moneyCanvas == null)
        {
            Transform existing = transform.Find("MoneyCanvas");
            if (existing != null)
                moneyCanvas = existing.GetComponent<Canvas>();
        }

        if (moneyCanvas == null)
        {
            GameObject canvasObject = new GameObject("MoneyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            moneyCanvas = canvasObject.GetComponent<Canvas>();
            moneyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (moneyText == null)
        {
            Transform existing = moneyCanvas.transform.Find("MoneyText");
            if (existing != null)
                moneyText = existing.GetComponent<Text>();
        }

        if (moneyText == null)
        {
            GameObject moneyTextObject = new GameObject("MoneyText", typeof(RectTransform), typeof(Text));
            moneyTextObject.transform.SetParent(moneyCanvas.transform, false);
            RectTransform rect = moneyTextObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -28f);
            rect.sizeDelta = new Vector2(420f, 90f);

            moneyText = moneyTextObject.GetComponent<Text>();
            moneyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            moneyText.fontSize = 44;
            moneyText.fontStyle = FontStyle.Bold;
            moneyText.alignment = TextAnchor.UpperLeft;
            moneyText.color = Color.white;
            moneyText.text = "$0";
        }

        if (moneyHud == null)
            moneyHud = moneyCanvas.GetComponent<MoneyHud>();
        if (moneyHud == null)
            moneyHud = moneyCanvas.gameObject.AddComponent<MoneyHud>();
        moneyHud.Initialize(moneyText);
    }

    private void EnsureFloor()
    {
        if (floorTransform == null)
        {
            Transform existing = transform.Find("GroundFloor");
            if (existing != null)
                floorTransform = existing;
        }

        if (floorTransform == null)
        {
            GameObject floorObject = ShootTheRockPrototypeBootstrap.CreateSpriteObject(
                "GroundFloor",
                transform,
                new Vector3(8f, -8.95f, 0f),
                new Vector2(180f, 0.85f),
                Color.white,
                2);
            floorTransform = floorObject.transform;
        }

        SpriteRenderer renderer = floorTransform.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = floorTransform.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = ShootTheRockPrototypeBootstrap.GetOrCreateWhiteSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = 2;

        if (floorCollider == null)
            floorCollider = floorTransform.GetComponent<BoxCollider2D>();
        if (floorCollider == null)
            floorCollider = floorTransform.gameObject.AddComponent<BoxCollider2D>();
        floorCollider.size = Vector2.one;
    }

    private void EnsureWallAnchor()
    {
        if (wallAnchor == null)
        {
            Transform existing = transform.Find("WallAnchor");
            if (existing != null)
                wallAnchor = existing;
        }

        if (wallAnchor == null)
        {
            GameObject anchorObject = new GameObject("WallAnchor");
            anchorObject.transform.SetParent(transform, false);
            wallAnchor = anchorObject.transform;
        }

        if (wallAnchor.position == Vector3.zero)
            wallAnchor.position = new Vector3(-1.25f, GetFloorTopY(), 0f);
    }

    private void EnsureRockWall()
    {
        if (rockWall == null)
        {
            Transform existing = transform.Find("RockWall");
            if (existing != null)
                rockWall = existing.GetComponent<RockWall>();
        }

        if (rockWall == null)
        {
            GameObject rockObject = new GameObject("RockWall", typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D));
            rockObject.transform.SetParent(transform, false);
            rockWall = rockObject.AddComponent<RockWall>();
        }
    }

    private void EnsureCannon()
    {
        if (cannonRoot == null)
        {
            Transform existing = transform.Find("CannonRoot");
            if (existing != null)
                cannonRoot = existing;
        }

        if (cannonRoot == null)
        {
            GameObject root = new GameObject("CannonRoot");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(-13.8f, GetFloorTopY() + 0.24f, 0f);
            cannonRoot = root.transform;
        }

        if (cannonBase == null)
        {
            Transform existing = cannonRoot.Find("Base");
            if (existing != null)
                cannonBase = existing;
        }
        if (cannonBase == null)
            cannonBase = ShootTheRockPrototypeBootstrap.CreateSpriteObject("Base", cannonRoot, Vector3.zero, new Vector2(0.62f, 0.62f), Color.white, 5).transform;

        if (cannonBarrel == null)
        {
            Transform existing = cannonRoot.Find("Barrel");
            if (existing != null)
                cannonBarrel = existing;
        }
        if (cannonBarrel == null)
            cannonBarrel = ShootTheRockPrototypeBootstrap.CreateSpriteObject("Barrel", cannonRoot, new Vector3(0.54f, 0f, 0f), new Vector2(1.12f, 0.17f), Color.white, 6).transform;

        if (firePoint == null)
        {
            Transform existing = cannonRoot.Find("FirePoint");
            if (existing != null)
                firePoint = existing;
        }
        if (firePoint == null)
        {
            GameObject firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(cannonRoot, false);
            firePointObject.transform.localPosition = new Vector3(1.06f, 0f, 0f);
            firePoint = firePointObject.transform;
        }

        EnsureSpriteVisual(cannonBase, 5);
        EnsureSpriteVisual(cannonBarrel, 6);
    }

    private void EnsureSpriteVisual(Transform target, int sortingOrder)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = target.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = ShootTheRockPrototypeBootstrap.GetOrCreateWhiteSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
    }

    private float GetFloorTopY()
    {
        if (floorTransform == null)
            return -8.95f + (0.85f * 0.5f);

        return floorTransform.position.y + (floorTransform.localScale.y * 0.5f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!autoRefreshPreview)
            return;

        EditorApplication.delayCall -= DelayedBuildPreview;
        EditorApplication.delayCall += DelayedBuildPreview;
    }

    private void DelayedBuildPreview()
    {
        if (this == null)
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        BuildPreview();
    }
#endif
}
