using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class ShootTheRockPrototypeBootstrap : MonoBehaviour
{
    private const string TestGoalBallName = "TestGoalBall";
    private const string TestGoalZoneName = "TestGoalZone";
    private const string PrototypeMarkersObjectName = "PrototypeMarkers";
    private static readonly Vector2 PrototypeLevelTwoFrameSize = new Vector2(22.08f, 22.816f);
    private static Sprite cachedWhiteSprite;
    private static Sprite cachedCircleSprite;
    private static PhysicsMaterial2D cachedGoalBallPhysicsMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBootstrapExists()
    {
        if (FindAnyObjectByType<ShootTheRockPrototypeBootstrap>() != null)
            return;

        GameObject bootstrapObject = new GameObject("ShootTheRockPrototypeBootstrap");
        bootstrapObject.AddComponent<ShootTheRockPrototypeBootstrap>();
    }

    private void Awake()
    {
        ShootTheRockPrototypeBootstrap[] existing = FindObjectsByType<ShootTheRockPrototypeBootstrap>();
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        BuildPrototypeScene();
    }

    private void BuildPrototypeScene()
    {
        ShootTheRockSceneRoot sceneRoot = FindAnyObjectByType<ShootTheRockSceneRoot>();
        if (sceneRoot != null)
        {
            sceneRoot.InitializeRuntime();
            return;
        }

        MotherloadWorldController motherloadWorldController = FindAnyObjectByType<MotherloadWorldController>();
        if (motherloadWorldController != null)
            return;

        RockWall existingWall = FindPreferredRockWall();
        if (existingWall != null)
        {
            BuildFromExistingScene(existingWall);
            return;
        }

        Camera sceneCamera = EnsureCamera();
        EnsureEventSystem();
        MoneyHud moneyHud = EnsureMoneyHud();
        float floorTopY = EnsureFloor();
        RockWall rockWall = EnsureRockWall(moneyHud, floorTopY);
        ShootTheRockPrototypeMarkers markers = EnsurePrototypeMarkers();
        ConfigureContainedLevelFrame(sceneCamera, rockWall);
        Transform cannonRoot = EnsureCannon(sceneCamera, rockWall, floorTopY, moneyHud, markers);
        CameraFramingController framingController = InitializeOptionalCameraFraming(sceneCamera, rockWall, cannonRoot);
        EnsureTestGoalBall(rockWall, markers);
        EnsureTestGoalZone(rockWall, markers);
        if (moneyHud != null)
            moneyHud.BindProgression(rockWall, framingController);
    }

    private RockWall FindPreferredRockWall()
    {
        RockWall[] walls = FindObjectsByType<RockWall>();
        if (walls == null || walls.Length == 0)
            return null;

        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
                return walls[i];
        }

        return null;
    }

    private void BuildFromExistingScene(RockWall rockWall)
    {
        Camera sceneCamera = EnsureCamera();
        EnsureEventSystem();
        MoneyHud moneyHud = FindAnyObjectByType<MoneyHud>();
        if (moneyHud == null)
        {
            moneyHud = EnsureMoneyHud();
        }
        else
        {
            Text existingText = null;
            Transform moneyTextTransform = moneyHud.transform.Find("MoneyText");
            if (moneyTextTransform != null)
                existingText = moneyTextTransform.GetComponent<Text>();
            if (existingText == null)
                existingText = moneyHud.GetComponentInChildren<Text>();
            if (existingText != null)
                moneyHud.Initialize(existingText);

            Canvas existingCanvas = moneyHud.GetComponent<Canvas>();
            EnsurePerformanceHud(existingCanvas);
        }

        float floorTopY = EnsureFloor();
        rockWall.Initialize(moneyHud, CreateUnlitMaterial(Color.white));
        ConfigureSceneOwnedWallFrame(rockWall);
        Transform cannonRoot = EnsureCannon(sceneCamera, rockWall, floorTopY, moneyHud, null, respectSceneTransform: true);
        InitializeOptionalCameraFraming(sceneCamera, rockWall, cannonRoot, sceneOwnsLayout: true);
        EnsureTestGoalBall(rockWall, null, respectSceneTransform: true);
        EnsureTestGoalZone(rockWall, null, respectSceneTransform: true);
        if (moneyHud != null)
            moneyHud.BindProgression(rockWall, null);
    }

    private CameraFramingController InitializeOptionalCameraFraming(Camera sceneCamera, RockWall rockWall, Transform cannonRoot, bool sceneOwnsLayout = false)
    {
        if (sceneCamera == null)
            return null;

        CameraFramingController framingController = sceneCamera.GetComponent<CameraFramingController>();
        if (sceneOwnsLayout)
        {
            if (framingController != null)
            {
                framingController.Initialize(sceneCamera, rockWall, cannonRoot);
                framingController.SetAutoFrameEnabled(false);
                framingController.SetPreserveCannonViewportAnchor(false);
            }

            return null;
        }

        if (framingController == null)
            framingController = sceneCamera.gameObject.AddComponent<CameraFramingController>();

        framingController.Initialize(sceneCamera, rockWall, cannonRoot);
        framingController.SetPreserveCannonViewportAnchor(false);
        framingController.SnapToCurrentFrame();
        return framingController;
    }

    private Camera EnsureCamera()
    {
        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
            sceneCamera = FindAnyObjectByType<Camera>();

        bool created = false;
        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
            created = true;
        }

        sceneCamera.orthographic = true;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;

        if (created)
        {
            sceneCamera.orthographicSize = 10.4f;
            sceneCamera.transform.position = new Vector3(-4.8f, 1.6f, -10f);
        }
        else if (sceneCamera.transform.position.z > -1f)
        {
            Vector3 currentPosition = sceneCamera.transform.position;
            sceneCamera.transform.position = new Vector3(currentPosition.x, currentPosition.y, -10f);
        }

        return sceneCamera;
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

    private MoneyHud EnsureMoneyHud()
    {
        MoneyHud existingHud = FindAnyObjectByType<MoneyHud>();
        if (existingHud != null)
        {
            Text existingText = null;
            Transform moneyTextTransform = existingHud.transform.Find("MoneyText");
            if (moneyTextTransform != null)
                existingText = moneyTextTransform.GetComponent<Text>();
            if (existingText == null)
                existingText = existingHud.GetComponentInChildren<Text>();
            if (existingText != null)
                existingHud.Initialize(existingText);

            Canvas existingCanvas = existingHud.GetComponent<Canvas>();
            EnsurePerformanceHud(existingCanvas);
            return existingHud;
        }

        GameObject canvasObject = new GameObject("MoneyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject moneyTextObject = new GameObject("MoneyText", typeof(RectTransform), typeof(Text));
        moneyTextObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = moneyTextObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -28f);
        rect.sizeDelta = new Vector2(420f, 90f);

        Text moneyText = moneyTextObject.GetComponent<Text>();
        moneyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        moneyText.fontSize = 44;
        moneyText.fontStyle = FontStyle.Bold;
        moneyText.alignment = TextAnchor.UpperLeft;
        moneyText.color = Color.white;
        moneyText.text = "$0";

        MoneyHud hud = canvasObject.AddComponent<MoneyHud>();
        hud.Initialize(moneyText);
        EnsurePerformanceHud(canvas);
        return hud;
    }

    private void EnsurePerformanceHud(Canvas canvas)
    {
        if (canvas == null)
            return;

        ShootTheRockPerformanceHud performanceHud = canvas.GetComponent<ShootTheRockPerformanceHud>();
        if (performanceHud == null)
            performanceHud = canvas.gameObject.AddComponent<ShootTheRockPerformanceHud>();

        performanceHud.Initialize();
    }

    private float EnsureFloor()
    {
        const string floorName = "GroundFloor";
        const float defaultFloorCenterX = 8f;
        const float defaultFloorCenterY = -8.95f;
        const float defaultFloorHeight = 0.85f;
        const float defaultFloorWidth = 180f;

        GameObject floorObject = GameObject.Find(floorName);
        if (floorObject == null)
        {
            floorObject = CreateSpriteObject(
                floorName,
                null,
                new Vector3(defaultFloorCenterX, defaultFloorCenterY, 0f),
                new Vector2(defaultFloorWidth, defaultFloorHeight),
                Color.white,
                2);
        }

        SpriteRenderer renderer = floorObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = floorObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateWhiteSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = 2;

        BoxCollider2D collider = floorObject.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = floorObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        return floorObject.transform.position.y + (floorObject.transform.localScale.y * 0.5f);
    }

    private RockWall EnsureRockWall(MoneyHud moneyHud, float floorTopY)
    {
        RockWall existingWall = FindPreferredRockWall();
        if (existingWall != null)
        {
            existingWall.Initialize(moneyHud, CreateUnlitMaterial(Color.white));
            return existingWall;
        }

        GetDefaultWallAnchor(floorTopY, out float bottomLeftX, out float bottomLeftY, out float fullWallWidth, out float fullWallHeight);

        GameObject rockObject = new GameObject("RockWall", typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D));
        rockObject.transform.position = new Vector3(bottomLeftX + (fullWallWidth * 0.5f), bottomLeftY + (fullWallHeight * 0.5f), 0f);
        RockWall wall = rockObject.AddComponent<RockWall>();
        wall.Initialize(moneyHud, CreateUnlitMaterial(Color.white));
        return wall;
    }

    private ShootTheRockPrototypeMarkers EnsurePrototypeMarkers()
    {
        ShootTheRockPrototypeMarkers markers = FindAnyObjectByType<ShootTheRockPrototypeMarkers>();
        if (markers != null)
        {
            markers.EnsureMarkers();
            return markers;
        }

        GameObject existingRoot = GameObject.Find(PrototypeMarkersObjectName);
        if (existingRoot == null)
            existingRoot = new GameObject(PrototypeMarkersObjectName);

        markers = existingRoot.GetComponent<ShootTheRockPrototypeMarkers>();
        if (markers == null)
            markers = existingRoot.AddComponent<ShootTheRockPrototypeMarkers>();

        markers.EnsureMarkers();
        return markers;
    }

    private void ConfigureContainedLevelFrame(Camera sceneCamera, RockWall rockWall)
    {
        if (rockWall == null)
            return;

        float aspect = sceneCamera != null && sceneCamera.aspect > 0.01f
            ? sceneCamera.aspect
            : (16f / 9f);

        float visibleHeight = PrototypeLevelTwoFrameSize.y;
        float visibleWidth = Mathf.Min(rockWall.WorldWidth, visibleHeight * aspect);
        rockWall.ConfigureSingleCameraFrame("LVL 2", visibleWidth, visibleHeight, Vector2.zero, Vector2.zero, centerWithinWall: true);
    }

    private void ConfigureSceneOwnedWallFrame(RockWall rockWall)
    {
        if (rockWall == null)
            return;

        rockWall.ConfigureSingleCameraFrame("SCENE", rockWall.WorldWidth, rockWall.WorldHeight, Vector2.zero, Vector2.zero, centerWithinWall: true);
    }

    private void GetDefaultWallAnchor(float floorTopY, out float bottomLeftX, out float bottomLeftY, out float fullWallWidth, out float fullWallHeight)
    {
        const float levelOneWidth = 11.04f;
        fullWallWidth = 48f;
        fullWallHeight = 56f;
        bottomLeftX = 4.27f - (levelOneWidth * 0.5f);
        bottomLeftY = floorTopY;
    }

    private Transform EnsureCannon(Camera sceneCamera, RockWall rockWall, float? floorTopY, MoneyHud moneyHud, ShootTheRockPrototypeMarkers markers, bool respectSceneTransform = false)
    {
        Transform cannonRoot = null;
        bool hadExistingCannon = false;

        CannonAim existingAim = FindAnyObjectByType<CannonAim>();
        if (existingAim != null)
        {
            cannonRoot = existingAim.transform;
            hadExistingCannon = true;
        }
        else
        {
            GameObject existingRootObject = GameObject.Find("CannonRoot");
            if (existingRootObject != null)
            {
                cannonRoot = existingRootObject.transform;
                hadExistingCannon = true;
            }
        }

        if (cannonRoot == null)
        {
            GameObject cannonObject = new GameObject("CannonRoot");
            cannonRoot = cannonObject.transform;
        }

        Vector3 playerPocketCenter = respectSceneTransform && hadExistingCannon
            ? cannonRoot.position
            : ResolvePlayerPocketCenter(rockWall, floorTopY ?? -8.525f, markers);
        if (rockWall != null)
            rockWall.CreateDebugPocket(playerPocketCenter, ResolvePlayerPocketSize(markers));

        if (!respectSceneTransform || !hadExistingCannon)
        {
            cannonRoot.position = playerPocketCenter;
            cannonRoot.rotation = Quaternion.identity;
        }

        Transform cannonBase = cannonRoot.Find("Base");
        if (cannonBase == null)
            cannonBase = CreateSpriteObject("Base", cannonRoot, Vector3.zero, new Vector2(0.62f, 0.62f), Color.white, 5).transform;

        Transform cannonBarrel = cannonRoot.Find("Barrel");
        if (cannonBarrel == null)
            cannonBarrel = CreateSpriteObject("Barrel", cannonRoot, new Vector3(0.54f, 0f, 0f), new Vector2(1.12f, 0.17f), Color.white, 6).transform;

        Transform firePoint = cannonRoot.Find("FirePoint");
        if (firePoint == null)
        {
            GameObject firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(cannonRoot, false);
            firePointObject.transform.localPosition = new Vector3(1.06f, 0f, 0f);
            firePoint = firePointObject.transform;
        }

        EnsureSpriteVisual(cannonBase, 5);
        EnsureSpriteVisual(cannonBarrel, 6);

        CircleCollider2D movementCollider = cannonRoot.GetComponent<CircleCollider2D>();
        if (movementCollider == null)
            movementCollider = cannonRoot.gameObject.AddComponent<CircleCollider2D>();
        movementCollider.radius = 0.36f;
        movementCollider.offset = Vector2.zero;
        movementCollider.isTrigger = false;

        Rigidbody2D body = cannonRoot.GetComponent<Rigidbody2D>();
        if (body == null)
            body = cannonRoot.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.useFullKinematicContacts = true;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        CannonAim aim = cannonRoot.GetComponent<CannonAim>();
        if (aim == null)
            aim = cannonRoot.gameObject.AddComponent<CannonAim>();
        aim.Initialize(sceneCamera);

        AutoShooter shooter = cannonRoot.GetComponent<AutoShooter>();
        if (shooter == null)
            shooter = cannonRoot.gameObject.AddComponent<AutoShooter>();
        shooter.Initialize(firePoint, rockWall);
        if (moneyHud != null)
            moneyHud.BindShooter(shooter);

        return cannonRoot;
    }

    private void EnsureSpriteVisual(Transform target, int sortingOrder)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = target.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateWhiteSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
    }

    private Vector3 ResolvePlayerPocketCenter(RockWall rockWall, float floorTopY, ShootTheRockPrototypeMarkers markers)
    {
        if (markers != null && markers.PlayerStartMarker != null)
            return markers.PlayerStartMarker.position;

        const float normalizedFrameX = 0.17f;
        const float normalizedFrameY = 0.62f;

        if (!rockWall.TryGetCameraFrameData(out Bounds wallBounds, out _, out _))
            return new Vector3(rockWall.transform.position.x - 4f, floorTopY + 6f, 0f);

        Vector2 preferred = new Vector2(
            Mathf.Lerp(wallBounds.min.x, wallBounds.max.x, normalizedFrameX),
            Mathf.Lerp(wallBounds.min.y, wallBounds.max.y, normalizedFrameY));

        return new Vector3(preferred.x, preferred.y, 0f);
    }

    private void EnsureTestGoalBall(RockWall rockWall, ShootTheRockPrototypeMarkers markers, bool respectSceneTransform = false)
    {
        if (rockWall == null)
            return;

        GameObject ballObject = GameObject.Find(TestGoalBallName);
        bool hadExistingBall = ballObject != null;
        if (ballObject == null)
            ballObject = new GameObject(TestGoalBallName, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D));

        Vector3 pocketCenter = respectSceneTransform && hadExistingBall
            ? ballObject.transform.position
            : ResolveTestGoalBallPocketCenter(rockWall, markers);
        rockWall.CreateDebugPocket(pocketCenter, ResolveBallPocketSize(markers));

        Vector3 spawnWorldPosition = pocketCenter + new Vector3(0f, 0.12f, 0f);
        if (!respectSceneTransform || !hadExistingBall)
        {
            ballObject.transform.position = spawnWorldPosition;
            ballObject.transform.rotation = Quaternion.identity;
            ballObject.transform.localScale = new Vector3(0.68f, 0.68f, 1f);
        }

        SpriteRenderer renderer = ballObject.GetComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateCircleSprite();
        renderer.color = new Color(1f, 0.87f, 0.2f, 1f);
        renderer.sortingOrder = 9;

        CircleCollider2D collider = ballObject.GetComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        collider.sharedMaterial = GetOrCreateGoalBallPhysicsMaterial();

        Rigidbody2D body = ballObject.GetComponent<Rigidbody2D>();
        ApplyGoalBallPhysicsProfile(body);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.sleepMode = RigidbodySleepMode2D.StartAwake;
    }

    private Vector3 ResolveTestGoalBallPocketCenter(RockWall rockWall, ShootTheRockPrototypeMarkers markers)
    {
        if (markers != null && markers.BallStartMarker != null)
            return markers.BallStartMarker.position;

        const float normalizedFrameX = 0.34f;
        const float normalizedFrameY = 0.36f;

        if (!rockWall.TryGetCameraFrameData(out Bounds wallBounds, out _, out _))
            return rockWall.transform.position;

        Vector2 preferred = new Vector2(
            Mathf.Lerp(wallBounds.min.x, wallBounds.max.x, normalizedFrameX),
            Mathf.Lerp(wallBounds.min.y, wallBounds.max.y, normalizedFrameY));

        return new Vector3(preferred.x, preferred.y, 0f);
    }

    private void EnsureTestGoalZone(RockWall rockWall, ShootTheRockPrototypeMarkers markers, bool respectSceneTransform = false)
    {
        if (rockWall == null)
            return;

        GameObject zoneObject = GameObject.Find(TestGoalZoneName);
        bool hadExistingZone = zoneObject != null;
        if (zoneObject == null)
            zoneObject = new GameObject(TestGoalZoneName, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(TestGoalZone));

        Vector3 goalCenter = respectSceneTransform && hadExistingZone
            ? zoneObject.transform.position
            : ResolveTestGoalZoneCenter(rockWall, markers);
        rockWall.CreateDebugPocket(goalCenter, ResolveGoalPocketSize(markers));

        if (!respectSceneTransform || !hadExistingZone)
        {
            zoneObject.transform.position = goalCenter;
            zoneObject.transform.rotation = Quaternion.identity;
            zoneObject.transform.localScale = new Vector3(2.4f, 1.2f, 1f);
        }

        SpriteRenderer renderer = zoneObject.GetComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateWhiteSprite();
        renderer.color = new Color(0.18f, 0.95f, 0.35f, 0.42f);
        renderer.sortingOrder = 4;

        BoxCollider2D collider = zoneObject.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one;

        TestGoalZone goalZone = zoneObject.GetComponent<TestGoalZone>();
        goalZone.Initialize(TestGoalBallName);
    }

    private Vector3 ResolveTestGoalZoneCenter(RockWall rockWall, ShootTheRockPrototypeMarkers markers)
    {
        if (markers != null && markers.GoalMarker != null)
            return markers.GoalMarker.position;

        const float normalizedFrameX = 0.12f;
        const float normalizedFrameY = 0.18f;

        if (!rockWall.TryGetCameraFrameData(out Bounds wallBounds, out _, out _))
            return new Vector3(rockWall.transform.position.x - 5f, rockWall.transform.position.y - 8f, 0f);

        Vector2 preferred = new Vector2(
            Mathf.Lerp(wallBounds.min.x, wallBounds.max.x, normalizedFrameX),
            Mathf.Lerp(wallBounds.min.y, wallBounds.max.y, normalizedFrameY));

        return new Vector3(preferred.x, preferred.y, 0f);
    }

    private Vector2 ResolvePlayerPocketSize(ShootTheRockPrototypeMarkers markers)
    {
        return markers != null ? markers.PlayerPocketSize : new Vector2(2.8f, 2.8f);
    }

    private Vector2 ResolveBallPocketSize(ShootTheRockPrototypeMarkers markers)
    {
        return markers != null ? markers.BallPocketSize : new Vector2(2.2f, 2.8f);
    }

    private Vector2 ResolveGoalPocketSize(ShootTheRockPrototypeMarkers markers)
    {
        return markers != null ? markers.GoalPocketSize : new Vector2(3f, 2.4f);
    }

    public static GameObject CreateSpriteObject(string name, Transform parent, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
    {
        GameObject obj = new GameObject(name, typeof(SpriteRenderer));
        if (parent != null)
            obj.transform.SetParent(parent, false);

        obj.transform.localPosition = localPosition;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return obj;
    }

    public static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader);
        SetMaterialColor(material, color);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", 0f);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        return material;
    }

    public static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_RendererColor"))
            material.SetColor("_RendererColor", color);

        Texture2D whiteTexture = GetOrCreateWhiteSprite().texture;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", whiteTexture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", whiteTexture);
    }

    public static Sprite GetOrCreateWhiteSprite()
    {
        if (cachedWhiteSprite != null)
            return cachedWhiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        cachedWhiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        cachedWhiteSprite.name = "RuntimeWhiteSprite";
        return cachedWhiteSprite;
    }

    public static Sprite GetOrCreateCircleSprite()
    {
        if (cachedCircleSprite != null)
            return cachedCircleSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        float radiusSquared = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                bool inside = delta.sqrMagnitude <= radiusSquared;
                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        cachedCircleSprite.name = "RuntimeCircleSprite";
        return cachedCircleSprite;
    }

    public static void ApplyGoalBallPhysicsProfile(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0.92f;
        body.mass = 0.55f;
        body.linearDamping = 0.045f;
        body.angularDamping = 0.035f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sleepMode = RigidbodySleepMode2D.StartAwake;
    }

    public static PhysicsMaterial2D GetOrCreateGoalBallPhysicsMaterial()
    {
        if (cachedGoalBallPhysicsMaterial != null)
            return cachedGoalBallPhysicsMaterial;

        cachedGoalBallPhysicsMaterial = new PhysicsMaterial2D("GoalBallPhysics")
        {
            friction = 0.18f,
            bounciness = 0.58f,
        };
        return cachedGoalBallPhysicsMaterial;
    }
}
