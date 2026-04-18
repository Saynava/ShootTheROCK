using UnityEngine;

public class AutoShooter : MonoBehaviour
{
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

    public int AttackSpeedLevel => attackSpeedLevel;
    public float CurrentFireInterval => fireInterval;
    public int NextAttackSpeedUpgradeCost => baseAttackSpeedUpgradeCost + (attackSpeedLevel * attackSpeedUpgradeCostStep);
    public bool CanUpgradeAttackSpeed => fireInterval > minimumFireInterval + 0.0001f;

    public int DamageLevel => damageLevel;
    public float BlastRadiusScale => blastRadiusScale;
    public int NextDamageUpgradeCost => baseDamageUpgradeCost + (damageLevel * damageUpgradeCostStep);
    public bool CanUpgradeDamage => blastRadiusScale < maximumBlastRadiusScale - 0.0001f;

    public void Initialize(Transform firePoint, RockWall rockWall)
    {
        this.firePoint = firePoint;
        this.rockWall = rockWall;
        nextShotTime = Time.time + 0.15f;
    }

    private void Update()
    {
        if (firePoint == null || rockWall == null)
            return;

        if (Time.time < nextShotTime)
            return;

        nextShotTime = Time.time + fireInterval;
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

    private void Fire()
    {
        GameObject projectileObject = ShootTheRockPrototypeBootstrap.CreateSpriteObject(
            "Projectile",
            null,
            firePoint.position,
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

        Projectile projectile = projectileObject.AddComponent<Projectile>();
        projectile.Initialize((Vector2)firePoint.right, projectileSpeed, rockWall, body, blastRadiusScale);
    }
}
