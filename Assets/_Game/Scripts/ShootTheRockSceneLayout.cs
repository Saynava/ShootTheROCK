using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct ShootTheRockRevealLevelData
{
    public int columnCount;
    public int rowCount;
    public Vector2 cameraCenter;
    public float cameraSize;
    [Range(0f, 1f)] public float revealThreshold;

    public ShootTheRockRevealLevelData(int columnCount, int rowCount, Vector2 cameraCenter, float cameraSize, float revealThreshold)
    {
        this.columnCount = columnCount;
        this.rowCount = rowCount;
        this.cameraCenter = cameraCenter;
        this.cameraSize = cameraSize;
        this.revealThreshold = revealThreshold;
    }
}

public class ShootTheRockSceneLayout : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera sceneCamera;
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

    [Header("Wall Authoring")]
    [Min(0.01f)]
    [SerializeField] private float wallCellSize = 0.092f;
    [SerializeField] private ShootTheRockRevealLevelData[] revealLevels =
    {
        new ShootTheRockRevealLevelData(120, 124, new Vector2(-4.8f, 1.6f), 10.4f, 0.20f),
        new ShootTheRockRevealLevelData(220, 228, new Vector2(-1.4f, 2.9f), 15.8f, 0.22f),
        new ShootTheRockRevealLevelData(360, 344, new Vector2(4.2f, 5.6f), 22.8f, 1f),
    };

    [Header("Editor Preview")]
    [SerializeField] private bool autoRefreshPreview = true;

    public Camera SceneCamera => sceneCamera;
    public MoneyHud MoneyHud => moneyHud;
    public Transform CannonRoot => cannonRoot;
    public Transform FirePoint => firePoint;
    public RockWall RockWall => rockWall;
    public float WallCellSize => wallCellSize;

    public Vector2 GetWallBottomLeftAnchor()
    {
        if (wallAnchor != null)
            return wallAnchor.position;

        return new Vector2(-1.25f, GetFloorTopY());
    }

    public ShootTheRockRevealLevelData[] GetRevealLevels()
    {
        if (revealLevels == null || revealLevels.Length == 0)
            revealLevels = CreateDefaultRevealLevels();

        return revealLevels;
    }

    public void EnsureSceneObjectsExist()
    {
        EnsureCamera();
        EnsureMoneyCanvas();
        EnsureFloor();
        EnsureWallAnchor();
        EnsureRockWall();
        EnsureCannon();
    }

    public void RefreshPreview()
    {
        EnsureSceneObjectsExist();
        if (rockWall == null)
            return;

        rockWall.Initialize(moneyHud, ShootTheRockPrototypeBootstrap.CreateUnlitMaterial(Color.white), GetWallBottomLeftAnchor(), GetRevealLevels(), wallCellSize);
    }

    [ContextMenu("Build / Refresh Scene Objects")]
    private void ContextBuildOrRefreshSceneObjects()
    {
        EnsureSceneObjectsExist();
        RefreshPreview();
    }

    [ContextMenu("Bake RockWall Scale Into Wall Data")]
    public void BakeRockWallScaleIntoWallData()
    {
        EnsureSceneObjectsExist();
        if (rockWall == null)
            return;

        Vector3 wallScale3 = rockWall.transform.localScale;
        float scaleX = Mathf.Max(0.01f, Mathf.Abs(wallScale3.x));
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(wallScale3.y));
        float uniformScale = Mathf.Max(scaleX, scaleY);

        if (Mathf.Approximately(uniformScale, 1f))
            return;

        Vector2 anchor = GetWallBottomLeftAnchor();

        for (int i = 0; i < revealLevels.Length; i++)
        {
            ShootTheRockRevealLevelData level = revealLevels[i];
            level.columnCount = Mathf.Max(8, Mathf.RoundToInt(level.columnCount * uniformScale));
            level.rowCount = Mathf.Max(8, Mathf.RoundToInt(level.rowCount * uniformScale));
            level.cameraCenter = anchor + ((level.cameraCenter - anchor) * uniformScale);
            level.cameraSize = Mathf.Max(1f, level.cameraSize * uniformScale);
            revealLevels[i] = level;
        }

        rockWall.transform.localScale = Vector3.one;
        RefreshPreview();
    }

    [ContextMenu("Reset Wall Authoring Defaults")]
    public void ResetWallAuthoringDefaults()
    {
        wallCellSize = 0.092f;
        revealLevels = CreateDefaultRevealLevels();
        if (rockWall != null)
            rockWall.transform.localScale = Vector3.one;
        RefreshPreview();
    }

    private ShootTheRockRevealLevelData[] CreateDefaultRevealLevels()
    {
        return new[]
        {
            new ShootTheRockRevealLevelData(120, 124, new Vector2(-4.8f, 1.6f), 10.4f, 0.20f),
            new ShootTheRockRevealLevelData(220, 228, new Vector2(-1.4f, 2.9f), 15.8f, 0.22f),
            new ShootTheRockRevealLevelData(360, 344, new Vector2(4.2f, 5.6f), 22.8f, 1f),
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        wallCellSize = Mathf.Max(0.01f, wallCellSize);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!autoRefreshPreview)
            return;

        EditorApplication.delayCall -= DelayedRefreshPreview;
        EditorApplication.delayCall += DelayedRefreshPreview;
    }

    private void DelayedRefreshPreview()
    {
        if (this == null)
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        RefreshPreview();
    }
#endif

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
            sceneCamera.transform.position = new Vector3(-4.8f, 1.6f, -10f);
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 10.4f;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = Color.black;
        }
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
            anchorObject.transform.position = new Vector3(-1.25f, GetFloorTopY(), 0f);
            wallAnchor = anchorObject.transform;
        }
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

        CannonAim aim = cannonRoot.GetComponent<CannonAim>();
        if (aim == null)
            aim = cannonRoot.gameObject.AddComponent<CannonAim>();

        AutoShooter shooter = cannonRoot.GetComponent<AutoShooter>();
        if (shooter == null)
            shooter = cannonRoot.gameObject.AddComponent<AutoShooter>();
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
}
