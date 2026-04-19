using UnityEngine;

public enum RockWallEffectType
{
    Corrosion = 0,
    Burn = 1,
    Fracture = 2,
    Freeze = 3,
}

public sealed class RockWallEffectRuntime
{
    public readonly RockWallEffectType effectType;
    public readonly int row;
    public readonly int column;
    public readonly int originRow;
    public readonly int originColumn;

    public float tickInterval;
    public float damagePerTick;
    public float blastRadiusScale;
    public float maxSpreadDistance;
    public float nextTickTime;
    public float expireTime;
    public bool allowDestroyCells;

    public RockWallEffectRuntime(RockWallEffectType effectType, int row, int column, float now, float duration, float tickInterval, float damagePerTick, float blastRadiusScale, bool allowDestroyCells, int originRow = -1, int originColumn = -1, float maxSpreadDistance = -1f)
    {
        this.effectType = effectType;
        this.row = row;
        this.column = column;
        this.originRow = originRow >= 0 ? originRow : row;
        this.originColumn = originColumn >= 0 ? originColumn : column;
        this.tickInterval = Mathf.Max(0.02f, tickInterval);
        this.damagePerTick = Mathf.Max(0.01f, damagePerTick);
        this.blastRadiusScale = Mathf.Max(0.25f, blastRadiusScale);
        this.maxSpreadDistance = Mathf.Max(1f, maxSpreadDistance > 0f ? maxSpreadDistance : this.blastRadiusScale * 2f);
        this.nextTickTime = now + this.tickInterval;
        this.expireTime = now + Mathf.Max(this.tickInterval, duration);
        this.allowDestroyCells = allowDestroyCells;
    }

    public void Refresh(float now, float duration, float tickInterval, float damagePerTick, float blastRadiusScale, bool allowDestroyCells, float maxSpreadDistance = -1f)
    {
        this.tickInterval = Mathf.Min(this.tickInterval, Mathf.Max(0.02f, tickInterval));
        this.damagePerTick = Mathf.Max(this.damagePerTick, Mathf.Max(0.01f, damagePerTick));
        this.blastRadiusScale = Mathf.Max(this.blastRadiusScale, Mathf.Max(0.25f, blastRadiusScale));
        this.maxSpreadDistance = Mathf.Max(this.maxSpreadDistance, Mathf.Max(1f, maxSpreadDistance > 0f ? maxSpreadDistance : this.blastRadiusScale * 2f));
        this.expireTime = Mathf.Max(this.expireTime, now + Mathf.Max(this.tickInterval, duration));
        this.nextTickTime = Mathf.Min(this.nextTickTime, now + this.tickInterval);
        this.allowDestroyCells |= allowDestroyCells;
    }

    public bool IsDue(float now)
    {
        return now >= nextTickTime;
    }

    public bool IsExpired(float now)
    {
        return now >= expireTime;
    }

    public void ScheduleNextTick(float now)
    {
        nextTickTime = now + tickInterval;
    }
}
