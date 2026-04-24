using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MotherloadBaseZone : MonoBehaviour
{
    [SerializeField] private MoneyHud moneyHud;
    [SerializeField] private MotherloadWorldController worldController;

    private int playerOverlapCount;
    private CannonAim playerCannon;
    private bool shopOpen;
    private TextMesh shopPromptText;

    public void Initialize(MoneyHud moneyHud)
    {
        this.moneyHud = moneyHud;
        if (worldController == null)
            worldController = GetComponentInParent<MotherloadWorldController>();
        EnsureShopPrompt();
        ApplyBaseState();
    }

    private void OnEnable()
    {
        ApplyBaseState();
    }

    private void OnDisable()
    {
        if (playerCannon != null)
            playerCannon.SetDockedAtBase(false);

        shopOpen = false;
        playerOverlapCount = 0;
        playerCannon = null;
        ApplyBaseState();
    }

    private void Update()
    {
        if (playerOverlapCount <= 0 || Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame)
            return;

        shopOpen = !shopOpen;
        ApplyBaseState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CannonAim cannon = ResolvePlayerCannon(other);
        if (cannon == null)
            return;

        playerCannon = cannon;
        playerOverlapCount++;
        ApplyBaseState();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CannonAim cannon = ResolvePlayerCannon(other);
        if (cannon == null)
            return;

        playerCannon = cannon;
        if (playerOverlapCount <= 0)
            playerOverlapCount = 1;

        ApplyBaseState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        CannonAim cannon = ResolvePlayerCannon(other);
        if (cannon == null)
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount <= 0)
        {
            shopOpen = false;
            cannon.SetDockedAtBase(false);
            if (ReferenceEquals(playerCannon, cannon))
                playerCannon = null;
        }

        ApplyBaseState();
    }

    private CannonAim ResolvePlayerCannon(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<CannonAim>() : null;
    }

    private void ApplyBaseState()
    {
        bool playerInside = playerOverlapCount > 0;

        if (moneyHud != null)
            moneyHud.SetUpgradeUiVisible(playerInside && shopOpen);

        if (shopPromptText != null)
        {
            shopPromptText.gameObject.SetActive(playerInside);
            shopPromptText.text = shopOpen ? "SHOP OPEN  [B]" : "PRESS B  SHOP";
            shopPromptText.color = shopOpen ? new Color(0.6f, 1f, 0.75f, 1f) : new Color(1f, 0.92f, 0.35f, 1f);
        }

        if (playerCannon != null)
        {
            playerCannon.SetDockedAtBase(playerInside);
            if (playerInside && worldController != null)
                worldController.HandlePlayerDockedAtBase(playerCannon);
        }
    }

    private void EnsureShopPrompt()
    {
        if (shopPromptText != null)
            return;

        Transform existing = transform.Find("BaseShopPrompt");
        if (existing != null)
            shopPromptText = existing.GetComponent<TextMesh>();

        if (shopPromptText == null)
        {
            GameObject promptObject = new GameObject("BaseShopPrompt", typeof(TextMesh));
            promptObject.transform.SetParent(transform, false);
            shopPromptText = promptObject.GetComponent<TextMesh>();
        }

        Transform promptTransform = shopPromptText.transform;
        promptTransform.localPosition = new Vector3(0f, 0.95f, -0.05f);
        promptTransform.localRotation = Quaternion.identity;
        promptTransform.localScale = new Vector3(0.12f, 0.12f, 1f);

        shopPromptText.anchor = TextAnchor.MiddleCenter;
        shopPromptText.alignment = TextAlignment.Center;
        shopPromptText.fontSize = 42;
        shopPromptText.fontStyle = FontStyle.Bold;
        shopPromptText.characterSize = 1f;
        shopPromptText.color = new Color(1f, 0.92f, 0.35f, 1f);
        MeshRenderer promptRenderer = shopPromptText.GetComponent<MeshRenderer>();
        if (promptRenderer != null)
            promptRenderer.sortingOrder = 30;
        shopPromptText.gameObject.SetActive(false);
    }
}
