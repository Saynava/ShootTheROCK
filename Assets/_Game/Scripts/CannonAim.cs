using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CannonAim : MonoBehaviour
{
    public event Action FuelChanged;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 9.25f;
    [SerializeField] private float horizontalThrustAcceleration = 8.5f;
    [SerializeField] private float airBrakeAcceleration = 0.45f;
    [SerializeField] private float groundBrakeAcceleration = 12f;
    [SerializeField] private float airDrag = 0.015f;
    [SerializeField] private float upwardThrustAcceleration = 13.5f;
    [SerializeField] private float downwardThrustAcceleration = 5f;
    [SerializeField] private float maxRiseSpeed = 6f;
    [SerializeField] private float maxFallSpeed = 12.5f;
    [SerializeField] private float controlledGravityScale = 0.55f;
    [SerializeField] private float driftGravityScale = 0.7f;
    [SerializeField] private float runawayFallGravityScale = 1.45f;
    [SerializeField] private float fallGravityRampStartSpeed = 3.2f;
    [SerializeField] private float fallGravityRampDelay = 0.38f;
    [SerializeField] private float fallGravityRampDuration = 1.8f;
    [SerializeField] private float collisionSkin = 0.02f;
    [SerializeField] private float groundedCheckDistance = 0.08f;

    [Header("Fuel")]
    [SerializeField] private bool enableFuelSystem;
    [Min(1f)] [SerializeField] private float maxFuel = 48f;
    [Min(0f)] [SerializeField] private float horizontalFuelBurnPerSecond = 3.4f;
    [Min(0f)] [SerializeField] private float upwardFuelBurnPerSecond = 8.2f;
    [Min(0f)] [SerializeField] private float downwardFuelBurnPerSecond = 1.2f;

    [Header("Motherload Impact Damage")]
    [SerializeField] private bool enableFallDamage = true;
    [Min(0f)] [SerializeField] private float fallDamageSafeImpactSpeed = 0.9f;
    [Min(0f)] [SerializeField] private float mediumImpactDamageSpeed = 1.8f;
    [Min(0f)] [SerializeField] private float heavyImpactDamageSpeed = 2.6f;
    [Min(0f)] [SerializeField] private float severeImpactDamageSpeed = 3.5f;
    [Min(0f)] [SerializeField] private float fallDamageFatalImpactSpeed = 6.5f;
    [Min(1)] [SerializeField] private int minorFallDamage = 1;
    [Min(1)] [SerializeField] private int mediumImpactDamage = 2;
    [Min(1)] [SerializeField] private int heavyImpactDamage = 6;
    [Min(1)] [SerializeField] private int severeImpactDamage = 12;
    [Min(1)] [SerializeField] private int fatalFallDamage = 30;
    [Min(0f)] [SerializeField] private float fallDamageCooldown = 0.3f;
    [Min(0.02f)] [SerializeField] private float fallImpactMemorySeconds = 0.35f;

    [Header("Fuel Shop")]
    [Range(1, 4)] [SerializeField] private int fuelTankRank = 1;
    [Min(1f)] [SerializeField] private float rank1FuelCapacity = 48f;
    [Min(1f)] [SerializeField] private float rank2FuelCapacity = 72f;
    [Min(1f)] [SerializeField] private float rank3FuelCapacity = 104f;
    [Min(1f)] [SerializeField] private float rank4FuelCapacity = 144f;
    [Min(0)] [SerializeField] private int rank2FuelUpgradeCost = 30;
    [Min(0)] [SerializeField] private int rank3FuelUpgradeCost = 72;
    [Min(0)] [SerializeField] private int rank4FuelUpgradeCost = 130;
    [Min(0.01f)] [SerializeField] private float fuelRefillCostPerUnit = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool enableCollisionDebugLogging = true;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private Camera sceneCamera;
    private Rigidbody2D body;
    private Collider2D movementCollider;
    private AutoShooter shooter;
    private MotherloadPlayerVitals motherloadVitals;
    private Transform aimBarrel;
    private Transform aimFirePoint;
    private Vector2 moveInput;
    private ContactFilter2D movementContactFilter;
    private float currentFuel;
    private float motherloadMovementMultiplier = 1f;
    private float nextFallDamageTime;
    private Vector2 recentImpactVelocity;
    private float recentImpactVelocityTime;
    private float uncontrolledFallTime;
    private float aimBarrelDistance = 0.54f;
    private float aimFirePointDistance = 1.06f;
    private int motherloadFuelUpgradeRank;
    private bool useMotherloadFuelCapacity;
    private bool fuelInitialized;
    private bool isDockedAtBase;
    private bool isOutOfFuel;

    public bool FuelSystemEnabled => enableFuelSystem;
    public float CurrentFuel => currentFuel;
    public float MaxFuel => Mathf.Max(1f, maxFuel);
    public float FuelNormalized => Mathf.Clamp01(currentFuel / Mathf.Max(1f, maxFuel));
    public bool IsDockedAtBase => isDockedAtBase;
    public bool IsOutOfFuel => enableFuelSystem && isOutOfFuel;
    public int FuelTankRank => Mathf.Clamp(fuelTankRank, 1, 4);
    public int MaxFuelTankRank => 4;

    public void Initialize(Camera targetCamera)
    {
        sceneCamera = targetCamera;
        CachePhysicsComponents();
        EnsureFuelInitialized();
    }

    private void Awake()
    {
        CachePhysicsComponents();
        EnsureFuelInitialized();
    }

    public void ConfigureFuelSystem(bool enabled)
    {
        enableFuelSystem = enabled;
        isDockedAtBase = false;
        if (body != null && enableFuelSystem)
            body.gravityScale = controlledGravityScale;
        SyncFuelCapacity(refillToFull: enabled || !fuelInitialized);
        isOutOfFuel = enableFuelSystem && currentFuel <= 0.001f;

        UpdateShooterFuelLock();
        NotifyFuelChanged();
    }

    public void SetDockedAtBase(bool value)
    {
        if (isDockedAtBase == value)
            return;

        isDockedAtBase = value;

        if (enableFuelSystem && isDockedAtBase)
        {
            currentFuel = MaxFuel;
            isOutOfFuel = false;
            UpdateShooterFuelLock();
        }

        NotifyFuelChanged();
    }

    public float GetFuelTankCapacityForRank(int rank)
    {
        switch (Mathf.Clamp(rank, 1, 4))
        {
            case 1:
                return Mathf.Max(1f, rank1FuelCapacity);
            case 2:
                return Mathf.Max(1f, rank2FuelCapacity);
            case 3:
                return Mathf.Max(1f, rank3FuelCapacity);
            case 4:
                return Mathf.Max(1f, rank4FuelCapacity);
            default:
                return Mathf.Max(1f, rank1FuelCapacity);
        }
    }

    public int GetFuelTankUpgradeCostForRank(int rank)
    {
        switch (Mathf.Clamp(rank, 1, 4))
        {
            case 2:
                return Mathf.Max(0, rank2FuelUpgradeCost);
            case 3:
                return Mathf.Max(0, rank3FuelUpgradeCost);
            case 4:
                return Mathf.Max(0, rank4FuelUpgradeCost);
            default:
                return 0;
        }
    }

    public bool IsFuelTankRankOwned(int rank)
    {
        return FuelTankRank >= Mathf.Clamp(rank, 1, MaxFuelTankRank);
    }

    public bool CanBuyFuelTankRank(int rank)
    {
        int resolvedRank = Mathf.Clamp(rank, 1, MaxFuelTankRank);
        return enableFuelSystem && resolvedRank > 1 && resolvedRank == FuelTankRank + 1;
    }

    public bool TryUpgradeFuelTankToRank(int rank)
    {
        int resolvedRank = Mathf.Clamp(rank, 1, MaxFuelTankRank);
        if (!CanBuyFuelTankRank(resolvedRank))
            return false;

        fuelTankRank = resolvedRank;
        SyncFuelCapacity(refillToFull: false);
        isOutOfFuel = enableFuelSystem && currentFuel <= 0.001f;
        UpdateShooterFuelLock();
        NotifyFuelChanged();
        return true;
    }

    public void ApplyMotherloadFuelTankRank(int rank)
    {
        int resolvedRank = Mathf.Clamp(rank, 0, 4);
        if (useMotherloadFuelCapacity && motherloadFuelUpgradeRank == resolvedRank && fuelInitialized)
            return;

        useMotherloadFuelCapacity = true;
        motherloadFuelUpgradeRank = resolvedRank;
        fuelTankRank = Mathf.Clamp(resolvedRank + 1, 1, MaxFuelTankRank);
        SyncFuelCapacity(refillToFull: true);
        isOutOfFuel = enableFuelSystem && currentFuel <= 0.001f;
        UpdateShooterFuelLock();
        NotifyFuelChanged();
    }

    public void SetMotherloadMovementMultiplier(float multiplier)
    {
        motherloadMovementMultiplier = Mathf.Max(0.25f, multiplier);
    }

    public void ForceRefillFuel()
    {
        SyncFuelCapacity(refillToFull: true);
        isOutOfFuel = false;
        UpdateShooterFuelLock();
        NotifyFuelChanged();
    }

    public void ResetMotion()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.rotation = 0f;
        }

        transform.rotation = Quaternion.identity;
        recentImpactVelocity = Vector2.zero;
        recentImpactVelocityTime = 0f;
        uncontrolledFallTime = 0f;
        if (body != null && enableFuelSystem)
            body.gravityScale = controlledGravityScale;
    }

    public void ApplyHubDomeBrake(float maxDownwardSpeed, float brakeAcceleration)
    {
        if (body == null)
            CachePhysicsComponents();
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
            return;

        Vector2 velocity = body.linearVelocity;
        float targetDownwardSpeed = Mathf.Max(0f, maxDownwardSpeed);
        if (velocity.y < -targetDownwardSpeed)
            velocity.y = Mathf.MoveTowards(velocity.y, -targetDownwardSpeed, Mathf.Max(0f, brakeAcceleration) * Time.fixedDeltaTime);

        body.linearVelocity = velocity;
        uncontrolledFallTime = 0f;
        if (enableFuelSystem)
            body.gravityScale = Mathf.Min(body.gravityScale, controlledGravityScale);

        recentImpactVelocity = velocity;
        recentImpactVelocityTime = Time.time;
    }

    public int GetFullRefillCost()
    {
        if (!enableFuelSystem)
            return 0;

        float missingFuel = Mathf.Max(0f, MaxFuel - currentFuel);
        if (missingFuel <= 0.01f)
            return 0;

        return Mathf.Max(1, Mathf.CeilToInt(missingFuel * Mathf.Max(0.01f, fuelRefillCostPerUnit)));
    }

    public bool TryRefillToFull()
    {
        if (!enableFuelSystem)
            return false;

        if (currentFuel >= MaxFuel - 0.01f)
            return false;

        currentFuel = MaxFuel;
        isOutOfFuel = false;
        UpdateShooterFuelLock();
        NotifyFuelChanged();
        return true;
    }

    private void Update()
    {
        if (sceneCamera == null)
            return;

        moveInput = ReadMovementInput();
        HandleAim();
    }

    private void FixedUpdate()
    {
        if (sceneCamera == null)
            return;

        ApplyMovement();
    }

    private void CachePhysicsComponents()
    {
        body = body != null ? body : GetComponent<Rigidbody2D>();
        movementCollider = movementCollider != null ? movementCollider : GetComponent<Collider2D>();
        shooter = shooter != null ? shooter : GetComponent<AutoShooter>();
        motherloadVitals = motherloadVitals != null ? motherloadVitals : GetComponent<MotherloadPlayerVitals>();
        CacheAimVisuals();
        movementContactFilter.useTriggers = false;
        movementContactFilter.useLayerMask = false;
        movementContactFilter.useDepth = false;
        movementContactFilter.useNormalAngle = false;
    }

    private void CacheAimVisuals()
    {
        if (aimBarrel == null)
            aimBarrel = transform.Find("Barrel");
        if (aimFirePoint == null)
            aimFirePoint = transform.Find("FirePoint");

        if (aimBarrel != null && aimBarrel.localPosition.sqrMagnitude > 0.001f)
            aimBarrelDistance = aimBarrel.localPosition.magnitude;
        if (aimFirePoint != null && aimFirePoint.localPosition.sqrMagnitude > 0.001f)
            aimFirePointDistance = aimFirePoint.localPosition.magnitude;
    }

    private void EnsureFuelInitialized()
    {
        if (fuelInitialized)
            return;

        SyncFuelCapacity(refillToFull: true);
    }

    private void SyncFuelCapacity(bool refillToFull)
    {
        maxFuel = useMotherloadFuelCapacity
            ? GetMotherloadFuelCapacityForUpgradeRank(motherloadFuelUpgradeRank)
            : GetFuelTankCapacityForRank(FuelTankRank);

        if (!fuelInitialized || refillToFull)
            currentFuel = maxFuel;
        else
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);

        fuelInitialized = true;
    }

    private float GetMotherloadFuelCapacityForUpgradeRank(int rank)
    {
        switch (Mathf.Clamp(rank, 0, 4))
        {
            case 0:
                return Mathf.Max(1f, rank1FuelCapacity);
            case 1:
                return Mathf.Max(1f, rank2FuelCapacity);
            case 2:
                return Mathf.Max(1f, rank3FuelCapacity);
            case 3:
                return Mathf.Max(1f, rank4FuelCapacity);
            default:
                return Mathf.Max(1f, rank4FuelCapacity + ((rank4FuelCapacity - rank3FuelCapacity) * 1.2f));
        }
    }

    private Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float horizontal = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            horizontal += 1f;

        float vertical = 0f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.spaceKey.isPressed)
            vertical += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            vertical -= 1f;

        return new Vector2(Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));
    }

    private void ApplyMovement()
    {
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
        {
            ApplyDynamicMovement();
            return;
        }

        if (moveInput.sqrMagnitude <= 0.0001f)
            return;

        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
        Vector2 desiredTarget = ClampPointToCamera(currentPosition + (moveInput * (moveSpeed * motherloadMovementMultiplier * Time.fixedDeltaTime)));
        Vector2 desiredDelta = desiredTarget - currentPosition;
        if (desiredDelta.sqrMagnitude <= 0.0001f)
            return;

        Vector2 resolvedTarget = desiredTarget;
        if (body != null && movementCollider != null)
        {
            float distance = desiredDelta.magnitude;
            Vector2 direction = desiredDelta / Mathf.Max(0.0001f, distance);
            int hitCount = body.Cast(direction, movementContactFilter, castHits, distance + collisionSkin);
            float allowedDistance = distance;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = castHits[i];
                if (!IsBlockingMovementHit(hit.collider))
                    continue;

                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - collisionSkin));
            }

            resolvedTarget = currentPosition + (direction * allowedDistance);
            resolvedTarget = ClampPointToCamera(resolvedTarget);
            body.MovePosition(resolvedTarget);
            return;
        }

        transform.position = resolvedTarget;
    }

    private void ApplyDynamicMovement()
    {
        Vector2 velocity = body.linearVelocity;

        float deltaTime = Time.fixedDeltaTime;
        UpdateFuelState(deltaTime);
        bool grounded = IsGrounded();
        bool applyingLift = !IsOutOfFuel && moveInput.y > 0.01f;
        UpdateMotherloadGravityState(velocity, grounded, applyingLift, deltaTime);

        if (IsOutOfFuel)
        {
            velocity.x = ApplyHorizontalSlowdown(velocity.x, grounded, deltaTime);
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
            body.linearVelocity = velocity;
            TrackPotentialImpactVelocity(velocity);
            ClampDynamicBodyToCamera();
            return;
        }

        float resolvedMaxHorizontalSpeed = moveSpeed * motherloadMovementMultiplier;
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float targetHorizontalSpeed = moveInput.x * resolvedMaxHorizontalSpeed;
            velocity.x = Mathf.MoveTowards(velocity.x, targetHorizontalSpeed, horizontalThrustAcceleration * motherloadMovementMultiplier * deltaTime);
        }
        else
        {
            velocity.x = ApplyHorizontalSlowdown(velocity.x, grounded, deltaTime);
        }

        float gravityCompensation = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0f, body.gravityScale);

        if (moveInput.y > 0.01f)
        {
            float riseAcceleration = upwardThrustAcceleration + gravityCompensation;
            velocity.y += riseAcceleration * moveInput.y * deltaTime;
        }
        else if (moveInput.y < -0.01f)
        {
            velocity.y -= downwardThrustAcceleration * -moveInput.y * deltaTime;
        }

        velocity.x = Mathf.Clamp(velocity.x, -resolvedMaxHorizontalSpeed, resolvedMaxHorizontalSpeed);
        velocity.x *= 1f / (1f + Mathf.Max(0f, airDrag) * deltaTime);
        velocity.y = Mathf.Clamp(velocity.y, -maxFallSpeed, maxRiseSpeed);
        body.linearVelocity = velocity;
        TrackPotentialImpactVelocity(velocity);
        ClampDynamicBodyToCamera();
    }

    private void TrackPotentialImpactVelocity(Vector2 velocity)
    {
        recentImpactVelocity = velocity.sqrMagnitude > 0.01f ? velocity : Vector2.zero;
        recentImpactVelocityTime = Time.time;
    }

    private void UpdateMotherloadGravityState(Vector2 velocity, bool grounded, bool applyingLift, float deltaTime)
    {
        if (!enableFuelSystem || body == null)
            return;

        if (grounded || applyingLift || velocity.y > -fallGravityRampStartSpeed)
            uncontrolledFallTime = Mathf.Max(0f, uncontrolledFallTime - (deltaTime * 2.5f));
        else
            uncontrolledFallTime += deltaTime;

        float ramp = Mathf.InverseLerp(
            Mathf.Max(0f, fallGravityRampDelay),
            Mathf.Max(0.01f, fallGravityRampDelay + fallGravityRampDuration),
            uncontrolledFallTime);

        if (grounded)
        {
            body.gravityScale = controlledGravityScale;
            return;
        }

        body.gravityScale = applyingLift
            ? controlledGravityScale
            : Mathf.Lerp(driftGravityScale, runawayFallGravityScale, ramp);
    }

    private float ApplyHorizontalSlowdown(float horizontalVelocity, bool grounded, float deltaTime)
    {
        float brakeAcceleration = grounded ? groundBrakeAcceleration : airBrakeAcceleration;
        return Mathf.MoveTowards(horizontalVelocity, 0f, Mathf.Max(0f, brakeAcceleration) * deltaTime);
    }

    private bool IsGrounded()
    {
        if (body == null || movementCollider == null)
            return false;

        int hitCount = body.Cast(Vector2.down, movementContactFilter, castHits, groundedCheckDistance + collisionSkin);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;
            if (hit.normal.y > 0.35f)
                return true;
        }

        return false;
    }

    private void UpdateFuelState(float deltaTime)
    {
        if (!enableFuelSystem)
        {
            SyncFuelCapacity(refillToFull: true);
            isOutOfFuel = false;
            UpdateShooterFuelLock();
            return;
        }

        float previousFuel = currentFuel;
        bool previousOutOfFuel = isOutOfFuel;

        float burnRate = Mathf.Abs(moveInput.x) * horizontalFuelBurnPerSecond;
        if (moveInput.y > 0.01f)
            burnRate += moveInput.y * upwardFuelBurnPerSecond;
        else if (moveInput.y < -0.01f)
            burnRate += -moveInput.y * downwardFuelBurnPerSecond;

        if (burnRate > 0.0001f)
            currentFuel = Mathf.Max(0f, currentFuel - (burnRate * deltaTime));

        isOutOfFuel = currentFuel <= 0.001f;
        UpdateShooterFuelLock();

        if (!Mathf.Approximately(previousFuel, currentFuel) || previousOutOfFuel != isOutOfFuel)
        {
            if (isOutOfFuel && !previousOutOfFuel)
                Debug.Log("[MotherloadFuel] Out of fuel. Thrust and shooting disabled until you buy more fuel at base.", this);

            NotifyFuelChanged();
        }
    }

    private void UpdateShooterFuelLock()
    {
        if (shooter == null)
            shooter = GetComponent<AutoShooter>();

        if (shooter == null)
            return;

        shooter.enabled = !enableFuelSystem || !isOutOfFuel;
    }

    private void NotifyFuelChanged()
    {
        FuelChanged?.Invoke();
    }

    private Vector2 ClampPointToCamera(Vector2 point)
    {
        if (sceneCamera == null || !sceneCamera.orthographic)
            return point;

        float radius = ResolveMovementRadius();
        float halfHeight = Mathf.Max(0f, sceneCamera.orthographicSize - radius);
        float halfWidth = Mathf.Max(0f, (sceneCamera.orthographicSize * sceneCamera.aspect) - radius);
        Vector3 cameraPosition = sceneCamera.transform.position;

        return new Vector2(
            Mathf.Clamp(point.x, cameraPosition.x - halfWidth, cameraPosition.x + halfWidth),
            Mathf.Clamp(point.y, cameraPosition.y - halfHeight, cameraPosition.y + halfHeight));
    }

    private void ClampDynamicBodyToCamera()
    {
        if (sceneCamera == null || !sceneCamera.orthographic || body == null)
            return;

        float radius = ResolveMovementRadius();
        float halfWidth = Mathf.Max(0f, (sceneCamera.orthographicSize * sceneCamera.aspect) - radius);
        Vector3 cameraPosition = sceneCamera.transform.position;
        float minX = cameraPosition.x - halfWidth;
        float maxX = cameraPosition.x + halfWidth;

        Vector2 position = body.position;
        float clampedX = Mathf.Clamp(position.x, minX, maxX);
        if (Mathf.Approximately(clampedX, position.x))
            return;

        body.position = new Vector2(clampedX, position.y);
        if ((clampedX <= minX && body.linearVelocity.x < 0f) || (clampedX >= maxX && body.linearVelocity.x > 0f))
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private float ResolveMovementRadius()
    {
        if (movementCollider is CircleCollider2D circleCollider)
            return circleCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        if (movementCollider != null)
            return Mathf.Max(movementCollider.bounds.extents.x, movementCollider.bounds.extents.y);

        return 0f;
    }

    private bool IsBlockingMovementHit(Collider2D hitCollider)
    {
        if (hitCollider == null || hitCollider.isTrigger)
            return false;

        RockWall hitWall = hitCollider.GetComponentInParent<RockWall>();
        if (hitWall != null)
            return true;

        MotherloadChunkRuntime motherloadChunk = hitCollider.GetComponentInParent<MotherloadChunkRuntime>();
        return motherloadChunk != null;
    }

    private void HandleAim()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;
        CacheAimVisuals();

        Vector3 screenPoint = mouse.position.ReadValue();
        Vector3 worldPoint = sceneCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(sceneCamera.transform.position.z)));
        Vector2 direction = worldPoint - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector3 localDirection = direction.normalized;
        Quaternion aimRotation = Quaternion.Euler(0f, 0f, angle);

        transform.rotation = Quaternion.identity;
        if (body != null)
            body.rotation = 0f;

        if (aimBarrel != null)
        {
            aimBarrel.localPosition = localDirection * aimBarrelDistance;
            aimBarrel.localRotation = aimRotation;
        }

        if (aimFirePoint != null)
        {
            aimFirePoint.localPosition = localDirection * aimFirePointDistance;
            aimFirePoint.localRotation = aimRotation;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleFallImpactDamage(collision);

        if (!enableCollisionDebugLogging || collision == null || collision.collider == null)
            return;

        MotherloadChunkRuntime chunk = collision.collider.GetComponentInParent<MotherloadChunkRuntime>();
        if (chunk != null)
        {
            Debug.Log(
                "[MotherloadPlayer] CollisionEnter"
                + " | collider=" + collision.collider.name
                + " | chunk=" + chunk.Coordinate
                + " | point=" + (collision.contactCount > 0 ? collision.GetContact(0).point.ToString("F3") : "none"),
                collision.collider);
            return;
        }

        RockWall wall = collision.collider.GetComponentInParent<RockWall>();
        if (wall != null)
        {
            Debug.Log(
                "[MotherloadPlayer] CollisionEnter"
                + " | collider=" + collision.collider.name
                + " | type=RockWall"
                + " | point=" + (collision.contactCount > 0 ? collision.GetContact(0).point.ToString("F3") : "none"),
                collision.collider);
        }
    }

    private void HandleFallImpactDamage(Collision2D collision)
    {
        if (!enableFallDamage || collision == null || collision.collider == null)
            return;
        if (collision.collider.isTrigger || Time.time < nextFallDamageTime)
            return;
        if (IsSafeLandingCollision(collision))
        {
            ClearImpactState();
            return;
        }

        if (motherloadVitals == null)
            motherloadVitals = GetComponent<MotherloadPlayerVitals>();
        if (motherloadVitals == null)
            return;

        float impactSpeed = ResolveCollisionImpactSpeed(collision);
        float damageImpactSpeed = impactSpeed;
        if (damageImpactSpeed < fallDamageSafeImpactSpeed)
            return;

        nextFallDamageTime = Time.time + fallDamageCooldown;
        ClearImpactState();
        int damage = ResolveImpactDamage(damageImpactSpeed);
        motherloadVitals.ApplyDamage(damage, "Killed by impact");
    }

    private bool IsSafeLandingCollision(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return false;

        MotherloadSafeLandingZone safeLandingZone = collision.collider.GetComponentInParent<MotherloadSafeLandingZone>();
        if (safeLandingZone == null)
            return false;
        if (collision.contactCount <= 0)
            return safeLandingZone.ContainsWorldPoint(transform.position);

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y > 0.35f && safeLandingZone.ContainsWorldPoint(contact.point))
                return true;
        }

        return false;
    }

    private float ResolveCollisionImpactSpeed(Collision2D collision)
    {
        if (collision == null || collision.contactCount <= 0)
            return collision != null ? collision.relativeVelocity.magnitude : 0f;

        Vector2 rememberedVelocity = Time.time - recentImpactVelocityTime <= fallImpactMemorySeconds
            ? recentImpactVelocity
            : Vector2.zero;
        float impactSpeed = 0f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 normal = contact.normal;
            impactSpeed = Mathf.Max(impactSpeed, Mathf.Abs(Vector2.Dot(collision.relativeVelocity, normal)));
            if (rememberedVelocity.sqrMagnitude > 0.0001f)
                impactSpeed = Mathf.Max(impactSpeed, Mathf.Abs(Vector2.Dot(rememberedVelocity, normal)));
        }

        return impactSpeed;
    }

    private void ClearImpactState()
    {
        recentImpactVelocity = Vector2.zero;
        recentImpactVelocityTime = 0f;
        uncontrolledFallTime = 0f;
    }

    private int ResolveImpactDamage(float impactSpeed)
    {
        if (impactSpeed >= fallDamageFatalImpactSpeed)
            return fatalFallDamage;
        if (impactSpeed >= severeImpactDamageSpeed)
            return severeImpactDamage;
        if (impactSpeed >= heavyImpactDamageSpeed)
            return heavyImpactDamage;
        if (impactSpeed >= mediumImpactDamageSpeed)
            return mediumImpactDamage;

        return minorFallDamage;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!enableCollisionDebugLogging || collision == null || collision.collider == null)
            return;

        MotherloadChunkRuntime chunk = collision.collider.GetComponentInParent<MotherloadChunkRuntime>();
        if (chunk != null)
        {
            Debug.Log("[MotherloadPlayer] CollisionExit | collider=" + collision.collider.name + " | chunk=" + chunk.Coordinate, collision.collider);
            return;
        }

        RockWall wall = collision.collider.GetComponentInParent<RockWall>();
        if (wall != null)
            Debug.Log("[MotherloadPlayer] CollisionExit | collider=" + collision.collider.name + " | type=RockWall", collision.collider);
    }
}
