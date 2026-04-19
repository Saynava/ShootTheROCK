using System.Collections.Generic;
using UnityEngine;

public class AutoShooter : MonoBehaviour
{
    private const int PrewarmProjectileCount = 8;
    private const float StressShotgunFireInterval = 0.1f;
    private const int StressShotgunPelletCount = 7;
    private const float StressShotgunSpreadAngle = 28f;

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

    private Transform firePoint;
    private RockWall rockWall;
    private float nextShotTime;
    private int attackSpeedLevel;
    private int damageLevel;
    private bool stressShotgunEnabled;
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
    public float CurrentEffectiveFireInterval => stressShotgunEnabled ? StressShotgunFireInterval : fireInterval;

    public void Initialize(Transform firePoint, RockWall rockWall)
    {
        this.firePoint = firePoint;
        this.rockWall = rockWall;
        nextShotTime = Time.time + 0.15f;
        EnsureProjectilePoolRoot();
        PrewarmProjectilePool();
    }

    private void Update()
    {
        if (firePoint == null || rockWall == null)
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

    private void Fire()
    {
        using (ShootTheRockPerformance.FireMarker.Auto())
        {
            ShootTheRockPerformance.RecordShot();

            if (stressShotgunEnabled)
            {
                FireStressShotgun();
                return;
            }

            FireSingleProjectile((Vector2)firePoint.right);
        }
    }

    private void FireStressShotgun()
    {
        if (StressShotgunPelletCount <= 1)
        {
            FireSingleProjectile((Vector2)firePoint.right);
            return;
        }

        float halfSpread = StressShotgunSpreadAngle * 0.5f;
        for (int i = 0; i < StressShotgunPelletCount; i++)
        {
            float t = StressShotgunPelletCount == 1 ? 0.5f : i / (float)(StressShotgunPelletCount - 1);
            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * firePoint.right;
            FireSingleProjectile(direction);
        }
    }

    private void FireSingleProjectile(Vector2 direction)
    {
        ShootTheRockPerformance.RecordPellet();
        Projectile projectile = AcquireProjectile();
        projectile.Launch(firePoint.position, direction, projectileSpeed, rockWall, blastRadiusScale);
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
