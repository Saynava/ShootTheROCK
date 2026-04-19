using Unity.Profiling;
using UnityEngine;

public static class ShootTheRockPerformance
{
    public readonly struct Snapshot
    {
        public readonly float fps;
        public readonly float frameMs;
        public readonly float shotsPerSecond;
        public readonly float pelletsPerSecond;
        public readonly float hitsPerSecond;
        public readonly float projectilePoolMissesPerSecond;
        public readonly float chipPoolMissesPerSecond;
        public readonly int activeProjectiles;
        public readonly int activeChipParticles;
        public readonly int cellsDestroyedLastFrame;
        public readonly int damageTierChangesLastFrame;
        public readonly int islandScanCellsLastFrame;
        public readonly int islandRemovedCellsLastFrame;
        public readonly int chunkBuildsLastFrame;
        public readonly int colliderRebuildsLastFrame;
        public readonly int colliderPathsLastFrame;
        public readonly int textureAppliesLastFrame;

        public Snapshot(
            float fps,
            float frameMs,
            float shotsPerSecond,
            float pelletsPerSecond,
            float hitsPerSecond,
            float projectilePoolMissesPerSecond,
            float chipPoolMissesPerSecond,
            int activeProjectiles,
            int activeChipParticles,
            int cellsDestroyedLastFrame,
            int damageTierChangesLastFrame,
            int islandScanCellsLastFrame,
            int islandRemovedCellsLastFrame,
            int chunkBuildsLastFrame,
            int colliderRebuildsLastFrame,
            int colliderPathsLastFrame,
            int textureAppliesLastFrame)
        {
            this.fps = fps;
            this.frameMs = frameMs;
            this.shotsPerSecond = shotsPerSecond;
            this.pelletsPerSecond = pelletsPerSecond;
            this.hitsPerSecond = hitsPerSecond;
            this.projectilePoolMissesPerSecond = projectilePoolMissesPerSecond;
            this.chipPoolMissesPerSecond = chipPoolMissesPerSecond;
            this.activeProjectiles = activeProjectiles;
            this.activeChipParticles = activeChipParticles;
            this.cellsDestroyedLastFrame = cellsDestroyedLastFrame;
            this.damageTierChangesLastFrame = damageTierChangesLastFrame;
            this.islandScanCellsLastFrame = islandScanCellsLastFrame;
            this.islandRemovedCellsLastFrame = islandRemovedCellsLastFrame;
            this.chunkBuildsLastFrame = chunkBuildsLastFrame;
            this.colliderRebuildsLastFrame = colliderRebuildsLastFrame;
            this.colliderPathsLastFrame = colliderPathsLastFrame;
            this.textureAppliesLastFrame = textureAppliesLastFrame;
        }
    }

    private const float SnapshotWindowSeconds = 0.5f;

    public static readonly ProfilerMarker ApplyHitMarker = new ProfilerMarker("ShootTheRock.ApplyHit");
    public static readonly ProfilerMarker CarveRockMarker = new ProfilerMarker("ShootTheRock.CarveRock");
    public static readonly ProfilerMarker ApplyEllipseDamageMarker = new ProfilerMarker("ShootTheRock.ApplyEllipseDamage");
    public static readonly ProfilerMarker RemoveDisconnectedIslandsMarker = new ProfilerMarker("ShootTheRock.RemoveDisconnectedIslands");
    public static readonly ProfilerMarker RebuildRockShapeMarker = new ProfilerMarker("ShootTheRock.RebuildRockShape");
    public static readonly ProfilerMarker RuntimeGridRebuildAllMarker = new ProfilerMarker("ShootTheRock.RuntimeGridRebuildAll");
    public static readonly ProfilerMarker RuntimeGridRebuildDirtyMarker = new ProfilerMarker("ShootTheRock.RuntimeGridRebuildDirty");
    public static readonly ProfilerMarker ChunkBuildMarker = new ProfilerMarker("ShootTheRock.ChunkBuild");
    public static readonly ProfilerMarker ChunkColliderMarker = new ProfilerMarker("ShootTheRock.ChunkColliderRebuild");
    public static readonly ProfilerMarker FireMarker = new ProfilerMarker("ShootTheRock.Fire");
    public static readonly ProfilerMarker ProjectileImpactMarker = new ProfilerMarker("ShootTheRock.ProjectileImpact");
    public static readonly ProfilerMarker ChipLaunchMarker = new ProfilerMarker("ShootTheRock.ChipLaunch");

    public static Snapshot Current => currentSnapshot;

    private static Snapshot currentSnapshot;

    private static float windowTime;
    private static int windowFrameCount;
    private static int windowShots;
    private static int windowPellets;
    private static int windowHits;
    private static int windowProjectilePoolMisses;
    private static int windowChipPoolMisses;

