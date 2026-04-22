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
    private Collider2D triggerCollider;
    private bool didHit;
    private float blastRadiusScale = 1f;
    private RockWall rockWall;
    private MotherloadWorldController motherloadWorldController;
    private AutoShooter owner;
    private bool isActiveTracked;
    private ProjectileMode mode;
    private float airburstFuseRemaining;
    private Vector2 lastTravelDirection = Vector2.right;
    private bool corrosionEnabled;
    private float corrosionDuration;
    private float corrosionTickInterval;
    private float corrosionDamagePerTick;
    private float corrosionBlastRadiusScale = 1f;
    private bool corrosionAllowDestroyCells = true;

    private bool ShouldLogMotherloadDebug => motherloadWorldController != null && motherloadWorldController.ShouldLogProjectileHits;

    public void ConfigurePool(AutoShooter owner, Rigidbody2D body)
    {
        this.owner = owner;
        this.body = body;
        triggerCollider = body != null ? body.GetComponent<Collider2D>() : GetComponent<Collider2D>();
    }

    public void Launch(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, MotherloadWorldController motherloadWorldController, float blastRadiusScale, float gravityScale)
    {
        LaunchInternal(worldPosition, direction, speed, rockWall, motherloadWorldController, blastRadiusScale, ProjectileMode.Standard, MaxLifetime, 0f, gravityScale, StandardScale);
    }

    public void LaunchAirburstGrenade(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, MotherloadWorldController motherloadWorldController, float blastRadiusScale, float fuseTime, float gravityScale)
    {
        LaunchInternal(worldPosition, direction, speed, rockWall, motherloadWorldController, blastRadiusScale, ProjectileMode.AirburstGrenade, MaxLifetime, fuseTime, gravityScale, AirburstGrenadeScale);
    }

    public void LaunchFragment(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, MotherloadWorldController motherloadWorldController, float blastRadiusScale, float gravityScale)
    {
        LaunchInternal(worldPosition, direction, speed, rockWall, motherloadWorldController, blastRadiusScale, ProjectileMode.Fragment, FragmentLifetime, 0f, gravityScale, FragmentScale);
    }

    public void ConfigureCorrosion(bool enabled, float duration, float tickInterval, float damagePerTick, float blastRadiusScale, bool allowDestroyCells = true)
    {
        corrosionEnabled = enabled;
        corrosionDuration = Mathf.Max(0.05f, duration);
        corrosionTickInterval = Mathf.Max(0.02f, tickInterval);
        corrosionDamagePerTick = Mathf.Max(0.01f, damagePerTick);
        corrosionBlastRadiusScale = Mathf.Max(0.25f, blastRadiusScale);
        corrosionAllowDestroyCells = allowDestroyCells;
    }

    private void LaunchInternal(Vector2 worldPosition, Vector2 direction, float speed, RockWall rockWall, MotherloadWorldController motherloadWorldController, float blastRadiusScale, ProjectileMode mode, float lifetime, float fuseTime, float gravityScale, Vector3 visualScale)
    {
        this.rockWall = rockWall;
        this.motherloadWorldController = motherloadWorldController;
        this.mode = mode;
        this.blastRadiusScale = Mathf.Max(0.2f, blastRadiusScale);
        this.airburstFuseRemaining = fuseTime;
        this.lastTravelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        didHit = false;
        lifetimeRemaining = lifetime;
        ConfigureCorrosion(false, 0.1f, 0.1f, 0.01f, 1f, true);

        if (triggerCollider != null)
            triggerCollider.enabled = true;

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

        if (ShouldLogMotherloadDebug)
        {
            motherloadWorldController.LogDebug(
                "Projectile launch"
                + " | mode=" + mode
                + " | from=" + worldPosition.ToString("F3")
                + " | dir=" + lastTravelDirection.ToString("F3")
                + " | speed=" + speed.ToString("F2")
                + " | blastScale=" + this.blastRadiusScale.ToString("F2"),
                this);
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

            MotherloadChunkRuntime motherloadChunk = other.GetComponent<MotherloadChunkRuntime>();
            if (motherloadChunk == null)
                motherloadChunk = other.GetComponentInParent<MotherloadChunkRuntime>();

            if (hitWall == null && motherloadChunk == null)
                return;

            didHit = true;
            if (triggerCollider != null)
                triggerCollider.enabled = false;

            Vector2 impactDirection = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                ? body.linearVelocity.normalized
                : lastTravelDirection;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }

            Vector2 queryPoint = (Vector2)transform.position - (impactDirection * 0.18f);
            Vector2 hitPoint = other.ClosestPoint(queryPoint);
            if ((hitPoint - queryPoint).sqrMagnitude <= 0.0001f)
                hitPoint = other.ClosestPoint((Vector2)transform.position - (impactDirection * 0.45f));

            if (rockWall == null)
                rockWall = hitWall;

            if (motherloadWorldController == null && motherloadChunk != null)
                motherloadWorldController = motherloadChunk.GetComponentInParent<MotherloadWorldController>();

            if (mode == ProjectileMode.AirburstGrenade)
            {
                ExplodeIntoFragments(hitPoint, impactDirection);
                return;
            }

            if (rockWall != null)
            {
                rockWall.ApplyHit(hitPoint, impactDirection, blastRadiusScale);
                ApplyCorrosion(hitPoint);
                ReturnToPool();
                return;
            }

            if (motherloadWorldController != null)
            {
                bool changed = motherloadWorldController.TryApplyProjectileHit(hitPoint, impactDirection, blastRadiusScale);
                if (motherloadWorldController.ShouldLogProjectileHits)
                {
                    string chunkName = motherloadChunk != null ? motherloadChunk.name : "no-chunk";
                    motherloadWorldController.LogDebug(
                        "Projectile impact"
                        + " | collider=" + other.name
                        + " | chunk=" + chunkName
                        + " | hitPoint=" + hitPoint.ToString("F3")
                        + " | changed=" + changed,
                        other);
                }
                ReturnToPool();
            }
        }
    }

    private void ExplodeIntoFragments(Vector2 worldPosition, Vector2 forwardDirection)
    {
        if (didHit && mode != ProjectileMode.AirburstGrenade)
            return;

        didHit = true;
        if (owner != null)
            owner.SpawnAirburstFragments(
                worldPosition,
                forwardDirection,
                rockWall,
                motherloadWorldController,
                blastRadiusScale,
                corrosionEnabled,
                corrosionDuration,
                corrosionTickInterval,
                corrosionDamagePerTick,
                corrosionBlastRadiusScale,
                corrosionAllowDestroyCells);

        ReturnToPool();
    }

    private void ApplyCorrosion(Vector2 worldPosition)
    {
        if (!corrosionEnabled || rockWall == null)
            return;

        rockWall.QueueWallEffect(
            worldPosition,
            RockWallEffectType.Corrosion,
            corrosionDuration,
            corrosionTickInterval,
            corrosionDamagePerTick,
            corrosionBlastRadiusScale,
            corrosionAllowDestroyCells);
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
        lifetimeRemaining = MaxLifetime;
        airburstFuseRemaining = 0f;
        mode = ProjectileMode.Standard;
        rockWall = null;
        motherloadWorldController = null;
        transform.localScale = StandardScale;
        ConfigureCorrosion(false, 0.1f, 0.1f, 0.01f, 1f, true);

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
