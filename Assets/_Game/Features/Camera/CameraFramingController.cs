using UnityEngine;

public class CameraFramingController : MonoBehaviour
{
    [SerializeField] private bool autoFrameEnabled;
    [SerializeField] private float smoothTime = 0.55f;
    [SerializeField] private float minimumLookAheadFromCannon = 9.5f;
    [SerializeField] private float minimumOrthographicSize = 1f;
    [SerializeField] private float settlePositionDistance = 0.025f;
    [SerializeField] private float settleSizeDistance = 0.025f;

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
        if (!TryBuildTarget(out transientTargetPosition, out transientTargetSize))
            return;

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

        targetPosition = BuildTargetPosition(wallBounds, lookOffset);
        targetSize = BuildTargetOrthographicSize(wallBounds, cameraPadding);
        return true;
    }

    private Vector3 BuildTargetPosition(Bounds wallBounds, Vector2 lookOffset)
    {
        Vector3 targetPosition = wallBounds.center + (Vector3)lookOffset;
        targetPosition.z = -10f;

        if (cannonRoot != null)
            targetPosition.x = Mathf.Max(targetPosition.x, cannonRoot.position.x + minimumLookAheadFromCannon);

        return targetPosition;
    }

    private float BuildTargetOrthographicSize(Bounds wallBounds, Vector2 cameraPadding)
    {
        float aspect = sceneCamera != null && sceneCamera.aspect > 0.01f ? sceneCamera.aspect : (16f / 9f);
        float halfHeight = wallBounds.extents.y + cameraPadding.y;
        float halfWidth = wallBounds.extents.x + cameraPadding.x;
        return Mathf.Max(minimumOrthographicSize, halfHeight, halfWidth / aspect);
    }
}
