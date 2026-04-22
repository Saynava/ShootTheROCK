using UnityEngine;

public class CameraFramingController : MonoBehaviour
{
    [SerializeField] private bool autoFrameEnabled;
    [SerializeField] private float smoothTime = 0.55f;
    [SerializeField] private float minimumOrthographicSize = 1f;
    [SerializeField] private float settlePositionDistance = 0.025f;
    [SerializeField] private float settleSizeDistance = 0.025f;
    [SerializeField] private bool preserveCannonViewportAnchor = true;
    [SerializeField] private Vector2 fallbackCannonViewportAnchor = new Vector2(0.18f, 0.22f);
    [SerializeField] private float revealZoomOutMultiplier = 1.18f;
    [SerializeField] private float revealZoomOutPerLevel = 0.45f;
    [SerializeField] private float minimumAnimatedZoomStep = 1.16f;

    private Camera sceneCamera;
    private RockWall rockWall;
    private Transform cannonRoot;
    private Vector3 positionVelocity;
    private float sizeVelocity;
    private Vector3 transientTargetPosition;
    private float transientTargetSize;
    private bool transientFrameActive;

    public bool AutoFrameEnabled => autoFrameEnabled;

    public void Initialize(Camera sceneCamera, RockWall rockWall, Transform cannonRoot)
    {
        this.sceneCamera = sceneCamera;
        this.rockWall = rockWall;
        this.cannonRoot = cannonRoot;
    }

    public void SetAutoFrameEnabled(bool value)
    {
        autoFrameEnabled = value;
        if (autoFrameEnabled)
            SnapToCurrentFrame();
    }

    public void SetPreserveCannonViewportAnchor(bool value)
    {
        preserveCannonViewportAnchor = value;
    }

    public void SnapToCurrentFrame()
    {
        if (!TryBuildTarget(out Vector3 targetPosition, out float targetSize))
            return;

        transientFrameActive = false;
        sceneCamera.transform.position = targetPosition;
        sceneCamera.orthographicSize = targetSize;
    }

    public void AnimateToCurrentFrame()
    {
        if (sceneCamera == null || rockWall == null)
            return;

        if (!rockWall.TryGetCameraFrameData(out Bounds wallBounds, out Vector2 cameraPadding, out Vector2 lookOffset))
            return;

        Vector2 viewportAnchor = ResolveCannonViewportAnchor();
        transientTargetSize = BuildTargetOrthographicSize(wallBounds, cameraPadding, viewportAnchor);

        float perLevelMultiplier = revealZoomOutMultiplier + (Mathf.Max(0, rockWall.CurrentLevelNumber - 2) * revealZoomOutPerLevel);
        transientTargetSize = Mathf.Max(
            transientTargetSize * Mathf.Max(1f, perLevelMultiplier),
            sceneCamera.orthographicSize * Mathf.Max(1.01f, minimumAnimatedZoomStep));

        transientTargetPosition = BuildTargetPosition(wallBounds, lookOffset, transientTargetSize, viewportAnchor);
        transientFrameActive = true;
    }

    private void LateUpdate()
    {
        if (sceneCamera == null || rockWall == null)
            return;

        if (autoFrameEnabled)
        {
            if (!TryBuildTarget(out transientTargetPosition, out transientTargetSize))
                return;
            transientFrameActive = true;
        }

        if (!transientFrameActive)
            return;

        sceneCamera.transform.position = Vector3.SmoothDamp(sceneCamera.transform.position, transientTargetPosition, ref positionVelocity, smoothTime);
        sceneCamera.orthographicSize = Mathf.SmoothDamp(sceneCamera.orthographicSize, transientTargetSize, ref sizeVelocity, smoothTime);

        bool reachedPosition = Vector3.Distance(sceneCamera.transform.position, transientTargetPosition) <= settlePositionDistance;
        bool reachedSize = Mathf.Abs(sceneCamera.orthographicSize - transientTargetSize) <= settleSizeDistance;
        if (!autoFrameEnabled && reachedPosition && reachedSize)
        {
            sceneCamera.transform.position = transientTargetPosition;
            sceneCamera.orthographicSize = transientTargetSize;
            transientFrameActive = false;
        }
    }

