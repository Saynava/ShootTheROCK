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
    private const int StarterOrePlacementAttemptsPerBody = 48;
    private const int SilverBeltStartChunkRow = 3;
    private const int GoldPreviewStartChunkRow = 7;
    private const float GasPocketTriggerRadius = 0.72f;
    private const float GasPocketExplosionRadius = 2.35f;
    private const float GasPocketExplosionCenterDamage = 14f;
    private const float GasPocketExplosionOuterDamage = 6f;
    private const int GasPocketHullDamage = 999;
    private const float MinimumBaseDomeHalfWidth = 12.6f;
    private const float MinimumBaseDomeHeight = 10.05f;

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

    [Header("Base Dome")]
    [SerializeField] private bool carveBaseDome = true;
    [Min(0.5f)] [SerializeField] private float baseDomeHalfWidth = MinimumBaseDomeHalfWidth;
    [Min(0.5f)] [SerializeField] private float baseDomeHeight = MinimumBaseDomeHeight;

    [Header("Starter Ore Band")]
    [Min(1)] [SerializeField] private int starterBandChunkRows = 7;
    [Min(0f)] [SerializeField] private float copperValuePerCell = 1f;
    [Min(0f)] [SerializeField] private float tinValuePerCell = 2f;
    [Min(0f)] [SerializeField] private float silverValuePerCell = 3f;
    [Min(0f)] [SerializeField] private float goldValuePerCell = 6f;
    [Min(1)] [SerializeField] private int starterBandMaxOresPerChunk = 4;
    [Min(0)] [SerializeField] private int starterBandMaxCopperPerChunk = 2;
    [Min(0)] [SerializeField] private int starterBandMaxTinPerChunk = 3;
    [Min(0)] [SerializeField] private int starterBandMaxSilverPerChunk = 2;
    [Min(1)] [SerializeField] private int starterOreBodyMinCells = 20;
    [Min(1)] [SerializeField] private int starterOreBodyMaxCells = 30;
    [Min(0)] [SerializeField] private int starterOreBodyPaddingCells = 4;

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
    private readonly MotherloadRunState runState = new MotherloadRunState();
    private readonly MotherloadMetaProgressionState metaProgression = new MotherloadMetaProgressionState();
    private bool initialized;
    private int runSequence;
    private Vector3 cameraFollowVelocity;

    public float ChunkWorldWidth => cellsPerChunkX / Mathf.Max(1f, cellsPerUnit);
    public float ChunkWorldHeight => cellsPerChunkY / Mathf.Max(1f, cellsPerUnit);
    public float WorldStripWidth => ChunkWorldWidth * Mathf.Max(1, chunkColumns);
    public float StripCenterX => stripCenterX;
    public float SurfaceY => surfaceY;
    public bool StreamsUpward => verticalFlowDirection == MotherloadVerticalFlowDirection.Upward;
    public float TerrainStartY => surfaceY;
    public bool DebugLoggingEnabled => enableDebugLogging;
    public bool ShouldLogChunkLifecycle => enableDebugLogging && logChunkLifecycle;
    public bool ShouldLogChunkRebuilds => enableDebugLogging && logChunkRebuilds;
    public bool ShouldLogProjectileHits => enableDebugLogging && logProjectileHits;
    public MotherloadRunState RunState => runState;
    public MotherloadMetaProgressionState MetaProgression => metaProgression;

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
        HandlePlayerHazardContact();
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
        EnsureRunStateInitialized();
        EnsureRoots();
        EnsureSurfaceBand();
        EnsureBaseInfrastructure();
        ApplyMetaProgressionToPlayer();

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
        if (moneyHud != null)
            moneyHud.BindMotherloadWorld(this);

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

    public bool CanBuyUpgrade(MotherloadUpgradeType upgradeType)
    {
        if (moneyHud == null || !metaProgression.CanUpgrade(upgradeType))
            return false;

        return moneyHud.ProgressionState.Money >= metaProgression.GetNextUpgradeCost(upgradeType);
    }

    public bool TryBuyUpgrade(MotherloadUpgradeType upgradeType)
    {
        if (!CanBuyUpgrade(upgradeType))
            return false;

        int cost = metaProgression.GetNextUpgradeCost(upgradeType);
        if (!moneyHud.ProgressionState.TrySpendMoney(cost))
            return false;

        if (!metaProgression.TryUpgrade(upgradeType))
        {
            moneyHud.ProgressionState.AddMoney(cost);
            return false;
        }

        runState.RefreshDerivedStats(metaProgression);
        runState.RepairHull();
        ApplyMetaProgressionToPlayer();
        return true;
    }

    public int GetUpgradeRank(MotherloadUpgradeType upgradeType)
    {
        return metaProgression.GetUpgradeRank(upgradeType);
    }

    public int GetUpgradeCost(MotherloadUpgradeType upgradeType)
    {
        return metaProgression.GetNextUpgradeCost(upgradeType);
    }

    public string BuildMotherloadHudSummary()
    {
        EnsureRunStateInitialized();
        string deathText = string.IsNullOrWhiteSpace(runState.LastDeathReason) ? string.Empty : "\nLAST DEATH: " + runState.LastDeathReason;
        return "BASE SHOP\n"
            + "MONEY $" + (moneyHud != null ? moneyHud.ProgressionState.Money : 0)
            + "  |  CARGO " + runState.CargoUsed + "/" + runState.CargoCapacity
            + " ($" + runState.CargoValue + ")\n"
            + "HULL " + runState.CurrentHull + "/" + runState.MaxHull
            + "  |  DEPTH ROW " + runState.MaxReachedChunkRow
            + "  |  SEED " + runState.CurrentSeed + "\n"
            + "RELICS: " + metaProgression.BuildRelicSummary()
            + deathText;
    }

    public string BuildUpgradeButtonText(MotherloadUpgradeType upgradeType, string label)
    {
        int rank = metaProgression.GetUpgradeRank(upgradeType);
        if (!metaProgression.CanUpgrade(upgradeType))
            return label + " RANK " + rank + " MAX";

        return "BUY " + label + " R" + (rank + 1) + "  $" + metaProgression.GetNextUpgradeCost(upgradeType);
    }

    public void HandlePlayerDockedAtBase(CannonAim cannon)
    {
        EnsureRunStateInitialized();
        int soldValue = runState.SellCargo();
        if (soldValue > 0 && moneyHud != null)
            moneyHud.AddMoney(soldValue);

        runState.RepairHull();
        ApplyMetaProgressionToPlayer();
        if (cannon != null)
            cannon.ForceRefillFuel();
        if (moneyHud != null)
            moneyHud.Refresh();
    }

    public void ApplyPlayerHullDamage(int amount, string reason)
    {
        EnsureRunStateInitialized();
        if (!runState.ApplyHullDamage(amount, reason, metaProgression))
        {
            if (moneyHud != null)
                moneyHud.Refresh();
            return;
        }

        ResetRunAfterDeath(runState.LastDeathReason);
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

    private void EnsureRunStateInitialized()
    {
        if (runState.CurrentSeed != 0)
            return;

        runState.ResetForNewRun(worldSeed, metaProgression);
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
        if (TryTriggerHazardAtWorldPoint(worldPoint, GasPocketTriggerRadius, "Shot gas pocket"))
            return true;

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
            totalOreYield.Tin += runtimeOreYield.Tin;
            totalOreYield.Silver += runtimeOreYield.Silver;
            totalOreYield.Gold += runtimeOreYield.Gold;
            totalOreYield.Relic += runtimeOreYield.Relic;
            if (runtimeChanged)
            {
                changed = true;
                changedChunks++;
            }

            if (chunkHits != null)
                chunkHits.Add(runtime.Coordinate + ":" + (runtimeChanged ? "changed" : "no-change") + "/" + runtimeOreYield);
        }

        if (totalOreYield.TotalIncludingSpecial > 0)
            HandleOreYield(totalOreYield);

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

    private void HandleOreYield(MotherloadOreYield oreYield)
    {
        EnsureRunStateInitialized();
        if (oreYield.TotalCount > 0)
        {
            MotherloadOreYield accepted = runState.AddCargo(oreYield);
            if (ShouldLogProjectileHits)
                LogDebug("Cargo pickup | accepted=" + accepted + " | cargo=" + runState.CargoUsed + "/" + runState.CargoCapacity + " | value=$" + runState.CargoValue, this);
        }

        if (oreYield.Relic > 0)
            TryGrantRelicFromPocket();

        if (moneyHud != null)
            moneyHud.Refresh();
    }

    private void TryGrantRelicFromPocket()
    {
        if (runState.FoundRelicThisRun)
            return;

        MotherloadRelicType[] relicPool =
        {
            MotherloadRelicType.GyroBoots,
            MotherloadRelicType.MagnetCoil,
            MotherloadRelicType.SurveyPing,
            MotherloadRelicType.EmergencySpark,
            MotherloadRelicType.SoftLandingModule,
            MotherloadRelicType.RicochetPrimer,
            MotherloadRelicType.OreLens,
        };

        for (int attempt = 0; attempt < relicPool.Length; attempt++)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(HashToUnit(runState.CurrentSeed + attempt, runState.MaxReachedChunkRow, 1700 + attempt) * relicPool.Length), 0, relicPool.Length - 1);
            MotherloadRelicType relicType = relicPool[index];
            if (!metaProgression.TryAddRelic(relicType))
                continue;

            runState.MarkRelicFound();
            ApplyMetaProgressionToPlayer();
            LogDebug("Relic found: " + relicType, this);
            return;
        }
    }

    private void ResetRunAfterDeath(string reason)
    {
        LogWarning("Run reset | reason=" + reason, this);

        if (moneyHud != null)
            moneyHud.ShowGameOver(reason);

        if (moneyHud != null && moneyHud.ProgressionState.Money > 0)
            moneyHud.ProgressionState.AddMoney(-moneyHud.ProgressionState.Money);

        runSequence++;
        worldSeed = BuildNextRunSeed();
        runState.ResetForNewRun(worldSeed, metaProgression);
        runState.SetLastDeathReason(reason);
        RegenerateWorldCache();
        RespawnPlayerAtBase();
        ApplyMetaProgressionToPlayer();
        if (moneyHud != null)
            moneyHud.Refresh();
    }

    private int BuildNextRunSeed()
    {
        unchecked
        {
            int hash = worldSeed;
            hash = (hash * 397) ^ (runSequence + 1);
            hash = (hash * 397) ^ Mathf.FloorToInt(Time.realtimeSinceStartup * 1000f);
            return hash == 0 ? 1337 + runSequence : Mathf.Abs(hash);
        }
    }

    private void RespawnPlayerAtBase()
    {
        if (focusTarget == null)
            return;

        focusTarget.position = GetSuggestedPlayerSpawnWorldPosition();
        CannonAim cannon = focusTarget.GetComponent<CannonAim>();
        if (cannon != null)
        {
            cannon.ResetMotion();
            cannon.ForceRefillFuel();
        }

        Rigidbody2D body = focusTarget.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        SnapCameraToFocus();
    }

    private void ApplyMetaProgressionToPlayer()
    {
        if (focusTarget == null)
            return;

        CannonAim cannon = focusTarget.GetComponent<CannonAim>();
        if (cannon != null)
        {
            cannon.ApplyMotherloadFuelTankRank(metaProgression.GetUpgradeRank(MotherloadUpgradeType.FuelTank));
            cannon.SetMotherloadMovementMultiplier(metaProgression.HasRelic(MotherloadRelicType.GyroBoots) ? 1.08f : 1f);
        }

        AutoShooter shooter = focusTarget.GetComponent<AutoShooter>();
        if (shooter != null)
        {
            shooter.ApplyMotherloadUpgradeRanks(
                metaProgression.GetUpgradeRank(MotherloadUpgradeType.BlasterDamage),
                metaProgression.GetUpgradeRank(MotherloadUpgradeType.FireRate));
        }

        MotherloadPlayerVitals vitals = focusTarget.GetComponent<MotherloadPlayerVitals>();
        if (vitals != null)
            vitals.Initialize(this);
    }

    private bool TryTriggerHazardAtWorldPoint(Vector2 worldPoint, float radiusWorld, string reason)
    {
        Vector2 triggerPoint = default;
        bool foundGasPocket = false;

        foreach (MotherloadChunkRuntime runtime in activeChunks.Values)
        {
            if (runtime == null)
                continue;

            if (!runtime.TryFindHazardNearWorldPoint(worldPoint, radiusWorld, out Vector2 hazardWorldPoint, out MotherloadHazardType hazardType))
                continue;

            if (hazardType != MotherloadHazardType.GasPocket)
                continue;

            triggerPoint = hazardWorldPoint;
            foundGasPocket = true;
            break;
        }

        if (!foundGasPocket)
            return false;

        TriggerGasPocketExplosion(triggerPoint, reason);
        return true;
    }

    private void TriggerGasPocketExplosion(Vector2 hazardWorldPoint, string reason)
    {
        foreach (MotherloadChunkRuntime runtime in activeChunks.Values)
        {
            if (runtime != null)
                runtime.ClearHazardsNearWorldPoint(hazardWorldPoint, GasPocketExplosionRadius);
        }

        TryApplyExplosionAtWorldPoint(hazardWorldPoint, GasPocketExplosionRadius, GasPocketExplosionCenterDamage, GasPocketExplosionOuterDamage);
        if (focusTarget != null)
        {
            float distance = Vector2.Distance(focusTarget.position, hazardWorldPoint);
            if (distance <= GasPocketExplosionRadius + 0.7f)
                ApplyPlayerHullDamage(GasPocketHullDamage, "Killed by gas pocket");
        }

        LogWarning("Gas pocket detonated | reason=" + reason + " | point=" + hazardWorldPoint.ToString("F3"), this);
    }

    private void HandlePlayerHazardContact()
    {
        if (focusTarget == null || !runState.IsAlive)
            return;

        Vector2 playerPoint = focusTarget.position;
        Vector2 triggerPoint = default;
        bool foundGasPocket = false;

        foreach (MotherloadChunkRuntime runtime in activeChunks.Values)
        {
            if (runtime == null)
                continue;

            if (!runtime.TryFindHazardNearWorldPoint(playerPoint, 0.42f, out Vector2 hazardWorldPoint, out MotherloadHazardType hazardType))
                continue;

            if (hazardType == MotherloadHazardType.GasPocket)
            {
                triggerPoint = hazardWorldPoint;
                foundGasPocket = true;
                break;
            }
        }

        if (foundGasPocket)
            TriggerGasPocketExplosion(triggerPoint, "Player touched gas pocket");
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
        baseBuildingCollider.isTrigger = true;
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
        baseZoneTransform.position = new Vector3(stripCenterX, surfaceY + (zoneHeight * 0.5f), 0f);
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
        runState.RecordReachedChunkRow(focusRow);
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

        GetCameraHalfExtents(out float halfWidth, out float halfHeight);
        float targetX = BuildCameraTargetX(focusTarget.position.x, halfWidth);
        float targetY = BuildCameraTargetY(focusTarget.position.y, halfHeight);
        float targetZ = sceneCamera.transform.position.z <= -1f ? sceneCamera.transform.position.z : -10f;
        targetPosition = new Vector3(targetX, targetY, targetZ);
        return true;
    }

    private void GetCameraHalfExtents(out float halfWidth, out float halfHeight)
    {
        if (sceneCamera == null)
        {
            halfHeight = 1f;
            halfWidth = 1f;
            return;
        }

        if (sceneCamera.orthographic)
        {
            halfHeight = Mathf.Max(0.01f, sceneCamera.orthographicSize);
            halfWidth = halfHeight * Mathf.Max(0.01f, sceneCamera.aspect);
            return;
        }

        float cameraDistance = focusTarget != null ? Mathf.Abs(sceneCamera.transform.position.z - focusTarget.position.z) : Mathf.Abs(sceneCamera.transform.position.z);
        halfHeight = Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Mathf.Max(0.01f, cameraDistance);
        halfWidth = halfHeight * Mathf.Max(0.01f, sceneCamera.aspect);
    }

    private float BuildCameraTargetX(float focusX, float halfWidth)
    {
        float stripHalfWidth = WorldStripWidth * 0.5f;
        float stripLeft = stripCenterX - stripHalfWidth;
        float stripRight = stripCenterX + stripHalfWidth;

        if (lockCameraXToStrip || halfWidth >= stripHalfWidth)
            return stripCenterX;

        return Mathf.Clamp(focusX + cameraFollowOffset.x, stripLeft + halfWidth, stripRight - halfWidth);
    }

    private float BuildCameraTargetY(float focusY, float halfHeight)
    {
        float centeredY = focusY;
        if (StreamsUpward)
        {
            float surfaceAtBottomY = TerrainStartY + halfHeight;
            return Mathf.Max(surfaceAtBottomY, centeredY);
        }

        float surfaceAtTopY = TerrainStartY - halfHeight;
        return Mathf.Min(surfaceAtTopY, centeredY);
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

                if (IsInsideBaseDome(worldX, worldY))
                {
                    chunkData.SetMaterial(row, column, MotherloadCellMaterial.Empty);
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
        StampGasPockets(chunkData);
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
        int chunkRow = ResolveChunkRowFromWorldY(worldY);
        return chunkRow < SilverBeltStartChunkRow ? MotherloadCellMaterial.Dirt : MotherloadCellMaterial.Stone;
    }

    private bool IsInsideBaseDome(float worldX, float worldY)
    {
        if (!StreamsUpward || !carveBaseDome || worldY < surfaceY)
            return false;

        float resolvedHalfWidth = Mathf.Max(MinimumBaseDomeHalfWidth, baseDomeHalfWidth);
        float resolvedHeight = Mathf.Max(MinimumBaseDomeHeight, baseDomeHeight);
        float normalizedXAbs = Mathf.Abs((worldX - stripCenterX) / Mathf.Max(0.01f, resolvedHalfWidth));
        if (normalizedXAbs > 1f)
            return false;

        float domeTopY = surfaceY + (Mathf.Sqrt(Mathf.Max(0f, 1f - (normalizedXAbs * normalizedXAbs))) * resolvedHeight);
        return worldY <= domeTopY;
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
        if (chunkData == null || chunkData.Coordinate.Row < 0 || chunkData.Coordinate.Row >= GoldPreviewStartChunkRow + 4)
            return;

        int targetOreCount = 1 + Mathf.FloorToInt(HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, 401) * Mathf.Clamp(starterBandMaxOresPerChunk, 1, 4));
        if (!TryBuildStarterOrePlan(chunkData.Coordinate, targetOreCount, out int copperCount, out int tinCount, out int silverCount))
            return;

        List<RectInt> reservations = new List<RectInt>(targetOreCount);
        StampStarterOreBodies(chunkData, MotherloadCellMaterial.Copper, copperCount, 500, reservations);
        StampStarterOreBodies(chunkData, MotherloadCellMaterial.Tin, tinCount, 700, reservations);
        StampStarterOreBodies(chunkData, MotherloadCellMaterial.Silver, silverCount, 900, reservations);
        StampDepthBandSpecials(chunkData, reservations);

        if (reservations.Count == 0)
            TryStampStarterOreBody(chunkData, MotherloadCellMaterial.Copper, 1200, reservations);
    }

    private bool TryBuildStarterOrePlan(MotherloadChunkCoordinate coordinate, int targetOreCount, out int copperCount, out int tinCount, out int silverCount)
    {
        copperCount = 0;
        tinCount = 0;
        silverCount = 0;

        targetOreCount = Mathf.Clamp(targetOreCount, 1, Mathf.Clamp(starterBandMaxOresPerChunk, 1, 4));
        for (int oreIndex = 0; oreIndex < targetOreCount; oreIndex++)
        {
            List<MotherloadCellMaterial> weightedOptions = new List<MotherloadCellMaterial>(7);
            bool silverBelt = coordinate.Row >= SilverBeltStartChunkRow;
            for (int i = copperCount; i < starterBandMaxCopperPerChunk; i++)
                weightedOptions.Add(MotherloadCellMaterial.Copper);
            for (int i = tinCount; i < starterBandMaxTinPerChunk; i++)
                weightedOptions.Add(MotherloadCellMaterial.Tin);
            for (int i = silverCount; i < starterBandMaxSilverPerChunk; i++)
                weightedOptions.Add(MotherloadCellMaterial.Silver);
            if (silverBelt && silverCount < starterBandMaxSilverPerChunk)
                weightedOptions.Add(MotherloadCellMaterial.Silver);

            if (weightedOptions.Count == 0)
                break;

            int pickIndex = Mathf.Clamp(
                Mathf.FloorToInt(HashToUnit(coordinate.Column, coordinate.Row, 430 + (oreIndex * 17)) * weightedOptions.Count),
                0,
                weightedOptions.Count - 1);

            switch (weightedOptions[pickIndex])
            {
                case MotherloadCellMaterial.Copper:
                    copperCount++;
                    break;
                case MotherloadCellMaterial.Tin:
                    tinCount++;
                    break;
                case MotherloadCellMaterial.Silver:
                    silverCount++;
                    break;
            }
        }

        return (copperCount + tinCount + silverCount) > 0;
    }

    private void StampStarterOreBodies(MotherloadChunkData chunkData, MotherloadCellMaterial oreMaterial, int oreCount, int saltBase, List<RectInt> reservations)
    {
        for (int oreIndex = 0; oreIndex < oreCount; oreIndex++)
            TryStampStarterOreBody(chunkData, oreMaterial, saltBase + (oreIndex * 997), reservations);
    }

    private bool TryStampStarterOreBody(MotherloadChunkData chunkData, MotherloadCellMaterial oreMaterial, int saltBase, List<RectInt> reservations)
    {
        int minCells = Mathf.Max(1, starterOreBodyMinCells);
        int maxCells = Mathf.Max(minCells, starterOreBodyMaxCells);

        for (int attempt = 0; attempt < StarterOrePlacementAttemptsPerBody; attempt++)
        {
            int attemptSalt = saltBase + (attempt * 101);
            int targetCells = Mathf.RoundToInt(Mathf.Lerp(minCells, maxCells, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, attemptSalt + 1)));
            float radiusX = Mathf.Lerp(2.35f, 3.4f, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, attemptSalt + 3));
            float radiusY = Mathf.Clamp(targetCells / (Mathf.PI * Mathf.Max(1.6f, radiusX)), 2.25f, 3.8f);
            int radiusXCells = Mathf.CeilToInt(radiusX);
            int radiusYCells = Mathf.CeilToInt(radiusY);
            int padding = Mathf.Max(1, starterOreBodyPaddingCells);

            int minCenterColumn = edgeBedrockThicknessCells + padding + radiusXCells + 1;
            int maxCenterColumn = chunkData.Width - edgeBedrockThicknessCells - padding - radiusXCells - 2;
            int minCenterRow = padding + radiusYCells + 1;
            int maxCenterRow = chunkData.Height - padding - radiusYCells - 2;
            if (maxCenterColumn < minCenterColumn || maxCenterRow < minCenterRow)
                return false;

            int centerColumn = Mathf.RoundToInt(Mathf.Lerp(minCenterColumn, maxCenterColumn, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, attemptSalt + 5)));
            int centerRow = Mathf.RoundToInt(Mathf.Lerp(minCenterRow, maxCenterRow, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, attemptSalt + 7)));

            RectInt candidateBounds = new RectInt(
                centerColumn - radiusXCells - 1,
                centerRow - radiusYCells - 1,
                (radiusXCells * 2) + 3,
                (radiusYCells * 2) + 3);

            if (DoesOreReservationOverlap(candidateBounds, reservations))
                continue;

            List<Vector2Int> candidateCells = new List<Vector2Int>(maxCells + 8);
            for (int row = centerRow - radiusYCells - 1; row <= centerRow + radiusYCells + 1; row++)
            {
                for (int column = centerColumn - radiusXCells - 1; column <= centerColumn + radiusXCells + 1; column++)
                {
                    if (row < 0 || row >= chunkData.Height || column < 0 || column >= chunkData.Width)
                        continue;

                    if (!CanStampOreIntoMaterial(chunkData.GetMaterial(row, column)))
                        continue;

                    float normalizedX = ((column + 0.5f) - (centerColumn + 0.5f)) / radiusX;
                    float normalizedY = ((row + 0.5f) - (centerRow + 0.5f)) / radiusY;
                    float ellipseDistance = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                    float shapeThreshold = 1f + Mathf.Lerp(-0.16f, 0.18f, HashToUnit(chunkData.Coordinate.Column + column, chunkData.Coordinate.Row + row, attemptSalt + 17));
                    if (ellipseDistance > shapeThreshold)
                        continue;

                    candidateCells.Add(new Vector2Int(column, row));
                }
            }

            if (candidateCells.Count < minCells)
                continue;

            candidateCells.Sort((a, b) =>
            {
                float aDx = (a.x + 0.5f) - (centerColumn + 0.5f);
                float aDy = (a.y + 0.5f) - (centerRow + 0.5f);
                float bDx = (b.x + 0.5f) - (centerColumn + 0.5f);
                float bDy = (b.y + 0.5f) - (centerRow + 0.5f);
                float aScore = (aDx * aDx) + (aDy * aDy);
                float bScore = (bDx * bDx) + (bDy * bDy);
                return aScore.CompareTo(bScore);
            });

            int cellsToPaint = Mathf.Clamp(targetCells, minCells, Mathf.Min(maxCells, candidateCells.Count));
            int minPaintedColumn = int.MaxValue;
            int maxPaintedColumn = int.MinValue;
            int minPaintedRow = int.MaxValue;
            int maxPaintedRow = int.MinValue;

            for (int i = 0; i < cellsToPaint; i++)
            {
                Vector2Int cell = candidateCells[i];
                chunkData.SetMaterial(cell.y, cell.x, oreMaterial);
                minPaintedColumn = Mathf.Min(minPaintedColumn, cell.x);
                maxPaintedColumn = Mathf.Max(maxPaintedColumn, cell.x);
                minPaintedRow = Mathf.Min(minPaintedRow, cell.y);
                maxPaintedRow = Mathf.Max(maxPaintedRow, cell.y);
            }

            reservations.Add(new RectInt(
                minPaintedColumn - padding,
                minPaintedRow - padding,
                (maxPaintedColumn - minPaintedColumn) + (padding * 2) + 1,
                (maxPaintedRow - minPaintedRow) + (padding * 2) + 1));
            return true;
        }

        return false;
    }

    private static bool DoesOreReservationOverlap(RectInt candidateBounds, List<RectInt> reservations)
    {
        for (int i = 0; i < reservations.Count; i++)
        {
            if (candidateBounds.Overlaps(reservations[i]))
                return true;
        }

        return false;
    }

    private static bool CanStampOreIntoMaterial(MotherloadCellMaterial material)
    {
        return material == MotherloadCellMaterial.Dirt || material == MotherloadCellMaterial.Stone;
    }

    private void StampDepthBandSpecials(MotherloadChunkData chunkData, List<RectInt> reservations)
    {
        if (chunkData.Coordinate.Row >= SilverBeltStartChunkRow)
        {
            float relicRoll = HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, 1301);
            if (relicRoll < 0.12f)
                TryStampStarterOreBody(chunkData, MotherloadCellMaterial.Relic, 1310, reservations);
        }

        if (chunkData.Coordinate.Row >= GoldPreviewStartChunkRow)
        {
            float goldRoll = HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, 1401);
            if (goldRoll < 0.35f)
                TryStampStarterOreBody(chunkData, MotherloadCellMaterial.Gold, 1420, reservations);
        }
    }

    private void StampGasPockets(MotherloadChunkData chunkData)
    {
        if (chunkData == null || chunkData.Coordinate.Row < 0)
            return;

        int pocketCount = HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, 1500) < 0.42f ? 1 : 0;
        if (chunkData.Coordinate.Row >= SilverBeltStartChunkRow && HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, 1501) < 0.3f)
            pocketCount++;

        for (int pocketIndex = 0; pocketIndex < pocketCount; pocketIndex++)
            StampGasPocket(chunkData, 1510 + (pocketIndex * 61));
    }

    private void StampGasPocket(MotherloadChunkData chunkData, int saltBase)
    {
        float radiusX = Mathf.Lerp(2.1f, 3.1f, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 1));
        float radiusY = Mathf.Lerp(2.1f, 3.3f, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 2));
        int radiusXCells = Mathf.CeilToInt(radiusX);
        int radiusYCells = Mathf.CeilToInt(radiusY);
        int padding = Mathf.Max(4, starterOreBodyPaddingCells);
        int minCenterColumn = edgeBedrockThicknessCells + padding + radiusXCells + 1;
        int maxCenterColumn = chunkData.Width - edgeBedrockThicknessCells - padding - radiusXCells - 2;
        int minCenterRow = padding + radiusYCells + 1;
        int maxCenterRow = chunkData.Height - padding - radiusYCells - 2;
        if (maxCenterColumn < minCenterColumn || maxCenterRow < minCenterRow)
            return;

        int centerColumn = Mathf.RoundToInt(Mathf.Lerp(minCenterColumn, maxCenterColumn, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 3)));
        int centerRow = Mathf.RoundToInt(Mathf.Lerp(minCenterRow, maxCenterRow, HashToUnit(chunkData.Coordinate.Column, chunkData.Coordinate.Row, saltBase + 4)));

        for (int row = centerRow - radiusYCells - 1; row <= centerRow + radiusYCells + 1; row++)
        {
            for (int column = centerColumn - radiusXCells - 1; column <= centerColumn + radiusXCells + 1; column++)
            {
                if (row < 0 || row >= chunkData.Height || column < 0 || column >= chunkData.Width)
                    continue;

                MotherloadCellMaterial material = chunkData.GetMaterial(row, column);
                if (material == MotherloadCellMaterial.Empty || material == MotherloadCellMaterial.Bedrock)
                    continue;

                float normalizedX = ((column + 0.5f) - (centerColumn + 0.5f)) / radiusX;
                float normalizedY = ((row + 0.5f) - (centerRow + 0.5f)) / radiusY;
                if ((normalizedX * normalizedX) + (normalizedY * normalizedY) > 1f)
                    continue;

                chunkData.SetHazard(row, column, MotherloadHazardType.GasPocket);
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
            uint hash = (uint)worldSeed
                + ((uint)x * 0x9E3779B9u)
                + ((uint)y * 0x85EBCA6Bu)
                + ((uint)salt * 0xC2B2AE35u);

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash >> 8) / 16777216f;
        }
    }

    private void AwardOreMoney(MotherloadOreYield oreYield)
    {
        if (oreYield.TotalCount <= 0 || moneyHud == null)
            return;

        float payoutFloat =
            (oreYield.Copper * Mathf.Max(0f, copperValuePerCell)) +
            (oreYield.Tin * Mathf.Max(0f, tinValuePerCell)) +
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
        starterBandChunkRows = Mathf.Max(1, starterBandChunkRows);
        starterBandMaxCopperPerChunk = Mathf.Max(0, starterBandMaxCopperPerChunk);
        starterBandMaxTinPerChunk = Mathf.Max(0, starterBandMaxTinPerChunk);
        starterBandMaxSilverPerChunk = Mathf.Max(0, starterBandMaxSilverPerChunk);
        int maxStarterOreCapacity = Mathf.Max(1, starterBandMaxCopperPerChunk + starterBandMaxTinPerChunk + starterBandMaxSilverPerChunk);
        starterBandMaxOresPerChunk = Mathf.Clamp(starterBandMaxOresPerChunk, 1, Mathf.Min(4, maxStarterOreCapacity));
        starterOreBodyMinCells = Mathf.Max(1, starterOreBodyMinCells);
        starterOreBodyMaxCells = Mathf.Max(starterOreBodyMinCells, starterOreBodyMaxCells);
        starterOreBodyPaddingCells = Mathf.Max(0, starterOreBodyPaddingCells);
        starterShaftHalfWidth = Mathf.Max(0.25f, starterShaftHalfWidth);
        starterShaftDepth = Mathf.Max(0.5f, starterShaftDepth);
        edgeBedrockThicknessCells = Mathf.Max(1, edgeBedrockThicknessCells);
        baseDomeHalfWidth = Mathf.Max(MinimumBaseDomeHalfWidth, baseDomeHalfWidth);
        baseDomeHeight = Mathf.Max(MinimumBaseDomeHeight, baseDomeHeight);
        projectileBlastRadius = Mathf.Max(0.1f, projectileBlastRadius);
        projectileCenterDamage = Mathf.Max(0.05f, projectileCenterDamage);
        projectileOuterDamage = Mathf.Max(0.01f, projectileOuterDamage);
        debugDigRadius = Mathf.Max(0.1f, debugDigRadius);
        debugDigDamageMultiplier = Mathf.Max(0.1f, debugDigDamageMultiplier);
        cameraFollowSmoothTime = Mathf.Max(0.01f, cameraFollowSmoothTime);
    }
}
