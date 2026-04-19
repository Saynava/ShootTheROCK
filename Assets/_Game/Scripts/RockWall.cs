using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class RockWall : MonoBehaviour
{
    private struct LegacyLevelShape
    {
        public readonly float WorldWidth;
        public readonly float WorldHeight;
        public readonly float CellsPerUnit;
        public readonly Vector2 CameraPadding;
        public readonly Vector2 CameraLookOffset;

        public LegacyLevelShape(float worldWidth, float worldHeight, float cellsPerUnit, Vector2 cameraPadding, Vector2 cameraLookOffset)
        {
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
            CellsPerUnit = cellsPerUnit;
            CameraPadding = cameraPadding;
            CameraLookOffset = cameraLookOffset;
        }
    }

    [System.Serializable]
    private struct ProgressionFrame
    {
        public string label;
        public float visibleWorldWidth;
        public float visibleWorldHeight;

        public ProgressionFrame(string label, float visibleWorldWidth, float visibleWorldHeight)
        {
            this.label = label;
            this.visibleWorldWidth = visibleWorldWidth;
            this.visibleWorldHeight = visibleWorldHeight;
        }
    }

    private const float MinimumAnchorColumnPercent = 0.08f;
    private const int MinimumAnchorColumnsAbsolute = 6;
    private const float SurfaceImpactOutsideBiasColumns = 0.32f;
    private const float CoreStampRadiusColumnsMin = 1.45f;
    private const float CoreStampRadiusColumnsMax = 2.15f;
    private const float CoreStampRadiusRowsMin = 1.6f;
    private const float CoreStampRadiusRowsMax = 2.45f;
    private const float SatelliteStampRadiusColumnsMin = 0.8f;
    private const float SatelliteStampRadiusColumnsMax = 1.25f;
    private const float SatelliteStampRadiusRowsMin = 0.9f;
    private const float SatelliteStampRadiusRowsMax = 1.45f;
    private const int SatelliteStampCountMin = 0;
    private const int SatelliteStampCountMax = 1;
    private const int ScatterPixelCountMin = 0;
    private const int ScatterPixelCountMax = 2;
    private const int MinimumCellsRemovedPerHit = 4;
    private const int DamageVisualTierCount = 4;
    private const int MaxChipParticlesPerHit = 12;
    private const int PrewarmChipParticleCount = 12;
    private const float LegacyCameraAspect = 16f / 9f;
    private static readonly Color[] DamageTierColors =
    {
        Color.white,
        new Color(0.68f, 0.68f, 0.68f, 1f),
        new Color(0.4f, 0.4f, 0.4f, 1f),
        new Color(0.16f, 0.16f, 0.16f, 1f),
    };

    [Header("Wall Size")]
    [Min(1f)]
    [SerializeField] private float worldWidth = 48f;
    [Min(1f)]
    [SerializeField] private float worldHeight = 56f;
    [Min(1f)]
    [SerializeField] private float cellsPerUnit = 10.869565f;

    [Header("Level Progression")]
    [SerializeField] private bool useCameraBoundLevels = true;
    [SerializeField] private ProgressionFrame[] progressionFrames =
    {
        new ProgressionFrame("LVL 1", 11.04f, 11.408f),
        new ProgressionFrame("LVL 2", 22.08f, 22.816f),
        new ProgressionFrame("LVL 3", 30f, 34f),
    };
    [SerializeField] private int activeProgressionFrameIndex;

    [Header("Optional Camera Framing")]
    [SerializeField] private Vector2 cameraPadding = new Vector2(1.8f, 0.85f);
    [SerializeField] private Vector2 cameraLookOffset = new Vector2(-1.1f, 0f);

    [Header("Cell Durability")]
    [Min(1f)]
    [SerializeField] private float cellMaxHitPoints = 3f;
    [Min(0.1f)]
    [SerializeField] private float baseImpactDamage = 1.35f;
    [Range(0.1f, 2f)]
    [SerializeField] private float edgeDamageMultiplier = 0.8f;
    [Range(0.1f, 2f)]
    [SerializeField] private float centerDamageMultiplier = 1.3f;

    [Header("Runtime Chunking")]
    [SerializeField] private bool useChunkedRuntime = true;
    [Min(8)]
    [SerializeField] private int chunkSizeInCells = 32;

    [Header("Runtime Performance")]
    [Min(0.01f)]
    [SerializeField] private float colliderRebuildInterval = 0.05f;
    [Min(0.01f)]
    [SerializeField] private float islandCleanupInterval = 0.15f;

    [Header("Debug View")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool drawChunkGizmos = true;

    [Header("Editor Behavior")]
    [SerializeField] private bool autoRebuildInEditMode = true;
    [SerializeField] private bool absorbTransformScaleIntoSize = true;

    private MoneyHud moneyHud;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private PolygonCollider2D polygonCollider;
    private Transform particlesRoot;
    private Mesh rockMesh;
    private Material wallMaterial;
    private Material[] damageTierMaterials;
    private bool[,] solidCells;
    private float[,] cellHitPoints;
    private float rowHeight;
    private float columnWidth;
    private int rowCount;
    private int columnCount;
    private int minimumColumnsRemaining;
    private RockWallRuntimeGrid runtimeGrid;
    private bool runtimeChunkFullRebuildRequired = true;
    private bool hasDirtyChunkRegion;
    private int dirtyMinRow;
    private int dirtyMaxRow;
    private int dirtyMinColumn;
    private int dirtyMaxColumn;
    private bool hasPendingColliderDirtyRegion;
    private int colliderDirtyMinRow;
    private int colliderDirtyMaxRow;
    private int colliderDirtyMinColumn;
    private int colliderDirtyMaxColumn;
    private float nextColliderRebuildTime;
    private bool hasPendingIslandCleanup;
    private float nextIslandCleanupTime;
    private bool[,] islandConnectedBuffer;
    private readonly Queue<Vector2Int> islandCleanupQueue = new Queue<Vector2Int>();
    private readonly List<Vector2> islandCleanupRemovedPixelCenters = new List<Vector2>();
    private readonly Queue<ChipParticle> chipParticlePool = new Queue<ChipParticle>();

    public float WorldWidth => worldWidth;
    public float WorldHeight => worldHeight;
    public float CellsPerUnit => cellsPerUnit;
    public int TotalLevelCount => useCameraBoundLevels && progressionFrames != null && progressionFrames.Length > 0 ? progressionFrames.Length : 1;
    public int CurrentLevelNumber => Mathf.Clamp(activeProgressionFrameIndex + 1, 1, TotalLevelCount);
    public bool CanAdvanceLevel => CurrentLevelNumber < TotalLevelCount;
    public string CurrentLevelLabel => GetCurrentFrameLabel();

    public Vector2 GetBottomLeftAnchorWorld()
    {
        return new Vector2(
            transform.position.x - (worldWidth * 0.5f),
            transform.position.y - (worldHeight * 0.5f));
    }

    public void ApplyMassiveWallPresetKeepingBottomLeft()
    {
        Vector2 bottomLeftAnchor = GetBottomLeftAnchorWorld();
        float preservedCellsPerUnit = Mathf.Max(1f, cellsPerUnit);

        worldWidth = 48f;
        worldHeight = 56f;
        cellsPerUnit = preservedCellsPerUnit;
        progressionFrames = new[]
        {
            new ProgressionFrame("LVL 1", 11.04f, 11.408f),
            new ProgressionFrame("LVL 2", 22.08f, 22.816f),
            new ProgressionFrame("LVL 3", 30f, 34f),
        };
        activeProgressionFrameIndex = 0;
        cellMaxHitPoints = 3f;
        baseImpactDamage = 1.35f;
        edgeDamageMultiplier = 0.8f;
        centerDamageMultiplier = 1.3f;

        transform.position = new Vector3(
            bottomLeftAnchor.x + (worldWidth * 0.5f),
            bottomLeftAnchor.y + (worldHeight * 0.5f),
            transform.position.z);

        EnsureRuntimeState(null);
        ValidateProgressionFrames();
        RebuildFromAuthoring(resetDamage: true);
    }

    public void Initialize(MoneyHud moneyHud, Material rockMaterial)
    {
        this.moneyHud = moneyHud;
        EnsureRuntimeState(rockMaterial);
        AbsorbTransformScaleIntoAuthoring();
        ValidateProgressionFrames();
        RebuildFromAuthoring(resetDamage: true);
    }

    public void Initialize(MoneyHud moneyHud, Material rockMaterial, Vector2 wallBottomLeftAnchor)
    {
        transform.position = new Vector3(
            wallBottomLeftAnchor.x + (worldWidth * 0.5f),
            wallBottomLeftAnchor.y + (worldHeight * 0.5f),
            transform.position.z);
        Initialize(moneyHud, rockMaterial);
    }

    public void Initialize(MoneyHud moneyHud, Material rockMaterial, Vector2 wallBottomLeftAnchor, ShootTheRockRevealLevelData[] legacyRevealLevels, float cellSizeOverride)
    {
        ApplyLegacyShape(BuildLegacyShape(legacyRevealLevels, Mathf.Max(0.01f, cellSizeOverride)));
        transform.position = new Vector3(
            wallBottomLeftAnchor.x + (worldWidth * 0.5f),
            wallBottomLeftAnchor.y + (worldHeight * 0.5f),
            transform.position.z);
        Initialize(moneyHud, rockMaterial);
    }

    public void Initialize(MoneyHud moneyHud, Material rockMaterial, Vector2 wallBottomLeftAnchor, RockWallDefinition definition)
    {
        ApplyLegacyShape(BuildLegacyShape(definition));
        transform.position = new Vector3(
            wallBottomLeftAnchor.x + (worldWidth * 0.5f),
            wallBottomLeftAnchor.y + (worldHeight * 0.5f),
            transform.position.z);
        Initialize(moneyHud, rockMaterial);
    }

    public void ApplyHit(Vector2 worldPoint, Vector2 pushDirection)
    {
        ApplyHit(worldPoint, pushDirection, 1f);
    }

    public void ApplyHit(Vector2 worldPoint, Vector2 pushDirection, float blastRadiusScale)
    {
        using (ShootTheRockPerformance.ApplyHitMarker.Auto())
        {
            EnsureRuntimeState(null);
            if (solidCells == null || rowCount <= 0 || columnCount <= 0)
                RebuildFromAuthoring(resetDamage: true);

            if (moneyHud == null)
                moneyHud = FindAnyObjectByType<MoneyHud>();

            Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
            Vector2 resolvedLocalPoint = ClampLocalPointToWallBounds(localPoint);
            Vector2 resolvedWorldPoint = transform.TransformPoint(resolvedLocalPoint);

            ShootTheRockPerformance.RecordHit();
            moneyHud?.AddMoney(1);
            List<Vector2> removedPixelCenters = CarveRock(resolvedWorldPoint, pushDirection, Mathf.Max(0.25f, blastRadiusScale));
            if (removedPixelCenters.Count == 0)
                removedPixelCenters = ForceImpactAtNearestVisibleCell(resolvedLocalPoint, Mathf.Max(0.25f, blastRadiusScale));

            ShootTheRockPerformance.RecordCellsDestroyed(removedPixelCenters.Count);
            if (removedPixelCenters.Count > 0)
                QueueIslandCleanup();

            SpawnHitParticles(removedPixelCenters, pushDirection);
            RebuildRockShape();
        }
    }

    public bool TryGetCameraFrameData(out Bounds wallBounds, out Vector2 resolvedCameraPadding, out Vector2 lookOffset)
    {
        if (rowCount <= 0 || columnCount <= 0 || worldWidth <= 0f || worldHeight <= 0f)
        {
            wallBounds = default;
            resolvedCameraPadding = Vector2.zero;
            lookOffset = Vector2.zero;
            return false;
        }

        if (!TryGetActiveFrameLocalRect(out Rect localRect))
        {
            wallBounds = default;
            resolvedCameraPadding = Vector2.zero;
            lookOffset = Vector2.zero;
            return false;
        }

        Vector3 frameCenterWorld = transform.TransformPoint(new Vector3(localRect.center.x, localRect.center.y, 0f));
        wallBounds = new Bounds(frameCenterWorld, new Vector3(localRect.width, localRect.height, 0.1f));
        resolvedCameraPadding = cameraPadding;
        lookOffset = cameraLookOffset;
        return true;
    }

    public void GetCameraTarget(out Vector3 position, out float orthographicSize)
    {
        if (!TryGetCameraFrameData(out Bounds wallBounds, out Vector2 resolvedCameraPadding, out Vector2 lookOffset))
        {
            position = new Vector3(0f, 0f, -10f);
            orthographicSize = 10f;
            return;
        }

        float halfHeight = wallBounds.extents.y + resolvedCameraPadding.y;
        float halfWidth = wallBounds.extents.x + resolvedCameraPadding.x;
        position = wallBounds.center + (Vector3)lookOffset;
        position.z = -10f;
        orthographicSize = Mathf.Max(1f, halfHeight, halfWidth / LegacyCameraAspect);
    }

    public bool ForceRevealNextLevel()
    {
        return TryAdvanceLevel();
    }

    public bool TryAdvanceLevel()
    {
        ValidateProgressionFrames();
        if (!CanAdvanceLevel)
            return false;

        activeProgressionFrameIndex = Mathf.Clamp(activeProgressionFrameIndex + 1, 0, TotalLevelCount - 1);
        runtimeChunkFullRebuildRequired = true;
        ClearDirtyChunkRegion();
        ClearColliderDirtyChunkRegion();
        if (Application.isPlaying)
            RebuildRockShape();
        return true;
    }

    [ContextMenu("Rebuild Wall From Authoring")]
    public void RebuildWallFromAuthoringContext()
    {
        RebuildFromAuthoring(resetDamage: true);
    }

    [ContextMenu("Apply Massive Wall Preset (Keep Bottom-Left)")]
    public void ApplyMassiveWallPresetKeepingBottomLeftContext()
    {
        ApplyMassiveWallPresetKeepingBottomLeft();
    }

    private void OnValidate()
    {
        worldWidth = Mathf.Max(1f, worldWidth);
        worldHeight = Mathf.Max(1f, worldHeight);
        cellsPerUnit = Mathf.Max(1f, cellsPerUnit);
        chunkSizeInCells = Mathf.Max(8, chunkSizeInCells);
        cellMaxHitPoints = Mathf.Max(1f, cellMaxHitPoints);
        baseImpactDamage = Mathf.Max(0.1f, baseImpactDamage);
        edgeDamageMultiplier = Mathf.Clamp(edgeDamageMultiplier, 0.1f, 2f);
        centerDamageMultiplier = Mathf.Clamp(centerDamageMultiplier, 0.1f, 2f);
        colliderRebuildInterval = Mathf.Max(0.01f, colliderRebuildInterval);
        islandCleanupInterval = Mathf.Max(0.01f, islandCleanupInterval);
        cameraPadding.x = Mathf.Max(0f, cameraPadding.x);
        cameraPadding.y = Mathf.Max(0f, cameraPadding.y);
        ValidateProgressionFrames();

        if (Application.isPlaying)
            return;

        EnsureRuntimeState(null);
        AbsorbTransformScaleIntoAuthoring();
        if (autoRebuildInEditMode)
            RebuildFromAuthoring(resetDamage: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (hasPendingIslandCleanup && Time.time >= nextIslandCleanupTime)
            ProcessPendingIslandCleanup();

        if (!ShouldUseChunkedRuntime())
            return;
        if (!hasPendingColliderDirtyRegion)
            return;
        if (Time.time < nextColliderRebuildTime)
            return;
        if (hasDirtyChunkRegion)
            return;

        RebuildRockShape();
    }

    private void ValidateProgressionFrames()
    {
        if (progressionFrames == null || progressionFrames.Length == 0)
        {
            progressionFrames = new[]
            {
                new ProgressionFrame("LVL 1", worldWidth, worldHeight),
            };
        }

        for (int i = 0; i < progressionFrames.Length; i++)
        {
            progressionFrames[i].visibleWorldWidth = Mathf.Clamp(progressionFrames[i].visibleWorldWidth, 1f, worldWidth);
            progressionFrames[i].visibleWorldHeight = Mathf.Clamp(progressionFrames[i].visibleWorldHeight, 1f, worldHeight);
            if (string.IsNullOrWhiteSpace(progressionFrames[i].label))
                progressionFrames[i].label = "LVL " + (i + 1);
        }

        activeProgressionFrameIndex = Mathf.Clamp(activeProgressionFrameIndex, 0, progressionFrames.Length - 1);
    }

    private void ApplyLegacyShape(LegacyLevelShape shape)
    {
        worldWidth = Mathf.Max(1f, shape.WorldWidth);
        worldHeight = Mathf.Max(1f, shape.WorldHeight);
        cellsPerUnit = Mathf.Max(1f, shape.CellsPerUnit);
        cameraPadding = new Vector2(Mathf.Max(0f, shape.CameraPadding.x), Mathf.Max(0f, shape.CameraPadding.y));
        cameraLookOffset = shape.CameraLookOffset;
    }

    private LegacyLevelShape BuildLegacyShape(RockWallDefinition definition)
    {
        if (definition == null || !definition.HasStages)
            return new LegacyLevelShape(worldWidth, worldHeight, cellsPerUnit, cameraPadding, cameraLookOffset);

        RockWallDefinition.RevealStage stage = definition.GetStageOrLast(0);
        return new LegacyLevelShape(stage.worldWidth, stage.worldHeight, stage.cellsPerUnit, stage.cameraPadding, stage.cameraLookOffset);
    }

    private LegacyLevelShape BuildLegacyShape(ShootTheRockRevealLevelData[] legacyRevealLevels, float cellSize)
    {
        if (legacyRevealLevels == null || legacyRevealLevels.Length == 0)
            return new LegacyLevelShape(worldWidth, worldHeight, cellsPerUnit, cameraPadding, cameraLookOffset);

        ShootTheRockRevealLevelData level = legacyRevealLevels[0];
        float resolvedWidth = Mathf.Max(1f, level.columnCount * cellSize);
        float resolvedHeight = Mathf.Max(1f, level.rowCount * cellSize);
        float resolvedCellsPerUnit = Mathf.Max(1f, 1f / cellSize);
        Vector2 wallCenter = new Vector2(resolvedWidth * 0.5f, resolvedHeight * 0.5f);
        Vector2 lookOffset = level.cameraCenter - wallCenter;
        float halfHeight = resolvedHeight * 0.5f;
        float halfWidth = resolvedWidth * 0.5f;
        float paddingY = Mathf.Max(0f, level.cameraSize - halfHeight);
        float paddingX = Mathf.Max(0f, (level.cameraSize * LegacyCameraAspect) - halfWidth);
        return new LegacyLevelShape(resolvedWidth, resolvedHeight, resolvedCellsPerUnit, new Vector2(paddingX, paddingY), lookOffset);
    }

    private string GetCurrentFrameLabel()
    {
        if (!useCameraBoundLevels || progressionFrames == null || progressionFrames.Length == 0)
            return "LVL 1";

        return progressionFrames[Mathf.Clamp(activeProgressionFrameIndex, 0, progressionFrames.Length - 1)].label;
    }

    private bool TryGetActiveFrameLocalRect(out Rect localRect)
    {
        float width = worldWidth;
        float height = worldHeight;

        if (useCameraBoundLevels && progressionFrames != null && progressionFrames.Length > 0)
        {
            ProgressionFrame frame = progressionFrames[Mathf.Clamp(activeProgressionFrameIndex, 0, progressionFrames.Length - 1)];
            width = Mathf.Clamp(frame.visibleWorldWidth, 1f, worldWidth);
            height = Mathf.Clamp(frame.visibleWorldHeight, 1f, worldHeight);
        }

        localRect = new Rect(-worldWidth * 0.5f, -worldHeight * 0.5f, width, height);
        return width > 0f && height > 0f;
    }

    private bool IsLocalPointInsideActiveFrame(Vector2 localPoint)
    {
        if (!TryGetActiveFrameLocalRect(out Rect localRect))
            return false;

        return localPoint.x >= localRect.xMin && localPoint.x <= localRect.xMax && localPoint.y >= localRect.yMin && localPoint.y <= localRect.yMax;
    }

    private Vector2 ClampLocalPointToActiveFrame(Vector2 localPoint)
    {
        if (!TryGetActiveFrameLocalRect(out Rect localRect))
            return localPoint;

        return new Vector2(
            Mathf.Clamp(localPoint.x, localRect.xMin + (columnWidth * 0.5f), localRect.xMax - (columnWidth * 0.5f)),
            Mathf.Clamp(localPoint.y, localRect.yMin + (rowHeight * 0.5f), localRect.yMax - (rowHeight * 0.5f)));
    }

    private Vector2 ClampLocalPointToWallBounds(Vector2 localPoint)
    {
        float halfColumn = columnWidth * 0.5f;
        float halfRow = rowHeight * 0.5f;
        return new Vector2(
            Mathf.Clamp(localPoint.x, (-worldWidth * 0.5f) + halfColumn, (worldWidth * 0.5f) - halfColumn),
            Mathf.Clamp(localPoint.y, (-worldHeight * 0.5f) + halfRow, (worldHeight * 0.5f) - halfRow));
    }

    private List<Vector2> ForceImpactAtNearestVisibleCell(Vector2 localPoint, float blastRadiusScale)
    {
        List<Vector2> removedPixelCenters = new List<Vector2>();
        int centerRow = GetRowIndexFromLocalY(localPoint.y);
        int centerColumn = GetColumnIndexFromLocalX(localPoint.x);
        if (!TryFindNearestSolidCell(centerRow, centerColumn, 8, out int foundRow, out int foundColumn))
            return removedPixelCenters;

        float forcedDamage = Mathf.Max(baseImpactDamage * 1.25f, cellMaxHitPoints);
        ApplyPointDamage(foundRow, foundColumn, forcedDamage, removedPixelCenters);
        return removedPixelCenters;
    }

    private void GetActiveFrameCellBounds(out int minRow, out int maxRow, out int minColumn, out int maxColumn)
    {
        minRow = 0;
        maxRow = Mathf.Max(0, rowCount - 1);
        minColumn = 0;
        maxColumn = Mathf.Max(0, columnCount - 1);

        if (rowCount <= 0 || columnCount <= 0)
            return;
        if (!TryGetActiveFrameLocalRect(out Rect localRect))
            return;

        int visibleColumns = Mathf.Clamp(Mathf.CeilToInt(localRect.width / Mathf.Max(0.0001f, columnWidth)), 1, columnCount);
        int visibleRows = Mathf.Clamp(Mathf.CeilToInt(localRect.height / Mathf.Max(0.0001f, rowHeight)), 1, rowCount);

        minColumn = 0;
        maxColumn = visibleColumns - 1;
        minRow = rowCount - visibleRows;
        maxRow = rowCount - 1;
    }

    private bool IsCellInsideActiveFrame(int row, int column)
    {
        GetActiveFrameCellBounds(out int minRow, out int maxRow, out int minColumn, out int maxColumn);
        return row >= minRow && row <= maxRow && column >= minColumn && column <= maxColumn;
    }

    private void AbsorbTransformScaleIntoAuthoring()
    {
        if (!absorbTransformScaleIntoSize)
            return;

        Vector3 localScale = transform.localScale;
        if (Mathf.Approximately(localScale.x, 1f) && Mathf.Approximately(localScale.y, 1f))
            return;

        worldWidth = Mathf.Max(1f, worldWidth * Mathf.Abs(localScale.x));
        worldHeight = Mathf.Max(1f, worldHeight * Mathf.Abs(localScale.y));
        ValidateProgressionFrames();
        transform.localScale = Vector3.one;
    }

    private void EnsureRuntimeState(Material rockMaterial)
    {
        meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        polygonCollider = polygonCollider != null ? polygonCollider : GetComponent<PolygonCollider2D>();
        if (polygonCollider == null)
            polygonCollider = gameObject.AddComponent<PolygonCollider2D>();

        if (rockMesh == null)
        {
            rockMesh = new Mesh();
            rockMesh.name = "RockWallMesh";
        }

        rockMesh.indexFormat = IndexFormat.UInt32;

        if (meshFilter.sharedMesh != rockMesh)
            meshFilter.sharedMesh = rockMesh;

        if (rockMaterial != null)
            wallMaterial = rockMaterial;
        else if (wallMaterial == null)
            wallMaterial = ShootTheRockPrototypeBootstrap.CreateUnlitMaterial(Color.white);

        if (damageTierMaterials == null || damageTierMaterials.Length != DamageVisualTierCount)
            damageTierMaterials = new Material[DamageVisualTierCount];

        for (int i = 0; i < DamageVisualTierCount; i++)
        {
            if (damageTierMaterials[i] == null)
                damageTierMaterials[i] = ShootTheRockPrototypeBootstrap.CreateUnlitMaterial(DamageTierColors[i]);
            else
                ShootTheRockPrototypeBootstrap.SetMaterialColor(damageTierMaterials[i], DamageTierColors[i]);
        }

        meshRenderer.sharedMaterials = damageTierMaterials;
        meshRenderer.sortingOrder = 3;
        meshRenderer.enabled = !ShouldUseChunkedRuntime();
        polygonCollider.enabled = !ShouldUseChunkedRuntime();

        if (ShouldUseChunkedRuntime())
        {
            if (runtimeGrid == null)
                runtimeGrid = new RockWallRuntimeGrid();
            runtimeGrid.Initialize(transform, chunkSizeInCells);
        }

        if (particlesRoot == null && Application.isPlaying)
        {
            particlesRoot = new GameObject("ParticlesRoot").transform;
            particlesRoot.SetParent(null, false);
        }
    }

    private void RebuildFromAuthoring(bool resetDamage)
    {
        EnsureRuntimeState(null);
        ValidateProgressionFrames();

        int targetColumns = Mathf.Max(8, Mathf.RoundToInt(worldWidth * cellsPerUnit));
        int targetRows = Mathf.Max(8, Mathf.RoundToInt(worldHeight * cellsPerUnit));
        bool sameShape = solidCells != null && targetColumns == columnCount && targetRows == rowCount;

        columnCount = targetColumns;
        rowCount = targetRows;
        worldWidth = columnCount / cellsPerUnit;
        worldHeight = rowCount / cellsPerUnit;
        columnWidth = worldWidth / columnCount;
        rowHeight = worldHeight / rowCount;
        minimumColumnsRemaining = Mathf.Clamp(
            Mathf.RoundToInt(columnCount * MinimumAnchorColumnPercent),
            MinimumAnchorColumnsAbsolute,
            Mathf.Max(MinimumAnchorColumnsAbsolute, columnCount - 1));

        if (resetDamage || !sameShape || solidCells == null || cellHitPoints == null)
        {
            solidCells = new bool[rowCount, columnCount];
            cellHitPoints = new float[rowCount, columnCount];
            ResetAllCellsToFullHealth();
        }

        runtimeChunkFullRebuildRequired = true;
        ClearDirtyChunkRegion();
        ClearColliderDirtyChunkRegion();
        ClearPendingIslandCleanup();
        EnsureIslandCleanupBuffer();
        RebuildRockShape();
    }

    private void ResetAllCellsToFullHealth()
    {
        if (solidCells == null || cellHitPoints == null)
            return;

        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                solidCells[row, column] = true;
                cellHitPoints[row, column] = cellMaxHitPoints;
            }
        }
    }

    private List<Vector2> CarveRock(Vector2 worldPoint, Vector2 pushDirection, float blastRadiusScale)
    {
        using (ShootTheRockPerformance.CarveRockMarker.Auto())
        {
            List<Vector2> removedPixelCenters = new List<Vector2>();
            Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
            Vector2 baseDirection = pushDirection.sqrMagnitude > 0.0001f
                ? pushDirection.normalized
                : Vector2.right;
            Vector2 perpendicular = new Vector2(-baseDirection.y, baseDirection.x);
            Vector2 coreCenter = ResolveImpactCenterLocal(localPoint, baseDirection);
            float clampedBlastScale = Mathf.Max(0.25f, blastRadiusScale);
            float offsetScale = Mathf.Lerp(1f, 1.45f, Mathf.InverseLerp(1f, 2.75f, clampedBlastScale));
            float impactDamage = baseImpactDamage * Mathf.Lerp(1f, 1.4f, Mathf.InverseLerp(1f, 2.75f, clampedBlastScale));

            ApplyEllipseDamage(
                coreCenter,
                Random.Range(CoreStampRadiusColumnsMin, CoreStampRadiusColumnsMax) * clampedBlastScale,
                Random.Range(CoreStampRadiusRowsMin, CoreStampRadiusRowsMax) * clampedBlastScale,
                0.9f,
                impactDamage,
                removedPixelCenters,
                allowEdgeNoise: true);

            int extraSatelliteCount = Mathf.Max(0, Mathf.FloorToInt((clampedBlastScale - 1f) * 2f));
            int satelliteCount = Random.Range(SatelliteStampCountMin, SatelliteStampCountMax + 1 + extraSatelliteCount);
            for (int i = 0; i < satelliteCount; i++)
            {
                Vector2 offset = (baseDirection * Random.Range(0.04f, 0.32f) * columnWidth * 1.2f * offsetScale)
                    + (perpendicular * Random.Range(-0.8f, 0.8f) * rowHeight * offsetScale);

                ApplyEllipseDamage(
                    coreCenter + offset,
                    Random.Range(SatelliteStampRadiusColumnsMin, SatelliteStampRadiusColumnsMax) * clampedBlastScale,
                    Random.Range(SatelliteStampRadiusRowsMin, SatelliteStampRadiusRowsMax) * clampedBlastScale,
                    Random.Range(0.9f, 1.0f),
                    impactDamage * 0.82f,
                    removedPixelCenters,
                    allowEdgeNoise: true);
            }

            int extraScatterCount = Mathf.Max(0, Mathf.CeilToInt((clampedBlastScale - 1f) * 3f));
            int scatterCount = Random.Range(ScatterPixelCountMin, ScatterPixelCountMax + 1 + extraScatterCount);
            for (int i = 0; i < scatterCount; i++)
            {
                Vector2 scatterOffset = (baseDirection * Random.Range(0.02f, 0.35f) * columnWidth * 1.2f * offsetScale)
                    + (perpendicular * Random.Range(-0.9f, 0.9f) * rowHeight * offsetScale);
                Vector2 scatterPoint = coreCenter + scatterOffset;
                int row = GetRowIndexFromLocalY(scatterPoint.y);
                int column = GetColumnIndexFromLocalX(scatterPoint.x);
                ApplyPointDamage(row, column, impactDamage * 0.72f, removedPixelCenters);
            }

            int minimumCellsRemoved = Mathf.Max(MinimumCellsRemovedPerHit, Mathf.RoundToInt(MinimumCellsRemovedPerHit * clampedBlastScale));
            if (removedPixelCenters.Count < minimumCellsRemoved)
                EnsureMinimumImpact(coreCenter, removedPixelCenters, clampedBlastScale, minimumCellsRemoved, impactDamage);

            return removedPixelCenters;
        }
    }

    private void EnsureMinimumImpact(Vector2 centerLocal, List<Vector2> removedPixelCenters, float blastRadiusScale, int minimumCellsRemoved, float impactDamage)
    {
        if (removedPixelCenters == null || removedPixelCenters.Count >= minimumCellsRemoved)
            return;

        int centerRow = GetRowIndexFromLocalY(centerLocal.y);
        int centerColumn = GetColumnIndexFromLocalX(centerLocal.x);
        if (!TryFindNearestSolidCell(centerRow, centerColumn, 6, out int foundRow, out int foundColumn))
            return;

        float fallbackScale = Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(1f, 2.75f, blastRadiusScale));
        ApplyEllipseDamage(
            GetCellCenterLocal(foundRow, foundColumn),
            1.15f * fallbackScale,
            1.25f * fallbackScale,
            1.04f,
            impactDamage,
            removedPixelCenters,
            allowEdgeNoise: false);
    }

    private bool TryFindNearestSolidCell(int centerRow, int centerColumn, int maxRadius, out int foundRow, out int foundColumn)
    {
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int row = Mathf.Max(0, centerRow - radius); row <= Mathf.Min(rowCount - 1, centerRow + radius); row++)
            {
                for (int column = Mathf.Max(0, centerColumn - radius); column <= Mathf.Min(columnCount - minimumColumnsRemaining - 1, centerColumn + radius); column++)
                {
                    if (!solidCells[row, column])
                        continue;

                    foundRow = row;
                    foundColumn = column;
                    return true;
                }
            }
        }

        foundRow = -1;
        foundColumn = -1;
        return false;
    }

    private void ApplyEllipseDamage(Vector2 centerLocal, float radiusColumns, float radiusRows, float threshold, float damageAmount, List<Vector2> removedPixelCenters, bool allowEdgeNoise)
    {
        using (ShootTheRockPerformance.ApplyEllipseDamageMarker.Auto())
        {
            int centerRow = GetRowIndexFromLocalY(centerLocal.y);
            int centerColumn = GetColumnIndexFromLocalX(centerLocal.x);
            int rowRadiusCeil = Mathf.CeilToInt(radiusRows) + 1;
            int columnRadiusCeil = Mathf.CeilToInt(radiusColumns) + 1;

            for (int row = Mathf.Max(0, centerRow - rowRadiusCeil); row <= Mathf.Min(rowCount - 1, centerRow + rowRadiusCeil); row++)
            {
                for (int column = Mathf.Max(0, centerColumn - columnRadiusCeil); column <= Mathf.Min(columnCount - minimumColumnsRemaining - 1, centerColumn + columnRadiusCeil); column++)
                {
                    if (!solidCells[row, column])
                        continue;

                    Vector2 cellCenter = GetCellCenterLocal(row, column);
                    float normalizedX = (cellCenter.x - centerLocal.x) / (columnWidth * radiusColumns);
                    float normalizedY = (cellCenter.y - centerLocal.y) / (rowHeight * radiusRows);
                    float ellipseDistance = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                    if (ellipseDistance > 1f)
                        continue;

                    float noisyThreshold = threshold;
                    if (allowEdgeNoise)
                        noisyThreshold += Random.Range(-0.14f, 0.12f);
                    if (ellipseDistance > noisyThreshold)
                        continue;

                    float distanceFactor = Mathf.Lerp(centerDamageMultiplier, edgeDamageMultiplier, Mathf.Clamp01(ellipseDistance));
                    ApplyPointDamage(row, column, damageAmount * distanceFactor, removedPixelCenters);
                }
            }
        }
    }

    private void ApplyPointDamage(int row, int column, float damageAmount, List<Vector2> removedPixelCenters)
    {
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount - minimumColumnsRemaining)
            return;
        if (!solidCells[row, column])
            return;

        int previousDamageTier = GetDamageVisualTier(row, column);
        float updatedHitPoints = Mathf.Max(0f, cellHitPoints[row, column] - damageAmount);
        bool cellDestroyed = updatedHitPoints <= 0f;

        cellHitPoints[row, column] = updatedHitPoints;

        if (!cellDestroyed)
        {
            int updatedDamageTier = GetDamageVisualTier(row, column);
            if (updatedDamageTier != previousDamageTier)
            {
                ShootTheRockPerformance.RecordDamageTierChange();
                MarkChunkCellDirty(row, column);
            }
            return;
        }

        solidCells[row, column] = false;
        cellHitPoints[row, column] = 0f;
        MarkChunkCellDirty(row, column);
        MarkColliderChunkDirty(row, column);
        removedPixelCenters.Add(transform.TransformPoint(GetCellCenterLocal(row, column)));
    }

    private void RemoveDisconnectedIslands(List<Vector2> removedPixelCenters)
    {
        using (ShootTheRockPerformance.RemoveDisconnectedIslandsMarker.Auto())
        {
            EnsureIslandCleanupBuffer();
            System.Array.Clear(islandConnectedBuffer, 0, islandConnectedBuffer.Length);
            islandCleanupQueue.Clear();

            int scanCount = 0;
            int removedCountBeforeIslandCleanup = removedPixelCenters.Count;

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = columnCount - minimumColumnsRemaining; column < columnCount; column++)
                {
                    scanCount++;
                    if (!solidCells[row, column] || islandConnectedBuffer[row, column])
                        continue;

                    islandConnectedBuffer[row, column] = true;
                    islandCleanupQueue.Enqueue(new Vector2Int(row, column));
                }
            }

            while (islandCleanupQueue.Count > 0)
            {
                Vector2Int current = islandCleanupQueue.Dequeue();
                TryVisitConnectedCell(current.x - 1, current.y, islandConnectedBuffer, islandCleanupQueue);
                TryVisitConnectedCell(current.x + 1, current.y, islandConnectedBuffer, islandCleanupQueue);
                TryVisitConnectedCell(current.x, current.y - 1, islandConnectedBuffer, islandCleanupQueue);
                TryVisitConnectedCell(current.x, current.y + 1, islandConnectedBuffer, islandCleanupQueue);
            }

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < columnCount; column++)
                {
                    scanCount++;
                    if (!solidCells[row, column] || islandConnectedBuffer[row, column])
                        continue;

                    solidCells[row, column] = false;
                    cellHitPoints[row, column] = 0f;
                    MarkChunkCellDirty(row, column);
                    MarkColliderChunkDirty(row, column);
                    removedPixelCenters.Add(transform.TransformPoint(GetCellCenterLocal(row, column)));
                }
            }

            ShootTheRockPerformance.RecordIslandScanCells(scanCount);
            ShootTheRockPerformance.RecordIslandRemovedCells(removedPixelCenters.Count - removedCountBeforeIslandCleanup);
        }
    }
    private void TryVisitConnectedCell(int row, int column, bool[,] connected, Queue<Vector2Int> queue)
    {
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            return;
        if (!solidCells[row, column] || connected[row, column])
            return;

        connected[row, column] = true;
        queue.Enqueue(new Vector2Int(row, column));
    }

    private int GetRowIndexFromLocalY(float localY)
    {
        float normalized = Mathf.InverseLerp(worldHeight * 0.5f, -worldHeight * 0.5f, localY);
        return Mathf.Clamp(Mathf.FloorToInt(normalized * rowCount), 0, rowCount - 1);
    }

    private int GetColumnIndexFromLocalX(float localX)
    {
        float normalized = Mathf.InverseLerp(-worldWidth * 0.5f, worldWidth * 0.5f, localX);
        return Mathf.Clamp(Mathf.FloorToInt(normalized * columnCount), 0, columnCount - 1);
    }

    private Vector2 GetCellCenterLocal(int rowIndex, int columnIndex)
    {
        float x = (-worldWidth * 0.5f) + ((columnIndex + 0.5f) * columnWidth);
        float topY = (worldHeight * 0.5f) - (rowIndex * rowHeight);
        float y = topY - (rowHeight * 0.5f);
        return new Vector2(x, y);
    }

    private Vector2 ResolveImpactCenterLocal(Vector2 localPoint, Vector2 baseDirection)
    {
        if (TryFindSurfaceImpactCell(localPoint, baseDirection, out int surfaceRow, out int surfaceColumn))
            return GetCellCenterLocal(surfaceRow, surfaceColumn) - (baseDirection * (columnWidth * SurfaceImpactOutsideBiasColumns));

        return localPoint - (baseDirection * (columnWidth * SurfaceImpactOutsideBiasColumns));
    }

    private bool TryFindSurfaceImpactCell(Vector2 localPoint, Vector2 baseDirection, out int foundRow, out int foundColumn)
    {
        float step = Mathf.Max(0.0001f, Mathf.Min(columnWidth, rowHeight) * 0.35f);
        float backtrackDistance = Mathf.Max(columnWidth, rowHeight) * 4.5f;
        float forwardDistance = Mathf.Max(columnWidth, rowHeight) * 1.6f;
        Vector2 start = localPoint - (baseDirection * backtrackDistance);
        float totalDistance = backtrackDistance + forwardDistance;
        int steps = Mathf.CeilToInt(totalDistance / step);
        int previousRow = int.MinValue;
        int previousColumn = int.MinValue;

        for (int i = 0; i <= steps; i++)
        {
            Vector2 sample = start + (baseDirection * (i * step));
            if (!TryGetCellIndexFromLocal(sample, out int row, out int column))
                continue;
            if (row == previousRow && column == previousColumn)
                continue;

            previousRow = row;
            previousColumn = column;

            if (!solidCells[row, column])
                continue;

            foundRow = row;
            foundColumn = column;
            return true;
        }

        foundRow = -1;
        foundColumn = -1;
        return false;
    }

    private bool TryGetCellIndexFromLocal(Vector2 localPoint, out int row, out int column)
    {
        float normalizedX = (localPoint.x + (worldWidth * 0.5f)) / worldWidth;
        float normalizedY = ((worldHeight * 0.5f) - localPoint.y) / worldHeight;
        if (normalizedX < 0f || normalizedX >= 1f || normalizedY < 0f || normalizedY >= 1f)
        {
            row = -1;
            column = -1;
            return false;
        }

        row = Mathf.Clamp(Mathf.FloorToInt(normalizedY * rowCount), 0, rowCount - 1);
        column = Mathf.Clamp(Mathf.FloorToInt(normalizedX * columnCount), 0, columnCount - 1);
        return true;
    }

    private int GetDamageVisualTier(int row, int column)
    {
        if (cellHitPoints == null)
            return 0;

        float ratio = Mathf.Clamp01(cellHitPoints[row, column] / Mathf.Max(0.0001f, cellMaxHitPoints));
        if (ratio > 0.75f)
            return 0;
        if (ratio > 0.5f)
            return 1;
        if (ratio > 0.25f)
            return 2;
        return 3;
    }

    private bool ShouldUseChunkedRuntime()
    {
        return useChunkedRuntime && Application.isPlaying;
    }

    private void RebuildRockShape()
    {
        using (ShootTheRockPerformance.RebuildRockShapeMarker.Auto())
        {
            if (ShouldUseChunkedRuntime())
            {
                if (runtimeGrid == null)
                    runtimeGrid = new RockWallRuntimeGrid();

                runtimeGrid.Initialize(transform, chunkSizeInCells);
                GetActiveFrameCellBounds(out int activeMinRow, out int activeMaxRow, out int activeMinColumn, out int activeMaxColumn);

                bool colliderDue = runtimeChunkFullRebuildRequired || (hasPendingColliderDirtyRegion && Time.time >= nextColliderRebuildTime);
                bool hasAnyDirtyWork = runtimeChunkFullRebuildRequired || hasDirtyChunkRegion || colliderDue;
                if (!hasAnyDirtyWork)
                    return;

                if (runtimeChunkFullRebuildRequired)
                {
                    runtimeGrid.RebuildAll(
                        solidCells,
                        cellHitPoints,
                        cellMaxHitPoints,
                        rowCount,
                        columnCount,
                        worldWidth,
                        worldHeight,
                        rowHeight,
                        columnWidth,
                        activeMinRow,
                        activeMaxRow,
                        activeMinColumn,
                        activeMaxColumn,
                        DamageTierColors,
                        rebuildCollider: true);
                }
                else
                {
                    GetCombinedDirtyChunkRegion(colliderDue, out int rebuildMinRow, out int rebuildMaxRow, out int rebuildMinColumn, out int rebuildMaxColumn);
                    runtimeGrid.RebuildDirty(
                        solidCells,
                        cellHitPoints,
                        cellMaxHitPoints,
                        rowCount,
                        columnCount,
                        worldWidth,
                        worldHeight,
                        rowHeight,
                        columnWidth,
                        rebuildMinRow,
                        rebuildMaxRow,
                        rebuildMinColumn,
                        rebuildMaxColumn,
                        activeMinRow,
                        activeMaxRow,
                        activeMinColumn,
                        activeMaxColumn,
                        DamageTierColors,
                        rebuildCollider: colliderDue);
                }

                runtimeChunkFullRebuildRequired = false;
                ClearDirtyChunkRegion();
                if (colliderDue)
                    ClearColliderDirtyChunkRegion();

                if (rockMesh != null)
                    rockMesh.Clear();
                if (polygonCollider != null)
                    polygonCollider.pathCount = 0;
                if (meshRenderer != null)
                    meshRenderer.enabled = false;
                if (polygonCollider != null)
                    polygonCollider.enabled = false;
                return;
            }

            RebuildLegacyRockShape();
        }
    }

    private void RebuildLegacyRockShape()
    {
        if (rockMesh == null)
            return;

        if (runtimeGrid != null)
            runtimeGrid.SetVisible(false);
        if (meshRenderer != null)
            meshRenderer.enabled = true;
        if (polygonCollider != null)
            polygonCollider.enabled = true;

        int minRow = 0;
        int maxRow = Mathf.Max(0, rowCount - 1);
        int minColumn = 0;
        int maxColumn = Mathf.Max(0, columnCount - 1);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int>[] trianglesByTier = new List<int>[DamageVisualTierCount];
        for (int i = 0; i < DamageVisualTierCount; i++)
            trianglesByTier[i] = new List<int>();

        for (int row = minRow; row <= maxRow; row++)
        {
            float topY = (worldHeight * 0.5f) - (row * rowHeight);
            float bottomY = topY - rowHeight;

            for (int column = minColumn; column <= maxColumn; column++)
            {
                if (!solidCells[row, column])
                    continue;

                float leftX = (-worldWidth * 0.5f) + (column * columnWidth);
                float rightX = leftX + columnWidth;
                int vertexIndex = vertices.Count;
                int damageTier = GetDamageVisualTier(row, column);

                vertices.Add(new Vector3(leftX, topY, 0f));
                vertices.Add(new Vector3(rightX, topY, 0f));
                vertices.Add(new Vector3(leftX, bottomY, 0f));
                vertices.Add(new Vector3(rightX, bottomY, 0f));

                uv.Add(new Vector2(0f, 1f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));

                trianglesByTier[damageTier].Add(vertexIndex);
                trianglesByTier[damageTier].Add(vertexIndex + 1);
                trianglesByTier[damageTier].Add(vertexIndex + 2);
                trianglesByTier[damageTier].Add(vertexIndex + 2);
                trianglesByTier[damageTier].Add(vertexIndex + 1);
                trianglesByTier[damageTier].Add(vertexIndex + 3);
            }
        }

        rockMesh.Clear();
        rockMesh.subMeshCount = DamageVisualTierCount;
        rockMesh.SetVertices(vertices);
        rockMesh.SetUVs(0, uv);
        for (int i = 0; i < DamageVisualTierCount; i++)
            rockMesh.SetTriangles(trianglesByTier[i], i, false);
        rockMesh.RecalculateBounds();
        rockMesh.RecalculateNormals();

        RebuildColliderPaths(minRow, maxRow, minColumn, maxColumn);
    }

    private void RebuildColliderPaths(int minRow, int maxRow, int minColumn, int maxColumn)
    {
        using (ShootTheRockPerformance.ChunkColliderMarker.Auto())
        {
            Dictionary<Vector2Int, Vector2Int> nextEdgeByStart = BuildBoundaryEdges(minRow, maxRow, minColumn, maxColumn);
            List<List<Vector2>> paths = new List<List<Vector2>>();

            while (nextEdgeByStart.Count > 0)
            {
                Vector2Int start = default;
                foreach (KeyValuePair<Vector2Int, Vector2Int> pair in nextEdgeByStart)
                {
                    start = pair.Key;
                    break;
                }

                Vector2Int current = start;
                List<Vector2> path = new List<Vector2>();
                int guard = 0;

                while (nextEdgeByStart.TryGetValue(current, out Vector2Int next) && guard < 20000)
                {
                    path.Add(GridCornerToLocal(current));
                    nextEdgeByStart.Remove(current);
                    current = next;
                    guard++;
                    if (current == start)
                        break;
                }

                if (path.Count >= 3)
                    paths.Add(path);
            }

            polygonCollider.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                polygonCollider.SetPath(i, paths[i].ToArray());

            ShootTheRockPerformance.RecordColliderRebuild(paths.Count);
        }
    }

    private Dictionary<Vector2Int, Vector2Int> BuildBoundaryEdges(int minRow, int maxRow, int minColumn, int maxColumn)
    {
        Dictionary<Vector2Int, Vector2Int> edges = new Dictionary<Vector2Int, Vector2Int>();

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                if (!solidCells[row, column])
                    continue;

                if (!IsSolidInRenderRange(row - 1, column, minRow, maxRow, minColumn, maxColumn))
                    edges[new Vector2Int(column, row)] = new Vector2Int(column + 1, row);
                if (!IsSolidInRenderRange(row, column + 1, minRow, maxRow, minColumn, maxColumn))
                    edges[new Vector2Int(column + 1, row)] = new Vector2Int(column + 1, row + 1);
                if (!IsSolidInRenderRange(row + 1, column, minRow, maxRow, minColumn, maxColumn))
                    edges[new Vector2Int(column + 1, row + 1)] = new Vector2Int(column, row + 1);
                if (!IsSolidInRenderRange(row, column - 1, minRow, maxRow, minColumn, maxColumn))
                    edges[new Vector2Int(column, row + 1)] = new Vector2Int(column, row);
            }
        }

        return edges;
    }

    private bool IsSolidInRenderRange(int row, int column, int minRow, int maxRow, int minColumn, int maxColumn)
    {
        if (row < minRow || row > maxRow || column < minColumn || column > maxColumn)
            return false;
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            return false;

        return solidCells[row, column];
    }

    private void MarkChunkCellDirty(int row, int column)
    {
        if (!ShouldUseChunkedRuntime())
            return;
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            return;

        if (!hasDirtyChunkRegion)
        {
            dirtyMinRow = dirtyMaxRow = row;
            dirtyMinColumn = dirtyMaxColumn = column;
            hasDirtyChunkRegion = true;
            return;
        }

        dirtyMinRow = Mathf.Min(dirtyMinRow, row);
        dirtyMaxRow = Mathf.Max(dirtyMaxRow, row);
        dirtyMinColumn = Mathf.Min(dirtyMinColumn, column);
        dirtyMaxColumn = Mathf.Max(dirtyMaxColumn, column);
    }

    private void MarkColliderChunkDirty(int row, int column)
    {
        if (!ShouldUseChunkedRuntime())
            return;
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            return;

        if (!hasPendingColliderDirtyRegion)
        {
            colliderDirtyMinRow = colliderDirtyMaxRow = row;
            colliderDirtyMinColumn = colliderDirtyMaxColumn = column;
            hasPendingColliderDirtyRegion = true;
            nextColliderRebuildTime = Time.time + colliderRebuildInterval;
            return;
        }

        colliderDirtyMinRow = Mathf.Min(colliderDirtyMinRow, row);
        colliderDirtyMaxRow = Mathf.Max(colliderDirtyMaxRow, row);
        colliderDirtyMinColumn = Mathf.Min(colliderDirtyMinColumn, column);
        colliderDirtyMaxColumn = Mathf.Max(colliderDirtyMaxColumn, column);
    }

    private void ClearDirtyChunkRegion()
    {
        hasDirtyChunkRegion = false;
        dirtyMinRow = dirtyMaxRow = 0;
        dirtyMinColumn = dirtyMaxColumn = 0;
    }

    private void ClearColliderDirtyChunkRegion()
    {
        hasPendingColliderDirtyRegion = false;
        colliderDirtyMinRow = colliderDirtyMaxRow = 0;
        colliderDirtyMinColumn = colliderDirtyMaxColumn = 0;
        nextColliderRebuildTime = 0f;
    }

    private void EnsureIslandCleanupBuffer()
    {
        if (rowCount <= 0 || columnCount <= 0)
            return;

        if (islandConnectedBuffer != null && islandConnectedBuffer.GetLength(0) == rowCount && islandConnectedBuffer.GetLength(1) == columnCount)
            return;

        islandConnectedBuffer = new bool[rowCount, columnCount];
    }

    private void QueueIslandCleanup()
    {
        if (!Application.isPlaying)
            return;

        hasPendingIslandCleanup = true;
        if (nextIslandCleanupTime <= 0f)
            nextIslandCleanupTime = Time.time + islandCleanupInterval;
    }

    private void ClearPendingIslandCleanup()
    {
        hasPendingIslandCleanup = false;
        nextIslandCleanupTime = 0f;
        islandCleanupRemovedPixelCenters.Clear();
        islandCleanupQueue.Clear();
    }

    private void ProcessPendingIslandCleanup()
    {
        if (!hasPendingIslandCleanup)
            return;

        hasPendingIslandCleanup = false;
        nextIslandCleanupTime = Time.time + islandCleanupInterval;
        islandCleanupRemovedPixelCenters.Clear();
        RemoveDisconnectedIslands(islandCleanupRemovedPixelCenters);

        if (islandCleanupRemovedPixelCenters.Count <= 0)
            return;

        ShootTheRockPerformance.RecordCellsDestroyed(islandCleanupRemovedPixelCenters.Count);
        RebuildRockShape();
        islandCleanupRemovedPixelCenters.Clear();
    }

    private void GetCombinedDirtyChunkRegion(bool includeColliderRegion, out int minRow, out int maxRow, out int minColumn, out int maxColumn)
    {
        bool hasRegion = false;
        minRow = maxRow = minColumn = maxColumn = 0;

        if (hasDirtyChunkRegion)
        {
            minRow = dirtyMinRow;
            maxRow = dirtyMaxRow;
            minColumn = dirtyMinColumn;
            maxColumn = dirtyMaxColumn;
            hasRegion = true;
        }

        if (includeColliderRegion && hasPendingColliderDirtyRegion)
        {
            if (!hasRegion)
            {
                minRow = colliderDirtyMinRow;
                maxRow = colliderDirtyMaxRow;
                minColumn = colliderDirtyMinColumn;
                maxColumn = colliderDirtyMaxColumn;
                hasRegion = true;
            }
            else
            {
                minRow = Mathf.Min(minRow, colliderDirtyMinRow);
                maxRow = Mathf.Max(maxRow, colliderDirtyMaxRow);
                minColumn = Mathf.Min(minColumn, colliderDirtyMinColumn);
                maxColumn = Mathf.Max(maxColumn, colliderDirtyMaxColumn);
            }
        }
    }

    public void ReleaseChipParticle(ChipParticle chipParticle)
    {
        if (chipParticle == null)
            return;
        if (particlesRoot != null)
            chipParticle.transform.SetParent(particlesRoot, false);
        chipParticle.gameObject.SetActive(false);
        chipParticlePool.Enqueue(chipParticle);
    }

    private void PrewarmChipParticlePool()
    {
        if (particlesRoot == null || chipParticlePool.Count > 0)
            return;

        for (int i = 0; i < PrewarmChipParticleCount; i++)
            chipParticlePool.Enqueue(CreateChipParticleInstance());
    }

    private ChipParticle AcquireChipParticle()
    {
        PrewarmChipParticlePool();

        while (chipParticlePool.Count > 0)
        {
            ChipParticle pooledChip = chipParticlePool.Dequeue();
            if (pooledChip != null)
                return pooledChip;
        }

        ShootTheRockPerformance.RecordChipPoolMiss();
        return CreateChipParticleInstance();
    }

    private ChipParticle CreateChipParticleInstance()
    {
        GameObject particle = ShootTheRockPrototypeBootstrap.CreateSpriteObject(
            "ChipParticle",
            particlesRoot,
            Vector3.zero,
            Vector2.one,
            Color.white,
            30);

        Rigidbody2D body = particle.AddComponent<Rigidbody2D>();
        body.gravityScale = 1.15f;
        body.linearDamping = 0.12f;
        body.angularDamping = 0.08f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.mass = 0.03f;
        body.simulated = false;

        SpriteRenderer renderer = particle.GetComponent<SpriteRenderer>();
        ChipParticle chip = particle.AddComponent<ChipParticle>();
        chip.ConfigurePool(this, body, renderer);
        particle.SetActive(false);
        return chip;
    }

    private Vector2 GridCornerToLocal(Vector2Int gridCorner)
    {
        float x = (-worldWidth * 0.5f) + (gridCorner.x * columnWidth);
        float y = (worldHeight * 0.5f) - (gridCorner.y * rowHeight);
        return new Vector2(x, y);
    }

    private void SpawnHitParticles(List<Vector2> removedPixelCenters, Vector2 pushDirection)
    {
        if (removedPixelCenters == null || removedPixelCenters.Count == 0 || particlesRoot == null)
            return;

        Vector2 baseDirection = pushDirection.sqrMagnitude > 0.0001f ? (-pushDirection).normalized : Vector2.left;
        int particleCount = Mathf.Min(MaxChipParticlesPerHit, removedPixelCenters.Count);
        int stride = Mathf.Max(1, Mathf.CeilToInt(removedPixelCenters.Count / (float)particleCount));

        for (int i = 0, spawned = 0; i < removedPixelCenters.Count && spawned < particleCount; i += stride, spawned++)
        {
            Vector2 spawnPoint = removedPixelCenters[i] + (baseDirection * columnWidth * 0.45f);
            float pixelScale = Random.Range(0.82f, 0.98f);
            float pixelWidth = columnWidth * pixelScale;
            float pixelHeight = rowHeight * Random.Range(0.82f, 0.98f);

            Vector2 directionalImpulse = baseDirection * Random.Range(0.1f, 0.26f);
            Vector2 tangent = new Vector2(-baseDirection.y, baseDirection.x);
            Vector2 scatterImpulse = (tangent * Random.Range(-0.08f, 0.08f)) + (baseDirection * Random.Range(-0.015f, 0.05f));

            ChipParticle chip = AcquireChipParticle();
            chip.Launch(
                spawnPoint,
                new Vector2(pixelWidth, pixelHeight),
                Color.white,
                directionalImpulse + scatterImpulse,
                Random.Range(-0.02f, 0.02f),
                Random.Range(0.8f, 1.25f));
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos)
            return;

        float resolvedWidth = Mathf.Max(1f, worldWidth);
        float resolvedHeight = Mathf.Max(1f, worldHeight);
        float resolvedCellsPerUnit = Mathf.Max(1f, cellsPerUnit);
        int resolvedColumnCount = Mathf.Max(8, Mathf.RoundToInt(resolvedWidth * resolvedCellsPerUnit));
        int resolvedRowCount = Mathf.Max(8, Mathf.RoundToInt(resolvedHeight * resolvedCellsPerUnit));
        float resolvedColumnWidth = resolvedWidth / resolvedColumnCount;
        float resolvedRowHeight = resolvedHeight / resolvedRowCount;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        Rect fullWallRect = new Rect(-resolvedWidth * 0.5f, -resolvedHeight * 0.5f, resolvedWidth, resolvedHeight);
        DrawGizmoRect(fullWallRect, new Color(0.2f, 1f, 0.35f, 1f));

        if (TryGetActiveFrameLocalRect(out Rect activeFrameRect))
            DrawGizmoRect(activeFrameRect, new Color(1f, 0.82f, 0.15f, 1f));

        int protectedColumns = Mathf.Clamp(
            Mathf.RoundToInt(resolvedColumnCount * MinimumAnchorColumnPercent),
            MinimumAnchorColumnsAbsolute,
            Mathf.Max(MinimumAnchorColumnsAbsolute, resolvedColumnCount - 1));
        float protectedWidth = protectedColumns * resolvedColumnWidth;
        Rect anchorRect = new Rect((resolvedWidth * 0.5f) - protectedWidth, -resolvedHeight * 0.5f, protectedWidth, resolvedHeight);
        DrawGizmoRect(anchorRect, new Color(1f, 0.25f, 0.25f, 1f));

        if (drawChunkGizmos && chunkSizeInCells > 0)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.55f);
            for (int column = chunkSizeInCells; column < resolvedColumnCount; column += chunkSizeInCells)
            {
                float x = (-resolvedWidth * 0.5f) + (column * resolvedColumnWidth);
                Gizmos.DrawLine(new Vector3(x, -resolvedHeight * 0.5f, 0f), new Vector3(x, resolvedHeight * 0.5f, 0f));
            }

            for (int row = chunkSizeInCells; row < resolvedRowCount; row += chunkSizeInCells)
            {
                float y = (resolvedHeight * 0.5f) - (row * resolvedRowHeight);
                Gizmos.DrawLine(new Vector3(-resolvedWidth * 0.5f, y, 0f), new Vector3(resolvedWidth * 0.5f, y, 0f));
            }
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void DrawGizmoRect(Rect rect, Color color)
    {
        Gizmos.color = color;
        Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMin, 0f);
        Vector3 bottomRight = new Vector3(rect.xMax, rect.yMin, 0f);
        Vector3 topRight = new Vector3(rect.xMax, rect.yMax, 0f);
        Vector3 topLeft = new Vector3(rect.xMin, rect.yMax, 0f);
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }
}
