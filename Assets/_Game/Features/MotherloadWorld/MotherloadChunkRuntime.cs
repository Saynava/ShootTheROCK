using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MotherloadChunkRuntime : MonoBehaviour
{
    private static readonly Color[] DamageTierColors =
    {
        Color.white,
        new Color(0.88f, 0.88f, 0.88f, 1f),
        new Color(0.72f, 0.72f, 0.72f, 1f),
        new Color(0.52f, 0.52f, 0.52f, 1f),
    };

    private MotherloadWorldController controller;
    private MotherloadChunkData data;
    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polygonCollider;
    private Texture2D texture;
    private Sprite sprite;
    private Color32[] pixelBuffer;
    private float cellsPerUnit;
    private Vector2 bottomLeftWorld;
    private float worldWidth;
    private float worldHeight;
    private readonly Dictionary<Vector2Int, Vector2Int> boundaryEdges = new Dictionary<Vector2Int, Vector2Int>(1024);
    private readonly List<List<Vector2>> colliderPaths = new List<List<Vector2>>(8);

    public MotherloadChunkCoordinate Coordinate => data != null ? data.Coordinate : default;
    public Rect WorldRect => new Rect(bottomLeftWorld.x, bottomLeftWorld.y, worldWidth, worldHeight);

    public string BuildDebugSummary()
    {
        string dataSummary = data != null ? data.BuildDebugSummary() : "no-data";
        int pathCount = polygonCollider != null ? polygonCollider.pathCount : 0;
        return "rect=" + WorldRect + ", colliderPaths=" + pathCount + ", " + dataSummary;
    }

    public void Initialize(MotherloadWorldController controller, MotherloadChunkData data, Vector2 bottomLeftWorld, float cellsPerUnit, int sortingOrder)
    {
        this.controller = controller;
        this.data = data;
        this.bottomLeftWorld = bottomLeftWorld;
        this.cellsPerUnit = Mathf.Max(1f, cellsPerUnit);
        worldWidth = data.Width / this.cellsPerUnit;
        worldHeight = data.Height / this.cellsPerUnit;

        EnsureComponents();

        transform.position = new Vector3(bottomLeftWorld.x + (worldWidth * 0.5f), bottomLeftWorld.y + (worldHeight * 0.5f), 0f);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        spriteRenderer.sortingOrder = sortingOrder;

        RebuildVisualAndCollider();
        gameObject.name = "MotherloadChunk_" + data.Coordinate.Column + "_" + data.Coordinate.Row;

        if (controller != null && controller.ShouldLogChunkLifecycle)
            controller.LogDebug("Chunk runtime initialized " + Coordinate + " | " + BuildDebugSummary(), this);
    }

    public bool OverlapsCircle(Vector2 worldPoint, float radiusWorld)
    {
        Rect rect = WorldRect;
        float clampedX = Mathf.Clamp(worldPoint.x, rect.xMin, rect.xMax);
        float clampedY = Mathf.Clamp(worldPoint.y, rect.yMin, rect.yMax);
        Vector2 closestPoint = new Vector2(clampedX, clampedY);
        return (closestPoint - worldPoint).sqrMagnitude <= (radiusWorld * radiusWorld);
    }

    public bool TryDigCircle(Vector2 worldPoint, float radiusWorld)
    {
        float digDamage = 999999f;
        return TryApplyBlast(worldPoint, radiusWorld, digDamage, digDamage);
    }

    public bool TryApplyBlast(Vector2 worldPoint, float radiusWorld, float centerDamage, float outerDamage)
    {
        return TryApplyBlast(worldPoint, radiusWorld, centerDamage, outerDamage, out _);
    }

    public bool TryApplyBlast(Vector2 worldPoint, float radiusWorld, float centerDamage, float outerDamage, out MotherloadOreYield oreYield)
    {
        oreYield = default;
        if (data == null || !OverlapsCircle(worldPoint, radiusWorld))
            return false;

        Vector2 local = worldPoint - bottomLeftWorld;
        float centerColumn = local.x * cellsPerUnit;
        float centerRow = local.y * cellsPerUnit;
        int radiusCells = Mathf.CeilToInt(Mathf.Max(0.1f, radiusWorld) * cellsPerUnit);
        bool allowNearestFallback = centerColumn >= 0f && centerColumn < data.Width && centerRow >= 0f && centerRow < data.Height;
        bool changed = data.ApplyBlast(centerColumn, centerRow, radiusCells, centerDamage, outerDamage, allowNearestFallback, out oreYield);
        if (controller != null && controller.ShouldLogProjectileHits)
        {
            controller.LogDebug(
                "Chunk blast " + Coordinate
                + " | point=" + worldPoint.ToString("F3")
                + " | localCell=(" + centerColumn.ToString("F2") + ", " + centerRow.ToString("F2") + ")"
                + " | radiusCells=" + radiusCells
                + " | fallback=" + allowNearestFallback
                + " | changed=" + changed
                + " | ore=" + oreYield,
                this);
        }

        if (!changed)
            return false;

        RebuildVisualAndCollider();
        return true;
    }

    public bool TryFindHazardNearWorldPoint(Vector2 worldPoint, float radiusWorld, out Vector2 hazardWorldPoint, out MotherloadHazardType hazardType)
    {
        hazardWorldPoint = default;
        hazardType = MotherloadHazardType.None;
        if (data == null || !OverlapsCircle(worldPoint, radiusWorld))
            return false;

        Vector2 local = worldPoint - bottomLeftWorld;
        float centerColumn = local.x * cellsPerUnit;
        float centerRow = local.y * cellsPerUnit;
        int radiusCells = Mathf.CeilToInt(Mathf.Max(0.1f, radiusWorld) * cellsPerUnit);
        int minRow = Mathf.Max(0, Mathf.FloorToInt(centerRow - radiusCells - 1f));
        int maxRow = Mathf.Min(data.Height - 1, Mathf.CeilToInt(centerRow + radiusCells + 1f));
        int minColumn = Mathf.Max(0, Mathf.FloorToInt(centerColumn - radiusCells - 1f));
        int maxColumn = Mathf.Min(data.Width - 1, Mathf.CeilToInt(centerColumn + radiusCells + 1f));

        float bestDistanceSquared = float.MaxValue;
        int bestRow = -1;
        int bestColumn = -1;
        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                MotherloadHazardType candidateHazard = data.GetHazard(row, column);
                if (candidateHazard == MotherloadHazardType.None)
                    continue;

                float dx = (column + 0.5f) - centerColumn;
                float dy = (row + 0.5f) - centerRow;
                float distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared > radiusCells * radiusCells || distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestRow = row;
                bestColumn = column;
                hazardType = candidateHazard;
            }
        }

        if (bestRow < 0 || bestColumn < 0)
            return false;

        hazardWorldPoint = bottomLeftWorld + new Vector2((bestColumn + 0.5f) / cellsPerUnit, (bestRow + 0.5f) / cellsPerUnit);
        return true;
    }

    public bool ClearHazardsNearWorldPoint(Vector2 worldPoint, float radiusWorld)
    {
        if (data == null || !OverlapsCircle(worldPoint, radiusWorld))
            return false;

        Vector2 local = worldPoint - bottomLeftWorld;
        float centerColumn = local.x * cellsPerUnit;
        float centerRow = local.y * cellsPerUnit;
        int radiusCells = Mathf.CeilToInt(Mathf.Max(0.1f, radiusWorld) * cellsPerUnit);
        bool changed = data.ClearHazardsInCircle(centerColumn, centerRow, radiusCells);
        if (changed)
            RebuildVisualAndCollider();
        return changed;
    }

    public void RebuildVisualAndCollider()
    {
        if (data == null)
            return;

        EnsureTexture(data.Width, data.Height);

        bool hasSolidPixels = false;
        for (int row = 0; row < data.Height; row++)
        {
            int pixelY = row;
            for (int column = 0; column < data.Width; column++)
            {
                int pixelX = column;
                int pixelIndex = (pixelY * data.Width) + pixelX;

                MotherloadHazardType hazardType = data.GetHazard(row, column);
                if (!data.IsSolid(row, column) && hazardType == MotherloadHazardType.None)
                {
                    pixelBuffer[pixelIndex] = new Color32(0, 0, 0, 0);
                    continue;
                }

                hasSolidPixels = true;
                pixelBuffer[pixelIndex] = (Color32)GetCellRenderColor(row, column);
            }
        }

        texture.SetPixels32(pixelBuffer);
        texture.Apply(false, false);
        spriteRenderer.enabled = hasSolidPixels;

        if (!hasSolidPixels)
        {
            polygonCollider.enabled = false;
            polygonCollider.pathCount = 0;
            if (controller != null && controller.ShouldLogChunkRebuilds)
                controller.LogDebug("Chunk rebuild cleared all solid pixels " + Coordinate, this);
            return;
        }

        RebuildColliderPaths();
    }

    private Color GetCellRenderColor(int row, int column)
    {
        MotherloadCellMaterial material = data.GetMaterial(row, column);
        MotherloadHazardType hazardType = data.GetHazard(row, column);
        if (hazardType != MotherloadHazardType.None)
            return GetHazardBaseColor(hazardType);

        Color baseColor = GetMaterialBaseColor(material);
        int damageTier = data.GetDamageVisualTier(row, column);
        Color damagedColor = baseColor * DamageTierColors[Mathf.Clamp(damageTier, 0, DamageTierColors.Length - 1)];
        damagedColor.a = 1f;
        return damagedColor;
    }

    private static Color GetMaterialBaseColor(MotherloadCellMaterial material)
    {
        switch (material)
        {
            case MotherloadCellMaterial.Dirt:
                return new Color(0.38f, 0.24f, 0.12f, 1f);
            case MotherloadCellMaterial.Stone:
                return new Color(0.24f, 0.24f, 0.27f, 1f);
            case MotherloadCellMaterial.Copper:
                return new Color(0.72f, 0.42f, 0.2f, 1f);
            case MotherloadCellMaterial.Tin:
                return new Color(0.42f, 0.7f, 0.78f, 1f);
            case MotherloadCellMaterial.Silver:
                return new Color(0.9f, 0.92f, 0.98f, 1f);
            case MotherloadCellMaterial.Gold:
                return new Color(0.9f, 0.76f, 0.18f, 1f);
            case MotherloadCellMaterial.Relic:
                return new Color(0.72f, 0.44f, 1f, 1f);
            case MotherloadCellMaterial.Bedrock:
                return new Color(0.08f, 0.08f, 0.1f, 1f);
            default:
                return Color.clear;
        }
    }

    private static Color GetHazardBaseColor(MotherloadHazardType hazardType)
    {
        switch (hazardType)
        {
            case MotherloadHazardType.GasPocket:
                return new Color(0.56f, 0.86f, 0.18f, 1f);
            default:
                return Color.clear;
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
    }

    private void EnsureTexture(int width, int height)
    {
        if (texture != null && texture.width == width && texture.height == height && sprite != null)
            return;

        CleanupVisualAssets();
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = gameObject.name + "_Texture"
        };
        pixelBuffer = new Color32[width * height];
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), cellsPerUnit, 0u, SpriteMeshType.FullRect);
        sprite.name = gameObject.name + "_Sprite";
        spriteRenderer.sprite = sprite;
    }

    private void RebuildColliderPaths()
    {
        BuildBoundaryEdges();

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
                path.Add(GridCornerToLocal(current));
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

        if (controller != null && controller.ShouldLogChunkRebuilds)
            controller.LogDebug("Chunk collider rebuild " + Coordinate + " | paths=" + colliderPaths.Count + " | " + data.BuildDebugSummary(), this);
    }

    private void BuildBoundaryEdges()
    {
        boundaryEdges.Clear();

        for (int row = 0; row < data.Height; row++)
        {
            for (int column = 0; column < data.Width; column++)
            {
                if (!data.IsSolid(row, column))
                    continue;

                if (!IsSolidWithinChunk(row - 1, column))
                    boundaryEdges[new Vector2Int(column, row)] = new Vector2Int(column + 1, row);
                if (!IsSolidWithinChunk(row, column + 1))
                    boundaryEdges[new Vector2Int(column + 1, row)] = new Vector2Int(column + 1, row + 1);
                if (!IsSolidWithinChunk(row + 1, column))
                    boundaryEdges[new Vector2Int(column + 1, row + 1)] = new Vector2Int(column, row + 1);
                if (!IsSolidWithinChunk(row, column - 1))
                    boundaryEdges[new Vector2Int(column, row + 1)] = new Vector2Int(column, row);
            }
        }
    }

    private bool IsSolidWithinChunk(int row, int column)
    {
        if (data == null)
            return false;

        if (row < 0 || row >= data.Height || column < 0 || column >= data.Width)
            return false;

        return data.IsSolid(row, column);
    }

    private Vector2 GridCornerToLocal(Vector2Int gridCorner)
    {
        float columnWidth = 1f / cellsPerUnit;
        float rowHeight = 1f / cellsPerUnit;
        float x = (-worldWidth * 0.5f) + (gridCorner.x * columnWidth);
        float y = (-worldHeight * 0.5f) + (gridCorner.y * rowHeight);
        return new Vector2(x, y);
    }

    private void OnDestroy()
    {
        CleanupVisualAssets();
    }

    private void CleanupVisualAssets()
    {
        if (sprite != null)
        {
            DestroyRuntimeObject(sprite);
            sprite = null;
        }

        if (texture != null)
        {
            DestroyRuntimeObject(texture);
            texture = null;
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
}
