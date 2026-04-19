using UnityEngine;

public class Projectile : MonoBehaviour
{
    private const float MaxLifetime = 4f;

    private float lifetimeRemaining = MaxLifetime;
    private Rigidbody2D body;
    private bool didHit;
    private float blastRadiusScale = 1f;
    private RockWall rockWall;
    private AutoShooter owner;
    private bool isActiveTracked;

    public void ConfigurePool(AutoShooter owner, Rigidbody2D body)
    {
        this.owner = owner;
        this.body = body;
    }

    public void Launch(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, float blastRadiusScale)
    {
        this.rockWall = rockWall;
        this.blastRadiusScale = Mathf.Max(0.25f, blastRadiusScale);
        didHit = false;
        lifetimeRemaining = MaxLifetime;

        transform.SetParent(null, true);
        transform.position = worldPosition;
        gameObject.SetActive(true);

        if (!isActiveTracked)
        {
            isActiveTracked = true;
            ShootTheRockPerformance.RecordProjectileActivated();
        }

        if (body != null)
        {
            body.simulated = true;
            body.position = worldPosition;
            body.linearVelocity = direction.normalized * speed;
            body.angularVelocity = 0f;
        }
    }

    private void Update()
    {
        lifetimeRemaining -= Time.deltaTime;
        if (lifetimeRemaining <= 0f)
            ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        using (ShootTheRockPerformance.ProjectileImpactMarker.Auto())
        {
            if (didHit)
                return;

            RockWall hitWall = other.GetComponent<RockWall>();
            if (hitWall == null)
                hitWall = other.GetComponentInParent<RockWall>();
            if (hitWall == null)
                return;

            didHit = true;
            Vector2 impactDirection = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                ? body.linearVelocity.normalized
                : Vector2.right;

            Vector2 queryPoint = (Vector2)transform.position - (impactDirection * 0.18f);
            Vector2 hitPoint = other.ClosestPoint(queryPoint);
            if ((hitPoint - queryPoint).sqrMagnitude <= 0.0001f)
                hitPoint = other.ClosestPoint((Vector2)transform.position - (impactDirection * 0.45f));

            if (rockWall == null)
                rockWall = hitWall;

            rockWall.ApplyHit(hitPoint, impactDirection, blastRadiusScale);
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        didHit = false;
        lifetimeRemaining = MaxLifetime;
        rockWall = null;

        if (isActiveTracked)
        {
            isActiveTracked = false;
            ShootTheRockPerformance.RecordProjectileDeactivated();
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        if (owner != null)
        {
            owner.ReleaseProjectile(this);
            return;
        }

        Destroy(gameObject);
    }
}
