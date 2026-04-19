using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShootTheRockPerformanceHud : MonoBehaviour
{
    private const string PanelName = "PerformancePanel";
    private const string TextName = "PerformanceText";

    [SerializeField] private bool visibleByDefault = true;
    [SerializeField] private bool allowToggle = true;
    [SerializeField] private float refreshInterval = 0.12f;

    private GameObject panelObject;
    private Text performanceText;
    private bool isInitialized;
    private bool isVisible;
    private float nextRefreshTime;

    public void Initialize()
    {
        if (isInitialized)
            return;

        EnsureUi();
        isVisible = visibleByDefault;
        SetVisible(isVisible);
        RefreshNow();
        isInitialized = true;
    }

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        Initialize();
        ShootTheRockPerformance.EndFrame(Time.unscaledDeltaTime);

        if (allowToggle && Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            isVisible = !isVisible;
            SetVisible(isVisible);
        }

        if (!isVisible)
            return;
        if (Time.unscaledTime < nextRefreshTime)
            return;

        RefreshNow();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void EnsureUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        Transform existingPanel = transform.Find(PanelName);
        if (existingPanel == null)
            existingPanel = CreatePanel(transform).transform;
        panelObject = existingPanel.gameObject;

        Transform existingText = existingPanel.Find(TextName);
        if (existingText == null)
            existingText = CreateText(existingPanel).transform;
        performanceText = existingText.GetComponent<Text>();
    }

    private void SetVisible(bool visible)
    {
        if (panelObject != null)
            panelObject.SetActive(visible);
    }

    private void RefreshNow()
    {
        if (performanceText == null)
            return;

        ShootTheRockPerformance.Snapshot snapshot = ShootTheRockPerformance.Current;
        performanceText.text =
            "PERF  (F3)\n" +
            "FPS " + snapshot.fps.ToString("0.0") +
            "  |  FRAME " + snapshot.frameMs.ToString("0.0") + " ms\n" +
            "SHOTS " + snapshot.shotsPerSecond.ToString("0.0") + "/s  |  PELLETS " + snapshot.pelletsPerSecond.ToString("0.0") + "/s\n" +
            "HITS " + snapshot.hitsPerSecond.ToString("0.0") + "/s  |  TEX " + snapshot.textureAppliesLastFrame + "/f\n" +
            "PROJ " + snapshot.activeProjectiles + " active  |  MISS " + snapshot.projectilePoolMissesPerSecond.ToString("0.0") + "/s\n" +
            "CHIPS " + snapshot.activeChipParticles + " active  |  MISS " + snapshot.chipPoolMissesPerSecond.ToString("0.0") + "/s\n" +
            "DESTROY " + snapshot.cellsDestroyedLastFrame + "/f  |  TIER " + snapshot.damageTierChangesLastFrame + "/f\n" +
            "ISLAND scan " + snapshot.islandScanCellsLastFrame + "  |  rm " + snapshot.islandRemovedCellsLastFrame + "\n" +
            "CHUNKS " + snapshot.chunkBuildsLastFrame + "/f  |  COLL " + snapshot.colliderRebuildsLastFrame + "/f\n" +
            "PATHS " + snapshot.colliderPathsLastFrame + "/f";
    }

    private GameObject CreatePanel(Transform parent)
    {
        GameObject createdPanel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
        createdPanel.transform.SetParent(parent, false);

        RectTransform rect = createdPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(420f, 260f);

        Image image = createdPanel.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.68f);
        return createdPanel;
    }

    private GameObject CreateText(Transform parent)
    {
        GameObject textObject = new GameObject(TextName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return textObject;
    }
}
