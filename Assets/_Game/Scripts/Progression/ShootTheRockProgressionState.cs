using System;

public class ShootTheRockProgressionState
{
    private int money;
    private int redEssence;
    private int blueEssence;
    private int greenEssence;

    public event Action Changed;

    public int Money => money;
    public int RedEssence => redEssence;
    public int BlueEssence => blueEssence;
    public int GreenEssence => greenEssence;

    public int GetEssence(EssenceType essenceType)
    {
        switch (essenceType)
        {
            case EssenceType.Red:
                return redEssence;
            case EssenceType.Blue:
                return blueEssence;
            case EssenceType.Green:
                return greenEssence;
            default:
                return 0;
        }
    }

    public void AddMoney(int amount)
    {
        if (amount == 0)
            return;

        money = Math.Max(0, money + amount);
        Changed?.Invoke();
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount <= 0)
            return true;
        if (money < amount)
            return false;

        money -= amount;
        Changed?.Invoke();
        return true;
    }

    public void AddEssence(EssenceType essenceType, int amount = 1)
    {
        if (essenceType == EssenceType.None || amount <= 0)
            return;

        switch (essenceType)
        {
            case EssenceType.Red:
                redEssence += amount;
                break;
            case EssenceType.Blue:
                blueEssence += amount;
                break;
            case EssenceType.Green:
                greenEssence += amount;
                break;
        }

        Changed?.Invoke();
    }

    public bool TrySpendEssence(EssenceType essenceType, int amount)
    {
        if (essenceType == EssenceType.None || amount <= 0)
            return true;

        int current = GetEssence(essenceType);
        if (current < amount)
            return false;

        switch (essenceType)
        {
            case EssenceType.Red:
                redEssence -= amount;
                break;
            case EssenceType.Blue:
                blueEssence -= amount;
                break;
            case EssenceType.Green:
                greenEssence -= amount;
                break;
        }

        Changed?.Invoke();
        return true;
    }
}
