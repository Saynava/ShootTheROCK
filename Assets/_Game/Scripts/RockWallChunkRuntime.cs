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
    private bool hasSolidPixels;
    private readonly Dictionary<Vector2Int, Vector2Int> boundaryEdges = new Dictionary<Vector2Int, Vector2Int>(1024);
    private readonly List<List<Vector2>> colliderPaths = new List<List<Vector2>>(8);

    public void Build(
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
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        int activeMinRow,
        int activeMaxRow,
        int activeMinColumn,
        int activeMaxColumn,
        Color[] damageTierColors,
        bool rebuildVisual,
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

            if (rebuildVisual)
            {
                hasSolidPixels = false;
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

                        hasSolidPixels = true;
                        float perCellMaxHitPoints = cellMaxHitPointsByCell != null
                            ? cellMaxHitPointsByCell[row, column]
                            : cellMaxHitPoints;
                        int damageTier = GetDamageVisualTier(cellHitPoints[row, column], perCellMaxHitPoints);
                        pixelBuffer[pixelIndex] = (Color32)GetCellRenderColor(
                            row,
                            column,
                            cellEssenceTypes != null ? cellEssenceTypes[row, column] : EssenceType.None,
                            cellEssenceTypes,
                            damageTier,
                            cellHitPoints[row, column],
                            perCellMaxHitPoints,
                            damageTierColors);
                    }
                }

                texture.SetPixels32(pixelBuffer);
                texture.Apply(false, false);
                ShootTheRockPerformance.RecordTextureApply();
                spriteRenderer.enabled = hasSolidPixels;
            }

            float leftX = (-worldWidth * 0.5f) + (startColumn * columnWidth);
            float rightX = (-worldWidth * 0.5f) + (endColumn * columnWidth);
            float topY = (worldHeight * 0.5f) - (startRow * rowHeight);
            float bottomY = (worldHeight * 0.5f) - (endRow * rowHeight);
            Vector2 chunkCenterLocal = new Vector2((leftX + rightX) * 0.5f, (topY + bottomY) * 0.5f);

            transform.localPosition = new Vector3(chunkCenterLocal.x, chunkCenterLocal.y, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (!hasSolidPixels)
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

    private Color GetCellRenderColor(int row, int column, EssenceType essenceType, EssenceType[,] cellEssenceTypes, int damageTier, float currentHitPoints, float cellMaxHitPoints, Color[] damageTierColors)
    {
        if (essenceType == EssenceType.None)
        {
            if (damageTierColors != null && damageTier >= 0 && damageTier < damageTierColors.Length)
                return damageTierColors[damageTier];

            return Color.white;
        }

        Color baseColor = GetEssenceBaseColor(essenceType);
        float clusterShade = GetEssenceClusterShade(row, column, essenceType, cellEssenceTypes);
        Color paleColor = Color.Lerp(Color.white, baseColor, 0.28f);
        Color richColor = Color.Lerp(baseColor, Color.white, 0.06f);
        Color toneColor = Color.Lerp(paleColor, richColor, clusterShade);

        float ratio = Mathf.Clamp01(currentHitPoints / Mathf.Max(0.0001f, cellMaxHitPoints));
        float brightness = Mathf.Lerp(0.22f, 1f, ratio);
        return new Color(toneColor.r * brightness, toneColor.g * brightness, toneColor.b * brightness, 1f);
    }

    private float GetEssenceClusterShade(int row, int column, EssenceType essenceType, EssenceType[,] cellEssenceTypes)
    {
        if (cellEssenceTypes == null)
            return 0.7f;

        int sameTypeNeighbors = 0;
        int checkedNeighbors = 0;
        int rowCount = cellEssenceTypes.GetLength(0);
        int columnCount = cellEssenceTypes.GetLength(1);

        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                    continue;

                int neighborRow = row + offsetY;
                int neighborColumn = column + offsetX;
                if (neighborRow < 0 || neighborRow >= rowCount || neighborColumn < 0 || neighborColumn >= columnCount)
                    continue;

                checkedNeighbors++;
                if (cellEssenceTypes[neighborRow, neighborColumn] == essenceType)
                    sameTypeNeighbors++;
            }
        }

        float density = checkedNeighbors <= 0 ? 0.5f : sameTypeNeighbors / (float)checkedNeighbors;
        float noise = HashToUnit(row, column);
        return Mathf.Clamp01((density * 0.78f) + (noise * 0.22f));
    }

    private float HashToUnit(int row, int column)
    {
        unchecked
        {
            int hash = row;
            hash = (hash * 397) ^ column;
            hash ^= (hash << 13);
            hash ^= (hash >> 17);
            hash ^= (hash << 5);
            int positive = hash & 0x7fffffff;
            return positive / 2147483647f;
        }
    }

    private Color GetEssenceBaseColor(EssenceType essenceType)
    {
        switch (essenceType)
        {
            case EssenceType.Red:
                return new Color(1f, 0.28f, 0.28f, 1f);
            case EssenceType.Blue:
                return new Color(0.28f, 0.62f, 1f, 1f);
            case EssenceType.Green:
                return new Color(0.32f, 1f, 0.45f, 1f);
            default:
                return Color.white;
        }
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
            BuildBoundaryEdges(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn);

            colliderPaths.Clear();
            while (boundaryEdges.Count > 0)
            {
                Vector2Int start = default;
                foreach (KeyValuePair<Vector2Int, Vector2Int> pair in boundaryEdges)
                {
                    start = pair.Key;
                    break;
                }

                Vector2Int current = start;
                List<Vector2> path = new List<Vector2>(64);
                int guard = 0;
                while (boundaryEdges.TryGetValue(current, out Vector2Int next) && guard < 20000)
                {
                    path.Add(GridCornerToChunkLocal(current, worldWidth, worldHeight, rowHeight, columnWidth, chunkCenterLocal));
                    boundaryEdges.Remove(current);
                    current = next;
                    guard++;
                    if (current == start)
                        break;
                }

                if (path.Count >= 3)
                    colliderPaths.Add(path);
            }

            polygonCollider.enabled = colliderPaths.Count > 0;
            polygonCollider.pathCount = colliderPaths.Count;
            for (int i = 0; i < colliderPaths.Count; i++)
                polygonCollider.SetPath(i, colliderPaths[i].ToArray());

            ShootTheRockPerformance.RecordColliderRebuild(colliderPaths.Count);
        }
    }

    private void BuildBoundaryEdges(
        bool[,] solidCells,
        int rowCount,
        int columnCount,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn)
    {
        boundaryEdges.Clear();

        for (int row = startRow; row < endRow; row++)
        {
            for (int column = startColumn; column < endColumn; column++)
            {
                if (!solidCells[row, column])
                    continue;

                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row - 1, column))
                    boundaryEdges[new Vector2Int(column, row)] = new Vector2Int(column + 1, row);
                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row, column + 1))
                    boundaryEdges[new Vector2Int(column + 1, row)] = new Vector2Int(column + 1, row + 1);
                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row + 1, column))
                    boundaryEdges[new Vector2Int(column + 1, row + 1)] = new Vector2Int(column, row + 1);
                if (!IsSolidForChunk(solidCells, rowCount, columnCount, startRow, endRow, startColumn, endColumn, row, column - 1))
                    boundaryEdges[new Vector2Int(column, row + 1)] = new Vector2Int(column, row);
            }
        }
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
