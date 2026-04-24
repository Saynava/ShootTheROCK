using System;

public sealed class MotherloadMetaProgressionState
{
    private readonly int[] upgradeRanks = new int[5];
    private readonly bool[] relics = new bool[8];

    public int TotalPaidUpgradeRanks
    {
        get
        {
            int total = 0;
            for (int i = 0; i < upgradeRanks.Length; i++)
                total += upgradeRanks[i];
            return total;
        }
    }

    public int GetUpgradeRank(MotherloadUpgradeType upgradeType)
    {
        int index = (int)upgradeType;
        if (index < 0 || index >= upgradeRanks.Length)
            return 0;

        return upgradeRanks[index];
    }

    public int GetNextUpgradeCost(MotherloadUpgradeType upgradeType)
    {
        switch (GetUpgradeRank(upgradeType))
        {
            case 0:
                return 120;
            case 1:
                return 280;
            case 2:
                return 600;
            case 3:
                return 1200;
            default:
                return 0;
        }
    }

    public bool CanUpgrade(MotherloadUpgradeType upgradeType)
    {
        return GetUpgradeRank(upgradeType) < 4;
    }

    public bool TryUpgrade(MotherloadUpgradeType upgradeType)
    {
        if (!CanUpgrade(upgradeType))
            return false;

        upgradeRanks[(int)upgradeType]++;
        return true;
    }

    public bool HasRelic(MotherloadRelicType relicType)
    {
        int index = (int)relicType;
        return index > 0 && index < relics.Length && relics[index];
    }

    public bool TryAddRelic(MotherloadRelicType relicType)
    {
        int index = (int)relicType;
        if (index <= 0 || index >= relics.Length || relics[index])
            return false;

        relics[index] = true;
        return true;
    }

    public string BuildRelicSummary()
    {
        string summary = string.Empty;
        for (int i = 1; i < relics.Length; i++)
        {
            if (!relics[i])
                continue;

            if (summary.Length > 0)
                summary += ", ";
            summary += ((MotherloadRelicType)i).ToString();
        }

        return summary.Length > 0 ? summary : "none";
    }
}
