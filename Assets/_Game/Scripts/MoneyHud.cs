using UnityEngine;
using UnityEngine.UI;

public class MoneyHud : MonoBehaviour
{
    private const string UpgradePanelName = "UpgradePanel";
    private const string UpgradeStatsName = "UpgradeStats";
    private const string AttackSpeedUpgradeButtonName = "AttackSpeedUpgradeButton";
    private const string DamageUpgradeButtonName = "DamageUpgradeButton";
    private const string NextLevelButtonName = "NextLevelButton";
    private const string StressShotgunButtonName = "StressShotgunButton";
    private const string AirburstGrenadeButtonName = "AirburstGrenadeButton";
    private const string LegacyComboButtonName = "ScatterGrenadeButton";
    private const string UpgradeButtonTextName = "Text";

    private int money;
    private Text moneyText;
    private AutoShooter shooter;
    private RockWall rockWall;
    private CameraFramingController cameraFramingController;
    private Text upgradeStatsText;
    private Button attackSpeedUpgradeButton;
    private Text attackSpeedUpgradeButtonText;
    private Button damageUpgradeButton;
    private Text damageUpgradeButtonText;
    private Button nextLevelButton;
    private Text nextLevelButtonText;
    private Button stressShotgunButton;
    private Text stressShotgunButtonText;
    private Button airburstGrenadeButton;
    private Text airburstGrenadeButtonText;

    public void Initialize(Text moneyText)
    {
        this.moneyText = moneyText;
        EnsureUpgradeUi();
        Refresh();
    }

    public void BindShooter(AutoShooter shooter)
    {
        this.shooter = shooter;
        EnsureUpgradeUi();
        Refresh();
    }

    public void BindProgression(RockWall rockWall, CameraFramingController cameraFramingController)
    {
        this.rockWall = rockWall;
        this.cameraFramingController = cameraFramingController;
        EnsureUpgradeUi();
        Refresh();
    }

    public void AddMoney(int amount)
    {
        money += amount;
        Refresh();
    }

    private void TryBuyAttackSpeedUpgrade()
    {
        if (shooter == null || !shooter.CanUpgradeAttackSpeed)
            return;

        int cost = shooter.NextAttackSpeedUpgradeCost;
        if (money < cost)
            return;

        if (!shooter.TryUpgradeAttackSpeed())
            return;

        money -= cost;
        Refresh();
    }

    private void TryBuyDamageUpgrade()
    {
        if (shooter == null || !shooter.CanUpgradeDamage)
            return;

        int cost = shooter.NextDamageUpgradeCost;
        if (money < cost)
            return;

        if (!shooter.TryUpgradeDamage())
            return;

        money -= cost;
        Refresh();
    }

    private void TryAdvanceLevelTest()
    {
        if (rockWall == null)
            return;
        if (!rockWall.TryAdvanceLevel())
            return;

        if (cameraFramingController != null)
            cameraFramingController.AnimateToCurrentFrame();

        Refresh();
    }

    private void ToggleStressShotgun()
    {
        if (shooter == null)
            return;

        shooter.ToggleStressShotgun();
        Refresh();
    }

    private void ToggleAirburstGrenade()
    {
        if (shooter == null)
            return;

        shooter.ToggleAirburstGrenade();
        Refresh();
    }

    private void Refresh()
    {
        if (moneyText != null)
            moneyText.text = "$" + money;

        EnsureUpgradeUi();
        RefreshStatsText();
        RefreshButtons();
    }

