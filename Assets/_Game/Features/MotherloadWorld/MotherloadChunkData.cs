using UnityEngine;

public sealed class MotherloadChunkData
{
    private const float BedrockHitPoints = 999999f;

    private readonly MotherloadCellMaterial[] materials;
    private readonly float[] currentHitPoints;
    private readonly float[] maxHitPoints;

    public MotherloadChunkCoordinate Coordinate { get; }
    public int Width { get; }
    public int Height { get; }

    public MotherloadChunkData(MotherloadChunkCoordinate coordinate, int width, int height)
    {
        Coordinate = coordinate;
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        materials = new MotherloadCellMaterial[Width * Height];
        currentHitPoints = new float[Width * Height];
        maxHitPoints = new float[Width * Height];
    }

    public MotherloadCellMaterial GetMaterial(int row, int column)
    {
        if (!IsInBounds(row, column))
            return MotherloadCellMaterial.Empty;

        return materials[GetIndex(row, column)];
    }

    public float GetCurrentHitPoints(int row, int column)
    {
        if (!IsInBounds(row, column))
            return 0f;

        return currentHitPoints[GetIndex(row, column)];
    }

    public float GetMaxHitPoints(int row, int column)
    {
        if (!IsInBounds(row, column))
            return 0f;

        return maxHitPoints[GetIndex(row, column)];
    }

    public bool IsSolid(int row, int column)
    {
        MotherloadCellMaterial material = GetMaterial(row, column);
        return material != MotherloadCellMaterial.Empty;
    }

    public int CountSolidCells()
    {
        int count = 0;
        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
            {
                if (IsSolid(row, column))
                    count++;
            }
        }

