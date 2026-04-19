using UnityEngine;

public class Projectile : MonoBehaviour
{
    private enum ProjectileMode
    {
        Standard,
        AirburstGrenade,
        Fragment,
    }

    private const float MaxLifetime = 12f;
    private const float FragmentLifetime = 4f;
    private const float ProjectileBoundsDespawnPadding = 18f;
    private const float FragmentBoundsDespawnPadding = 6f;
    private static readonly Vector3 StandardScale = new Vector3(0.16f, 0.16f, 1f);
    private static readonly Vector3 AirburstGrenadeScale = new Vector3(0.18f, 0.18f, 1f);
    private static readonly Vector3 FragmentScale = new Vector3(0.11f, 0.11f, 1f);

    private float lifetimeRemaining = MaxLifetime;
    private Rigidbody2D body;
    private bool didHit;
    private float blastRadiusScale = 1f;
    private RockWall rockWall;
    private AutoShooter owner;
    private bool isActiveTracked;
    private ProjectileMode mode;
    private float airburstFuseRemaining;
    private Vector2 lastTravelDirection = Vector2.right;

    public void ConfigurePool(AutoShooter owner, Rigidbody2D body)
    {
        this.owner = owner;
        this.body = body;
    }

    public void Launch(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, float blastRadiusScale, float gravityScale)
    {
        LaunchInternal(worldPosition, direction, speed, rockWall, blastRadiusScale, ProjectileMode.Standard, MaxLifetime, 0f, gravityScale, StandardScale);
    }

    public void LaunchAirburstGrenade(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, float blastRadiusScale, float fuseTime, float gravityScale)
    {
        LaunchInternal(worldPosition, direction, speed, rockWall, blastRadiusScale, ProjectileMode.AirburstGrenade, MaxLifetime, fuseTime, gravityScale, AirburstGrenadeScale);
    }

    public void LaunchFragment(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, float blastRadiusScale, float gravityScale)
    {
        LaunchInternal(worldPosition, direction, speed, rockWall, blastRadiusScale, ProjectileMode.Fragment, FragmentLifetime, 0f, gravityScale, FragmentScale);
    }

    private void LaunchInternal(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, float blastRadiusScale, ProjectileMode mode, float lifetime, float fuseTime, float gravityScale, Vector3 visualScale)
    {
        this.rockWall = rockWall;
        this.mode = mode;
        this.blastRadiusScale = Mathf.Max(0.2f, blastRadiusScale);
        this.airburstFuseRemaining = fuseTime;
        this.lastTravelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        didHit = false;
        lifetimeRemaining = lifetime;

        transform.SetParent(null, true);
        transform.position = worldPosition;
        transform.localScale = visualScale;
        gameObject.SetActive(true);

        if (!isActiveTracked)
        {
            isActiveTracked = true;
            ShootTheRockPerformance.RecordProjectileActivated();
        }

        if (body != null)
        {
            body.gravityScale = gravityScale;
            body.simulated = true;
            body.position = worldPosition;
            body.linearVelocity = lastTravelDirection * speed;
            body.angularVelocity = 0f;
        }
    }

    private void Update()
    {
        if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f)
            lastTravelDirection = body.linearVelocity.normalized;

        if (ShouldDespawnOutsideWallBounds())
        {
            ReturnToPool();
            return;
        }

        lifetimeRemaining -= Time.deltaTime;
        if (mode == ProjectileMode.AirburstGrenade)
        {
            airburstFuseRemaining -= Time.deltaTime;
            if (airburstFuseRemaining <= 0f && !didHit)
            {
                ExplodeIntoFragments((Vector2)transform.position, lastTravelDirection);
                return;
            }
        }

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
                : lastTravelDirection;

            Vector2 queryPoint = (Vector2)transform.position - (impactDirection * 0.18f);
            Vector2 hitPoint = other.ClosestPoint(queryPoint);
            if ((hitPoint - queryPoint).sqrMagnitude <= 0.0001f)
                hitPoint = other.ClosestPoint((Vector2)transform.position - (impactDirection * 0.45f));

            if (rockWall == null)
                rockWall = hitWall;

            if (mode == ProjectileMode.AirburstGrenade)
            {
                ExplodeIntoFragments(hitPoint, impactDirection);
                return;
            }

            rockWall.ApplyHit(hitPoint, impactDirection, blastRadiusScale);
            ReturnToPool();
        }
    }

    private void ExplodeIntoFragments(Vector2 worldPosition, Vector2 forwardDirection)
    {
        if (didHit && mode != ProjectileMode.AirburstGrenade)
            return;

        didHit = true;
        if (owner != null)
            owner.SpawnAirburstFragments(worldPosition, forwardDirection, rockWall, blastRadiusScale);

        ReturnToPool();
    }

    private bool ShouldDespawnOutsideWallBounds()
    {
        if (rockWall == null)
            return false;

        Vector2 wallCenter = rockWall.transform.position;
        float padding = mode == ProjectileMode.Fragment ? FragmentBoundsDespawnPadding : ProjectileBoundsDespawnPadding;
        float halfWidth = (rockWall.WorldWidth * 0.5f) + padding;
        float halfHeight = (rockWall.WorldHeight * 0.5f) + padding;
        Vector2 position = transform.position;
        Vector2 velocity = body != null ? body.linearVelocity : (lastTravelDirection * 0.01f);

        bool outsideLeft = position.x < wallCenter.x - halfWidth;
        bool outsideRight = position.x > wallCenter.x + halfWidth;
        bool outsideBottom = position.y < wallCenter.y - halfHeight;
        bool outsideTop = position.y > wallCenter.y + halfHeight;

        if (outsideLeft && velocity.x <= 0f)
            return true;
        if (outsideRight && velocity.x >= 0f)
            return true;
        if (outsideBottom && velocity.y <= 0f)
            return true;
        if (outsideTop && velocity.y >= 0f)
            return true;

        return false;
    }

    private void ReturnToPool()
    {
        didHit = false;
        lifetimeRemaining = MaxLifetime;
        airburstFuseRemaining = 0f;
        mode = ProjectileMode.Standard;
        rockWall = null;
        transform.localScale = StandardScale;

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