    private void RefreshStatsText()
    {
        if (upgradeStatsText == null)
            return;

        string levelText = rockWall == null
            ? "LVL ?/?"
            : rockWall.CurrentLevelLabel + "  (" + rockWall.CurrentLevelNumber + "/" + rockWall.TotalLevelCount + ")";

        if (shooter == null)
        {
            upgradeStatsText.text = levelText + "\nCANNON\nWaiting for cannon";
            return;
        }

        upgradeStatsText.text =
            levelText + "\n" +
            "ATK SPD  LVL " + shooter.AttackSpeedLevel +
            "  |  RATE " + shooter.CurrentFireInterval.ToString("0.00") + "s\n" +
            "DMG  LVL " + shooter.DamageLevel +
            "  |  BLAST x" + shooter.BlastRadiusScale.ToString("0.00") + "\n" +
            "DEBUG  SG " + (shooter.StressShotgunEnabled ? "ON" : "OFF") +
            "  |  GRENADE " + (shooter.AirburstGrenadeEnabled ? "ON" : "OFF") + "\n" +
            "COMBO " + ((shooter.StressShotgunEnabled && shooter.AirburstGrenadeEnabled) ? "ON" : "OFF") +
            "  |  RATE " + shooter.CurrentEffectiveFireInterval.ToString("0.00") + "s";
    }

    private void RefreshButtons()
    {
        if (attackSpeedUpgradeButton != null)
        {
            bool canUpgradeAttackSpeed = shooter != null && shooter.CanUpgradeAttackSpeed && money >= shooter.NextAttackSpeedUpgradeCost;
            attackSpeedUpgradeButton.interactable = canUpgradeAttackSpeed;
        }

        if (damageUpgradeButton != null)
        {
            bool canUpgradeDamage = shooter != null && shooter.CanUpgradeDamage && money >= shooter.NextDamageUpgradeCost;
            damageUpgradeButton.interactable = canUpgradeDamage;
        }

        if (nextLevelButton != null)
            nextLevelButton.interactable = rockWall != null && rockWall.CanAdvanceLevel;

        if (stressShotgunButton != null)
            stressShotgunButton.interactable = shooter != null;

        if (airburstGrenadeButton != null)
            airburstGrenadeButton.interactable = shooter != null;

        if (attackSpeedUpgradeButtonText != null)
        {
            if (shooter == null)
                attackSpeedUpgradeButtonText.text = "NO CANNON";
            else if (!shooter.CanUpgradeAttackSpeed)
                attackSpeedUpgradeButtonText.text = "ATK SPD MAX";
            else
                attackSpeedUpgradeButtonText.text = "BUY ATK SPD  $" + shooter.NextAttackSpeedUpgradeCost;
        }

        if (damageUpgradeButtonText != null)
        {
            if (shooter == null)
                damageUpgradeButtonText.text = "NO CANNON";
            else if (!shooter.CanUpgradeDamage)
                damageUpgradeButtonText.text = "DMG MAX";
            else
                damageUpgradeButtonText.text = "BUY DMG  $" + shooter.NextDamageUpgradeCost;
        }

        if (nextLevelButtonText != null)
        {
            if (rockWall == null)
                nextLevelButtonText.text = "NEXT LVL (NO WALL)";
            else if (!rockWall.CanAdvanceLevel)
                nextLevelButtonText.text = "FINAL LVL";
            else
                nextLevelButtonText.text = "TEST NEXT LVL";
        }

        if (stressShotgunButtonText != null)
        {
            if (shooter == null)
                stressShotgunButtonText.text = "SHOTGUN (NO CANNON)";
            else if (shooter.StressShotgunEnabled)
                stressShotgunButtonText.text = "DEBUG SHOTGUN 0.10s  ON";
            else
                stressShotgunButtonText.text = "DEBUG SHOTGUN 0.10s  OFF";
        }

        if (airburstGrenadeButtonText != null)
        {
            if (shooter == null)
                airburstGrenadeButtonText.text = "GRENADE (NO CANNON)";
            else if (shooter.AirburstGrenadeEnabled)
                airburstGrenadeButtonText.text = "AIRBURST GRENADE  ON";
            else
                airburstGrenadeButtonText.text = "AIRBURST GRENADE  OFF";
        }
    }

