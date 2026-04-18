using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class ShootTheRockPrototypeBootstrap : MonoBehaviour
{
    private static Sprite cachedWhiteSprite;

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
        Transform cannonRoot = EnsureCannon(sceneCamera, rockWall, floorTopY, moneyHud);
        CameraFramingController framingController = InitializeOptionalCameraFraming(sceneCamera, rockWall, cannonRoot);
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
            moneyHud = EnsureMoneyHud();

        float floorTopY = EnsureFloor();
        rockWall.Initialize(moneyHud, CreateUnlitMaterial(Color.white));
        Transform cannonRoot = EnsureCannon(sceneCamera, rockWall, floorTopY, moneyHud);
        CameraFramingController framingController = InitializeOptionalCameraFraming(sceneCamera, rockWall, cannonRoot);
        if (moneyHud != null)
            moneyHud.BindProgression(rockWall, framingController);
    }

    private CameraFramingController InitializeOptionalCameraFraming(Camera sceneCamera, RockWall rockWall, Transform cannonRoot)
    {
        if (sceneCamera == null)
            return null;

        CameraFramingController framingController = sceneCamera.GetComponent<CameraFramingController>();
        if (framingController == null)
            framingController = sceneCamera.gameObject.AddComponent<CameraFramingController>();

        framingController.Initialize(sceneCamera, rockWall, cannonRoot);
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
        return hud;
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

    private void GetDefaultWallAnchor(float floorTopY, out float bottomLeftX, out float bottomLeftY, out float fullWallWidth, out float fullWallHeight)
    {
        const float levelOneWidth = 11.04f;
        fullWallWidth = 33.12f;
        fullWallHeight = 34.224f;
        bottomLeftX = 4.27f - (levelOneWidth * 0.5f);
        bottomLeftY = floorTopY;
    }

    private Transform EnsureCannon(Camera sceneCamera, RockWall rockWall, float? floorTopY, MoneyHud moneyHud)
    {
        Transform cannonRoot = null;

        CannonAim existingAim = FindAnyObjectByType<CannonAim>();
        if (existingAim != null)
            cannonRoot = existingAim.transform;
        else
        {
            GameObject existingRootObject = GameObject.Find("CannonRoot");
            if (existingRootObject != null)
                cannonRoot = existingRootObject.transform;
        }

        if (cannonRoot == null)
        {
            float resolvedFloorTopY = floorTopY ?? -8.525f;
            GameObject cannonObject = new GameObject("CannonRoot");
            cannonObject.transform.position = new Vector3(-13.8f, resolvedFloorTopY + 0.24f, 0f);
            cannonRoot = cannonObject.transform;
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
}
