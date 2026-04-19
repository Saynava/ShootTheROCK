using System.Collections.Generic;
using UnityEngine;

public class RockWallRuntimeGrid
{
    private const string ChunkRootName = "RuntimeChunks";

    private Transform ownerTransform;
    private Transform chunkRoot;
    private int chunkSizeInCells;
    private readonly List<RockWallChunkRuntime> chunks = new List<RockWallChunkRuntime>();

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
                    int startRow = chunkRow * chunkSizeInCells;
                    int endRow = Mathf.Min(rowCount, startRow + chunkSizeInCells);
                    int startColumn = chunkColumn * chunkSizeInCells;
                    int endColumn = Mathf.Min(columnCount, startColumn + chunkSizeInCells);

                    RockWallChunkRuntime chunk = chunks[chunkIndex++];
                    chunk.gameObject.SetActive(true);
                    chunk.Build(
                        solidCells,
                        cellHitPoints,
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
                        rebuildCollider);
                }
            }

            for (int i = requiredChunkCount; i < chunks.Count; i++)
                chunks[i].gameObject.SetActive(false);
        }
    }

    public void RebuildDirty(
        bool[,] solidCells,
        float[,] cellHitPoints,
        float cellMaxHitPoints,
        int rowCount,
        int columnCount,
        float worldWidth,
        float worldHeight,
        float rowHeight,
        float columnWidth,
        int dirtyMinRow,
        int dirtyMaxRow,
        int dirtyMinColumn,
        int dirtyMaxColumn,
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

            int minChunkRow = Mathf.Clamp(dirtyMinRow / chunkSizeInCells, 0, Mathf.Max(0, chunkRows - 1));
            int maxChunkRow = Mathf.Clamp(dirtyMaxRow / chunkSizeInCells, 0, Mathf.Max(0, chunkRows - 1));
            int minChunkColumn = Mathf.Clamp(dirtyMinColumn / chunkSizeInCells, 0, Mathf.Max(0, chunkColumns - 1));
            int maxChunkColumn = Mathf.Clamp(dirtyMaxColumn / chunkSizeInCells, 0, Mathf.Max(0, chunkColumns - 1));

            for (int chunkRow = minChunkRow; chunkRow <= maxChunkRow; chunkRow++)
            {
                for (int chunkColumn = minChunkColumn; chunkColumn <= maxChunkColumn; chunkColumn++)
                {
                    int chunkIndex = (chunkRow * chunkColumns) + chunkColumn;
                    int startRow = chunkRow * chunkSizeInCells;
                    int endRow = Mathf.Min(rowCount, startRow + chunkSizeInCells);
                    int startColumn = chunkColumn * chunkSizeInCells;
                    int endColumn = Mathf.Min(columnCount, startColumn + chunkSizeInCells);

                    RockWallChunkRuntime chunk = chunks[chunkIndex];
                    chunk.gameObject.SetActive(true);
                    chunk.Build(
                        solidCells,
                        cellHitPoints,
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
                        rebuildCollider);
                }
            }
        }
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
