using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProjectileImpactEffect : MonoBehaviour
{
    private const string ExplosionSpriteAssetPath = "Assets/_Game/Art/GeneratedSprites/rock_enemy 1.png";
    private const float DefaultDuration = 0.22f;
    private const float StartDiameterMultiplier = 0.52f;
    private const float EndDiameterMultiplier = 1.9f;
    private const float FallbackSpriteDiameterWorld = 1f;
    private const int SortingOrder = 48;

    private static Sprite cachedExplosionSprite;

    private SpriteRenderer spriteRenderer;
    private float age;
    private float duration;
    private float startScale;
    private float endScale;
    private Color baseColor;

    public static void Spawn(Vector2 worldPosition, float radiusWorld = 1f)
    {
        GameObject effectObject = new GameObject("ProjectileImpactExplosion", typeof(SpriteRenderer), typeof(ProjectileImpactEffect));
        effectObject.transform.position = new Vector3(worldPosition.x, worldPosition.y, -0.08f);
        effectObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        ProjectileImpactEffect effect = effectObject.GetComponent<ProjectileImpactEffect>();
        effect.Initialize(Mathf.Max(0.08f, radiusWorld));
    }

    private void Initialize(float radiusWorld)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetExplosionSprite();
        spriteRenderer.sortingOrder = SortingOrder;
        spriteRenderer.color = Color.white;

        duration = DefaultDuration;
        float spriteDiameter = ResolveSpriteDiameterWorld(spriteRenderer.sprite);
        startScale = (radiusWorld * 2f * StartDiameterMultiplier) / spriteDiameter;
        endScale = (radiusWorld * 2f * EndDiameterMultiplier) / spriteDiameter;
        baseColor = Color.white;
        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float easedOut = 1f - ((1f - t) * (1f - t));
        float easedIn = t * t;

        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, easedOut);
        baseColor.a = Mathf.Lerp(1f, 0f, easedIn);
        spriteRenderer.color = baseColor;

        if (age >= duration)
            Destroy(gameObject);
    }

    private static Sprite GetExplosionSprite()
    {
        if (cachedExplosionSprite != null)
            return cachedExplosionSprite;

#if UNITY_EDITOR
        cachedExplosionSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ExplosionSpriteAssetPath);
        if (cachedExplosionSprite != null)
            return cachedExplosionSprite;
#endif

        cachedExplosionSprite = CreateFallbackExplosionSprite();
        return cachedExplosionSprite;
    }

    private static float ResolveSpriteDiameterWorld(Sprite sprite)
    {
        if (sprite == null)
            return FallbackSpriteDiameterWorld;

        float diameter = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        return diameter > 0.0001f ? diameter : FallbackSpriteDiameterWorld;
    }

    private static Sprite CreateFallbackExplosionSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float radius01 = Mathf.Clamp01(delta.magnitude / (size * 0.5f));
                float alpha = 1f - radius01;
                Color color = Color.Lerp(new Color(1f, 0.2f, 0.03f, 0f), new Color(1f, 0.92f, 0.18f, 1f), alpha);
                color.a = Mathf.SmoothStep(0f, 1f, alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "RuntimeFallbackExplosionSprite";
        return sprite;
    }
}
