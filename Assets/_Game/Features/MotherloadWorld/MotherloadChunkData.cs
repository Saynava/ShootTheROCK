using UnityEngine;

public struct MotherloadOreYield
{
    public int Copper;
    public int Tin;
    public int Silver;
    public int Gold;
    public int Relic;

    public int TotalCount => Copper + Tin + Silver + Gold;
    public int TotalIncludingSpecial => TotalCount + Relic;

    public override string ToString()
    {
        return "copper=" + Copper + ", tin=" + Tin + ", silver=" + Silver + ", gold=" + Gold + ", relic=" + Relic;
    }
}

public sealed class MotherloadChunkData
{
    private const float BedrockHitPoints = 999999f;

    private readonly MotherloadCellMaterial[] materials;
    private readonly MotherloadHazardType[] hazards;
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
        hazards = new MotherloadHazardType[Width * Height];
        currentHitPoints = new float[Width * Height];
        maxHitPoints = new float[Width * Height];
    }

    public MotherloadCellMaterial GetMaterial(int row, int column)
    {
        if (!IsInBounds(row, column))
            return MotherloadCellMaterial.Empty;

        return materials[GetIndex(row, column)];
    }

    public MotherloadHazardType GetHazard(int row, int column)
    {
        if (!IsInBounds(row, column))
            return MotherloadHazardType.None;

        return hazards[GetIndex(row, column)];
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
        int tin = 0;
        int silver = 0;
        int gold = 0;
        int relic = 0;
        int bedrock = 0;
        int gas = 0;

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
                    case MotherloadCellMaterial.Tin:
                        tin++;
                        break;
                    case MotherloadCellMaterial.Silver:
                        silver++;
                        break;
                    case MotherloadCellMaterial.Gold:
                        gold++;
                        break;
                    case MotherloadCellMaterial.Relic:
                        relic++;
                        break;
                    case MotherloadCellMaterial.Bedrock:
                        bedrock++;
                        break;
                }

                if (GetHazard(row, column) == MotherloadHazardType.GasPocket)
                    gas++;
            }
        }

        return "solid=" + CountSolidCells()
            + ", damaged=" + CountDamagedSolidCells()
            + ", empty=" + empty
            + ", dirt=" + dirt
            + ", stone=" + stone
            + ", copper=" + copper
            + ", tin=" + tin
            + ", silver=" + silver
            + ", gold=" + gold
            + ", relic=" + relic
            + ", bedrock=" + bedrock
            + ", gas=" + gas;
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

    public void SetHazard(int row, int column, MotherloadHazardType hazardType)
    {
        if (!IsInBounds(row, column))
            return;

        hazards[GetIndex(row, column)] = hazardType;
    }

    public bool ClearHazard(int row, int column)
    {
        if (!IsInBounds(row, column))
            return false;

        int index = GetIndex(row, column);
        if (hazards[index] == MotherloadHazardType.None)
            return false;

        hazards[index] = MotherloadHazardType.None;
        return true;
    }

    public bool ClearHazardsInCircle(float centerColumn, float centerRow, int radiusCells)
    {
        bool changed = false;
        radiusCells = Mathf.Max(1, radiusCells);
        int minRow = Mathf.Max(0, Mathf.FloorToInt(centerRow - radiusCells - 1f));
        int maxRow = Mathf.Min(Height - 1, Mathf.CeilToInt(centerRow + radiusCells + 1f));
        int minColumn = Mathf.Max(0, Mathf.FloorToInt(centerColumn - radiusCells - 1f));
        int maxColumn = Mathf.Min(Width - 1, Mathf.CeilToInt(centerColumn + radiusCells + 1f));

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                if (GetHazard(row, column) == MotherloadHazardType.None)
                    continue;

                float dx = (column + 0.5f) - centerColumn;
                float dy = (row + 0.5f) - centerRow;
                if ((dx * dx) + (dy * dy) > radiusCells * radiusCells)
                    continue;

                hazards[GetIndex(row, column)] = MotherloadHazardType.None;
                changed = true;
            }
        }

        return changed;
    }

    public bool DigCircle(int centerColumn, int centerRow, int radiusCells)
    {
        float digDamage = 999999f;
        return ApplyBlast(centerColumn + 0.5f, centerRow + 0.5f, radiusCells, digDamage, digDamage, allowNearestFallback: true, out _);
    }

    public bool ApplyBlast(int centerColumn, int centerRow, int radiusCells, float centerDamage, float outerDamage)
    {
        return ApplyBlast(centerColumn + 0.5f, centerRow + 0.5f, radiusCells, centerDamage, outerDamage, allowNearestFallback: true, out _);
    }

    public bool ApplyBlast(float centerColumn, float centerRow, int radiusCells, float centerDamage, float outerDamage, bool allowNearestFallback)
    {
        return ApplyBlast(centerColumn, centerRow, radiusCells, centerDamage, outerDamage, allowNearestFallback, out _);
    }

    public bool ApplyBlast(float centerColumn, float centerRow, int radiusCells, float centerDamage, float outerDamage, bool allowNearestFallback, out MotherloadOreYield oreYield)
    {
        oreYield = default;
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
                if (ApplyPointDamage(row, column, damage, ref oreYield))
                    changed = true;
            }
        }

        if (changed || touchedSolid)
            return changed;

        if (!allowNearestFallback)
            return false;

        return ForceImpactAtNearestSolidCell(Mathf.RoundToInt(centerRow), Mathf.RoundToInt(centerColumn), centerDamage, ref oreYield);
    }

    private bool ForceImpactAtNearestSolidCell(int centerRow, int centerColumn, float damageAmount, ref MotherloadOreYield oreYield)
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

        return ApplyPointDamage(bestRow, bestColumn, damageAmount, ref oreYield);
    }

    private bool ApplyPointDamage(int row, int column, float damageAmount, ref MotherloadOreYield oreYield)
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

        switch (material)
        {
            case MotherloadCellMaterial.Copper:
                oreYield.Copper++;
                break;
            case MotherloadCellMaterial.Tin:
                oreYield.Tin++;
                break;
            case MotherloadCellMaterial.Silver:
                oreYield.Silver++;
                break;
            case MotherloadCellMaterial.Gold:
                oreYield.Gold++;
                break;
            case MotherloadCellMaterial.Relic:
                oreYield.Relic++;
                break;
        }

        materials[index] = MotherloadCellMaterial.Empty;
        hazards[index] = MotherloadHazardType.None;
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
                return 2f;
            case MotherloadCellMaterial.Tin:
                return 2.35f;
            case MotherloadCellMaterial.Silver:
                return 2.75f;
            case MotherloadCellMaterial.Gold:
                return 3.5f;
            case MotherloadCellMaterial.Relic:
                return 2.2f;
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
