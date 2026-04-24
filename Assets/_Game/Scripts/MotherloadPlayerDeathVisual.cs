using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class MotherloadPlayerDeathVisual : MonoBehaviour
{
    private const string WreckSpriteAssetPath = "Assets/_Game/Art/GeneratedSprites/rock_enemy 2.png";
    private const string WreckObjectName = "DeathWreckVisual";
    private const int WreckSortingOrder = 52;
    private const float TargetWreckWidthWorld = 1.28f;
    private const float TargetWreckHeightWorld = 0.88f;

    private static Sprite cachedWreckSprite;

    private SpriteRenderer[] cachedRenderers;
    private bool[] cachedRendererEnabled;
    private SpriteRenderer wreckRenderer;

    public void ShowWreck()
    {
        EnsureCachedRenderers();
        EnsureWreckRenderer();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && cachedRenderers[i] != wreckRenderer)
                cachedRenderers[i].enabled = false;
        }

        wreckRenderer.enabled = true;
        wreckRenderer.sprite = GetWreckSprite();
        wreckRenderer.sortingOrder = WreckSortingOrder;
        wreckRenderer.color = Color.white;
        wreckRenderer.transform.localPosition = Vector3.zero;
        wreckRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        ApplyWreckScale();
    }

    public void RestoreShip()
    {
        EnsureCachedRenderers();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && cachedRenderers[i] != wreckRenderer)
                cachedRenderers[i].enabled = i < cachedRendererEnabled.Length && cachedRendererEnabled[i];
        }

        if (wreckRenderer != null)
            wreckRenderer.enabled = false;
    }

    private void EnsureCachedRenderers()
    {
        if (cachedRenderers != null)
            return;

        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        cachedRendererEnabled = new bool[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
            cachedRendererEnabled[i] = cachedRenderers[i] != null && cachedRenderers[i].enabled;
    }

    private void EnsureWreckRenderer()
    {
        if (wreckRenderer != null)
            return;

        Transform wreckTransform = transform.Find(WreckObjectName);
        if (wreckTransform == null)
        {
            GameObject wreckObject = new GameObject(WreckObjectName, typeof(SpriteRenderer));
            wreckObject.transform.SetParent(transform, false);
            wreckTransform = wreckObject.transform;
        }

        wreckRenderer = wreckTransform.GetComponent<SpriteRenderer>();
        if (wreckRenderer == null)
            wreckRenderer = wreckTransform.gameObject.AddComponent<SpriteRenderer>();
    }

    private void ApplyWreckScale()
    {
        Sprite sprite = wreckRenderer.sprite;
        if (sprite == null || sprite.bounds.size.x <= 0.0001f || sprite.bounds.size.y <= 0.0001f)
        {
            wreckRenderer.transform.localScale = Vector3.one;
            return;
        }

        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        float localScaleX = TargetWreckWidthWorld / (sprite.bounds.size.x * parentScaleX);
        float localScaleY = TargetWreckHeightWorld / (sprite.bounds.size.y * parentScaleY);
        float uniformScale = Mathf.Min(localScaleX, localScaleY);
        wreckRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    private static Sprite GetWreckSprite()
    {
        if (cachedWreckSprite != null)
            return cachedWreckSprite;

#if UNITY_EDITOR
        cachedWreckSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WreckSpriteAssetPath);
        if (cachedWreckSprite != null)
            return cachedWreckSprite;
#endif

        cachedWreckSprite = CreateFallbackWreckSprite();
        return cachedWreckSprite;
    }

    private static Sprite CreateFallbackWreckSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.58f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float radius = new Vector2(delta.x / 1.35f, delta.y).magnitude;
                float alpha = Mathf.Clamp01(1f - radius / 28f);
                Color color = Color.Lerp(new Color(0.22f, 0.19f, 0.16f, 0f), new Color(0.58f, 0.52f, 0.45f, 1f), alpha);
                color.a = Mathf.SmoothStep(0f, 1f, alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "RuntimeFallbackWreckSprite";
        return sprite;
    }
}
