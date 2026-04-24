using UnityEngine;

[DisallowMultipleComponent]
public sealed class MotherloadPlayerVitals : MonoBehaviour
{
    private MotherloadWorldController worldController;

    public void Initialize(MotherloadWorldController worldController)
    {
        this.worldController = worldController;
    }

    public void ApplyDamage(int amount, string reason)
    {
        if (worldController != null)
            worldController.ApplyPlayerHullDamage(amount, reason);
    }
}