    private bool TryBuildTarget(out Vector3 targetPosition, out float targetSize)
    {
        if (sceneCamera == null || rockWall == null)
        {
            targetPosition = default;
            targetSize = minimumOrthographicSize;
            return false;
        }

        if (!rockWall.TryGetCameraFrameData(out Bounds wallBounds, out Vector2 cameraPadding, out Vector2 lookOffset))
        {
            targetPosition = default;
            targetSize = minimumOrthographicSize;
            return false;
        }

        Vector2 viewportAnchor = ResolveCannonViewportAnchor();
        targetSize = BuildTargetOrthographicSize(wallBounds, cameraPadding, viewportAnchor);
        targetPosition = BuildTargetPosition(wallBounds, lookOffset, targetSize, viewportAnchor);
        return true;
    }

    private Vector2 ResolveCannonViewportAnchor()
    {
        if (!preserveCannonViewportAnchor || sceneCamera == null || cannonRoot == null)
            return fallbackCannonViewportAnchor;

        Vector3 viewportPoint = sceneCamera.WorldToViewportPoint(cannonRoot.position);
        if (viewportPoint.z <= 0f)
            return fallbackCannonViewportAnchor;

        if (viewportPoint.x < -0.25f || viewportPoint.x > 1.25f || viewportPoint.y < -0.25f || viewportPoint.y > 1.25f)
            return fallbackCannonViewportAnchor;

        return new Vector2(
            Mathf.Clamp(viewportPoint.x, 0.05f, 0.95f),
            Mathf.Clamp(viewportPoint.y, 0.05f, 0.95f));
    }

    private Vector3 BuildTargetPosition(Bounds wallBounds, Vector2 lookOffset, float targetSize, Vector2 viewportAnchor)
    {
        if (preserveCannonViewportAnchor && cannonRoot != null)
        {
            float aspect = sceneCamera != null && sceneCamera.aspect > 0.01f ? sceneCamera.aspect : (16f / 9f);
            float halfWidth = targetSize * aspect;
            float halfHeight = targetSize;
            float targetX = cannonRoot.position.x + ((0.5f - viewportAnchor.x) * 2f * halfWidth);
            float targetY = cannonRoot.position.y + ((0.5f - viewportAnchor.y) * 2f * halfHeight);
            return new Vector3(targetX, targetY, -10f);
        }

        Vector3 targetPosition = wallBounds.center + (Vector3)lookOffset;
        targetPosition.z = -10f;
        return targetPosition;
    }

    private float BuildTargetOrthographicSize(Bounds wallBounds, Vector2 cameraPadding, Vector2 viewportAnchor)
    {
        float aspect = sceneCamera != null && sceneCamera.aspect > 0.01f ? sceneCamera.aspect : (16f / 9f);
        float halfHeight = wallBounds.extents.y + cameraPadding.y;
        float halfWidth = wallBounds.extents.x + cameraPadding.x;
        float targetSize = Mathf.Max(minimumOrthographicSize, halfHeight, halfWidth / aspect);

        if (!preserveCannonViewportAnchor || cannonRoot == null)
            return targetSize;

        float anchorX = Mathf.Clamp(viewportAnchor.x, 0.05f, 0.95f);
        float anchorY = Mathf.Clamp(viewportAnchor.y, 0.05f, 0.95f);
        float wallMinX = wallBounds.min.x - cameraPadding.x;
        float wallMaxX = wallBounds.max.x + cameraPadding.x;
        float wallMinY = wallBounds.min.y - cameraPadding.y;
        float wallMaxY = wallBounds.max.y + cameraPadding.y;
        float cannonX = cannonRoot.position.x;
        float cannonY = cannonRoot.position.y;

        float requiredHalfWidthFromLeft = (cannonX - wallMinX) / (2f * anchorX);
        float requiredHalfWidthFromRight = (wallMaxX - cannonX) / (2f * (1f - anchorX));
        float requiredHalfHeightFromBottom = (cannonY - wallMinY) / (2f * anchorY);
        float requiredHalfHeightFromTop = (wallMaxY - cannonY) / (2f * (1f - anchorY));

        float anchoredHalfWidth = Mathf.Max(halfWidth, requiredHalfWidthFromLeft, requiredHalfWidthFromRight);
        float anchoredHalfHeight = Mathf.Max(halfHeight, requiredHalfHeightFromBottom, requiredHalfHeightFromTop);
        return Mathf.Max(targetSize, anchoredHalfHeight, anchoredHalfWidth / aspect);
    }
}
