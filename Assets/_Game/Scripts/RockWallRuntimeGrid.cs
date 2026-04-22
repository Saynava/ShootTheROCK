using System.Collections.Generic;
using UnityEngine;

public class RockWallRuntimeGrid
{
    private const string ChunkRootName = "RuntimeChunks";

    private Transform ownerTransform;
    private Transform chunkRoot;
    private int chunkSizeInCells;
    private readonly List<RockWallChunkRuntime> chunks = new List<RockWallChunkRuntime>();
    private readonly HashSet<int> rebuildChunkIndices = new HashSet<int>();

    public void Initialize(Transform ownerTransform, int chunkSizeInCells)
    {
        this.ownerTransform = ownerTransform;
        this.chunkSizeInCells = Mathf.Max(8, chunkSizeInCells);
        EnsureChunkRoot();
    }

    public void SetVisible(bool visible)
    {
        if (chunkRoot != null)
            chunkRoot.gameObject.SetActive(visible);
    }

    public void RebuildAll(
        bool[,] solidCells,
        float[,] cellHitPoints,
        float[,] cellMaxHitPointsByCell,
        EssenceType[,] cellEssenceTypes,
        float cellMaxHitPoints,
        int rowCount,
        int columnCount,
        float worldWidth,
        float worldHeight,
        float rowHeight,
        float columnWidth,
        int activeMinRow,
        int activeMaxRow,
        int activeMinColumn,
        int activeMaxColumn,
        Color[] damageTierColors,
        bool rebuildCollider)
    {
        using (ShootTheRockPerformance.RuntimeGridRebuildAllMarker.Auto())
        {
            if (ownerTransform == null || solidCells == null || cellHitPoints == null)
                return;

            EnsureChunkRoot();
            chunkRoot.gameObject.SetActive(true);

            int chunkRows = Mathf.CeilToInt(rowCount / (float)chunkSizeInCells);
            int chunkColumns = Mathf.CeilToInt(columnCount / (float)chunkSizeInCells);
            int requiredChunkCount = chunkRows * chunkColumns;
            EnsureChunkCount(requiredChunkCount);

            int chunkIndex = 0;
            for (int chunkRow = 0; chunkRow < chunkRows; chunkRow++)
            {
                for (int chunkColumn = 0; chunkColumn < chunkColumns; chunkColumn++)
                {
                    BuildChunk(
                        chunkIndex++,
                        solidCells,
                        cellHitPoints,
                        cellMaxHitPointsByCell,
                        cellEssenceTypes,
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
                        damageTierColors,
                        rebuildVisual: true,
                        rebuildCollider: rebuildCollider);
                }
            }

            for (int i = requiredChunkCount; i < chunks.Count; i++)
                chunks[i].gameObject.SetActive(false);
        }
    }

    public void RebuildDirty(
        bool[,] solidCells,
        float[,] cellHitPoints,
        float[,] cellMaxHitPointsByCell,
        EssenceType[,] cellEssenceTypes,
        float cellMaxHitPoints,
        int rowCount,
        int columnCount,
        float worldWidth,
        float worldHeight,
        float rowHeight,
        float columnWidth,
        ICollection<int> dirtyVisualChunkIndices,
        ICollection<int> dirtyColliderChunkIndices,
        int activeMinRow,
        int activeMaxRow,
        int activeMinColumn,
        int activeMaxColumn,
        Color[] damageTierColors,
        bool rebuildCollider)
    {
        using (ShootTheRockPerformance.RuntimeGridRebuildDirtyMarker.Auto())
        {
            if (ownerTransform == null || solidCells == null || cellHitPoints == null)
                return;

            EnsureChunkRoot();
            chunkRoot.gameObject.SetActive(true);

            int chunkRows = Mathf.CeilToInt(rowCount / (float)chunkSizeInCells);
            int chunkColumns = Mathf.CeilToInt(columnCount / (float)chunkSizeInCells);
            EnsureChunkCount(chunkRows * chunkColumns);

            rebuildChunkIndices.Clear();
            if (dirtyVisualChunkIndices != null)
            {
                foreach (int chunkIndex in dirtyVisualChunkIndices)
                    rebuildChunkIndices.Add(chunkIndex);
            }

            if (rebuildCollider && dirtyColliderChunkIndices != null)
            {
                foreach (int chunkIndex in dirtyColliderChunkIndices)
                    rebuildChunkIndices.Add(chunkIndex);
            }

            foreach (int chunkIndex in rebuildChunkIndices)
            {
                if (chunkIndex < 0 || chunkIndex >= chunks.Count)
                    continue;

                bool rebuildVisual = dirtyVisualChunkIndices != null && dirtyVisualChunkIndices.Contains(chunkIndex);
                bool chunkNeedsCollider = rebuildCollider && dirtyColliderChunkIndices != null && dirtyColliderChunkIndices.Contains(chunkIndex);
                if (!rebuildVisual && !chunkNeedsCollider)
                    continue;

                BuildChunk(
                    chunkIndex,
                    solidCells,
                    cellHitPoints,
                    cellMaxHitPointsByCell,
                    cellEssenceTypes,
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
                    damageTierColors,
                    rebuildVisual,
                    chunkNeedsCollider);
            }
        }
    }

