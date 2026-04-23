using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MotherloadVerticalFlowDirection
{
    Downward = 0,
    Upward = 1,
}

[DisallowMultipleComponent]
public class MotherloadWorldController : MonoBehaviour
{
    private const string ChunksRootName = "MotherloadChunks";
    private const string SurfaceBandName = "MotherloadSurfaceBand";
    private const string BaseBuildingName = "MotherloadBaseBuilding";
    private const string BaseZoneName = "MotherloadBaseZone";
    private static readonly Vector2[] StarterCopperPocketLayout =
    {
        new Vector2(0.50f, 6.5f),
        new Vector2(0.50f, 11.5f),
        new Vector2(0.22f, 9.5f),
        new Vector2(0.22f, 15.5f),
        new Vector2(0.78f, 9.5f),
        new Vector2(0.78f, 15.5f),
    };

    private static readonly Vector2Int[] CopperVeinShape =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(0, -1),
    };

    [Header("References")]
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private MoneyHud moneyHud;
    [SerializeField] private Transform chunksRoot;
    [SerializeField] private Transform surfaceBand;
    [SerializeField] private Transform baseBuilding;
    [SerializeField] private MotherloadBaseZone baseZone;

    [Header("Strip Layout")]
    [SerializeField] private int worldSeed = 1337;
    [Min(1)] [SerializeField] private int chunkColumns = 7;
    [Min(16)] [SerializeField] private int cellsPerChunkX = 64;
    [Min(16)] [SerializeField] private int cellsPerChunkY = 64;
    [Min(1f)] [SerializeField] private float cellsPerUnit = 8f;
    [SerializeField] private float stripCenterX = 0f;
    [SerializeField] private float surfaceY = -1.5f;
    [SerializeField] private MotherloadVerticalFlowDirection verticalFlowDirection = MotherloadVerticalFlowDirection.Downward;
    [Min(1f)] [SerializeField] private float upwardTerrainStartOffset = 4.75f;
    [Min(0)] [SerializeField] private int activeRowsAboveFocus = 1;
    [Min(1)] [SerializeField] private int activeRowsBelowFocus = 5;

    [Header("Generation")]
    [SerializeField] private bool enableStarterShaft = false;
    [Min(0.25f)] [SerializeField] private float starterShaftHalfWidth = 1.45f;
    [Min(0.5f)] [SerializeField] private float starterShaftDepth = 6.5f;
    [SerializeField] private bool enableCaveGeneration = false;
    [Range(0.5f, 0.98f)] [SerializeField] private float caveThreshold = 0.84f;
    [Range(0f, 1f)] [SerializeField] private float caveDetailThreshold = 0.52f;
    [Min(1)] [SerializeField] private int edgeBedrockThicknessCells = 2;

    [Header("Layer Layout")]
    [Min(8f)] [SerializeField] private float dirtLayerDepth = 48f;

    [Header("Starting Dirt Ores")]
    [Min(0f)] [SerializeField] private float copperValuePerCell = 0.45f;
    [Min(0f)] [SerializeField] private float silverValuePerCell = 14f;
    [Min(0f)] [SerializeField] private float goldValuePerCell = 40f;
    [Min(0)] [SerializeField] private int copperClustersPerChunk = 1;
    [Min(0)] [SerializeField] private int silverClustersPerChunk = 1;
    [Range(0f, 1f)] [SerializeField] private float rareGoldChunkChance = 0.06f;
    [Min(1)] [SerializeField] private int copperClusterMinRadiusCells = 1;
    [Min(1)] [SerializeField] private int copperClusterMaxRadiusCells = 2;
    [Min(1)] [SerializeField] private int silverClusterMinRadiusCells = 1;
    [Min(1)] [SerializeField] private int silverClusterMaxRadiusCells = 1;
    [Min(1)] [SerializeField] private int goldClusterMinRadiusCells = 1;
    [Min(1)] [SerializeField] private int goldClusterMaxRadiusCells = 1;

    [Header("Starter Loop Economy")]
    [Min(1f)] [SerializeField] private float starterCopperPocketMaxDepth = 18f;
    [Min(1)] [SerializeField] private int starterCopperPocketSizeCells = 2;
    [Min(0f)] [SerializeField] private float deepCopperStartDepth = 22f;
    [Min(0f)] [SerializeField] private float silverStartDepth = 34f;
    [Min(0f)] [SerializeField] private float goldStartDepth = 46f;

    [Header("Destruction")]
    [Min(0.1f)] [SerializeField] private float projectileBlastRadius = 0.95f;
    [Min(0.05f)] [SerializeField] private float projectileCenterDamage = 1.9f;
    [Min(0.01f)] [SerializeField] private float projectileOuterDamage = 0.55f;

    [Header("Debug Dig")]
    [SerializeField] private bool enableDebugMouseDig = false;
    [Min(0.1f)] [SerializeField] private float debugDigRadius = 0.9f;
    [Min(0.1f)] [SerializeField] private float debugDigDamageMultiplier = 3.5f;

    [Header("Debug Logging")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private bool logChunkLifecycle = true;
    [SerializeField] private bool logChunkRebuilds = true;
    [SerializeField] private bool logProjectileHits = true;

    [Header("Camera Follow")]
    [SerializeField] private bool followFocusWithCamera = true;
    [SerializeField] private bool lockCameraXToStrip = false;
    [SerializeField] private Vector2 cameraFollowOffset = new Vector2(0f, -1.35f);
    [Min(0.01f)] [SerializeField] private float cameraFollowSmoothTime = 0.16f;

    private readonly Dictionary<MotherloadChunkCoordinate, MotherloadChunkData> chunkDataByCoordinate = new Dictionary<MotherloadChunkCoordinate, MotherloadChunkData>();
    private readonly Dictionary<MotherloadChunkCoordinate, MotherloadChunkRuntime> activeChunks = new Dictionary<MotherloadChunkCoordinate, MotherloadChunkRuntime>();
    private readonly List<MotherloadChunkCoordinate> releaseBuffer = new List<MotherloadChunkCoordinate>();
    private bool initialized;
    private Vector3 cameraFollowVelocity;

    public float ChunkWorldWidth => cellsPerChunkX / Mathf.Max(1f, cellsPerUnit);
    public float ChunkWorldHeight => cellsPerChunkY / Mathf.Max(1f, cellsPerUnit);
    public float WorldStripWidth => ChunkWorldWidth * Mathf.Max(1, chunkColumns);
    public float StripCenterX => stripCenterX;
    public float SurfaceY => surfaceY;
    public bool StreamsUpward => verticalFlowDirection == MotherloadVerticalFlowDirection.Upward;
    public float TerrainStartY => StreamsUpward ? surfaceY + upwardTerrainStartOffset : surfaceY;
    public bool DebugLoggingEnabled => enableDebugLogging;
    public bool ShouldLogChunkLifecycle => enableDebugLogging && logChunkLifecycle;
    public bool ShouldLogChunkRebuilds => enableDebugLogging && logChunkRebuilds;
    public bool ShouldLogProjectileHits => enableDebugLogging && logProjectileHits;

    private void Awake()
    {
        if (Application.isPlaying)
            InitializeRuntime();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            InitializeRuntime();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!initialized)
            InitializeRuntime();
        else
            EnsureReferences();

        SyncActiveChunks();
        HandleDebugDigInput();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        UpdateCameraFollow();
    }

    [ContextMenu("Regenerate Motherload World Cache")]
    public void RegenerateWorldCache()
    {
        LogDebug("RegenerateWorldCache requested, clearing cached chunk data and active runtime chunk objects.", this);
        chunkDataByCoordinate.Clear();

        foreach (MotherloadChunkRuntime runtime in activeChunks.Values)
        {
            if (runtime == null)
                continue;

            DestroyRuntimeObject(runtime.gameObject);
        }

        activeChunks.Clear();
        initialized = false;

        if (Application.isPlaying)
            InitializeRuntime();

        LogDebug("RegenerateWorldCache complete.", this);
    }

    [ContextMenu("Dump Motherload Active Chunk Debug")]
    public void DumpActiveChunkDebug()
    {
        LogDebug("Active chunk dump: " + BuildActiveChunkSummary(), this);
    }

    public void InitializeRuntime()
    {
        EnsureReferences();
        EnsureRoots();
        EnsureSurfaceBand();
        EnsureBaseInfrastructure();

        bool wasInitialized = initialized;
        if (!wasInitialized)
            ClearStaleRuntimeChunks();

        SyncActiveChunks();
        if (!wasInitialized)
            SnapCameraToFocus();
        initialized = true;

        if (!wasInitialized)
        {
            LogDebug(
                "InitializeRuntime complete"
                + " | seed=" + worldSeed
                + " | stripWidth=" + WorldStripWidth.ToString("F2")
                + " | chunkSize=" + cellsPerChunkX + "x" + cellsPerChunkY
                + " | cellsPerUnit=" + cellsPerUnit.ToString("F2")
                + " | direction=" + verticalFlowDirection
                + " | terrainStartY=" + TerrainStartY.ToString("F2")
                + " | active=" + activeChunks.Count
                + " | caveGen=" + enableCaveGeneration
                + " | debugMouseDig=" + enableDebugMouseDig,
                this);
        }
    }

    public void BuildEditorPreview()
    {
        EnsureReferences();
        EnsureRoots();
        ClearStaleRuntimeChunks();
        chunkDataByCoordinate.Clear();
        EnsureSurfaceBand();
        EnsureBaseInfrastructure();

        if (focusTarget == null && baseBuilding != null)
            focusTarget = baseBuilding;

        SyncActiveChunks();
        MarkEditorPreviewArtifacts();
        initialized = false;
    }

    public void SetSceneCamera(Camera targetCamera)
    {
        sceneCamera = targetCamera;
    }

    public void SetFocusTarget(Transform target)
    {
        focusTarget = target;
    }

    public void SetMoneyHud(MoneyHud targetMoneyHud)
    {
        moneyHud = targetMoneyHud;
        if (baseZone != null)
            baseZone.Initialize(moneyHud);

        if (moneyHud != null)
            moneyHud.SetUpgradeUiVisible(false);
    }

    public void SetLockCameraXToStrip(bool value)
    {
        lockCameraXToStrip = value;
    }

    public void ConfigureVerticalFlow(MotherloadVerticalFlowDirection direction, float upwardStartOffset)
    {
        verticalFlowDirection = direction;
        upwardTerrainStartOffset = Mathf.Max(1f, upwardStartOffset);
    }

    public Vector3 GetSuggestedPlayerSpawnWorldPosition()
    {
        const float defaultSpawnClearance = 0.95f;

        if (baseBuilding != null)
        {
            BoxCollider2D baseCollider = baseBuilding.GetComponent<BoxCollider2D>();
            if (baseCollider != null)
            {
                Bounds bounds = baseCollider.bounds;
                return new Vector3(bounds.center.x, bounds.max.y + defaultSpawnClearance, 0f);
            }
        }

        return new Vector3(stripCenterX, surfaceY + 2.4f, 0f);
    }

    public void SnapCameraToFocus()
    {
        if (!TryBuildCameraTargetPosition(out Vector3 targetPosition))
            return;

        sceneCamera.transform.position = targetPosition;
        cameraFollowVelocity = Vector3.zero;
    }

    public bool TryDigAtWorldPoint(Vector2 worldPoint, float radiusWorld)
    {
        float digDamage = Mathf.Max(projectileCenterDamage, projectileOuterDamage) * Mathf.Max(1f, debugDigDamageMultiplier) * 8f;
        return TryApplyExplosionAtWorldPoint(worldPoint, radiusWorld, digDamage, digDamage);
    }

    public bool TryApplyProjectileHit(Vector2 worldPoint, Vector2 impactDirection, float blastScale = 1f)
    {
        float resolvedBlastScale = Mathf.Max(0.25f, blastScale);
        float radiusWorld = projectileBlastRadius * resolvedBlastScale;
        float centerDamage = projectileCenterDamage * resolvedBlastScale;
        float outerDamage = projectileOuterDamage * resolvedBlastScale;

        if (ShouldLogProjectileHits)
        {
            LogDebug(
                "Projectile hit request"
                + " | point=" + worldPoint.ToString("F3")
                + " | dir=" + impactDirection.ToString("F3")
                + " | blastScale=" + resolvedBlastScale.ToString("F2")
                + " | radius=" + radiusWorld.ToString("F2")
                + " | damage=" + centerDamage.ToString("F2") + "/" + outerDamage.ToString("F2"),
                this);
        }

        return TryApplyExplosionAtWorldPoint(worldPoint, radiusWorld, centerDamage, outerDamage);
    }

    public bool TryApplyExplosionAtWorldPoint(Vector2 worldPoint, float radiusWorld, float centerDamage, float outerDamage)
    {
        bool changed = false;
        int overlappedChunks = 0;
        int changedChunks = 0;
        MotherloadOreYield totalOreYield = default;
        System.Collections.Generic.List<string> chunkHits = ShouldLogProjectileHits ? new System.Collections.Generic.List<string>() : null;

        foreach (MotherloadChunkRuntime runtime in activeChunks.Values)
        {
            if (runtime == null || !runtime.OverlapsCircle(worldPoint, radiusWorld))
                continue;

            overlappedChunks++;
            bool runtimeChanged = runtime.TryApplyBlast(worldPoint, radiusWorld, centerDamage, outerDamage, out MotherloadOreYield runtimeOreYield);
            totalOreYield.Copper += runtimeOreYield.Copper;
            totalOreYield.Silver += runtimeOreYield.Silver;
            totalOreYield.Gold += runtimeOreYield.Gold;
            if (runtimeChanged)
            {
                changed = true;
                changedChunks++;
            }

            if (chunkHits != null)
                chunkHits.Add(runtime.Coordinate + ":" + (runtimeChanged ? "changed" : "no-change") + "/" + runtimeOreYield);
        }

        if (totalOreYield.TotalCount > 0)
            AwardOreMoney(totalOreYield);

        if (ShouldLogProjectileHits)
        {
            string chunkSummary = chunkHits != null && chunkHits.Count > 0 ? string.Join(", ", chunkHits) : "none";
            LogDebug(
                "Explosion resolved"
                + " | point=" + worldPoint.ToString("F3")
                + " | radius=" + radiusWorld.ToString("F2")
                + " | overlapped=" + overlappedChunks
                + " | changed=" + changedChunks
                + " | ore=" + totalOreYield
                + " | chunks=" + chunkSummary,
                this);
        }

        return changed;
    }

    private void EnsureReferences()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        if (focusTarget == null)
        {
            CannonAim cannonAim = FindAnyObjectByType<CannonAim>();
            if (cannonAim != null)
                focusTarget = cannonAim.transform;
        }

        if (focusTarget == null && sceneCamera != null)
            focusTarget = sceneCamera.transform;
    }

    private void EnsureRoots()
    {
        if (chunksRoot == null)
        {
            Transform existing = transform.Find(ChunksRootName);
            if (existing != null)
                chunksRoot = existing;
        }

        if (chunksRoot == null)
        {
            GameObject rootObject = new GameObject(ChunksRootName);
            rootObject.transform.SetParent(transform, false);
            chunksRoot = rootObject.transform;
        }
    }

    private void EnsureSurfaceBand()
    {
        if (surfaceBand == null)
        {
            Transform existing = transform.Find(SurfaceBandName);
            if (existing != null)
                surfaceBand = existing;
        }

        if (surfaceBand == null)
        {
            GameObject surfaceObject = ShootTheRockPrototypeBootstrap.CreateSpriteObject(
                SurfaceBandName,
                transform,
                Vector3.zero,
                Vector2.one,
                new Color(0.2f, 0.12f, 0.05f, 1f),
                2);
            surfaceBand = surfaceObject.transform;
        }

        const float platformHeight = 0.36f;
        float surfaceWidth = StreamsUpward ? WorldStripWidth : ChunkWorldWidth;
        surfaceBand.position = new Vector3(stripCenterX, surfaceY - (platformHeight * 0.5f), 0f);
        surfaceBand.localScale = new Vector3(surfaceWidth, platformHeight, 1f);

        BoxCollider2D surfaceCollider = surfaceBand.GetComponent<BoxCollider2D>();
        if (surfaceCollider == null)
            surfaceCollider = surfaceBand.gameObject.AddComponent<BoxCollider2D>();
        surfaceCollider.isTrigger = false;
        surfaceCollider.size = Vector2.one;
    }

    private void EnsureBaseInfrastructure()
    {
        const float baseWidthFactor = 0.72f;
        const float baseHeight = 1.6f;
        const float zoneHeight = 2.4f;

        float platformWidth = ChunkWorldWidth;

        if (baseBuilding == null)
            baseBuilding = transform.Find(BaseBuildingName);

        if (baseBuilding == null)
        {
            GameObject buildingObject = ShootTheRockPrototypeBootstrap.CreateSpriteObject(
                BaseBuildingName,
                transform,
                Vector3.zero,
                Vector2.one,
                new Color(0.42f, 0.44f, 0.48f, 1f),
                4);
            baseBuilding = buildingObject.transform;
        }

        float buildingWidth = Mathf.Max(2.5f, platformWidth * baseWidthFactor);
        baseBuilding.position = new Vector3(stripCenterX, surfaceY + (baseHeight * 0.5f), 0f);
        baseBuilding.localScale = new Vector3(buildingWidth, baseHeight, 1f);
        BoxCollider2D baseBuildingCollider = baseBuilding.GetComponent<BoxCollider2D>();
        if (baseBuildingCollider == null)
            baseBuildingCollider = baseBuilding.gameObject.AddComponent<BoxCollider2D>();
        baseBuildingCollider.isTrigger = false;
        baseBuildingCollider.size = Vector2.one;

        if (baseZone == null)
            baseZone = transform.Find(BaseZoneName)?.GetComponent<MotherloadBaseZone>();

        if (baseZone == null)
        {
            GameObject zoneObject = new GameObject(BaseZoneName, typeof(BoxCollider2D), typeof(MotherloadBaseZone));
            zoneObject.transform.SetParent(transform, false);
            baseZone = zoneObject.GetComponent<MotherloadBaseZone>();
        }

        Transform baseZoneTransform = baseZone.transform;
        baseZoneTransform.position = new Vector3(stripCenterX, surfaceY + baseHeight + (zoneHeight * 0.35f), 0f);
        baseZoneTransform.localScale = Vector3.one;
        BoxCollider2D zoneCollider = baseZone.GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
        zoneCollider.size = new Vector2(Mathf.Max(buildingWidth + 1.2f, 3.25f), zoneHeight);
        baseZone.Initialize(moneyHud);
    }

    private void ClearStaleRuntimeChunks()
    {
        activeChunks.Clear();
        releaseBuffer.Clear();

        if (chunksRoot == null)
            return;

        for (int i = chunksRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = chunksRoot.GetChild(i);
            if (child == null)
                continue;

            DestroyRuntimeObject(child.gameObject);
        }

        LogDebug("Cleared stale Motherload chunk runtime children before fresh init.", chunksRoot);
    }

    private void SyncActiveChunks()
    {
        if (focusTarget == null)
            return;

        int focusRow = ResolveChunkRowFromWorldY(focusTarget.position.y);
        HashSet<MotherloadChunkCoordinate> desired = new HashSet<MotherloadChunkCoordinate>();

        int minRow = Mathf.Max(0, focusRow - activeRowsAboveFocus);
        int maxRow = Mathf.Max(minRow, focusRow + activeRowsBelowFocus);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = 0; column < Mathf.Max(1, chunkColumns); column++)
            {
                MotherloadChunkCoordinate coordinate = new MotherloadChunkCoordinate(column, row);
                desired.Add(coordinate);
                if (!activeChunks.ContainsKey(coordinate))
                    ActivateChunk(coordinate);
            }
        }

        releaseBuffer.Clear();
        foreach (KeyValuePair<MotherloadChunkCoordinate, MotherloadChunkRuntime> pair in activeChunks)
        {
            if (!desired.Contains(pair.Key))
                releaseBuffer.Add(pair.Key);
        }

        for (int i = 0; i < releaseBuffer.Count; i++)
            DeactivateChunk(releaseBuffer[i]);
    }

    private void ActivateChunk(MotherloadChunkCoordinate coordinate)
    {
        MotherloadChunkData chunkData = GetOrCreateChunkData(coordinate);
        GameObject chunkObject = new GameObject();
        chunkObject.transform.SetParent(chunksRoot, false);
        MotherloadChunkRuntime runtime = chunkObject.AddComponent<MotherloadChunkRuntime>();
        runtime.Initialize(this, chunkData, GetChunkBottomLeft(coordinate), cellsPerUnit, sortingOrder: 0);

        if (!Application.isPlaying)
            MarkEditorPreviewObject(chunkObject);

        activeChunks[coordinate] = runtime;

        if (ShouldLogChunkLifecycle)
            LogDebug("ActivateChunk " + coordinate + " | " + chunkData.BuildDebugSummary(), runtime);
    }

    private void DeactivateChunk(MotherloadChunkCoordinate coordinate)
    {
        if (!activeChunks.TryGetValue(coordinate, out MotherloadChunkRuntime runtime))
            return;

        activeChunks.Remove(coordinate);
        if (ShouldLogChunkLifecycle)
            LogDebug("DeactivateChunk " + coordinate, runtime);
        if (runtime != null)
            DestroyRuntimeObject(runtime.gameObject);
    }

    private void UpdateCameraFollow()
    {
        if (!followFocusWithCamera)
            return;

        if (!TryBuildCameraTargetPosition(out Vector3 targetPosition))
            return;

        sceneCamera.transform.position = Vector3.SmoothDamp(
            sceneCamera.transform.position,
            targetPosition,
            ref cameraFollowVelocity,
            cameraFollowSmoothTime);
    }

    private bool TryBuildCameraTargetPosition(out Vector3 targetPosition)
    {
        if (sceneCamera == null || focusTarget == null)
        {
            targetPosition = default;
            return false;
        }

        float targetX = lockCameraXToStrip ? stripCenterX + cameraFollowOffset.x : focusTarget.position.x + cameraFollowOffset.x;
        float targetY = focusTarget.position.y + cameraFollowOffset.y;
        float targetZ = sceneCamera.transform.position.z <= -1f ? sceneCamera.transform.position.z : -10f;
        targetPosition = new Vector3(targetX, targetY, targetZ);
        return true;
    }

    private MotherloadChunkData GetOrCreateChunkData(MotherloadChunkCoordinate coordinate)
    {
        if (chunkDataByCoordinate.TryGetValue(coordinate, out MotherloadChunkData existing))
            return existing;

        MotherloadChunkData created = new MotherloadChunkData(coordinate, cellsPerChunkX, cellsPerChunkY);
        PopulateChunkData(created);
        chunkDataByCoordinate[coordinate] = created;
        return created;
    }

    private void PopulateChunkData(MotherloadChunkData chunkData)
    {
        Vector2 bottomLeft = GetChunkBottomLeft(chunkData.Coordinate);
        float totalWidth = WorldStripWidth;
        float stripLeft = stripCenterX - (totalWidth * 0.5f);
        float stripRight = stripCenterX + (totalWidth * 0.5f);

        for (int row = 0; row < chunkData.Height; row++)
        {
            for (int column = 0; column < chunkData.Width; column++)
            {
                float worldX = bottomLeft.x + ((column + 0.5f) / cellsPerUnit);
                float worldY = bottomLeft.y + ((row + 0.5f) / cellsPerUnit);
                float depth = GetDepthAlongStream(worldY);

                if (!IsInsideTerrainBand(worldY))
                {
                    chunkData.SetMaterial(row, column, MotherloadCellMaterial.Empty);
                    continue;
                }

                if (enableStarterShaft && depth <= starterShaftDepth && Mathf.Abs(worldX - stripCenterX) <= starterShaftHalfWidth)
                {
                    chunkData.SetMaterial(row, column, MotherloadCellMaterial.Empty);
                    continue;
                }

                bool leftEdge = worldX <= stripLeft + (edgeBedrockThicknessCells / cellsPerUnit);
                bool rightEdge = worldX >= stripRight - (edgeBedrockThicknessCells / cellsPerUnit);
                if (leftEdge || rightEdge)
                {
                    chunkData.SetMaterial(row, column, MotherloadCellMaterial.Bedrock);
                    continue;
                }

                if (ShouldCarveCave(worldX, worldY, depth))
                {
                    chunkData.SetMaterial(row, column, MotherloadCellMaterial.Empty);
                    continue;
                }

                chunkData.SetMaterial(row, column, ResolveMaterialAt(worldX, worldY, depth));
            }
        }

        StampStartingDirtOres(chunkData, bottomLeft);
    }

    private bool ShouldCarveCave(float worldX, float worldY, float depth)
    {
        if (!enableCaveGeneration || depth < 8f)
            return false;

        float largeNoise = SampleNoise(worldX, worldY, 0.11f, 0.17f);
        float detailNoise = SampleNoise(worldX, worldY, 0.24f, 0.61f);
        float depthBias = Mathf.Clamp01((depth - 8f) / 48f) * 0.05f;
        return largeNoise >= caveThreshold - depthBias && detailNoise >= caveDetailThreshold;
    }

    private MotherloadCellMaterial ResolveMaterialAt(float worldX, float worldY, float depth)
    {
        return depth < dirtLayerDepth ? MotherloadCellMaterial.Dirt : MotherloadCellMaterial.Stone;
    }

    private bool IsInsideTerrainBand(float worldY)
    {
        return StreamsUpward ? worldY >= TerrainStartY : worldY <= TerrainStartY;
    }

    private float GetDepthAlongStream(float worldY)
    {
        return Mathf.Max(0f, StreamsUpward ? worldY - TerrainStartY : TerrainStartY - worldY);
    }

    private void StampStartingDirtOres(MotherloadChunkData chunkData, Vector2 bottomLeft)
    {
        float chunkStartDepth = GetDepthAlongStream(bottomLeft.y);
        float chunkEndDepth = GetDepthAlongStream(bottomLeft.y + ChunkWorldHeight);
        float chunkMinDepth = Mathf.Min(chunkStartDepth, chunkEndDepth);
        float chunkMaxDepth = Mathf.Max(chunkStartDepth, chunkEndDepth);
        if (chunkMinDepth >= dirtLayerDepth || chunkMaxDepth <= 2f)
            return;

        StampStarterCopperPockets(chunkData, bottomLeft, chunkMinDepth, chunkMaxDepth);
        StampOreClusters(chunkData, bottomLeft, MotherloadCellMaterial.Copper, Mathf.Max(0, copperClustersPerChunk), copperClusterMinRadiusCells, copperClusterMaxRadiusCells, deepCopperStartDepth, dirtLayerDepth - 2f, 101);
        StampOreClusters(chunkData, bottomLeft, MotherloadCellMaterial.Silver, Mathf.Max(0, silverClustersPerChunk), silverClusterMinRadiusCells, silverClusterMaxRadiusCells, silverStartDepth, dirtLayerDepth - 2f, 211);

        if (chunkMaxDepth >= goldStartDepth && HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, 307) <= rareGoldChunkChance)
            StampOreClusters(chunkData, bottomLeft, MotherloadCellMaterial.Gold, 1, goldClusterMinRadiusCells, goldClusterMaxRadiusCells, goldStartDepth, dirtLayerDepth - 2f, 307);
    }

    private void StampStarterCopperPockets(MotherloadChunkData chunkData, Vector2 bottomLeft, float chunkMinDepth, float chunkMaxDepth)
    {
        float starterMaxDepth = Mathf.Min(dirtLayerDepth - 2f, starterCopperPocketMaxDepth);
        if (chunkMinDepth > starterMaxDepth || chunkMaxDepth < 3f)
            return;

        for (int i = 0; i < StarterCopperPocketLayout.Length; i++)
        {
            Vector2 pocket = StarterCopperPocketLayout[i];
            float depth = pocket.y;
            if (depth < chunkMinDepth || depth > chunkMaxDepth || depth > starterMaxDepth)
                continue;

            float worldX = Mathf.Lerp(stripCenterX - (WorldStripWidth * 0.5f), stripCenterX + (WorldStripWidth * 0.5f), pocket.x);
            float worldY = StreamsUpward ? TerrainStartY + depth : TerrainStartY - depth;
            StampCopperVein(chunkData, bottomLeft, worldX, worldY, saltBase: 500 + i, allowSecondaryLobe: false);
        }
    }

    private void StampCopperVein(MotherloadChunkData chunkData, Vector2 bottomLeft, float worldX, float worldY, int saltBase, bool allowSecondaryLobe)
    {
        int centerColumn = Mathf.RoundToInt((worldX - bottomLeft.x) * cellsPerUnit);
        int centerRow = Mathf.RoundToInt((worldY - bottomLeft.y) * cellsPerUnit);
        int mirrorX = HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 1) >= 0.5f ? 1 : -1;
        int mirrorY = HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 2) >= 0.5f ? 1 : -1;

        StampCopperVeinCells(chunkData, centerColumn, centerRow, mirrorX, mirrorY);

        if (!allowSecondaryLobe)
            return;

        if (HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 3) < 0.58f)
            return;

        int secondaryColumn = centerColumn + (mirrorX * 2);
        int secondaryRow = centerRow + (mirrorY >= 0 ? 1 : -1);
        StampCopperVeinCells(chunkData, secondaryColumn, secondaryRow, mirrorX, mirrorY);
    }

    private void StampCopperVeinCells(MotherloadChunkData chunkData, int centerColumn, int centerRow, int mirrorX, int mirrorY)
    {
        for (int i = 0; i < CopperVeinShape.Length; i++)
        {
            Vector2Int offset = CopperVeinShape[i];
            int column = centerColumn + (offset.x * mirrorX);
            int row = centerRow + (offset.y * mirrorY);
            if (row < 0 || row >= chunkData.Height || column < 0 || column >= chunkData.Width)
                continue;

            if (chunkData.GetMaterial(row, column) != MotherloadCellMaterial.Dirt)
                continue;

            chunkData.SetMaterial(row, column, MotherloadCellMaterial.Copper);
        }
    }

    private void StampOreClusters(MotherloadChunkData chunkData, Vector2 bottomLeft, MotherloadCellMaterial oreMaterial, int clusterCount, int minRadiusCells, int maxRadiusCells, float minDepth, float maxDepth, int saltBase)
    {
        if (clusterCount <= 0 || maxDepth <= minDepth)
            return;

        int minRadius = Mathf.Max(1, minRadiusCells);
        int maxRadius = Mathf.Max(minRadius, maxRadiusCells);

        for (int clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
        {
            int radiusCells = Mathf.RoundToInt(Mathf.Lerp(minRadius, maxRadius, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + (clusterIndex * 13) + 1)));
            int padding = radiusCells + 2;
            int centerColumn = Mathf.RoundToInt(Mathf.Lerp(padding, chunkData.Width - 1 - padding, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + (clusterIndex * 17) + 5)));
            int centerRow = Mathf.RoundToInt(Mathf.Lerp(padding, chunkData.Height - 1 - padding, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + (clusterIndex * 19) + 9)));

            float centerWorldY = bottomLeft.y + (centerRow / cellsPerUnit);
            float centerDepth = GetDepthAlongStream(centerWorldY);
            if (centerDepth < minDepth || centerDepth > maxDepth)
                continue;

            if (oreMaterial == MotherloadCellMaterial.Copper)
            {
                float centerWorldX = bottomLeft.x + (centerColumn / cellsPerUnit);
                StampCopperVein(chunkData, bottomLeft, centerWorldX, centerWorldY, saltBase + (clusterIndex * 37), allowSecondaryLobe: true);
                continue;
            }

            for (int row = centerRow - radiusCells - 1; row <= centerRow + radiusCells + 1; row++)
            {
                for (int column = centerColumn - radiusCells - 1; column <= centerColumn + radiusCells + 1; column++)
                {
                    if (row < 0 || row >= chunkData.Height || column < 0 || column >= chunkData.Width)
                        continue;

                    if (chunkData.GetMaterial(row, column) != MotherloadCellMaterial.Dirt)
                        continue;

                    float worldY = bottomLeft.y + ((row + 0.5f) / cellsPerUnit);
                    float depth = GetDepthAlongStream(worldY);
                    if (depth < minDepth || depth > maxDepth)
                        continue;

                    float dx = (column + 0.5f) - (centerColumn + 0.5f);
                    float dy = (row + 0.5f) - (centerRow + 0.5f);
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float shapeNoise = Mathf.Lerp(-0.35f, 0.4f, HashToUnit(chunkData.Coordinate.Column + column, chunkData.Coordinate.Row + row, saltBase + (clusterIndex * 23) + 17));
                    float threshold = radiusCells - 0.15f + shapeNoise;
                    if (distance > threshold)
                        continue;

                    chunkData.SetMaterial(row, column, oreMaterial);
                }
            }
        }
    }

    private float SampleNoise(float worldX, float worldY, float frequency, float salt)
    {
        float x = (worldX * frequency) + (worldSeed * 0.0017f) + salt;
        float y = (worldY * frequency) + (worldSeed * 0.0023f) + (salt * 1.37f);
        return Mathf.PerlinNoise(x, y);
    }

    private float HashToUnit(int x, int y, int salt)
    {
        unchecked
        {
            int hash = worldSeed;
            hash = (hash * 397) ^ x;
            hash = (hash * 397) ^ y;
            hash = (hash * 397) ^ salt;
            hash ^= (hash << 13);
            hash ^= (hash >> 17);
            hash ^= (hash << 5);
            int positive = hash & 0x7fffffff;
            return positive / 2147483647f;
        }
    }

    private void AwardOreMoney(MotherloadOreYield oreYield)
    {
        if (oreYield.TotalCount <= 0 || moneyHud == null)
            return;

        float payoutFloat =
            (oreYield.Copper * Mathf.Max(0f, copperValuePerCell)) +
            (oreYield.Silver * Mathf.Max(0f, silverValuePerCell)) +
            (oreYield.Gold * Mathf.Max(0f, goldValuePerCell));
        int payout = Mathf.RoundToInt(payoutFloat);
        if (payout <= 0)
            return;

        moneyHud.AddMoney(payout);
        LogDebug("Ore payout | " + oreYield + " | money=$" + payout + " (raw=" + payoutFloat.ToString("0.00") + ")", this);
    }

    private int ResolveChunkRowFromWorldY(float worldY)
    {
        float depth = GetDepthAlongStream(worldY);
        return Mathf.Max(0, Mathf.FloorToInt(depth / ChunkWorldHeight));
    }

    private Vector2 GetChunkBottomLeft(MotherloadChunkCoordinate coordinate)
    {
        float totalWidth = WorldStripWidth;
        float stripLeft = stripCenterX - (totalWidth * 0.5f);
        float x = stripLeft + (coordinate.Column * ChunkWorldWidth);
        float y = StreamsUpward
            ? TerrainStartY + (coordinate.Row * ChunkWorldHeight)
            : TerrainStartY - ((coordinate.Row + 1) * ChunkWorldHeight);
        return new Vector2(x, y);
    }

    public bool IsSolidAt(MotherloadChunkCoordinate coordinate, int row, int column)
    {
        MotherloadChunkCoordinate resolvedCoordinate = coordinate;
        int resolvedRow = row;
        int resolvedColumn = column;

        while (resolvedColumn < 0)
        {
            resolvedCoordinate = new MotherloadChunkCoordinate(resolvedCoordinate.Column - 1, resolvedCoordinate.Row);
            resolvedColumn += cellsPerChunkX;
        }

        while (resolvedColumn >= cellsPerChunkX)
        {
            resolvedCoordinate = new MotherloadChunkCoordinate(resolvedCoordinate.Column + 1, resolvedCoordinate.Row);
            resolvedColumn -= cellsPerChunkX;
        }

        while (resolvedRow < 0)
        {
            resolvedCoordinate = new MotherloadChunkCoordinate(resolvedCoordinate.Column, resolvedCoordinate.Row - 1);
            resolvedRow += cellsPerChunkY;
        }

        while (resolvedRow >= cellsPerChunkY)
        {
            resolvedCoordinate = new MotherloadChunkCoordinate(resolvedCoordinate.Column, resolvedCoordinate.Row + 1);
            resolvedRow -= cellsPerChunkY;
        }

        if (resolvedCoordinate.Column < 0 || resolvedCoordinate.Column >= Mathf.Max(1, chunkColumns))
            return false;

        if (resolvedCoordinate.Row < 0)
            return false;

        MotherloadChunkData chunkData = GetOrCreateChunkData(resolvedCoordinate);
        return chunkData != null && chunkData.IsSolid(resolvedRow, resolvedColumn);
    }

    private void HandleDebugDigInput()
    {
        if (!enableDebugMouseDig || sceneCamera == null || Mouse.current == null || !Mouse.current.leftButton.isPressed)
            return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(sceneCamera.transform.position.z)));
        float debugCenterDamage = projectileCenterDamage * Mathf.Max(1f, debugDigDamageMultiplier);
        float debugOuterDamage = projectileOuterDamage * Mathf.Max(1f, debugDigDamageMultiplier);
        if (ShouldLogProjectileHits)
            LogDebug("Debug dig input | point=" + ((Vector2)world).ToString("F3") + " | radius=" + debugDigRadius.ToString("F2"), this);
        TryApplyExplosionAtWorldPoint((Vector2)world, debugDigRadius, debugCenterDamage, debugOuterDamage);
    }

    public void LogDebug(string message, Object context = null)
    {
        if (!enableDebugLogging)
            return;

        Debug.Log("[Motherload] " + message, context != null ? context : this);
    }

    public void LogWarning(string message, Object context = null)
    {
        if (!enableDebugLogging)
            return;

        Debug.LogWarning("[Motherload] " + message, context != null ? context : this);
    }

    private string BuildActiveChunkSummary()
    {
        if (activeChunks.Count == 0)
            return "no active chunks";

        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>(activeChunks.Count);
        foreach (KeyValuePair<MotherloadChunkCoordinate, MotherloadChunkRuntime> pair in activeChunks)
        {
            string summary = pair.Value != null ? pair.Value.BuildDebugSummary() : "runtime-null";
            parts.Add(pair.Key + "=" + summary);
        }

        return string.Join(" | ", parts);
    }

    private void MarkEditorPreviewArtifacts()
    {
        if (Application.isPlaying)
            return;

        if (chunksRoot != null)
            MarkEditorPreviewObject(chunksRoot.gameObject);
        if (surfaceBand != null)
            MarkEditorPreviewObject(surfaceBand.gameObject);
        if (baseBuilding != null)
            MarkEditorPreviewObject(baseBuilding.gameObject);
        if (baseZone != null)
            MarkEditorPreviewObject(baseZone.gameObject);
    }

    private static void MarkEditorPreviewObject(GameObject gameObject)
    {
        if (gameObject == null)
            return;

        gameObject.hideFlags = HideFlags.DontSaveInEditor;

        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
                components[i].hideFlags = HideFlags.DontSaveInEditor;
        }
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void OnValidate()
    {
        chunkColumns = Mathf.Max(1, chunkColumns);
        cellsPerChunkX = Mathf.Max(16, cellsPerChunkX);
        cellsPerChunkY = Mathf.Max(16, cellsPerChunkY);
        cellsPerUnit = Mathf.Max(1f, cellsPerUnit);
        activeRowsAboveFocus = Mathf.Max(0, activeRowsAboveFocus);
        activeRowsBelowFocus = Mathf.Max(1, activeRowsBelowFocus);
        upwardTerrainStartOffset = Mathf.Max(1f, upwardTerrainStartOffset);
        starterCopperPocketMaxDepth = Mathf.Max(1f, starterCopperPocketMaxDepth);
        starterCopperPocketSizeCells = Mathf.Max(1, starterCopperPocketSizeCells);
        deepCopperStartDepth = Mathf.Max(starterCopperPocketMaxDepth + 1f, deepCopperStartDepth);
        silverStartDepth = Mathf.Max(deepCopperStartDepth + 2f, silverStartDepth);
        goldStartDepth = Mathf.Max(silverStartDepth + 4f, goldStartDepth);
        starterShaftHalfWidth = Mathf.Max(0.25f, starterShaftHalfWidth);
        starterShaftDepth = Mathf.Max(0.5f, starterShaftDepth);
        edgeBedrockThicknessCells = Mathf.Max(1, edgeBedrockThicknessCells);
        projectileBlastRadius = Mathf.Max(0.1f, projectileBlastRadius);
        projectileCenterDamage = Mathf.Max(0.05f, projectileCenterDamage);
        projectileOuterDamage = Mathf.Max(0.01f, projectileOuterDamage);
        debugDigRadius = Mathf.Max(0.1f, debugDigRadius);
        debugDigDamageMultiplier = Mathf.Max(0.1f, debugDigDamageMultiplier);
        cameraFollowSmoothTime = Mathf.Max(0.01f, cameraFollowSmoothTime);
    }
}
