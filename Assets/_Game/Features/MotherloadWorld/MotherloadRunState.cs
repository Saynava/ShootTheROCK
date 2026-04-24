using System;
using UnityEngine;

public sealed class MotherloadRunState
{
    public const int CopperValue = 1;
    public const int TinValue = 2;
    public const int SilverValue = 4;
    public const int GoldValue = 8;

    public int CurrentSeed { get; private set; }
    private int copperCargo;
    private int tinCargo;
    private int silverCargo;
    private int goldCargo;

    public int CopperCargo => copperCargo;
    public int TinCargo => tinCargo;
    public int SilverCargo => silverCargo;
    public int GoldCargo => goldCargo;
    public int CargoCapacity { get; private set; }
    public int MaxHull { get; private set; }
    public int CurrentHull { get; private set; }
    public int MaxReachedChunkRow { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool FoundRelicThisRun { get; private set; }
    public bool EmergencySparkUsedThisRun { get; private set; }
    public string LastDeathReason { get; private set; } = string.Empty;

    public int CargoUsed => CopperCargo + TinCargo + SilverCargo + GoldCargo;
    public int CargoFree => Mathf.Max(0, CargoCapacity - CargoUsed);
    public int CargoValue => (CopperCargo * CopperValue) + (TinCargo * TinValue) + (SilverCargo * SilverValue) + (GoldCargo * GoldValue);

    public void ResetForNewRun(int seed, MotherloadMetaProgressionState metaProgression)
    {
        CurrentSeed = seed;
        copperCargo = 0;
        tinCargo = 0;
        silverCargo = 0;
        goldCargo = 0;
        MaxReachedChunkRow = 0;
        FoundRelicThisRun = false;
        EmergencySparkUsedThisRun = false;
        LastDeathReason = string.Empty;
        IsAlive = true;
        RefreshDerivedStats(metaProgression);
        CurrentHull = MaxHull;
    }

    public void RefreshDerivedStats(MotherloadMetaProgressionState metaProgression)
    {
        int cargoRank = metaProgression != null ? metaProgression.GetUpgradeRank(MotherloadUpgradeType.CargoBay) : 0;
        int hullRank = metaProgression != null ? metaProgression.GetUpgradeRank(MotherloadUpgradeType.HullPlating) : 0;
        CargoCapacity = 30 + (cargoRank * 12);
        int previousMaxHull = Mathf.Max(1, MaxHull);
        MaxHull = 6 + (hullRank * 3);
        if (CurrentHull <= 0)
            CurrentHull = MaxHull;
        else if (MaxHull != previousMaxHull)
            CurrentHull = Mathf.Clamp(CurrentHull + (MaxHull - previousMaxHull), 1, MaxHull);
        else
            CurrentHull = Mathf.Clamp(CurrentHull, 1, MaxHull);
    }

    public MotherloadOreYield AddCargo(MotherloadOreYield oreYield)
    {
        MotherloadOreYield accepted = default;
        AddOreCells(ref copperCargo, ref accepted.Copper, oreYield.Copper);
        AddOreCells(ref tinCargo, ref accepted.Tin, oreYield.Tin);
        AddOreCells(ref silverCargo, ref accepted.Silver, oreYield.Silver);
        AddOreCells(ref goldCargo, ref accepted.Gold, oreYield.Gold);
        accepted.Relic = oreYield.Relic;
        return accepted;
    }

    public int SellCargo()
    {
        int value = CargoValue;
        copperCargo = 0;
        tinCargo = 0;
        silverCargo = 0;
        goldCargo = 0;
        return value;
    }

    public void RepairHull()
    {
        CurrentHull = MaxHull;
    }

    public bool ApplyHullDamage(int amount, string deathReason, MotherloadMetaProgressionState metaProgression)
    {
        if (!IsAlive || amount <= 0)
            return false;

        int resolvedDamage = amount;
        if (metaProgression != null && metaProgression.HasRelic(MotherloadRelicType.SoftLandingModule))
            resolvedDamage = Mathf.Max(1, Mathf.CeilToInt(resolvedDamage * 0.5f));

        CurrentHull = Mathf.Max(0, CurrentHull - resolvedDamage);
        if (CurrentHull > 0)
            return false;

        if (metaProgression != null && metaProgression.HasRelic(MotherloadRelicType.EmergencySpark) && !EmergencySparkUsedThisRun)
        {
            EmergencySparkUsedThisRun = true;
            CurrentHull = 1;
            return false;
        }

        IsAlive = false;
        LastDeathReason = string.IsNullOrWhiteSpace(deathReason) ? "Destroyed" : deathReason;
        return true;
    }

    public void RecordReachedChunkRow(int row)
    {
        MaxReachedChunkRow = Mathf.Max(MaxReachedChunkRow, row);
    }

    public void MarkRelicFound()
    {
        FoundRelicThisRun = true;
    }

    public void SetLastDeathReason(string reason)
    {
        LastDeathReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;
    }

    private void AddOreCells(ref int cargo, ref int accepted, int requested)
    {
        if (requested <= 0 || CargoFree <= 0)
            return;

        int amount = Math.Min(requested, CargoFree);
        cargo += amount;
        accepted += amount;
    }
}