    private void BuildChunk(
        int chunkIndex,
        bool[,] solidCells,
        float[,] cellHitPoints,
        float[,] cellMaxHitPointsByCell,
        EssenceType[,] cellEssenceTypes,
        float cellMaxHitPoints,
        int rowCount,
        int columnCount,
        float worldWidth,
        float worldHeight,
        float rowHeight,
        float columnWidth,
        int activeMinRow,
        int activeMaxRow,
        int activeMinColumn,
        int activeMaxColumn,
        Color[] damageTierColors,
        bool rebuildVisual,
        bool rebuildCollider)
    {
        int chunkColumns = Mathf.CeilToInt(columnCount / (float)chunkSizeInCells);
        int chunkRow = chunkIndex / chunkColumns;
        int chunkColumn = chunkIndex % chunkColumns;
        int startRow = chunkRow * chunkSizeInCells;
        int endRow = Mathf.Min(rowCount, startRow + chunkSizeInCells);
        int startColumn = chunkColumn * chunkSizeInCells;
        int endColumn = Mathf.Min(columnCount, startColumn + chunkSizeInCells);

        RockWallChunkRuntime chunk = chunks[chunkIndex];
        chunk.gameObject.SetActive(true);
        chunk.Build(
            solidCells,
            cellHitPoints,
            cellMaxHitPointsByCell,
            cellEssenceTypes,
            cellMaxHitPoints,
            rowCount,
            columnCount,
            worldWidth,
            worldHeight,
            rowHeight,
            columnWidth,
            startRow,
            endRow,
            startColumn,
            endColumn,
            activeMinRow,
            activeMaxRow,
            activeMinColumn,
            activeMaxColumn,
            damageTierColors,
            rebuildVisual,
            rebuildCollider);
    }

    private void EnsureChunkRoot()
    {
        if (ownerTransform == null)
            return;

        if (chunkRoot == null)
        {
            Transform existing = ownerTransform.Find(ChunkRootName);
            if (existing != null)
                chunkRoot = existing;
        }

        if (chunkRoot == null)
        {
            GameObject rootObject = new GameObject(ChunkRootName);
            rootObject.transform.SetParent(ownerTransform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            chunkRoot = rootObject.transform;
        }
    }

    private void EnsureChunkCount(int requiredChunkCount)
    {
        while (chunks.Count < requiredChunkCount)
        {
            GameObject chunkObject = new GameObject($"Chunk_{chunks.Count:00}");
            chunkObject.transform.SetParent(chunkRoot, false);
            chunkObject.transform.localPosition = Vector3.zero;
            chunkObject.transform.localRotation = Quaternion.identity;
            chunkObject.transform.localScale = Vector3.one;
            RockWallChunkRuntime chunk = chunkObject.AddComponent<RockWallChunkRuntime>();
            chunks.Add(chunk);
        }
    }
}