    private void EnsureUpgradeUi()
    {
        if (upgradeStatsText != null && attackSpeedUpgradeButton != null && attackSpeedUpgradeButtonText != null && damageUpgradeButton != null && damageUpgradeButtonText != null && nextLevelButton != null && nextLevelButtonText != null && stressShotgunButton != null && stressShotgunButtonText != null && airburstGrenadeButton != null && airburstGrenadeButtonText != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        Transform panelTransform = transform.Find(UpgradePanelName);
        if (panelTransform == null)
            panelTransform = CreateUpgradePanel(canvas.transform).transform;
        ApplyPanelLayout(panelTransform as RectTransform);

        RemoveLegacyComboButton(panelTransform);

        Transform statsTransform = panelTransform.Find(UpgradeStatsName);
        if (statsTransform == null)
            statsTransform = CreateStatsText(panelTransform).transform;
        ApplyStatsLayout(statsTransform as RectTransform);
        upgradeStatsText = statsTransform.GetComponent<Text>();

        Transform attackButtonTransform = panelTransform.Find(AttackSpeedUpgradeButtonName);
        if (attackButtonTransform == null)
            attackButtonTransform = CreateUpgradeButton(panelTransform, AttackSpeedUpgradeButtonName, new Vector2(12f, 112f), new Vector2(-12f, 152f)).transform;
        attackSpeedUpgradeButton = attackButtonTransform.GetComponent<Button>();
        ApplyButtonLayout(attackButtonTransform, new Vector2(12f, 212f), new Vector2(-12f, 252f));

        Transform attackButtonTextTransform = attackButtonTransform.Find(UpgradeButtonTextName);
        if (attackButtonTextTransform == null)
            attackButtonTextTransform = CreateUpgradeButtonText(attackButtonTransform).transform;
        attackSpeedUpgradeButtonText = attackButtonTextTransform.GetComponent<Text>();

        Transform damageButtonTransform = panelTransform.Find(DamageUpgradeButtonName);
        if (damageButtonTransform == null)
            damageButtonTransform = CreateUpgradeButton(panelTransform, DamageUpgradeButtonName, new Vector2(12f, 62f), new Vector2(-12f, 102f)).transform;
        damageUpgradeButton = damageButtonTransform.GetComponent<Button>();
        ApplyButtonLayout(damageButtonTransform, new Vector2(12f, 162f), new Vector2(-12f, 202f));

        Transform damageButtonTextTransform = damageButtonTransform.Find(UpgradeButtonTextName);
        if (damageButtonTextTransform == null)
            damageButtonTextTransform = CreateUpgradeButtonText(damageButtonTransform).transform;
        damageUpgradeButtonText = damageButtonTextTransform.GetComponent<Text>();

        Transform nextLevelTransform = panelTransform.Find(NextLevelButtonName);
        if (nextLevelTransform == null)
            nextLevelTransform = CreateUpgradeButton(panelTransform, NextLevelButtonName, new Vector2(12f, 62f), new Vector2(-12f, 102f)).transform;
        nextLevelButton = nextLevelTransform.GetComponent<Button>();
        ApplyButtonLayout(nextLevelTransform, new Vector2(12f, 112f), new Vector2(-12f, 152f));

        Transform nextLevelTextTransform = nextLevelTransform.Find(UpgradeButtonTextName);
        if (nextLevelTextTransform == null)
            nextLevelTextTransform = CreateUpgradeButtonText(nextLevelTransform).transform;
        nextLevelButtonText = nextLevelTextTransform.GetComponent<Text>();

        Transform stressShotgunTransform = panelTransform.Find(StressShotgunButtonName);
        if (stressShotgunTransform == null)
            stressShotgunTransform = CreateUpgradeButton(panelTransform, StressShotgunButtonName, new Vector2(12f, 112f), new Vector2(-12f, 152f)).transform;
        stressShotgunButton = stressShotgunTransform.GetComponent<Button>();
        ApplyButtonLayout(stressShotgunTransform, new Vector2(12f, 62f), new Vector2(-12f, 102f));

        Transform stressShotgunTextTransform = stressShotgunTransform.Find(UpgradeButtonTextName);
        if (stressShotgunTextTransform == null)
            stressShotgunTextTransform = CreateUpgradeButtonText(stressShotgunTransform).transform;
        stressShotgunButtonText = stressShotgunTextTransform.GetComponent<Text>();

        Transform airburstGrenadeTransform = panelTransform.Find(AirburstGrenadeButtonName);
        if (airburstGrenadeTransform == null)
            airburstGrenadeTransform = CreateUpgradeButton(panelTransform, AirburstGrenadeButtonName, new Vector2(12f, 12f), new Vector2(-12f, 52f)).transform;
        airburstGrenadeButton = airburstGrenadeTransform.GetComponent<Button>();
        ApplyButtonLayout(airburstGrenadeTransform, new Vector2(12f, 12f), new Vector2(-12f, 52f));

        Transform airburstGrenadeTextTransform = airburstGrenadeTransform.Find(UpgradeButtonTextName);
        if (airburstGrenadeTextTransform == null)
            airburstGrenadeTextTransform = CreateUpgradeButtonText(airburstGrenadeTransform).transform;
        airburstGrenadeButtonText = airburstGrenadeTextTransform.GetComponent<Text>();

        attackSpeedUpgradeButton.onClick.RemoveListener(TryBuyAttackSpeedUpgrade);
        attackSpeedUpgradeButton.onClick.AddListener(TryBuyAttackSpeedUpgrade);
        damageUpgradeButton.onClick.RemoveListener(TryBuyDamageUpgrade);
        damageUpgradeButton.onClick.AddListener(TryBuyDamageUpgrade);
        nextLevelButton.onClick.RemoveListener(TryAdvanceLevelTest);
        nextLevelButton.onClick.AddListener(TryAdvanceLevelTest);
        stressShotgunButton.onClick.RemoveListener(ToggleStressShotgun);
        stressShotgunButton.onClick.AddListener(ToggleStressShotgun);
        airburstGrenadeButton.onClick.RemoveListener(ToggleAirburstGrenade);
        airburstGrenadeButton.onClick.AddListener(ToggleAirburstGrenade);
    }

    private GameObject CreateUpgradePanel(Transform parent)
    {
        GameObject panelObject = new GameObject(UpgradePanelName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        ApplyPanelLayout(rect);

        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        return panelObject;
    }

    private GameObject CreateStatsText(Transform parent)
    {
        GameObject textObject = new GameObject(UpgradeStatsName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        ApplyStatsLayout(rect);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        return textObject;
    }

    private void ApplyPanelLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -130f);
        rect.sizeDelta = new Vector2(380f, 510f);
    }

    private void ApplyStatsLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(12f, -494f);
        rect.offsetMax = new Vector2(-12f, -270f);
    }

    private void RemoveLegacyComboButton(Transform panelTransform)
    {
        Transform legacyComboTransform = panelTransform.Find(LegacyComboButtonName);
        if (legacyComboTransform == null)
            return;

        if (Application.isPlaying)
            Destroy(legacyComboTransform.gameObject);
        else
            DestroyImmediate(legacyComboTransform.gameObject);
    }

    private GameObject CreateUpgradeButton(Transform parent, string buttonName, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ApplyButtonLayout(buttonObject.transform, offsetMin, offsetMax);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.18f);

        ColorBlock colors = buttonObject.GetComponent<Button>().colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.18f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.28f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.38f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.08f);
        buttonObject.GetComponent<Button>().colors = colors;
        return buttonObject;
    }

    private void ApplyButtonLayout(Transform buttonTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = buttonTransform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private GameObject CreateUpgradeButtonText(Transform parent)
    {
        GameObject textObject = new GameObject(UpgradeButtonTextName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return textObject;
    }
}
