using System.Collections.Generic;
using UnityEngine;

public class AutoShooter : MonoBehaviour
{
    private const int PrewarmProjectileCount = 8;
    private const float DefaultStressShotgunFireInterval = 0.06f;
    private const float MinimumStressShotgunFireInterval = 0.02f;
    private const float MaximumStressShotgunFireInterval = 0.2f;
    private const int DefaultStressShotgunPelletCount = 11;
    private const int MinimumStressShotgunPelletCount = 1;
    private const int MaximumStressShotgunPelletCount = 25;
    private const float StressShotgunSpreadAngle = 28f;
    private const float AirburstGrenadeFuseTime = 0.43f;
    private const float AirburstGrenadeSpeedMultiplier = 0.72f;
    private const int DefaultAirburstFragmentCount = 13;
    private const int MinimumAirburstFragmentCount = 1;
    private const int MaximumAirburstFragmentCount = 25;
    private const float AirburstFragmentSpreadAngle = 46f;
    private const float AirburstFragmentSpeedMultiplier = 1.25f;
    private const float AirburstFragmentBlastScaleMultiplier = 0.45f;
    private const float AirburstFragmentGravity = 0.18f;
    private const float CorrosionEffectDuration = 4.4f;
    private const float CorrosionEffectTickInterval = 0.14f;
    private const float CorrosionDamagePerTick = 0.42f;
    private const float CorrosionBlastScaleMultiplier = 1.05f;
    private const float CorrosionMinimumBlastScale = 1.15f;
    private const float DefaultDebugBlastScaleMultiplier = 1.45f;
    private const float MinimumDebugBlastScaleMultiplier = 0.5f;
    private const float MaximumDebugBlastScaleMultiplier = 4f;
    private const float DefaultCorrosionRadiusMultiplier = 1.6f;
    private const float MinimumCorrosionRadiusMultiplier = 0.5f;
    private const float MaximumCorrosionRadiusMultiplier = 4f;

    [Header("Firing")]
    [SerializeField] private float fireInterval = 2f;
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float projectileGravity = 1.1f;

    [Header("Attack Speed Upgrade")]
    [SerializeField] private float fireIntervalStep = 0.18f;
    [SerializeField] private float minimumFireInterval = 0.35f;
    [SerializeField] private int baseAttackSpeedUpgradeCost = 10;
    [SerializeField] private int attackSpeedUpgradeCostStep = 8;

    [Header("Damage Upgrade")]
    [SerializeField] private float blastRadiusScale = 1f;
    [SerializeField] private float blastRadiusScaleStep = 0.22f;
    [SerializeField] private float maximumBlastRadiusScale = 2.75f;
    [SerializeField] private int baseDamageUpgradeCost = 14;
    [SerializeField] private int damageUpgradeCostStep = 12;

    [Header("Debug Stress Tuning")]
    [SerializeField] private float debugBlastScaleMultiplier = DefaultDebugBlastScaleMultiplier;
    [SerializeField] private float corrosionRadiusMultiplier = DefaultCorrosionRadiusMultiplier;
    [SerializeField] private float stressShotgunFireInterval = DefaultStressShotgunFireInterval;
    [SerializeField] private int stressShotgunPelletCount = DefaultStressShotgunPelletCount;
    [SerializeField] private int airburstFragmentCount = DefaultAirburstFragmentCount;

    private Transform firePoint;
    private RockWall rockWall;
    private MotherloadWorldController motherloadWorldController;
    private float nextShotTime;
    private int attackSpeedLevel;
    private int damageLevel;
    private bool stressShotgunEnabled;
    private bool airburstGrenadeEnabled;
    private bool corrosionEnabled;
    private Transform projectilePoolRoot;
    private readonly Queue<Projectile> projectilePool = new Queue<Projectile>();

    public int AttackSpeedLevel => attackSpeedLevel;
    public float CurrentFireInterval => fireInterval;
    public int NextAttackSpeedUpgradeCost => baseAttackSpeedUpgradeCost + (attackSpeedLevel * attackSpeedUpgradeCostStep);
    public bool CanUpgradeAttackSpeed => fireInterval > minimumFireInterval + 0.0001f;

