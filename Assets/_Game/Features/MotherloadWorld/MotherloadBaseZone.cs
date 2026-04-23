using UnityEngine;

[DisallowMultipleComponent]
public class MotherloadBaseZone : MonoBehaviour
{
    [SerializeField] private MoneyHud moneyHud;

    private int playerOverlapCount;
    private CannonAim playerCannon;

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
        if (playerCannon != null)
            playerCannon.SetDockedAtBase(false);

        playerOverlapCount = 0;
        playerCannon = null;
        ApplyUpgradeVisibility();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CannonAim cannon = ResolvePlayerCannon(other);
        if (cannon == null)
            return;

        playerCannon = cannon;
        playerOverlapCount++;
        ApplyUpgradeVisibility();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CannonAim cannon = ResolvePlayerCannon(other);
        if (cannon == null)
            return;

        playerCannon = cannon;
        if (playerOverlapCount <= 0)
            playerOverlapCount = 1;

        ApplyUpgradeVisibility();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        CannonAim cannon = ResolvePlayerCannon(other);
        if (cannon == null)
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount <= 0)
        {
            cannon.SetDockedAtBase(false);
            if (ReferenceEquals(playerCannon, cannon))
                playerCannon = null;
        }

        ApplyUpgradeVisibility();
    }

    private CannonAim ResolvePlayerCannon(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<CannonAim>() : null;
    }

    private void ApplyUpgradeVisibility()
    {
        bool playerInside = playerOverlapCount > 0;

        if (moneyHud != null)
            moneyHud.SetUpgradeUiVisible(playerInside);

        if (playerCannon != null)
            playerCannon.SetDockedAtBase(playerInside);
    }
}
