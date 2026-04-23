using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ShootTheRockSceneMode
{
    PrototypeWall,
    Motherload
}

public class ShootTheRockSceneRoot : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private ShootTheRockSceneMode sceneMode = ShootTheRockSceneMode.PrototypeWall;

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
    [SerializeField] private Transform playerSpawnAnchor;
    [SerializeField] private Rigidbody2D testGoalBallBody;
    [SerializeField] private TestGoalZone testGoalZone;
    [SerializeField] private ShootTheRockPrototypeMarkers prototypeMarkers;
    [SerializeField] private MotherloadWorldController motherloadWorldController;

    [Header("Prototype Layout")]
    [SerializeField] private Vector2 playerPocketSize = new Vector2(2.8f, 2.8f);
    [SerializeField] private Vector2 ballPocketSize = new Vector2(2.2f, 2.8f);
    [SerializeField] private Vector2 goalPocketSize = new Vector2(3f, 2.4f);
    [SerializeField] private bool keepSceneCameraTransform = true;
    [SerializeField] private bool fitWallToSceneCamera = true;
    [SerializeField] private Vector2 sceneWallExtraSize = Vector2.zero;

    [Header("Player Runtime")]
    [Min(0f)] [SerializeField] private float playerGravityScale = 3f;

    [Header("Motherload Layout")]
    [SerializeField] private MotherloadVerticalFlowDirection motherloadVerticalFlowDirection = MotherloadVerticalFlowDirection.Upward;
    [Min(1f)] [SerializeField] private float motherloadUpwardTerrainStartOffset = 4.75f;

    [Header("Editor Preview")]
    [SerializeField] private bool autoRefreshPreview = true;

    private RockWallDefinition runtimeFallbackDefinition;
    private RockWallDefinition sceneCameraWallDefinition;

    public Camera SceneCamera => sceneCamera;
    public MoneyHud MoneyHud => moneyHud;
    public RockWall RockWall => rockWall;
    public Transform CannonRoot => cannonRoot;
    public Transform FirePoint => firePoint;
    public Transform PlayerSpawnAnchor => playerSpawnAnchor;
    public Rigidbody2D TestGoalBallBody => testGoalBallBody;
    public TestGoalZone TestGoalZone => testGoalZone;
    public RockWallDefinition EffectiveWallDefinition => GetEffectiveWallDefinition();

    private bool IsMotherloadModeActive => sceneMode == ShootTheRockSceneMode.Motherload;

    public void EnsureSceneObjectsExist()
    {
        EnsureCamera();
        EnsureMoneyCanvas();
        EnsureCannon();
        EnsureCameraFramingController();

        CacheSceneReferencesForModeRouting();
        EnsurePlayerSpawnAnchor();

        if (IsMotherloadModeActive && ResolveMotherloadMode(activeIfMissing: true))
            return;

        EnsureFloor();
        EnsureWallAnchor();
        EnsureRockWall();
        EnsurePrototypeMarkers();
        EnsureTestGoalBall();
        EnsureTestGoalZone();
    }

    public void BuildPreview()
    {
        if (IsMotherloadModeActive)
        {
            EnsureCamera();
            EnsureMoneyCanvas();
            EnsureCannon();
            EnsureCameraFramingController();
            EnsureMotherloadWorldController();
            CacheSceneReferencesForModeRouting();
            EnsurePlayerSpawnAnchor();
            PrepareMotherloadEditorPreviewState();

            if (motherloadWorldController != null)
            {
                motherloadWorldController.gameObject.SetActive(true);
                ConfigureMotherloadWorldLayout();
                motherloadWorldController.SetSceneCamera(sceneCamera);
                motherloadWorldController.SetFocusTarget(cannonRoot);
                motherloadWorldController.SetMoneyHud(moneyHud);
                motherloadWorldController.SetLockCameraXToStrip(false);
            }

            AlignPlayerSpawnAnchorToMotherloadBase();
            SnapCannonToSpawnAnchor();

            if (motherloadWorldController != null)
                motherloadWorldController.BuildEditorPreview();

            return;
        }

        EnsureSceneObjectsExist();

        if (ResolveMotherloadMode(activeIfMissing: false))
        {
            BuildMotherloadPreview();
            return;
        }

        PreparePrototypeSceneState();
        SyncPrototypeObjectsFromMarkers();
        InitializeWall();
        ApplyPrototypePockets();
        ConfigureCameraFramingForSceneTruth();
    }

    public void InitializeRuntime()
    {
        EnsureEventSystem();
        EnsureSceneObjectsExist();

        if (ResolveMotherloadMode(activeIfMissing: false))
        {
            InitializeMotherloadRuntime();
            return;
        }

        PreparePrototypeSceneState();
        SyncPrototypeObjectsFromMarkers();
        InitializeWall();
        ApplyPrototypePockets();
        InitializeCannonRuntime();
        InitializeGoalRuntime();
        ConfigureCameraFramingForSceneTruth();
        if (moneyHud != null)
            moneyHud.BindProgression(rockWall, keepSceneCameraTransform ? null : cameraFramingController);
    }

    public Vector2 GetWallBottomLeftAnchor()
    {
        if (ShouldFitWallToSceneCamera() && TryGetSceneCameraRect(out Rect cameraRect))
            return cameraRect.min;

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
        if (ShouldFitWallToSceneCamera() && TryBuildSceneCameraWallDefinition(out RockWallDefinition fittedDefinition))
            return fittedDefinition;

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
        rockWall.SetGenerateEssenceDeposits(false);
        rockWall.Initialize(moneyHud, ShootTheRockPrototypeBootstrap.CreateUnlitMaterial(Color.white), GetWallBottomLeftAnchor(), GetEffectiveWallDefinition());

        if (ShouldFitWallToSceneCamera())
            rockWall.ConfigureSingleCameraFrame("SCENE", rockWall.WorldWidth, rockWall.WorldHeight, Vector2.zero, Vector2.zero, centerWithinWall: true);
    }

    private void InitializeCannonRuntime()
    {
        if (cannonRoot == null || sceneCamera == null || rockWall == null)
            return;

        CircleCollider2D movementCollider = cannonRoot.GetComponent<CircleCollider2D>();
        if (movementCollider == null)
            movementCollider = cannonRoot.gameObject.AddComponent<CircleCollider2D>();
        movementCollider.radius = 0.36f;
        movementCollider.offset = Vector2.zero;
        movementCollider.isTrigger = false;

        Rigidbody2D body = cannonRoot.GetComponent<Rigidbody2D>();
        if (body == null)
            body = cannonRoot.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = playerGravityScale;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.useFullKinematicContacts = false;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        CannonAim aim = cannonRoot.GetComponent<CannonAim>();
        if (aim == null)
            aim = cannonRoot.gameObject.AddComponent<CannonAim>();
        aim.Initialize(sceneCamera);
        aim.ConfigureFuelSystem(false);

        AutoShooter shooter = cannonRoot.GetComponent<AutoShooter>();
        if (shooter == null)
            shooter = cannonRoot.gameObject.AddComponent<AutoShooter>();
        shooter.enabled = true;

        Transform effectiveFirePoint = firePoint != null ? firePoint : cannonRoot.Find("FirePoint");
        shooter.Initialize(effectiveFirePoint, rockWall);
        if (moneyHud != null)
        {
            moneyHud.BindShooter(shooter);
            moneyHud.BindCannon(aim);
        }
    }

    private void BuildMotherloadPreview()
    {
        PrepareMotherloadSceneState();
        AlignPlayerSpawnAnchorToMotherloadBase();
        SnapCannonToSpawnAnchor();
        ConfigureMotherloadWorldRuntime();
    }

    private void InitializeMotherloadRuntime()
    {
        PrepareMotherloadSceneState();
        ConfigureMotherloadWorldRuntime();
        AlignPlayerSpawnAnchorToMotherloadBase();
        SnapCannonToSpawnAnchor();
        InitializeMotherloadCannonRuntime();
    }

    private void InitializeMotherloadCannonRuntime()
    {
        if (cannonRoot == null || sceneCamera == null)
            return;

        CircleCollider2D movementCollider = cannonRoot.GetComponent<CircleCollider2D>();
        if (movementCollider == null)
            movementCollider = cannonRoot.gameObject.AddComponent<CircleCollider2D>();
        movementCollider.radius = 0.36f;
        movementCollider.offset = Vector2.zero;
        movementCollider.isTrigger = false;

        Rigidbody2D body = cannonRoot.GetComponent<Rigidbody2D>();
        if (body == null)
            body = cannonRoot.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = playerGravityScale;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.useFullKinematicContacts = false;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        CannonAim aim = cannonRoot.GetComponent<CannonAim>();
        if (aim == null)
            aim = cannonRoot.gameObject.AddComponent<CannonAim>();
        aim.Initialize(sceneCamera);
        aim.ConfigureFuelSystem(true);

        AutoShooter shooter = cannonRoot.GetComponent<AutoShooter>();
        if (shooter == null)
            shooter = cannonRoot.gameObject.AddComponent<AutoShooter>();

        Transform effectiveFirePoint = firePoint != null ? firePoint : cannonRoot.Find("FirePoint");
        shooter.enabled = motherloadWorldController != null;
        shooter.Initialize(effectiveFirePoint, null, motherloadWorldController);

        if (moneyHud != null)
        {
            moneyHud.BindShooter(shooter);
            moneyHud.BindCannon(aim);
            moneyHud.BindProgression(null, null);
            moneyHud.SetUpgradeUiVisible(false);
        }
    }

    private void ConfigureMotherloadWorldRuntime()
    {
        if (motherloadWorldController == null)
            return;

        motherloadWorldController.gameObject.SetActive(true);
        ConfigureMotherloadWorldLayout();
        motherloadWorldController.SetSceneCamera(sceneCamera);
        motherloadWorldController.SetFocusTarget(cannonRoot);
        motherloadWorldController.SetMoneyHud(moneyHud);
        motherloadWorldController.SetLockCameraXToStrip(false);
        motherloadWorldController.InitializeRuntime();
    }

    private void PrepareMotherloadSceneState()
    {
        SetObjectActive(floorTransform, false);
        SetObjectActive(wallAnchor, false);
        SetObjectActive(rockWall, false);
        SetObjectActive(testGoalBallBody, false);
        SetObjectActive(testGoalZone, false);
        SetObjectActive(prototypeMarkers, false);
        SetObjectActive(moneyCanvas, true);

        if (cameraFramingController != null)
            cameraFramingController.enabled = false;

        if (motherloadWorldController != null)
        {
            motherloadWorldController.gameObject.SetActive(true);
            ConfigureMotherloadWorldLayout();
        }
    }

    private void AlignPlayerSpawnAnchorToMotherloadBase()
    {
        if (playerSpawnAnchor == null || motherloadWorldController == null)
            return;

        playerSpawnAnchor.position = motherloadWorldController.GetSuggestedPlayerSpawnWorldPosition();
    }

    private void ConfigureMotherloadWorldLayout()
    {
        if (motherloadWorldController == null)
            return;

        motherloadWorldController.ConfigureVerticalFlow(motherloadVerticalFlowDirection, motherloadUpwardTerrainStartOffset);
    }

    private void PrepareMotherloadEditorPreviewState()
    {
        SetObjectActive(floorTransform, false);
        SetObjectActive(wallAnchor, false);
        SetObjectActive(rockWall, false);
        SetObjectActive(testGoalBallBody, false);
        SetObjectActive(testGoalZone, false);
        SetObjectActive(prototypeMarkers, false);
        SetObjectActive(moneyCanvas, false);

        if (cameraFramingController != null)
            cameraFramingController.enabled = false;

        if (motherloadWorldController == null)
            motherloadWorldController = ResolveMotherloadWorldController();

        if (motherloadWorldController != null)
            motherloadWorldController.gameObject.SetActive(false);
    }

    private void PreparePrototypeSceneState()
    {
        SetObjectActive(floorTransform, true);
        SetObjectActive(wallAnchor, true);
        SetObjectActive(rockWall, true);
        SetObjectActive(testGoalBallBody, true);
        SetObjectActive(testGoalZone, true);
        SetObjectActive(prototypeMarkers, true);
        SetObjectActive(moneyCanvas, true);

        if (motherloadWorldController != null)
            motherloadWorldController.gameObject.SetActive(false);

        if (cameraFramingController != null)
            cameraFramingController.enabled = true;
    }

    private void SnapCannonToSpawnAnchor()
    {
        if (cannonRoot == null || playerSpawnAnchor == null)
            return;

        cannonRoot.position = playerSpawnAnchor.position;
        cannonRoot.rotation = Quaternion.identity;

        Rigidbody2D body = cannonRoot.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = playerSpawnAnchor.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void EnsureMotherloadWorldController()
    {
        motherloadWorldController = ResolveMotherloadWorldController();

        if (motherloadWorldController != null)
            return;

        GameObject worldObject = new GameObject("MotherloadWorld");
        worldObject.transform.SetParent(transform, false);
        motherloadWorldController = worldObject.AddComponent<MotherloadWorldController>();
    }

    private MotherloadWorldController ResolveMotherloadWorldController()
    {
        if (motherloadWorldController != null)
            return motherloadWorldController;

        MotherloadWorldController controller = GetComponentInChildren<MotherloadWorldController>(true);
        if (controller != null)
            return controller;

        controller = FindAnyObjectByType<MotherloadWorldController>();
        if (controller != null)
            return controller;

        MotherloadWorldController[] allControllers = Resources.FindObjectsOfTypeAll<MotherloadWorldController>();
        for (int i = 0; i < allControllers.Length; i++)
        {
            if (allControllers[i] == null || allControllers[i].gameObject == null)
                continue;

            if (!allControllers[i].gameObject.scene.IsValid())
                continue;

            return allControllers[i];
        }

        return null;
    }

    private bool ResolveMotherloadMode(bool activeIfMissing)
    {
        if (!IsMotherloadModeActive)
            return false;

        if (motherloadWorldController == null)
            motherloadWorldController = ResolveMotherloadWorldController();

        if (motherloadWorldController == null && activeIfMissing)
            EnsureMotherloadWorldController();

        if (motherloadWorldController == null)
            return false;

        if (activeIfMissing)
            SetObjectActive(motherloadWorldController, true);

        return true;
    }

    [ContextMenu("Switch to Motherload Mode")]
    public void SwitchToMotherloadMode()
    {
        sceneMode = ShootTheRockSceneMode.Motherload;
        BuildPreview();
    }

    [ContextMenu("Switch to Prototype Wall Mode")]
    public void SwitchToPrototypeWallMode()
    {
        sceneMode = ShootTheRockSceneMode.PrototypeWall;
        BuildPreview();
    }

    private static void SetObjectActive(Component component, bool value)
    {
        if (component != null)
            component.gameObject.SetActive(value);
    }

    private static void SetObjectActive(Transform target, bool value)
    {
        if (target != null)
            target.gameObject.SetActive(value);
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
        if (cameraFramingController == null && !keepSceneCameraTransform)
            cameraFramingController = sceneCamera.gameObject.AddComponent<CameraFramingController>();
    }

    private void EnsureEventSystem()
    {
        EventSystem existingEventSystem = FindAnyObjectByType<EventSystem>();
        if (existingEventSystem != null)
        {
            if (existingEventSystem.GetComponent<InputSystemUIInputModule>() == null)
                existingEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystemObject);
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

    private void EnsurePrototypeMarkers()
    {
        if (prototypeMarkers == null)
            prototypeMarkers = FindAnyObjectByType<ShootTheRockPrototypeMarkers>();

        if (prototypeMarkers == null)
        {
            GameObject existing = GameObject.Find("PrototypeMarkers");
            if (existing != null)
                prototypeMarkers = existing.GetComponent<ShootTheRockPrototypeMarkers>();
        }

        if (prototypeMarkers == null)
        {
            GameObject markersObject = new GameObject("PrototypeMarkers");
            prototypeMarkers = markersObject.AddComponent<ShootTheRockPrototypeMarkers>();
        }

        prototypeMarkers.EnsureMarkers();
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
            root.transform.localPosition = Vector3.zero;
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

    private void EnsurePlayerSpawnAnchor()
    {
        if (playerSpawnAnchor == null)
            playerSpawnAnchor = transform.Find("PlayerSpawnAnchor");

        if (playerSpawnAnchor != null)
            return;

        GameObject anchorObject = new GameObject("PlayerSpawnAnchor");
        anchorObject.transform.SetParent(transform, false);
        anchorObject.transform.position = ResolveDefaultPlayerSpawnAnchorPosition();
        anchorObject.transform.rotation = Quaternion.identity;
        anchorObject.transform.localScale = Vector3.one;
        playerSpawnAnchor = anchorObject.transform;
    }

    private Vector3 ResolveDefaultPlayerSpawnAnchorPosition()
    {
        if (prototypeMarkers != null && prototypeMarkers.PlayerStartMarker != null)
            return prototypeMarkers.PlayerStartMarker.position;

        if (motherloadWorldController != null)
            return motherloadWorldController.GetSuggestedPlayerSpawnWorldPosition();

        if (cannonRoot != null)
            return cannonRoot.position;

        return transform.position;
    }

    private void CacheSceneReferencesForModeRouting()
    {
        if (floorTransform == null)
            floorTransform = transform.Find("GroundFloor");

        if (wallAnchor == null)
            wallAnchor = transform.Find("WallAnchor");

        if (rockWall == null)
            rockWall = transform.Find("RockWall")?.GetComponent<RockWall>();

        if (cannonBase == null && cannonRoot != null)
            cannonBase = cannonRoot.Find("Base");

        if (cannonBarrel == null && cannonRoot != null)
            cannonBarrel = cannonRoot.Find("Barrel");

        if (firePoint == null && cannonRoot != null)
            firePoint = cannonRoot.Find("FirePoint");

        if (playerSpawnAnchor == null)
            playerSpawnAnchor = transform.Find("PlayerSpawnAnchor");

        if (testGoalBallBody == null)
        {
            Transform existing = transform.Find("TestGoalBall");
            if (existing != null)
                testGoalBallBody = existing.GetComponent<Rigidbody2D>();
        }

        if (testGoalZone == null)
        {
            Transform existing = transform.Find("TestGoalZone");
            if (existing != null)
                testGoalZone = existing.GetComponent<TestGoalZone>();
        }

        if (prototypeMarkers == null)
            prototypeMarkers = transform.Find("PrototypeMarkers")?.GetComponent<ShootTheRockPrototypeMarkers>();

        if (prototypeMarkers == null)
            prototypeMarkers = FindAnyObjectByType<ShootTheRockPrototypeMarkers>();

        if (moneyCanvas == null)
            moneyCanvas = transform.Find("MoneyCanvas")?.GetComponent<Canvas>();

        if (moneyCanvas != null && moneyText == null)
            moneyText = moneyCanvas.transform.Find("MoneyText")?.GetComponent<Text>();

        if (moneyCanvas != null && moneyHud == null)
            moneyHud = moneyCanvas.GetComponent<MoneyHud>();

        if (motherloadWorldController == null)
            motherloadWorldController = ResolveMotherloadWorldController();

        if (cameraFramingController == null && sceneCamera != null)
            cameraFramingController = sceneCamera.GetComponent<CameraFramingController>();
    }

    private void EnsureTestGoalBall()
    {
        if (testGoalBallBody == null)
        {
            Transform existing = transform.Find("TestGoalBall");
            if (existing != null)
                testGoalBallBody = existing.GetComponent<Rigidbody2D>();
        }

        if (testGoalBallBody == null)
        {
            GameObject ballObject = new GameObject("TestGoalBall", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D));
            ballObject.transform.SetParent(transform, false);
            ballObject.transform.localPosition = new Vector3(-2f, -2f, 0f);
            ballObject.transform.localRotation = Quaternion.identity;
            ballObject.transform.localScale = new Vector3(0.68f, 0.68f, 1f);
            testGoalBallBody = ballObject.GetComponent<Rigidbody2D>();
        }

        SpriteRenderer renderer = testGoalBallBody.GetComponent<SpriteRenderer>();
        renderer.sprite = ShootTheRockPrototypeBootstrap.GetOrCreateCircleSprite();
        renderer.color = new Color(1f, 0.87f, 0.2f, 1f);
        renderer.sortingOrder = 9;

        CircleCollider2D collider = testGoalBallBody.GetComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        collider.sharedMaterial = ShootTheRockPrototypeBootstrap.GetOrCreateGoalBallPhysicsMaterial();

        ShootTheRockPrototypeBootstrap.ApplyGoalBallPhysicsProfile(testGoalBallBody);
    }

    private void EnsureTestGoalZone()
    {
        if (testGoalZone == null)
        {
            Transform existing = transform.Find("TestGoalZone");
            if (existing != null)
                testGoalZone = existing.GetComponent<TestGoalZone>();
        }

        if (testGoalZone == null)
        {
            GameObject zoneObject = new GameObject("TestGoalZone", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(TestGoalZone));
            zoneObject.transform.SetParent(transform, false);
            zoneObject.transform.localPosition = new Vector3(-10f, -8f, 0f);
            zoneObject.transform.localRotation = Quaternion.identity;
            zoneObject.transform.localScale = new Vector3(2.4f, 1.2f, 1f);
            testGoalZone = zoneObject.GetComponent<TestGoalZone>();
        }

        SpriteRenderer renderer = testGoalZone.GetComponent<SpriteRenderer>();
        renderer.sprite = ShootTheRockPrototypeBootstrap.GetOrCreateWhiteSprite();
        renderer.color = new Color(0.18f, 0.95f, 0.35f, 0.42f);
        renderer.sortingOrder = 4;

        BoxCollider2D collider = testGoalZone.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one;
    }

    private void SyncPrototypeObjectsFromMarkers()
    {
        if (cannonRoot != null && playerSpawnAnchor != null)
        {
            cannonRoot.position = playerSpawnAnchor.position;
            cannonRoot.rotation = Quaternion.identity;
        }

        if (prototypeMarkers == null)
            return;

        playerPocketSize = prototypeMarkers.PlayerPocketSize;
        ballPocketSize = prototypeMarkers.BallPocketSize;
        goalPocketSize = prototypeMarkers.GoalPocketSize;

        if (testGoalBallBody != null && prototypeMarkers.BallStartMarker != null)
        {
            testGoalBallBody.transform.position = prototypeMarkers.BallStartMarker.position;
            testGoalBallBody.transform.rotation = Quaternion.identity;
        }

        if (testGoalZone != null && prototypeMarkers.GoalMarker != null)
        {
            testGoalZone.transform.position = prototypeMarkers.GoalMarker.position;
            testGoalZone.transform.rotation = Quaternion.identity;
        }
    }

    private void ApplyPrototypePockets()
    {
        if (rockWall == null)
            return;

        Vector3 playerSpawnPosition = playerSpawnAnchor != null
            ? playerSpawnAnchor.position
            : (cannonRoot != null ? cannonRoot.position : transform.position);
        rockWall.CreateDebugPocket(playerSpawnPosition, playerPocketSize);

        if (testGoalBallBody != null)
            rockWall.CreateDebugPocket(testGoalBallBody.transform.position, ballPocketSize);

        if (testGoalZone != null)
            rockWall.CreateDebugPocket(testGoalZone.transform.position, goalPocketSize);
    }

    private void InitializeGoalRuntime()
    {
        if (testGoalBallBody != null)
        {
            testGoalBallBody.linearVelocity = Vector2.zero;
            testGoalBallBody.angularVelocity = 0f;
            testGoalBallBody.sleepMode = RigidbodySleepMode2D.StartAwake;
        }

        if (testGoalZone != null)
            testGoalZone.Initialize(testGoalBallBody != null ? testGoalBallBody.gameObject.name : "TestGoalBall");
    }

    private void ConfigureCameraFramingForSceneTruth()
    {
        EnsureCameraFramingController();
        if (cameraFramingController == null || sceneCamera == null || rockWall == null)
            return;

        cameraFramingController.Initialize(sceneCamera, rockWall, cannonRoot);
        cameraFramingController.SetPreserveCannonViewportAnchor(false);
        cameraFramingController.SetAutoFrameEnabled(!keepSceneCameraTransform);

        if (!keepSceneCameraTransform)
            cameraFramingController.SnapToCurrentFrame();
    }

    private bool ShouldFitWallToSceneCamera()
    {
        return keepSceneCameraTransform && fitWallToSceneCamera && sceneCamera != null;
    }

    private bool TryGetSceneCameraRect(out Rect rect)
    {
        if (sceneCamera == null || !sceneCamera.orthographic)
        {
            rect = default;
            return false;
        }

        float halfHeight = Mathf.Max(0.01f, sceneCamera.orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.01f, sceneCamera.aspect);
        Vector2 extraHalfSize = new Vector2(
            Mathf.Max(0f, sceneWallExtraSize.x) * 0.5f,
            Mathf.Max(0f, sceneWallExtraSize.y) * 0.5f);

        Vector2 min = new Vector2(
            sceneCamera.transform.position.x - halfWidth - extraHalfSize.x,
            sceneCamera.transform.position.y - halfHeight - extraHalfSize.y);
        Vector2 max = new Vector2(
            sceneCamera.transform.position.x + halfWidth + extraHalfSize.x,
            sceneCamera.transform.position.y + halfHeight + extraHalfSize.y);

        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 0f && rect.height > 0f;
    }

    private bool TryBuildSceneCameraWallDefinition(out RockWallDefinition definition)
    {
        if (!TryGetSceneCameraRect(out Rect cameraRect))
        {
            definition = null;
            return false;
        }

        RockWallDefinition sourceDefinition = wallDefinition != null ? wallDefinition : runtimeFallbackDefinition;
        if (sourceDefinition == null)
        {
            runtimeFallbackDefinition = ScriptableObject.CreateInstance<RockWallDefinition>();
            runtimeFallbackDefinition.hideFlags = HideFlags.DontSave;
            runtimeFallbackDefinition.ResetToDefaults();
            sourceDefinition = runtimeFallbackDefinition;
        }

        RockWallDefinition.RevealStage sourceStage = sourceDefinition != null && sourceDefinition.HasStages
            ? sourceDefinition.GetStageOrLast(0)
            : RockWallDefinition.CreateDefaultStages()[0];

        if (sceneCameraWallDefinition == null)
        {
            sceneCameraWallDefinition = ScriptableObject.CreateInstance<RockWallDefinition>();
            sceneCameraWallDefinition.hideFlags = HideFlags.DontSave;
        }

        sceneCameraWallDefinition.SetStages(new[]
        {
            new RockWallDefinition.RevealStage(
                cameraRect.width,
                cameraRect.height,
                Mathf.Max(1f, sourceStage.cellsPerUnit),
                1f,
                Vector2.zero,
                Vector2.zero)
        });

        definition = sceneCameraWallDefinition;
        return true;
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

    private void Reset()
    {
        EnsureCannon();
        EnsurePlayerSpawnAnchor();
        if (cannonRoot != null && playerSpawnAnchor != null)
            cannonRoot.position = playerSpawnAnchor.position;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        motherloadUpwardTerrainStartOffset = Mathf.Max(1f, motherloadUpwardTerrainStartOffset);

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
