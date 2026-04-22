using UnityEngine;

[DisallowMultipleComponent]
public class MotherloadBaseZone : MonoBehaviour
{
    [SerializeField] private MoneyHud moneyHud;

    private int playerOverlapCount;

    public void Initialize(MoneyHud moneyHud)
    {
        this.moneyHud = moneyHud;
        ApplyUpgradeVisibility();
    }

    private void OnEnable()
    {
        ApplyUpgradeVisibility();
    }

    private void OnDisable()
    {
        playerOverlapCount = 0;
        ApplyUpgradeVisibility();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerOverlapCount++;
        ApplyUpgradeVisibility();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (playerOverlapCount <= 0)
            playerOverlapCount = 1;

        ApplyUpgradeVisibility();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        ApplyUpgradeVisibility();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        return other != null && other.GetComponentInParent<CannonAim>() != null;
    }

    private void ApplyUpgradeVisibility()
    {
        if (moneyHud != null)
            moneyHud.SetUpgradeUiVisible(playerOverlapCount > 0);
    }
}
