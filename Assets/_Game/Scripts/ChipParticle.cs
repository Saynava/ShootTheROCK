using UnityEngine;

public class ChipParticle : MonoBehaviour
{
    private Rigidbody2D body;
    private float lifetime;
    private float startLifetime;
    private SpriteRenderer spriteRenderer;

    public void Initialize(Rigidbody2D body, float lifetime)
    {
        this.body = body;
        this.lifetime = lifetime;
        startLifetime = lifetime;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;

        if (spriteRenderer != null)
        {
            float fade = Mathf.Clamp01((lifetime - 0.12f) / Mathf.Max(0.0001f, startLifetime - 0.12f));
            Color color = spriteRenderer.color;
            color.a = fade;
            spriteRenderer.color = color;
        }

        if (body != null && lifetime <= 0.18f)
            body.simulated = false;

        if (lifetime <= 0f)
            Destroy(gameObject);
    }
}