        return count;
    }

    public int CountDamagedSolidCells()
    {
        int count = 0;
        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
            {
                if (!IsSolid(row, column))
                    continue;

                if (GetCurrentHitPoints(row, column) < GetMaxHitPoints(row, column))
                    count++;
            }
        }

        return count;
    }

    public string BuildDebugSummary()
    {
        int empty = 0;
        int dirt = 0;
        int stone = 0;
        int copper = 0;
        int silver = 0;
        int gold = 0;
        int bedrock = 0;

        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
            {
                switch (GetMaterial(row, column))
                {
                    case MotherloadCellMaterial.Empty:
                        empty++;
                        break;
                    case MotherloadCellMaterial.Dirt:
                        dirt++;
                        break;
                    case MotherloadCellMaterial.Stone:
                        stone++;
                        break;
                    case MotherloadCellMaterial.Copper:
                        copper++;
                        break;
                    case MotherloadCellMaterial.Silver:
                        silver++;
                        break;
                    case MotherloadCellMaterial.Gold:
                        gold++;
                        break;
                    case MotherloadCellMaterial.Bedrock:
                        bedrock++;
                        break;
                }
            }
        }

        return "solid=" + CountSolidCells()
            + ", damaged=" + CountDamagedSolidCells()
            + ", empty=" + empty
            + ", dirt=" + dirt
            + ", stone=" + stone
            + ", copper=" + copper
            + ", silver=" + silver
            + ", gold=" + gold
            + ", bedrock=" + bedrock;
    }

    public int GetDamageVisualTier(int row, int column)
    {
        float resolvedMaxHitPoints = Mathf.Max(0.0001f, GetMaxHitPoints(row, column));
        float ratio = Mathf.Clamp01(GetCurrentHitPoints(row, column) / resolvedMaxHitPoints);
        if (ratio > 0.75f)
            return 0;
        if (ratio > 0.5f)
            return 1;
        if (ratio > 0.25f)
            return 2;
        return 3;
    }

    public void SetMaterial(int row, int column, MotherloadCellMaterial material)
    {
        if (!IsInBounds(row, column))
            return;

        int index = GetIndex(row, column);
        materials[index] = material;

        float resolvedMaxHitPoints = ResolveMaxHitPoints(material);
        maxHitPoints[index] = resolvedMaxHitPoints;
        currentHitPoints[index] = resolvedMaxHitPoints;
    }

    public bool DigCircle(int centerColumn, int centerRow, int radiusCells)
    {
        float digDamage = 999999f;
        return ApplyBlast(centerColumn + 0.5f, centerRow + 0.5f, radiusCells, digDamage, digDamage, allowNearestFallback: true);
    }

    public bool ApplyBlast(int centerColumn, int centerRow, int radiusCells, float centerDamage, float outerDamage)
    {
        return ApplyBlast(centerColumn + 0.5f, centerRow + 0.5f, radiusCells, centerDamage, outerDamage, allowNearestFallback: true);
    }

    public bool ApplyBlast(float centerColumn, float centerRow, int radiusCells, float centerDamage, float outerDamage, bool allowNearestFallback)
    {
        radiusCells = Mathf.Max(1, radiusCells);
        centerDamage = Mathf.Max(0.01f, centerDamage);
        outerDamage = Mathf.Max(0.01f, outerDamage);
        bool changed = false;
        bool touchedSolid = false;

        int minRow = Mathf.Max(0, Mathf.FloorToInt(centerRow - radiusCells - 1f));
        int maxRow = Mathf.Min(Height - 1, Mathf.CeilToInt(centerRow + radiusCells + 1f));
        int minColumn = Mathf.Max(0, Mathf.FloorToInt(centerColumn - radiusCells - 1f));
        int maxColumn = Mathf.Min(Width - 1, Mathf.CeilToInt(centerColumn + radiusCells + 1f));

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                MotherloadCellMaterial material = GetMaterial(row, column);
                if (material == MotherloadCellMaterial.Empty || material == MotherloadCellMaterial.Bedrock)
                    continue;

                float dx = (column + 0.5f) - centerColumn;
                float dy = (row + 0.5f) - centerRow;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (distance > radiusCells)
                    continue;

                float normalizedDistance = distance / Mathf.Max(1f, radiusCells);
                float edgeNoise = 1f;
                if (normalizedDistance > 0.72f)
                    edgeNoise += Mathf.Lerp(-0.08f, 0.08f, HashToUnit(row, column));
                if (normalizedDistance > edgeNoise)
                    continue;

                touchedSolid = true;
                float falloff = 1f - normalizedDistance;
                float damageFactor = 1f - Mathf.Pow(1f - falloff, 1.85f);
                float noise = Mathf.Lerp(0.96f, 1.04f, HashToUnit(row * 3, column * 5));
                float damage = Mathf.Lerp(outerDamage, centerDamage, damageFactor) * noise;
                if (ApplyPointDamage(row, column, damage))
                    changed = true;
            }
        }

        if (changed || touchedSolid)
            return changed;

        if (!allowNearestFallback)
            return false;

        return ForceImpactAtNearestSolidCell(Mathf.RoundToInt(centerRow), Mathf.RoundToInt(centerColumn), centerDamage);
    }

    private bool ForceImpactAtNearestSolidCell(int centerRow, int centerColumn, float damageAmount)
    {
        int bestRow = -1;
        int bestColumn = -1;
        int bestDistanceSquared = int.MaxValue;

        for (int row = Mathf.Max(0, centerRow - 3); row <= Mathf.Min(Height - 1, centerRow + 3); row++)
        {
            for (int column = Mathf.Max(0, centerColumn - 3); column <= Mathf.Min(Width - 1, centerColumn + 3); column++)
            {
                MotherloadCellMaterial material = GetMaterial(row, column);
                if (material == MotherloadCellMaterial.Empty || material == MotherloadCellMaterial.Bedrock)
                    continue;

                int dx = column - centerColumn;
                int dy = row - centerRow;
                int distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestRow = row;
                bestColumn = column;
            }
        }

        if (bestRow < 0 || bestColumn < 0)
            return false;

        return ApplyPointDamage(bestRow, bestColumn, damageAmount);
    }

    private bool ApplyPointDamage(int row, int column, float damageAmount)
    {
        if (!IsInBounds(row, column))
            return false;

        int index = GetIndex(row, column);
        MotherloadCellMaterial material = materials[index];
        if (material == MotherloadCellMaterial.Empty || material == MotherloadCellMaterial.Bedrock)
            return false;

        float previousHitPoints = currentHitPoints[index];
        float updatedHitPoints = Mathf.Max(0f, previousHitPoints - damageAmount);
        if (Mathf.Approximately(previousHitPoints, updatedHitPoints))
            return false;

        currentHitPoints[index] = updatedHitPoints;
        if (updatedHitPoints > 0f)
            return true;

        materials[index] = MotherloadCellMaterial.Empty;
        currentHitPoints[index] = 0f;
        maxHitPoints[index] = 0f;
        return true;
    }

    private float ResolveMaxHitPoints(MotherloadCellMaterial material)
    {
        switch (material)
        {
            case MotherloadCellMaterial.Dirt:
                return 1f;
            case MotherloadCellMaterial.Stone:
                return 2.4f;
            case MotherloadCellMaterial.Copper:
                return 3.1f;
            case MotherloadCellMaterial.Silver:
                return 3.8f;
            case MotherloadCellMaterial.Gold:
                return 4.6f;
            case MotherloadCellMaterial.Bedrock:
                return BedrockHitPoints;
            default:
                return 0f;
        }
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

    private bool IsInBounds(int row, int column)
    {
        return row >= 0 && row < Height && column >= 0 && column < Width;
    }

    private int GetIndex(int row, int column)
    {
        return (row * Width) + column;
    }
}
