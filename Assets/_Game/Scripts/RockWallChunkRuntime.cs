using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RockWallChunkRuntime : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polygonCollider;
    private Texture2D texture;
    private Sprite sprite;
    private Color32[] pixelBuffer;
    private int textureWidth;
    private int textureHeight;

    public void Build(
        bool[,] solidCells,
        float[,] cellHitPoints,
        float cellMaxHitPoints,
        int rowCount,
        int columnCount,
        float worldWidth,
        float worldHeight,
        float rowHeight,
        float columnWidth,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        int activeMinRow,
        int activeMaxRow,
        int activeMinColumn,
        int activeMaxColumn,
        Color[] damageTierColors,
        bool rebuildCollider)
    {
        using (ShootTheRockPerformance.ChunkBuildMarker.Auto())
        {
            ShootTheRockPerformance.RecordChunkBuild();
            EnsureComponents();

            int chunkRows = Mathf.Max(1, endRow - startRow);
            int chunkColumns = Mathf.Max(1, endColumn - startColumn);
            float pixelsPerUnit = Mathf.Max(1f, 1f / Mathf.Max(0.0001f, columnWidth));
            EnsureTexture(chunkColumns, chunkRows, pixelsPerUnit);

            bool hasAnySolid = false;
            for (int row = startRow; row < endRow; row++)
            {
                int localRow = row - startRow;
                int pixelY = chunkRows - 1 - localRow;

                for (int column = startColumn; column < endColumn; column++)
                {
                    int pixelX = column - startColumn;
                    int pixelIndex = (pixelY * chunkColumns) + pixelX;

                    if (!solidCells[row, column])
                    {
                        pixelBuffer[pixelIndex] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    hasAnySolid = true;
                    int damageTier = GetDamageVisualTier(cellHitPoints[row, column], cellMaxHitPoints);
                    pixelBuffer[pixelIndex] = damageTier < damageTierColors.Length
                        ? (Color32)damageTierColors[damageTier]
                        : new Color32(255, 255, 255, 255);
                }
            }

            texture.SetPixels32(pixelBuffer);
            texture.Apply(false, false);
            ShootTheRockPerformance.RecordTextureApply();

            float leftX = (-worldWidth * 0.5f) + (startColumn * columnWidth);
            float rightX = (-worldWidth * 0.5f) + (endColumn * columnWidth);
            float topY = (worldHeight * 0.5f) - (startRow * rowHeight);
            float bottomY = (worldHeight * 0.5f) - (endRow * rowHeight);
            Vector2 chunkCenterLocal = new Vector2((leftX + rightX) * 0.5f, (topY + bottomY) * 0.5f);

            transform.localPosition = new Vector3(chunkCenterLocal.x, chunkCenterLocal.y, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            spriteRenderer.enabled = hasAnySolid;

            if (!hasAnySolid)
            {
                polygonCollider.enabled = false;
                polygonCollider.pathCount = 0;
                return;
            }

            if (!rebuildCollider)
                return;

            RebuildColliderPaths(
                solidCells,
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
                chunkCenterLocal);
        }
    }

    private void EnsureComponents()
    {
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        polygonCollider = polygonCollider != null ? polygonCollider : GetComponent<PolygonCollider2D>();
        if (polygonCollider == null)
            polygonCollider = gameObject.AddComponent<PolygonCollider2D>();

        spriteRenderer.sortingOrder = 3;
    }

    private void EnsureTexture(int width, int height, float pixelsPerUnit)
    {
        if (texture != null && textureWidth == width && textureHeight == height && sprite != null)
            return;

        textureWidth = width;
        textureHeight = height;
        pixelBuffer = new Color32[textureWidth * textureHeight];

        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = gameObject.name + "_Texture"
        };
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.name = gameObject.name + "_Sprite";
        spriteRenderer.sprite = sprite;
    }

    private int GetDamageVisualTier(float currentHitPoints, float cellMaxHitPoints)
    {
        float ratio = Mathf.Clamp01(currentHitPoints / Mathf.Max(0.0001f, cellMaxHitPoints));
        if (ratio > 0.75f)
            return 0;
        if (ratio > 0.5f)
            return 1;
        if (ratio > 0.25f)
            return 2;
        return 3;
    }

    private void RebuildColliderPaths(
        bool[,] solidCells,
        int rowCount,
        int columnCount,
        float worldWidth,
        float worldHeight,
        float rowHeight,
        float columnWidth,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        Vector2 chunkCenterLocal)
    {
        using (ShootTheRockPerformance.ChunkColliderMarker.Auto())
        {
            Dictionary<Vector2Int, Vector2Int> nextEdgeByStart = BuildBoundaryEdges(
                solidCells,
                rowCount,
                columnCount,
                startRow,
                endRow,
                startColumn,
                endColumn);

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
                    path.Add(GridCornerToChunkLocal(current, worldWidth, worldHeight, rowHeight, columnWidth, chunkCenterLocal));
                    nextEdgeByStart.Remove(current);
                    current = next;
                    guard++;
                    if (current == start)
                        break;
                }

                if (path.Count >= 3)
                    paths.Add(path);
            }

            polygonCollider.enabled = paths.Count > 0;
            polygonCollider.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                polygonCollider.SetPath(i, paths[i].ToArray());

            ShootTheRockPerformance.RecordColliderRebuild(paths.Count);
        }
    }

    private Dictionary<Vector2Int, Vector2Int> BuildBoundaryEdges(
        bool[,] solidCells,
        int rowCount,
        int columnCount,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn)
    {
        Dictionary<Vector2Int, Vector2Int> edges = new Dictionary<Vector2Int, Vector2Int>();

        for (int row = startRow; row < endRow; row++)
        {
            for (int column = startColumn; column < endColumn; column++)
            {
                if (!solidCells[row, column])
                    continue;

                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row - 1, column))
                    edges[new Vector2Int(column, row)] = new Vector2Int(column + 1, row);
                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row, column + 1))
                    edges[new Vector2Int(column + 1, row)] = new Vector2Int(column + 1, row + 1);
                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row + 1, column))
                    edges[new Vector2Int(column + 1, row + 1)] = new Vector2Int(column, row + 1);
                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row, column - 1))
                    edges[new Vector2Int(column, row + 1)] = new Vector2Int(column, row);
            }
        }

        return edges;
    }

    private bool IsSolidForChunk(
        bool[,] solidCells,
        int rowCount,
        int columnCount,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        int row,
        int column)
    {
        if (row < startRow || row >= endRow || column < startColumn || column >= endColumn)
            return false;
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            return false;

        return solidCells[row, column];
    }

    private Vector2 GridCornerToChunkLocal(Vector2Int gridCorner, float worldWidth, float worldHeight, float rowHeight, float columnWidth, Vector2 chunkCenterLocal)
    {
        float x = (-worldWidth * 0.5f) + (gridCorner.x * columnWidth);
        float y = (worldHeight * 0.5f) - (gridCorner.y * rowHeight);
        return new Vector2(x - chunkCenterLocal.x, y - chunkCenterLocal.y);
    }
}