    private static int frameShots;
    private static int framePellets;
    private static int frameHits;
    private static int frameProjectilePoolMisses;
    private static int frameChipPoolMisses;
    private static int frameCellsDestroyed;
    private static int frameDamageTierChanges;
    private static int frameIslandScanCells;
    private static int frameIslandRemovedCells;
    private static int frameChunkBuilds;
    private static int frameColliderRebuilds;
    private static int frameColliderPaths;
    private static int frameTextureApplies;

    private static int activeProjectiles;
    private static int activeChipParticles;
    private static int lastClosedFrame = -1;

    public static void RecordShot()
    {
        frameShots++;
    }

    public static void RecordPellet()
    {
        framePellets++;
    }

    public static void RecordHit()
    {
        frameHits++;
    }

    public static void RecordProjectilePoolMiss()
    {
        frameProjectilePoolMisses++;
    }

    public static void RecordChipPoolMiss()
    {
        frameChipPoolMisses++;
    }

    public static void RecordProjectileActivated()
    {
        activeProjectiles++;
    }

    public static void RecordProjectileDeactivated()
    {
        activeProjectiles = Mathf.Max(0, activeProjectiles - 1);
    }

    public static void RecordChipActivated()
    {
        activeChipParticles++;
    }

    public static void RecordChipDeactivated()
    {
        activeChipParticles = Mathf.Max(0, activeChipParticles - 1);
    }

    public static void RecordCellsDestroyed(int count)
    {
        if (count > 0)
            frameCellsDestroyed += count;
    }

    public static void RecordDamageTierChange(int count = 1)
    {
        if (count > 0)
            frameDamageTierChanges += count;
    }

    public static void RecordIslandScanCells(int count)
    {
        if (count > 0)
            frameIslandScanCells += count;
    }

    public static void RecordIslandRemovedCells(int count)
    {
        if (count > 0)
            frameIslandRemovedCells += count;
    }

    public static void RecordChunkBuild(int count = 1)
    {
        if (count > 0)
            frameChunkBuilds += count;
    }

    public static void RecordColliderRebuild(int pathCount)
    {
        frameColliderRebuilds++;
        frameColliderPaths += Mathf.Max(0, pathCount);
    }

    public static void RecordTextureApply(int count = 1)
    {
        if (count > 0)
            frameTextureApplies += count;
    }

    public static void EndFrame(float unscaledDeltaTime)
    {
        int frame = Time.frameCount;
        if (lastClosedFrame == frame)
            return;

        lastClosedFrame = frame;
        float clampedDeltaTime = Mathf.Max(0.0001f, unscaledDeltaTime);
        windowTime += clampedDeltaTime;
        windowFrameCount++;
        windowShots += frameShots;
        windowPellets += framePellets;
        windowHits += frameHits;
        windowProjectilePoolMisses += frameProjectilePoolMisses;
        windowChipPoolMisses += frameChipPoolMisses;

        if (windowTime >= SnapshotWindowSeconds)
        {
            float divisor = Mathf.Max(0.0001f, windowTime);
            currentSnapshot = new Snapshot(
                windowFrameCount / divisor,
                (divisor / Mathf.Max(1, windowFrameCount)) * 1000f,
                windowShots / divisor,
                windowPellets / divisor,
                windowHits / divisor,
                windowProjectilePoolMisses / divisor,
                windowChipPoolMisses / divisor,
                activeProjectiles,
                activeChipParticles,
                frameCellsDestroyed,
                frameDamageTierChanges,
                frameIslandScanCells,
                frameIslandRemovedCells,
                frameChunkBuilds,
                frameColliderRebuilds,
                frameColliderPaths,
                frameTextureApplies);

            windowTime = 0f;
            windowFrameCount = 0;
            windowShots = 0;
            windowPellets = 0;
            windowHits = 0;
            windowProjectilePoolMisses = 0;
            windowChipPoolMisses = 0;
        }
        else
        {
            currentSnapshot = new Snapshot(
                currentSnapshot.fps,
                currentSnapshot.frameMs,
                currentSnapshot.shotsPerSecond,
                currentSnapshot.pelletsPerSecond,
                currentSnapshot.hitsPerSecond,
                currentSnapshot.projectilePoolMissesPerSecond,
                currentSnapshot.chipPoolMissesPerSecond,
                activeProjectiles,
                activeChipParticles,
                frameCellsDestroyed,
                frameDamageTierChanges,
                frameIslandScanCells,
                frameIslandRemovedCells,
                frameChunkBuilds,
                frameColliderRebuilds,
                frameColliderPaths,
                frameTextureApplies);
        }

        frameShots = 0;
        framePellets = 0;
        frameHits = 0;
        frameProjectilePoolMisses = 0;
        frameChipPoolMisses = 0;
        frameCellsDestroyed = 0;
        frameDamageTierChanges = 0;
        frameIslandScanCells = 0;
        frameIslandRemovedCells = 0;
        frameChunkBuilds = 0;
        frameColliderRebuilds = 0;
        frameColliderPaths = 0;
        frameTextureApplies = 0;
    }
}