    public int DamageLevel => damageLevel;
    public float BlastRadiusScale => blastRadiusScale;
    public int NextDamageUpgradeCost => baseDamageUpgradeCost + (damageLevel * damageUpgradeCostStep);
    public bool CanUpgradeDamage => blastRadiusScale < maximumBlastRadiusScale - 0.0001f;
    public bool StressShotgunEnabled => stressShotgunEnabled;
    public bool AirburstGrenadeEnabled => airburstGrenadeEnabled;
    public bool CorrosionEnabled => corrosionEnabled;
    public float CurrentEffectiveFireInterval => stressShotgunEnabled ? stressShotgunFireInterval : fireInterval;
    public float DebugBlastScaleMultiplier => debugBlastScaleMultiplier;
    public float CorrosionRadiusMultiplier => corrosionRadiusMultiplier;
    public float StressShotgunFireInterval => stressShotgunFireInterval;
    public int StressShotgunPelletCount => stressShotgunPelletCount;
    public int AirburstFragmentCount => airburstFragmentCount;
    public float CurrentProjectileBlastScale => blastRadiusScale * debugBlastScaleMultiplier;

    public void Initialize(Transform firePoint, RockWall rockWall, MotherloadWorldController motherloadWorldController = null)
    {
        this.firePoint = firePoint;
        this.rockWall = rockWall;
        this.motherloadWorldController = motherloadWorldController;
        nextShotTime = Time.time + 0.15f;
        EnsureProjectilePoolRoot();
        PrewarmProjectilePool();
    }

    private void Update()
    {
        if (firePoint == null || (rockWall == null && motherloadWorldController == null))
            return;

        if (Time.time < nextShotTime)
            return;

        nextShotTime = Time.time + CurrentEffectiveFireInterval;
        Fire();
    }

    public bool TryUpgradeAttackSpeed()
    {
        if (!CanUpgradeAttackSpeed)
            return false;

        float updatedInterval = Mathf.Max(minimumFireInterval, fireInterval - fireIntervalStep);
        if (Mathf.Approximately(updatedInterval, fireInterval))
            return false;

        fireInterval = updatedInterval;
        attackSpeedLevel++;
        return true;
    }

    public bool TryUpgradeDamage()
    {
        if (!CanUpgradeDamage)
            return false;

        float updatedBlastScale = Mathf.Min(maximumBlastRadiusScale, blastRadiusScale + blastRadiusScaleStep);
        if (Mathf.Approximately(updatedBlastScale, blastRadiusScale))
            return false;

        blastRadiusScale = updatedBlastScale;
        damageLevel++;
        return true;
    }

    public void ReleaseProjectile(Projectile projectile)
    {
        if (projectile == null)
            return;

        EnsureProjectilePoolRoot();
        projectile.transform.SetParent(projectilePoolRoot, false);
        projectile.transform.localPosition = Vector3.zero;
        projectile.gameObject.SetActive(false);
        projectilePool.Enqueue(projectile);
    }

    public void ToggleStressShotgun()
    {
        stressShotgunEnabled = !stressShotgunEnabled;
        nextShotTime = Time.time + 0.05f;
    }

    public void ToggleAirburstGrenade()
    {
        airburstGrenadeEnabled = !airburstGrenadeEnabled;
        nextShotTime = Time.time + 0.05f;
    }

    public void ToggleCorrosion()
    {
        corrosionEnabled = !corrosionEnabled;
        nextShotTime = Time.time + 0.05f;
    }

    public void SetDebugBlastScaleMultiplier(float value)
    {
        debugBlastScaleMultiplier = Mathf.Clamp(value, MinimumDebugBlastScaleMultiplier, MaximumDebugBlastScaleMultiplier);
    }

