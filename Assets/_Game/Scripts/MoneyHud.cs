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
    private const string CorrosionButtonName = "CorrosionButton";
    private const string BlastScaleSliderRowName = "BlastScaleSliderRow";
    private const string CorrosionRadiusSliderRowName = "CorrosionRadiusSliderRow";
    private const string StressRateSliderRowName = "StressRateSliderRow";
    private const string StressPelletSliderRowName = "StressPelletSliderRow";
    private const string FragmentCountSliderRowName = "FragmentCountSliderRow";
    private const string LegacyComboButtonName = "ScatterGrenadeButton";
    private const string UpgradeButtonTextName = "Text";
    private const string SliderLabelName = "Label";
    private const string SliderControlName = "Slider";
    private const string FuelHudRootName = "FuelHud";
    private const string FuelLabelName = "FuelLabel";
    private const string MotherloadStatusLabelName = "MotherloadStatusLabel";
    private const string FuelBarBackgroundName = "FuelBarBackground";
    private const string FuelFillName = "FuelFill";
    private const string GameOverRootName = "GameOverBanner";
    private const string GameOverTextName = "GameOverText";
    private const float GameOverDurationSeconds = 2.4f;

    private ShootTheRockProgressionState progressionState;
    private Text moneyText;
    private CannonAim cannonAim;
    private GameObject fuelHudRoot;
    private Text fuelLabel;
    private Text motherloadStatusLabel;
    private Image fuelFillImage;
    private RectTransform fuelFillRect;
    private GameObject gameOverRoot;
    private Text gameOverText;
    private float gameOverHideTime = -1f;
    private AutoShooter shooter;
    private RockWall rockWall;
    private MotherloadWorldController motherloadWorldController;
    private CameraFramingController cameraFramingController;
    private GameObject upgradePanel;
    private bool upgradeUiVisible = true;
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
    private Button corrosionButton;
    private Text corrosionButtonText;
    private Slider blastScaleSlider;
    private Text blastScaleSliderLabel;
    private Slider corrosionRadiusSlider;
    private Text corrosionRadiusSliderLabel;
    private Slider stressRateSlider;
    private Text stressRateSliderLabel;
    private Slider stressPelletSlider;
    private Text stressPelletSliderLabel;
    private Slider fragmentCountSlider;
    private Text fragmentCountSliderLabel;

    public ShootTheRockProgressionState ProgressionState
    {
        get
        {
            if (progressionState == null)
                BindProgressionState(new ShootTheRockProgressionState());

            return progressionState;
        }
    }

    private bool IsMotherloadShopMode => cannonAim != null && cannonAim.FuelSystemEnabled && rockWall == null;
    private bool HasMotherloadWorld => motherloadWorldController != null;

    public void Initialize(Text moneyText)
    {
        this.moneyText = moneyText;
        if (progressionState == null)
            BindProgressionState(new ShootTheRockProgressionState());
        EnsureUpgradeUi();
        Refresh();
    }

    public void BindProgressionState(ShootTheRockProgressionState progressionState)
    {
        if (ReferenceEquals(this.progressionState, progressionState) && progressionState != null)
            return;

        if (this.progressionState != null)
            this.progressionState.Changed -= HandleProgressionStateChanged;

        this.progressionState = progressionState ?? new ShootTheRockProgressionState();
        this.progressionState.Changed += HandleProgressionStateChanged;
        Refresh();
    }

    public void BindShooter(AutoShooter shooter)
    {
        this.shooter = shooter;
        EnsureUpgradeUi();
        Refresh();
    }

    public void BindCannon(CannonAim cannonAim)
    {
        if (ReferenceEquals(this.cannonAim, cannonAim))
        {
            RefreshFuelUi();
            return;
        }

        if (this.cannonAim != null)
            this.cannonAim.FuelChanged -= HandleFuelChanged;

        this.cannonAim = cannonAim;

        if (this.cannonAim != null)
            this.cannonAim.FuelChanged += HandleFuelChanged;

        EnsureFuelUi();
        RefreshFuelUi();
    }

    public void BindMotherloadWorld(MotherloadWorldController worldController)
    {
        motherloadWorldController = worldController;
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

    public void SetUpgradeUiVisible(bool visible)
    {
        upgradeUiVisible = visible;
        EnsureUpgradeUi();
        ApplyUpgradePanelVisibility();
    }

    public void AddMoney(int amount)
    {
        ProgressionState.AddMoney(amount);
    }

    public void ShowGameOver(string reason)
    {
        EnsureGameOverUi();
        if (gameOverRoot == null || gameOverText == null)
            return;

        string resolvedReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : "\n" + reason.ToUpperInvariant();
        gameOverText.text = "GAME OVER" + resolvedReason;
        gameOverRoot.SetActive(true);
        gameOverRoot.transform.SetAsLastSibling();
        gameOverHideTime = Time.unscaledTime + GameOverDurationSeconds;
    }

    private void Update()
    {
        if (gameOverRoot == null || !gameOverRoot.activeSelf || gameOverHideTime < 0f)
            return;
        if (Time.unscaledTime < gameOverHideTime)
            return;

        gameOverRoot.SetActive(false);
        gameOverHideTime = -1f;
    }

    private void TryBuyAttackSpeedUpgrade()
    {
        if (IsMotherloadShopMode && HasMotherloadWorld)
        {
            TryBuyMotherloadUpgrade(MotherloadUpgradeType.CargoBay);
            return;
        }

        if (IsMotherloadShopMode)
        {
            TryBuyFuelTankRank(1);
            return;
        }

        if (shooter == null || !shooter.CanUpgradeAttackSpeed)
            return;

        int cost = shooter.NextAttackSpeedUpgradeCost;
        if (ProgressionState.Money < cost)
            return;

        if (!shooter.TryUpgradeAttackSpeed())
            return;

        if (!ProgressionState.TrySpendMoney(cost))
            return;
    }

    private void TryBuyDamageUpgrade()
    {
        if (IsMotherloadShopMode && HasMotherloadWorld)
        {
            TryBuyMotherloadUpgrade(MotherloadUpgradeType.FuelTank);
            return;
        }

        if (IsMotherloadShopMode)
        {
            TryBuyFuelTankRank(2);
            return;
        }

        if (shooter == null || !shooter.CanUpgradeDamage)
            return;

        int cost = shooter.NextDamageUpgradeCost;
        if (ProgressionState.Money < cost)
            return;

        if (!shooter.TryUpgradeDamage())
            return;

        if (!ProgressionState.TrySpendMoney(cost))
            return;
    }

    private void TryAdvanceLevelTest()
    {
        if (IsMotherloadShopMode && HasMotherloadWorld)
        {
            TryBuyMotherloadUpgrade(MotherloadUpgradeType.HullPlating);
            return;
        }

        if (IsMotherloadShopMode)
        {
            TryBuyFuelTankRank(3);
            return;
        }

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
        if (IsMotherloadShopMode && HasMotherloadWorld)
        {
            TryBuyMotherloadUpgrade(MotherloadUpgradeType.BlasterDamage);
            return;
        }

        if (IsMotherloadShopMode)
        {
            TryBuyFuelTankRank(4);
            return;
        }

        if (shooter == null)
            return;

        shooter.ToggleStressShotgun();
        Refresh();
    }

    private void ToggleAirburstGrenade()
    {
        if (IsMotherloadShopMode && HasMotherloadWorld)
        {
            TryBuyMotherloadUpgrade(MotherloadUpgradeType.FireRate);
            return;
        }

        if (IsMotherloadShopMode)
        {
            TryBuyFuelRefill();
            return;
        }

        if (shooter == null)
            return;

        shooter.ToggleAirburstGrenade();
        Refresh();
    }

    private void ToggleCorrosion()
    {
        if (IsMotherloadShopMode)
            return;

        if (shooter == null)
            return;

        shooter.ToggleCorrosion();
        Refresh();
    }

    private void OnBlastScaleSliderChanged(float value)
    {
        if (shooter == null)
            return;

        shooter.SetDebugBlastScaleMultiplier(value);
        Refresh();
    }

    private void OnCorrosionRadiusSliderChanged(float value)
    {
        if (shooter == null)
            return;

        shooter.SetCorrosionRadiusMultiplier(value);
        Refresh();
    }

    private void OnStressRateSliderChanged(float value)
    {
        if (shooter == null)
            return;

        shooter.SetStressShotgunFireInterval(value);
        Refresh();
    }

    private void OnStressPelletSliderChanged(float value)
    {
        if (shooter == null)
            return;

        shooter.SetStressShotgunPelletCount(value);
        Refresh();
    }

    private void OnFragmentCountSliderChanged(float value)
    {
        if (shooter == null)
            return;

        shooter.SetAirburstFragmentCount(value);
        Refresh();
    }

    public void Refresh()
    {
        if (moneyText != null)
            moneyText.text = "$" + ProgressionState.Money;

        EnsureFuelUi();
        RefreshFuelUi();
        EnsureUpgradeUi();
        ApplyUpgradePanelVisibility();
        RefreshStatsText();
        RefreshButtons();
        RefreshTuningControls();
    }

    private void RefreshStatsText()
    {
        if (upgradeStatsText == null)
            return;

        if (IsMotherloadShopMode)
        {
            if (HasMotherloadWorld)
            {
                upgradeStatsText.text = motherloadWorldController.BuildMotherloadHudSummary();
                return;
            }

            upgradeStatsText.text =
                "BASE SHOP\n" +
                "TANK RANK " + (cannonAim != null ? cannonAim.FuelTankRank : 1) + "/4" +
                "  |  CAP " + (cannonAim != null ? Mathf.CeilToInt(cannonAim.MaxFuel) : 0) + "\n" +
                "FUEL " + (cannonAim != null ? Mathf.CeilToInt(cannonAim.CurrentFuel) : 0) + "/" + (cannonAim != null ? Mathf.CeilToInt(cannonAim.MaxFuel) : 0) +
                (cannonAim != null && cannonAim.IsDockedAtBase ? "  |  DOCKED" : string.Empty) + "\n" +
                "AUTO REFUEL AT BASE\n" +
                "DIRT ORES: COPPER / SILVER / GOLD";
            return;
        }

        string levelText = rockWall == null
            ? "LVL ?/?"
            : rockWall.CurrentLevelLabel + "  (" + rockWall.CurrentLevelNumber + "/" + rockWall.TotalLevelCount + ")";
        string headerText = levelText + "  |  " + BuildEssenceSummaryLine();

        if (shooter == null)
        {
            upgradeStatsText.text =
                headerText + "\n" +
                "CANNON\nWaiting for cannon";
            return;
        }

        upgradeStatsText.text =
            headerText + "\n" +
            "ATK SPD LVL " + shooter.AttackSpeedLevel +
            "  |  BASE " + shooter.CurrentFireInterval.ToString("0.00") + "s\n" +
            "DMG LVL " + shooter.DamageLevel +
            "  |  BASE BLAST x" + shooter.BlastRadiusScale.ToString("0.00") + "\n" +
            "TEST BLAST x" + shooter.DebugBlastScaleMultiplier.ToString("0.00") +
            "  |  TOTAL x" + shooter.CurrentProjectileBlastScale.ToString("0.00") + "\n" +
            "SG " + (shooter.StressShotgunEnabled ? "ON" : "OFF") +
            " @" + shooter.StressShotgunFireInterval.ToString("0.00") + "s" +
            "  |  PELLETS " + shooter.StressShotgunPelletCount + "\n" +
            "GRENADE " + (shooter.AirburstGrenadeEnabled ? "ON" : "OFF") +
            "  |  FRAGS " + shooter.AirburstFragmentCount + "\n" +
            "CORR " + (shooter.CorrosionEnabled ? "ON" : "OFF") +
            " x" + shooter.CorrosionRadiusMultiplier.ToString("0.00") +
            "  |  3-WAY " + ((shooter.StressShotgunEnabled && shooter.AirburstGrenadeEnabled && shooter.CorrosionEnabled) ? "ON" : "OFF");
    }

    private void RefreshButtons()
    {
        if (IsMotherloadShopMode)
        {
            SetControlVisible(attackSpeedUpgradeButton, true);
            SetControlVisible(damageUpgradeButton, true);
            SetControlVisible(nextLevelButton, true);
            SetControlVisible(stressShotgunButton, true);
            SetControlVisible(airburstGrenadeButton, HasMotherloadWorld);
            SetControlVisible(corrosionButton, false);

            if (HasMotherloadWorld)
            {
                if (attackSpeedUpgradeButton != null)
                    attackSpeedUpgradeButton.interactable = motherloadWorldController.CanBuyUpgrade(MotherloadUpgradeType.CargoBay);
                if (damageUpgradeButton != null)
                    damageUpgradeButton.interactable = motherloadWorldController.CanBuyUpgrade(MotherloadUpgradeType.FuelTank);
                if (nextLevelButton != null)
                    nextLevelButton.interactable = motherloadWorldController.CanBuyUpgrade(MotherloadUpgradeType.HullPlating);
                if (stressShotgunButton != null)
                    stressShotgunButton.interactable = motherloadWorldController.CanBuyUpgrade(MotherloadUpgradeType.BlasterDamage);
                if (airburstGrenadeButton != null)
                    airburstGrenadeButton.interactable = motherloadWorldController.CanBuyUpgrade(MotherloadUpgradeType.FireRate);

                if (attackSpeedUpgradeButtonText != null)
                    attackSpeedUpgradeButtonText.text = motherloadWorldController.BuildUpgradeButtonText(MotherloadUpgradeType.CargoBay, "CARGO BAY");
                if (damageUpgradeButtonText != null)
                    damageUpgradeButtonText.text = motherloadWorldController.BuildUpgradeButtonText(MotherloadUpgradeType.FuelTank, "FUEL TANK");
                if (nextLevelButtonText != null)
                    nextLevelButtonText.text = motherloadWorldController.BuildUpgradeButtonText(MotherloadUpgradeType.HullPlating, "HULL");
                if (stressShotgunButtonText != null)
                    stressShotgunButtonText.text = motherloadWorldController.BuildUpgradeButtonText(MotherloadUpgradeType.BlasterDamage, "BLASTER");
                if (airburstGrenadeButtonText != null)
                    airburstGrenadeButtonText.text = motherloadWorldController.BuildUpgradeButtonText(MotherloadUpgradeType.FireRate, "FIRE RATE");
                if (corrosionButtonText != null)
                    corrosionButtonText.text = string.Empty;
                return;
            }

            if (attackSpeedUpgradeButton != null)
                attackSpeedUpgradeButton.interactable = false;
            if (damageUpgradeButton != null)
                damageUpgradeButton.interactable = cannonAim != null && cannonAim.CanBuyFuelTankRank(2) && ProgressionState.Money >= cannonAim.GetFuelTankUpgradeCostForRank(2);
            if (nextLevelButton != null)
                nextLevelButton.interactable = cannonAim != null && cannonAim.CanBuyFuelTankRank(3) && ProgressionState.Money >= cannonAim.GetFuelTankUpgradeCostForRank(3);
            if (stressShotgunButton != null)
                stressShotgunButton.interactable = cannonAim != null && cannonAim.CanBuyFuelTankRank(4) && ProgressionState.Money >= cannonAim.GetFuelTankUpgradeCostForRank(4);
            if (airburstGrenadeButton != null)
                airburstGrenadeButton.interactable = false;

            if (attackSpeedUpgradeButtonText != null)
                attackSpeedUpgradeButtonText.text = BuildFuelTankButtonText(1, "STANDARD");
            if (damageUpgradeButtonText != null)
                damageUpgradeButtonText.text = BuildFuelTankButtonText(2, "EXTENDED");
            if (nextLevelButtonText != null)
                nextLevelButtonText.text = BuildFuelTankButtonText(3, "LONG HAUL");
            if (stressShotgunButtonText != null)
                stressShotgunButtonText.text = BuildFuelTankButtonText(4, "DEEP CORE");
            if (airburstGrenadeButtonText != null)
                airburstGrenadeButtonText.text = "AUTO REFUEL";
            if (corrosionButtonText != null)
                corrosionButtonText.text = string.Empty;
            return;
        }

        SetControlVisible(attackSpeedUpgradeButton, true);
        SetControlVisible(damageUpgradeButton, true);
        SetControlVisible(nextLevelButton, true);
        SetControlVisible(stressShotgunButton, true);
        SetControlVisible(airburstGrenadeButton, true);
        SetControlVisible(corrosionButton, true);

        if (attackSpeedUpgradeButton != null)
        {
            bool canUpgradeAttackSpeed = shooter != null && shooter.CanUpgradeAttackSpeed && ProgressionState.Money >= shooter.NextAttackSpeedUpgradeCost;
            attackSpeedUpgradeButton.interactable = canUpgradeAttackSpeed;
        }

        if (damageUpgradeButton != null)
        {
            bool canUpgradeDamage = shooter != null && shooter.CanUpgradeDamage && ProgressionState.Money >= shooter.NextDamageUpgradeCost;
            damageUpgradeButton.interactable = canUpgradeDamage;
        }

        if (nextLevelButton != null)
            nextLevelButton.interactable = rockWall != null && rockWall.CanAdvanceLevel;

        if (stressShotgunButton != null)
            stressShotgunButton.interactable = shooter != null;

        if (airburstGrenadeButton != null)
            airburstGrenadeButton.interactable = shooter != null;

        if (corrosionButton != null)
            corrosionButton.interactable = shooter != null;

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
                stressShotgunButtonText.text = "DEBUG SHOTGUN ON";
            else
                stressShotgunButtonText.text = "DEBUG SHOTGUN OFF";
        }

        if (airburstGrenadeButtonText != null)
        {
            if (shooter == null)
                airburstGrenadeButtonText.text = "GRENADE (NO CANNON)";
            else if (shooter.AirburstGrenadeEnabled)
                airburstGrenadeButtonText.text = "AIRBURST GRENADE ON";
            else
                airburstGrenadeButtonText.text = "AIRBURST GRENADE OFF";
        }

        if (corrosionButtonText != null)
        {
            if (shooter == null)
                corrosionButtonText.text = "CORROSION (NO CANNON)";
            else if (shooter.CorrosionEnabled)
                corrosionButtonText.text = "CORROSION ON";
            else
                corrosionButtonText.text = "CORROSION OFF";
        }
    }

    private void RefreshTuningControls()
    {
        bool showDebugControls = !IsMotherloadShopMode;
        SetSliderRowVisible(blastScaleSlider, showDebugControls);
        SetSliderRowVisible(corrosionRadiusSlider, showDebugControls);
        SetSliderRowVisible(stressRateSlider, showDebugControls);
        SetSliderRowVisible(stressPelletSlider, showDebugControls);
        SetSliderRowVisible(fragmentCountSlider, showDebugControls);

        if (!showDebugControls)
            return;

        bool interactable = shooter != null;

        RefreshSlider(
            blastScaleSlider,
            blastScaleSliderLabel,
            interactable,
            shooter != null ? shooter.DebugBlastScaleMultiplier : 1.45f,
            "BLAST SIZE",
            "0.00",
            isWholeNumber: false);

        RefreshSlider(
            corrosionRadiusSlider,
            corrosionRadiusSliderLabel,
            interactable,
            shooter != null ? shooter.CorrosionRadiusMultiplier : 1.6f,
            "CORROSION WIDTH",
            "0.00",
            isWholeNumber: false);

        RefreshSlider(
            stressRateSlider,
            stressRateSliderLabel,
            interactable,
            shooter != null ? shooter.StressShotgunFireInterval : 0.06f,
            "SHOTGUN RATE (s)",
            "0.00",
            isWholeNumber: false);

        RefreshSlider(
            stressPelletSlider,
            stressPelletSliderLabel,
            interactable,
            shooter != null ? shooter.StressShotgunPelletCount : 11f,
            "SHOTGUN PELLETS",
            "0",
            isWholeNumber: true);

        RefreshSlider(
            fragmentCountSlider,
            fragmentCountSliderLabel,
            interactable,
            shooter != null ? shooter.AirburstFragmentCount : 13f,
            "AIRBURST FRAGS",
            "0",
            isWholeNumber: true);
    }

    private void RefreshSlider(Slider slider, Text label, bool interactable, float value, string prefix, string format, bool isWholeNumber)
    {
        if (slider != null)
        {
            slider.interactable = interactable;
            slider.SetValueWithoutNotify(value);
        }

        if (label != null)
            label.text = prefix + "  " + value.ToString(format);
    }

    private void EnsureUpgradeUi()
    {
        if (upgradeStatsText != null &&
            attackSpeedUpgradeButton != null && attackSpeedUpgradeButtonText != null &&
            damageUpgradeButton != null && damageUpgradeButtonText != null &&
            nextLevelButton != null && nextLevelButtonText != null &&
            stressShotgunButton != null && stressShotgunButtonText != null &&
            airburstGrenadeButton != null && airburstGrenadeButtonText != null &&
            corrosionButton != null && corrosionButtonText != null &&
            blastScaleSlider != null && blastScaleSliderLabel != null &&
            corrosionRadiusSlider != null && corrosionRadiusSliderLabel != null &&
            stressRateSlider != null && stressRateSliderLabel != null &&
            stressPelletSlider != null && stressPelletSliderLabel != null &&
            fragmentCountSlider != null && fragmentCountSliderLabel != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        Transform panelTransform = transform.Find(UpgradePanelName);
        if (panelTransform == null)
            panelTransform = CreateUpgradePanel(canvas.transform).transform;
        upgradePanel = panelTransform.gameObject;
        ApplyPanelLayout(panelTransform as RectTransform);

        RemoveLegacyComboButton(panelTransform);

        Transform statsTransform = panelTransform.Find(UpgradeStatsName);
        if (statsTransform == null)
            statsTransform = CreateStatsText(panelTransform).transform;
        ApplyStatsLayout(statsTransform as RectTransform);
        upgradeStatsText = statsTransform.GetComponent<Text>();

        EnsureSliderRow(panelTransform, BlastScaleSliderRowName, 170f, 48f, 0.5f, 4f, false, out blastScaleSlider, out blastScaleSliderLabel);
        EnsureSliderRow(panelTransform, CorrosionRadiusSliderRowName, 226f, 48f, 0.5f, 4f, false, out corrosionRadiusSlider, out corrosionRadiusSliderLabel);
        EnsureSliderRow(panelTransform, StressRateSliderRowName, 282f, 48f, 0.02f, 0.2f, false, out stressRateSlider, out stressRateSliderLabel);
        EnsureSliderRow(panelTransform, StressPelletSliderRowName, 338f, 48f, 1f, 25f, true, out stressPelletSlider, out stressPelletSliderLabel);
        EnsureSliderRow(panelTransform, FragmentCountSliderRowName, 394f, 48f, 1f, 25f, true, out fragmentCountSlider, out fragmentCountSliderLabel);

        Transform attackButtonTransform = panelTransform.Find(AttackSpeedUpgradeButtonName);
        if (attackButtonTransform == null)
            attackButtonTransform = CreateUpgradeButton(panelTransform, AttackSpeedUpgradeButtonName, new Vector2(12f, 262f), new Vector2(-12f, 302f)).transform;
        attackSpeedUpgradeButton = attackButtonTransform.GetComponent<Button>();
        ApplyButtonLayout(attackButtonTransform, new Vector2(12f, 262f), new Vector2(-12f, 302f));

        Transform attackButtonTextTransform = attackButtonTransform.Find(UpgradeButtonTextName);
        if (attackButtonTextTransform == null)
            attackButtonTextTransform = CreateUpgradeButtonText(attackButtonTransform).transform;
        attackSpeedUpgradeButtonText = attackButtonTextTransform.GetComponent<Text>();

        Transform damageButtonTransform = panelTransform.Find(DamageUpgradeButtonName);
        if (damageButtonTransform == null)
            damageButtonTransform = CreateUpgradeButton(panelTransform, DamageUpgradeButtonName, new Vector2(12f, 212f), new Vector2(-12f, 252f)).transform;
        damageUpgradeButton = damageButtonTransform.GetComponent<Button>();
        ApplyButtonLayout(damageButtonTransform, new Vector2(12f, 212f), new Vector2(-12f, 252f));

        Transform damageButtonTextTransform = damageButtonTransform.Find(UpgradeButtonTextName);
        if (damageButtonTextTransform == null)
            damageButtonTextTransform = CreateUpgradeButtonText(damageButtonTransform).transform;
        damageUpgradeButtonText = damageButtonTextTransform.GetComponent<Text>();

        Transform nextLevelTransform = panelTransform.Find(NextLevelButtonName);
        if (nextLevelTransform == null)
            nextLevelTransform = CreateUpgradeButton(panelTransform, NextLevelButtonName, new Vector2(12f, 162f), new Vector2(-12f, 202f)).transform;
        nextLevelButton = nextLevelTransform.GetComponent<Button>();
        ApplyButtonLayout(nextLevelTransform, new Vector2(12f, 162f), new Vector2(-12f, 202f));

        Transform nextLevelTextTransform = nextLevelTransform.Find(UpgradeButtonTextName);
        if (nextLevelTextTransform == null)
            nextLevelTextTransform = CreateUpgradeButtonText(nextLevelTransform).transform;
        nextLevelButtonText = nextLevelTextTransform.GetComponent<Text>();

        Transform stressShotgunTransform = panelTransform.Find(StressShotgunButtonName);
        if (stressShotgunTransform == null)
            stressShotgunTransform = CreateUpgradeButton(panelTransform, StressShotgunButtonName, new Vector2(12f, 112f), new Vector2(-12f, 152f)).transform;
        stressShotgunButton = stressShotgunTransform.GetComponent<Button>();
        ApplyButtonLayout(stressShotgunTransform, new Vector2(12f, 112f), new Vector2(-12f, 152f));

        Transform stressShotgunTextTransform = stressShotgunTransform.Find(UpgradeButtonTextName);
        if (stressShotgunTextTransform == null)
            stressShotgunTextTransform = CreateUpgradeButtonText(stressShotgunTransform).transform;
        stressShotgunButtonText = stressShotgunTextTransform.GetComponent<Text>();

        Transform airburstGrenadeTransform = panelTransform.Find(AirburstGrenadeButtonName);
        if (airburstGrenadeTransform == null)
            airburstGrenadeTransform = CreateUpgradeButton(panelTransform, AirburstGrenadeButtonName, new Vector2(12f, 62f), new Vector2(-12f, 102f)).transform;
        airburstGrenadeButton = airburstGrenadeTransform.GetComponent<Button>();
        ApplyButtonLayout(airburstGrenadeTransform, new Vector2(12f, 62f), new Vector2(-12f, 102f));

        Transform airburstGrenadeTextTransform = airburstGrenadeTransform.Find(UpgradeButtonTextName);
        if (airburstGrenadeTextTransform == null)
            airburstGrenadeTextTransform = CreateUpgradeButtonText(airburstGrenadeTransform).transform;
        airburstGrenadeButtonText = airburstGrenadeTextTransform.GetComponent<Text>();

        Transform corrosionTransform = panelTransform.Find(CorrosionButtonName);
        if (corrosionTransform == null)
            corrosionTransform = CreateUpgradeButton(panelTransform, CorrosionButtonName, new Vector2(12f, 12f), new Vector2(-12f, 52f)).transform;
        corrosionButton = corrosionTransform.GetComponent<Button>();
        ApplyButtonLayout(corrosionTransform, new Vector2(12f, 12f), new Vector2(-12f, 52f));

        Transform corrosionTextTransform = corrosionTransform.Find(UpgradeButtonTextName);
        if (corrosionTextTransform == null)
            corrosionTextTransform = CreateUpgradeButtonText(corrosionTransform).transform;
        corrosionButtonText = corrosionTextTransform.GetComponent<Text>();

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
        corrosionButton.onClick.RemoveListener(ToggleCorrosion);
        corrosionButton.onClick.AddListener(ToggleCorrosion);

        blastScaleSlider.onValueChanged.RemoveListener(OnBlastScaleSliderChanged);
        blastScaleSlider.onValueChanged.AddListener(OnBlastScaleSliderChanged);
        corrosionRadiusSlider.onValueChanged.RemoveListener(OnCorrosionRadiusSliderChanged);
        corrosionRadiusSlider.onValueChanged.AddListener(OnCorrosionRadiusSliderChanged);
        stressRateSlider.onValueChanged.RemoveListener(OnStressRateSliderChanged);
        stressRateSlider.onValueChanged.AddListener(OnStressRateSliderChanged);
        stressPelletSlider.onValueChanged.RemoveListener(OnStressPelletSliderChanged);
        stressPelletSlider.onValueChanged.AddListener(OnStressPelletSliderChanged);
        fragmentCountSlider.onValueChanged.RemoveListener(OnFragmentCountSliderChanged);
        fragmentCountSlider.onValueChanged.AddListener(OnFragmentCountSliderChanged);

        ApplyUpgradePanelVisibility();
    }

    private void ApplyUpgradePanelVisibility()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(upgradeUiVisible);
    }

    private void EnsureFuelUi()
    {
        if (fuelHudRoot != null && fuelLabel != null && motherloadStatusLabel != null && fuelFillImage != null && fuelFillRect != null)
        {
            ApplyFuelHudLayout(fuelHudRoot.transform as RectTransform);
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        Transform rootTransform = transform.Find(FuelHudRootName);
        if (rootTransform == null)
            rootTransform = CreateFuelHud(canvas.transform).transform;

        fuelHudRoot = rootTransform.gameObject;
        ApplyFuelHudLayout(rootTransform as RectTransform);

        Transform labelTransform = rootTransform.Find(FuelLabelName);
        if (labelTransform == null)
            labelTransform = CreateFuelLabel(rootTransform).transform;
        fuelLabel = labelTransform.GetComponent<Text>();
        ApplyFuelLabelLayout(labelTransform as RectTransform);

        Transform statusTransform = rootTransform.Find(MotherloadStatusLabelName);
        if (statusTransform == null)
            statusTransform = CreateMotherloadStatusLabel(rootTransform).transform;
        motherloadStatusLabel = statusTransform.GetComponent<Text>();
        ApplyMotherloadStatusLayout(statusTransform as RectTransform);

        Transform barBackgroundTransform = rootTransform.Find(FuelBarBackgroundName);
        if (barBackgroundTransform == null)
            barBackgroundTransform = CreateFuelBarBackground(rootTransform).transform;
        ApplyFuelBarLayout(barBackgroundTransform as RectTransform);

        Transform fillTransform = barBackgroundTransform.Find(FuelFillName);
        if (fillTransform == null)
            fillTransform = CreateFuelFill(barBackgroundTransform).transform;
        fuelFillImage = fillTransform.GetComponent<Image>();
        fuelFillRect = fillTransform as RectTransform;
    }

    private void EnsureGameOverUi()
    {
        if (gameOverRoot != null && gameOverText != null)
        {
            ApplyGameOverLayout(gameOverRoot.transform as RectTransform);
            ApplyGameOverTextLayout(gameOverText.transform as RectTransform);
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        Transform rootTransform = transform.Find(GameOverRootName);
        if (rootTransform == null)
            rootTransform = CreateGameOverBanner(canvas.transform).transform;

        gameOverRoot = rootTransform.gameObject;
        ApplyGameOverLayout(rootTransform as RectTransform);

        Transform textTransform = rootTransform.Find(GameOverTextName);
        if (textTransform == null)
            textTransform = CreateGameOverText(rootTransform).transform;

        gameOverText = textTransform.GetComponent<Text>();
        ApplyGameOverTextLayout(textTransform as RectTransform);
        gameOverRoot.SetActive(false);
    }

    private void RefreshFuelUi()
    {
        if (fuelHudRoot == null)
            return;

        bool showFuel = cannonAim != null && cannonAim.FuelSystemEnabled;
        fuelHudRoot.SetActive(showFuel);
        if (!showFuel)
            return;

        float normalizedFuel = cannonAim.FuelNormalized;
        if (fuelFillRect != null)
            fuelFillRect.anchorMax = new Vector2(normalizedFuel, 1f);

        if (fuelFillImage != null)
        {
            Color okColor = Color.Lerp(new Color(0.92f, 0.24f, 0.16f, 1f), new Color(0.2f, 0.9f, 0.35f, 1f), normalizedFuel);
            fuelFillImage.color = cannonAim.IsOutOfFuel ? new Color(0.92f, 0.16f, 0.16f, 1f) : okColor;
        }

        if (fuelLabel != null)
        {
            int currentFuelValue = Mathf.CeilToInt(cannonAim.CurrentFuel);
            int maxFuelValue = Mathf.CeilToInt(cannonAim.MaxFuel);
            string suffix = cannonAim.IsDockedAtBase ? "  AT BASE" : string.Empty;
            fuelLabel.text = cannonAim.IsOutOfFuel
                ? "FUEL EMPTY  " + currentFuelValue + "/" + maxFuelValue
                : "FUEL  " + currentFuelValue + "/" + maxFuelValue + suffix;
            fuelLabel.color = cannonAim.IsOutOfFuel ? new Color(1f, 0.55f, 0.55f, 1f) : Color.white;
        }

        RefreshMotherloadStatusUi();
    }

    private void RefreshMotherloadStatusUi()
    {
        if (motherloadStatusLabel == null)
            return;

        bool showStatus = HasMotherloadWorld && motherloadWorldController.RunState != null && cannonAim != null;
        motherloadStatusLabel.gameObject.SetActive(showStatus);
        if (!showStatus)
            return;

        MotherloadRunState runState = motherloadWorldController.RunState;
        string warningText = BuildMotherloadWarningText(runState);
        motherloadStatusLabel.text =
            "CARGO " + runState.CargoUsed + "/" + runState.CargoCapacity +
            "  |  HP " + runState.CurrentHull + "/" + runState.MaxHull +
            (warningText.Length > 0 ? "\n" + warningText : string.Empty);
        motherloadStatusLabel.color = warningText.Length > 0 ? new Color(1f, 0.72f, 0.22f, 1f) : new Color(0.86f, 0.92f, 1f, 1f);
    }

    private string BuildMotherloadWarningText(MotherloadRunState runState)
    {
        string warnings = string.Empty;

        if (cannonAim != null)
        {
            if (cannonAim.IsOutOfFuel)
                AppendWarning(ref warnings, "FUEL EMPTY");
            else if (cannonAim.FuelNormalized <= 0.25f)
                AppendWarning(ref warnings, "LOW FUEL");
        }

        if (runState != null)
        {
            if (runState.CargoFree <= 0)
                AppendWarning(ref warnings, "CARGO FULL");
            else if (runState.CargoFree <= 3)
                AppendWarning(ref warnings, "CARGO NEAR FULL");

            float hullRatio = runState.MaxHull > 0 ? runState.CurrentHull / (float)runState.MaxHull : 0f;
            if (runState.CurrentHull <= 1 || hullRatio <= 0.34f)
                AppendWarning(ref warnings, "LOW HP");
        }

        return warnings;
    }

    private static void AppendWarning(ref string warnings, string warning)
    {
        if (warnings.Length > 0)
            warnings += "  |  ";
        warnings += warning;
    }

    private GameObject CreateFuelHud(Transform parent)
    {
        GameObject rootObject = new GameObject(FuelHudRootName, typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(parent, false);

        ApplyFuelHudLayout(rootObject.GetComponent<RectTransform>());

        Image image = rootObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);
        return rootObject;
    }

    private void ApplyFuelHudLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -18f);
        rect.sizeDelta = HasMotherloadWorld ? new Vector2(300f, 78f) : new Vector2(240f, 44f);
    }

    private GameObject CreateFuelLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(FuelLabelName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        ApplyFuelLabelLayout(labelObject.GetComponent<RectTransform>());

        Text text = labelObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 15;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        return labelObject;
    }

    private void ApplyFuelLabelLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(10f, -22f);
        rect.offsetMax = new Vector2(-10f, -4f);
    }

    private GameObject CreateMotherloadStatusLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(MotherloadStatusLabelName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        ApplyMotherloadStatusLayout(labelObject.GetComponent<RectTransform>());
        Text text = labelObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return labelObject;
    }

    private void ApplyMotherloadStatusLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(10f, -56f);
        rect.offsetMax = new Vector2(-10f, -24f);
    }

    private GameObject CreateFuelBarBackground(Transform parent)
    {
        GameObject barObject = new GameObject(FuelBarBackgroundName, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(parent, false);

        ApplyFuelBarLayout(barObject.GetComponent<RectTransform>());

        Image image = barObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.12f);
        return barObject;
    }

    private void ApplyFuelBarLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(10f, 8f);
        rect.offsetMax = new Vector2(-10f, 20f);
    }

    private GameObject CreateFuelFill(Transform parent)
    {
        GameObject fillObject = new GameObject(FuelFillName, typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(parent, false);

        RectTransform rect = fillObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = fillObject.GetComponent<Image>();
        image.color = new Color(0.2f, 0.9f, 0.35f, 1f);
        return fillObject;
    }

    private GameObject CreateGameOverBanner(Transform parent)
    {
        GameObject bannerObject = new GameObject(GameOverRootName, typeof(RectTransform), typeof(Image));
        bannerObject.transform.SetParent(parent, false);
        ApplyGameOverLayout(bannerObject.GetComponent<RectTransform>());

        Image image = bannerObject.GetComponent<Image>();
        image.color = new Color(0.02f, 0f, 0f, 0.78f);
        return bannerObject;
    }

    private void ApplyGameOverLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 95f);
        rect.sizeDelta = new Vector2(520f, 138f);
    }

    private GameObject CreateGameOverText(Transform parent)
    {
        GameObject textObject = new GameObject(GameOverTextName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        ApplyGameOverTextLayout(textObject.GetComponent<RectTransform>());

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(1f, 0.28f, 0.22f, 1f);
        return textObject;
    }

    private void ApplyGameOverTextLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 12f);
        rect.offsetMax = new Vector2(-18f, -12f);
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

    private void EnsureSliderRow(Transform parent, string rowName, float topOffset, float height, float minValue, float maxValue, bool wholeNumbers, out Slider slider, out Text label)
    {
        Transform rowTransform = parent.Find(rowName);
        if (rowTransform == null)
            rowTransform = CreateSliderRow(parent, rowName, topOffset, height).transform;
        ApplyTopSectionLayout(rowTransform as RectTransform, topOffset, height);

        Transform labelTransform = rowTransform.Find(SliderLabelName);
        if (labelTransform == null)
            labelTransform = CreateSliderLabel(rowTransform).transform;
        label = labelTransform.GetComponent<Text>();

        Transform sliderTransform = rowTransform.Find(SliderControlName);
        if (sliderTransform == null)
            sliderTransform = CreateSliderControl(rowTransform).transform;
        slider = sliderTransform.GetComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = wholeNumbers;
    }

    private GameObject CreateSliderRow(Transform parent, string rowName, float topOffset, float height)
    {
        GameObject rowObject = new GameObject(rowName, typeof(RectTransform), typeof(Image));
        rowObject.transform.SetParent(parent, false);
        ApplyTopSectionLayout(rowObject.GetComponent<RectTransform>(), topOffset, height);

        Image image = rowObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.08f);
        return rowObject;
    }

    private GameObject CreateSliderLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(SliderLabelName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(10f, -22f);
        rect.offsetMax = new Vector2(-10f, -2f);

        Text text = labelObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        return labelObject;
    }

    private GameObject CreateSliderControl(Transform parent)
    {
        GameObject sliderObject = new GameObject(SliderControlName, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.offsetMin = new Vector2(14f, 8f);
        sliderRect.offsetMax = new Vector2(-14f, 28f);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.offsetMin = new Vector2(0f, -5f);
        backgroundRect.offsetMax = new Vector2(0f, 5f);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.14f);

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(0f, -5f);
        fillRect.offsetMax = new Vector2(0f, 5f);
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(1f, 1f, 1f, 0.45f);

        GameObject handleSlideAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform handleSlideAreaRect = handleSlideAreaObject.GetComponent<RectTransform>();
        handleSlideAreaRect.anchorMin = new Vector2(0f, 0f);
        handleSlideAreaRect.anchorMax = new Vector2(1f, 1f);
        handleSlideAreaRect.offsetMin = new Vector2(10f, 0f);
        handleSlideAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(handleSlideAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 24f);
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = Color.white;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
        slider.colors = colors;

        return sliderObject;
    }

    private void ApplyPanelLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -130f);
        rect.sizeDelta = new Vector2(400f, 760f);
    }

    private void ApplyStatsLayout(RectTransform rect)
    {
        ApplyTopSectionLayout(rect, 12f, 148f);
    }

    private void ApplyTopSectionLayout(RectTransform rect, float topOffset, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(12f, -(topOffset + height));
        rect.offsetMax = new Vector2(-12f, -topOffset);
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

    private void TryBuyFuelTankRank(int rank)
    {
        if (cannonAim == null || !cannonAim.CanBuyFuelTankRank(rank))
            return;

        int cost = cannonAim.GetFuelTankUpgradeCostForRank(rank);
        if (ProgressionState.Money < cost)
            return;

        if (!ProgressionState.TrySpendMoney(cost))
            return;

        if (!cannonAim.TryUpgradeFuelTankToRank(rank))
        {
            ProgressionState.AddMoney(cost);
            return;
        }

        Refresh();
    }

    private void TryBuyMotherloadUpgrade(MotherloadUpgradeType upgradeType)
    {
        if (motherloadWorldController == null)
            return;

        if (motherloadWorldController.TryBuyUpgrade(upgradeType))
            Refresh();
    }

    private void TryBuyFuelRefill()
    {
        if (cannonAim == null || !cannonAim.IsDockedAtBase)
            return;

        int cost = cannonAim.GetFullRefillCost();
        if (cost <= 0 || ProgressionState.Money < cost)
            return;

        if (!ProgressionState.TrySpendMoney(cost))
            return;

        if (!cannonAim.TryRefillToFull())
        {
            ProgressionState.AddMoney(cost);
            return;
        }

        Refresh();
    }

    private string BuildFuelTankButtonText(int rank, string label)
    {
        if (cannonAim == null)
            return "TANK RANK " + rank + "  (NO CANNON)";

        int capacity = Mathf.CeilToInt(cannonAim.GetFuelTankCapacityForRank(rank));
        if (cannonAim.IsFuelTankRankOwned(rank))
            return label + "  " + capacity + "L  OWNED";

        if (!cannonAim.CanBuyFuelTankRank(rank))
            return label + "  " + capacity + "L  LOCKED";

        return "BUY " + label + "  " + capacity + "L  $" + cannonAim.GetFuelTankUpgradeCostForRank(rank);
    }

    private string BuildGasStationButtonText()
    {
        if (cannonAim == null)
            return "GAS STATION  (NO CANNON)";
        if (!cannonAim.IsDockedAtBase)
            return "GAS STATION  DOCK AT BASE";

        int cost = cannonAim.GetFullRefillCost();
        return cost <= 0 ? "GAS STATION  TANK FULL" : "FILL TANK  $" + cost;
    }

    private void SetControlVisible(Behaviour behaviour, bool visible)
    {
        if (behaviour != null)
            behaviour.gameObject.SetActive(visible);
    }

    private void SetSliderRowVisible(Slider slider, bool visible)
    {
        if (slider != null && slider.transform.parent != null)
            slider.transform.parent.gameObject.SetActive(visible);
    }

    private string BuildEssenceSummaryLine()
    {
        return "R " + ProgressionState.RedEssence +
               "  B " + ProgressionState.BlueEssence +
               "  G " + ProgressionState.GreenEssence;
    }

    private void HandleProgressionStateChanged()
    {
        Refresh();
    }

    private void HandleFuelChanged()
    {
        RefreshFuelUi();
    }

    private void OnDestroy()
    {
        if (progressionState != null)
            progressionState.Changed -= HandleProgressionStateChanged;
        if (cannonAim != null)
            cannonAim.FuelChanged -= HandleFuelChanged;
    }
}
