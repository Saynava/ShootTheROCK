using UnityEngine;

public class ChipParticle : MonoBehaviour
{
    private Rigidbody2D body;
    private float lifetime;
    private float startLifetime;
    private SpriteRenderer spriteRenderer;
    private RockWall owner;
    private bool isActiveTracked;

    public void ConfigurePool(RockWall owner, Rigidbody2D body, SpriteRenderer spriteRenderer)
    {
        this.owner = owner;
        this.body = body;
        this.spriteRenderer = spriteRenderer;
    }

    public void Launch(Vector2 worldPosition, Vector2 size, Color color, Vector2 impulse, float torque, float lifetime)
    {
        using (ShootTheRockPerformance.ChipLaunchMarker.Auto())
        {
            transform.position = worldPosition;
            transform.localScale = new Vector3(size.x, size.y, 1f);
            this.lifetime = lifetime;
            startLifetime = lifetime;

            if (spriteRenderer != null)
            {
                color.a = 1f;
                spriteRenderer.color = color;
                spriteRenderer.enabled = true;
            }

            gameObject.SetActive(true);
            if (!isActiveTracked)
            {
                isActiveTracked = true;
                owner?.NotifyChipParticleActivated();
                ShootTheRockPerformance.RecordChipActivated();
            }

            if (body != null)
            {
                body.position = worldPosition;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = true;
                body.AddForce(impulse, ForceMode2D.Impulse);
                body.AddTorque(torque, ForceMode2D.Impulse);
            }
        }
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
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        if (isActiveTracked)
        {
            isActiveTracked = false;
            owner?.NotifyChipParticleDeactivated();
            ShootTheRockPerformance.RecordChipDeactivated();
        }

        if (owner != null)
        {
            owner.ReleaseChipParticle(this);
            return;
        }

        Destroy(gameObject);
    }
}