    public void SetCorrosionRadiusMultiplier(float value)
    {
        corrosionRadiusMultiplier = Mathf.Clamp(value, MinimumCorrosionRadiusMultiplier, MaximumCorrosionRadiusMultiplier);
    }

    public void SetStressShotgunFireInterval(float value)
    {
        stressShotgunFireInterval = Mathf.Clamp(value, MinimumStressShotgunFireInterval, MaximumStressShotgunFireInterval);
        nextShotTime = Mathf.Min(nextShotTime, Time.time + stressShotgunFireInterval);
    }

    public void SetStressShotgunPelletCount(float value)
    {
        stressShotgunPelletCount = Mathf.Clamp(Mathf.RoundToInt(value), MinimumStressShotgunPelletCount, MaximumStressShotgunPelletCount);
    }

    public void SetAirburstFragmentCount(float value)
    {
        airburstFragmentCount = Mathf.Clamp(Mathf.RoundToInt(value), MinimumAirburstFragmentCount, MaximumAirburstFragmentCount);
    }

    private void Fire()
    {
        using (ShootTheRockPerformance.FireMarker.Auto())
        {
            ShootTheRockPerformance.RecordShot();

            if (stressShotgunEnabled && airburstGrenadeEnabled)
            {
                FireAirburstShotgun();
                return;
            }

            if (stressShotgunEnabled)
            {
                FireStressShotgun();
                return;
            }

            if (airburstGrenadeEnabled)
            {
                FireAirburstGrenade();
                return;
            }

            FireSingleProjectile((Vector2)firePoint.right);
        }
    }

    private void FireStressShotgun()
    {
        float halfSpread = StressShotgunSpreadAngle * 0.5f;
        for (int i = 0; i < stressShotgunPelletCount; i++)
        {
            float t = stressShotgunPelletCount == 1 ? 0.5f : i / (float)(stressShotgunPelletCount - 1);
            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * firePoint.right;
            FireSingleProjectile(direction);
        }
    }

    private void FireSingleProjectile(Vector2 direction)
    {
        float resolvedBlastScale = CurrentProjectileBlastScale;
        ShootTheRockPerformance.RecordPellet();
        Projectile projectile = AcquireProjectile();
        projectile.Launch(firePoint.position, direction, projectileSpeed, rockWall, motherloadWorldController, resolvedBlastScale, projectileGravity);
        ConfigureProjectileCorrosion(projectile, resolvedBlastScale);
    }

    private void FireAirburstGrenade()
    {
        LaunchAirburstGrenade((Vector2)firePoint.right, AirburstGrenadeFuseTime);
    }

    private void FireAirburstShotgun()
    {
        float halfSpread = StressShotgunSpreadAngle * 0.5f;
        for (int i = 0; i < stressShotgunPelletCount; i++)
        {
            float t = stressShotgunPelletCount == 1 ? 0.5f : i / (float)(stressShotgunPelletCount - 1);
            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            Vector2 direction = (Quaternion.Euler(0f, 0f, angle) * firePoint.right).normalized;
            LaunchAirburstGrenade(direction, AirburstGrenadeFuseTime);
        }
    }

    private void LaunchAirburstGrenade(Vector2 direction, float fuseTime)
    {
        float resolvedBlastScale = CurrentProjectileBlastScale;
        ShootTheRockPerformance.RecordPellet();
        Projectile projectile = AcquireProjectile();
        projectile.LaunchAirburstGrenade(
            firePoint.position,
            direction,
            projectileSpeed * AirburstGrenadeSpeedMultiplier,
            rockWall,
            motherloadWorldController,
            resolvedBlastScale,
            fuseTime,
            projectileGravity);
        ConfigureProjectileCorrosion(projectile, resolvedBlastScale);
    }

