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

    public float tickInterval;
    public float damagePerTick;
    public float blastRadiusScale;
    public float nextTickTime;
    public float expireTime;
    public bool allowDestroyCells;

    public RockWallEffectRuntime(RockWallEffectType effectType, int row, int column, float now, float duration, float tickInterval, float damagePerTick, float blastRadiusScale, bool allowDestroyCells)
    {
        this.effectType = effectType;
        this.row = row;
        this.column = column;
        this.tickInterval = Mathf.Max(0.02f, tickInterval);
        this.damagePerTick = Mathf.Max(0.01f, damagePerTick);
        this.blastRadiusScale = Mathf.Max(0.25f, blastRadiusScale);
        this.nextTickTime = now + this.tickInterval;
        this.expireTime = now + Mathf.Max(this.tickInterval, duration);
        this.allowDestroyCells = allowDestroyCells;
    }

    public void Refresh(float now, float duration, float tickInterval, float damagePerTick, float blastRadiusScale, bool allowDestroyCells)
    {
        this.tickInterval = Mathf.Min(this.tickInterval, Mathf.Max(0.02f, tickInterval));
        this.damagePerTick = Mathf.Max(this.damagePerTick, Mathf.Max(0.01f, damagePerTick));
        this.blastRadiusScale = Mathf.Max(this.blastRadiusScale, Mathf.Max(0.25f, blastRadiusScale));
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
