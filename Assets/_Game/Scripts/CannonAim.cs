using UnityEngine;
using UnityEngine.InputSystem;

public class CannonAim : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float upwardThrustAcceleration = 26f;
    [SerializeField] private float downwardThrustAcceleration = 18f;
    [SerializeField] private float maxRiseSpeed = 8.5f;
    [SerializeField] private float maxFallSpeed = 14f;
    [SerializeField] private float collisionSkin = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool enableCollisionDebugLogging = true;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private Camera sceneCamera;
    private Rigidbody2D body;
    private Collider2D movementCollider;
    private Vector2 moveInput;
    private ContactFilter2D movementContactFilter;

    public void Initialize(Camera targetCamera)
    {
        sceneCamera = targetCamera;
        CachePhysicsComponents();
    }

    private void Awake()
    {
        CachePhysicsComponents();
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
        movementContactFilter.useTriggers = false;
        movementContactFilter.useLayerMask = false;
        movementContactFilter.useDepth = false;
        movementContactFilter.useNormalAngle = false;
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
        Vector2 desiredTarget = ClampPointToCamera(currentPosition + (moveInput * (moveSpeed * Time.fixedDeltaTime)));
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
        velocity.x = moveInput.x * moveSpeed;

        float deltaTime = Time.fixedDeltaTime;
        float gravityCompensation = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0f, body.gravityScale);

        if (moveInput.y > 0.01f)
        {
            float riseAcceleration = upwardThrustAcceleration + gravityCompensation;
            velocity.y = Mathf.MoveTowards(velocity.y, maxRiseSpeed, riseAcceleration * moveInput.y * deltaTime);
        }
        else if (moveInput.y < -0.01f)
        {
            velocity.y = Mathf.MoveTowards(velocity.y, -maxFallSpeed, downwardThrustAcceleration * -moveInput.y * deltaTime);
        }

        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        body.linearVelocity = velocity;
        ClampDynamicBodyToCamera();
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

        Vector3 screenPoint = mouse.position.ReadValue();
        Vector3 worldPoint = sceneCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(sceneCamera.transform.position.z)));
        Vector2 direction = worldPoint - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
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