    public void SpawnAirburstFragments(
        Vector2 worldPosition,
        Vector2 forwardDirection,
        RockWall sourceWall,
        MotherloadWorldController sourceMotherloadWorldController,
        float sourceBlastScale,
        bool applyCorrosion,
        float corrosionDuration,
        float corrosionTickInterval,
        float corrosionDamagePerTick,
        float corrosionBlastScale,
        bool corrosionAllowDestroyCells)
    {
        Vector2 normalizedForward = forwardDirection.sqrMagnitude > 0.0001f ? forwardDirection.normalized : (Vector2)firePoint.right;
        float halfSpread = AirburstFragmentSpreadAngle * 0.5f;
        float fragmentBlastScale = Mathf.Max(0.2f, sourceBlastScale * AirburstFragmentBlastScaleMultiplier);

        for (int i = 0; i < airburstFragmentCount; i++)
        {
            float t = airburstFragmentCount == 1 ? 0.5f : i / (float)(airburstFragmentCount - 1);
            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            Vector2 fragmentDirection = (Quaternion.Euler(0f, 0f, angle) * normalizedForward).normalized;

            ShootTheRockPerformance.RecordPellet();
            Projectile fragment = AcquireProjectile();
            fragment.LaunchFragment(
                worldPosition,
                fragmentDirection,
                projectileSpeed * AirburstFragmentSpeedMultiplier,
                sourceWall != null ? sourceWall : rockWall,
                sourceMotherloadWorldController != null ? sourceMotherloadWorldController : motherloadWorldController,
                fragmentBlastScale,
                AirburstFragmentGravity);
            fragment.ConfigureCorrosion(
                applyCorrosion,
                corrosionDuration,
                corrosionTickInterval,
                corrosionDamagePerTick,
                corrosionBlastScale,
                corrosionAllowDestroyCells);
        }
    }

    private Projectile AcquireProjectile()
    {
        while (projectilePool.Count > 0)
        {
            Projectile pooledProjectile = projectilePool.Dequeue();
            if (pooledProjectile != null)
                return pooledProjectile;
        }

        ShootTheRockPerformance.RecordProjectilePoolMiss();
        return CreateProjectileInstance();
    }

    private void ConfigureProjectileCorrosion(Projectile projectile, float sourceBlastScale)
    {
        if (projectile == null)
            return;

        float width01 = Mathf.InverseLerp(MinimumCorrosionRadiusMultiplier, MaximumCorrosionRadiusMultiplier, corrosionRadiusMultiplier);
        float corrosionReachScale = Mathf.Lerp(1f, 2.7f, width01);
        float sourceInfluence = Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(1f, 4f, sourceBlastScale));
        float corrosionDamageBoost = Mathf.Lerp(0.9f, 2.25f, width01);

        projectile.ConfigureCorrosion(
            corrosionEnabled,
            CorrosionEffectDuration,
            CorrosionEffectTickInterval,
            CorrosionDamagePerTick * corrosionDamageBoost,
            Mathf.Max(CorrosionMinimumBlastScale, corrosionReachScale * sourceInfluence),
            allowDestroyCells: true);
    }

    private void EnsureProjectilePoolRoot()
    {
        if (projectilePoolRoot != null)
            return;

        GameObject poolRootObject = GameObject.Find("ProjectilePool");
        if (poolRootObject == null)
            poolRootObject = new GameObject("ProjectilePool");

        projectilePoolRoot = poolRootObject.transform;
    }

    private void PrewarmProjectilePool()
    {
        if (projectilePool.Count > 0)
            return;

        for (int i = 0; i < PrewarmProjectileCount; i++)
            projectilePool.Enqueue(CreateProjectileInstance());
    }

    private Projectile CreateProjectileInstance()
    {
        EnsureProjectilePoolRoot();

        GameObject projectileObject = ShootTheRockPrototypeBootstrap.CreateSpriteObject(
            "Projectile",
            projectilePoolRoot,
            Vector3.zero,
            new Vector2(0.16f, 0.16f),
            Color.white,
            20);

        CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;

        Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
        body.gravityScale = projectileGravity;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.freezeRotation = true;
        body.simulated = false;

        Projectile projectile = projectileObject.AddComponent<Projectile>();
        projectile.ConfigurePool(this, body);
        projectileObject.SetActive(false);
        return projectile;
    }
}
