using UnityEngine;

public class PrototypeCameraDirector : MonoBehaviour
{
    private Camera sceneCamera;
    private RockWall rockWall;
    private Transform cannonRoot;
    private Vector3 positionVelocity;
    private float sizeVelocity;

    public void Initialize(Camera sceneCamera, RockWall rockWall, Transform cannonRoot)
    {
        this.sceneCamera = sceneCamera;
        this.rockWall = rockWall;
        this.cannonRoot = cannonRoot;
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (sceneCamera == null || rockWall == null)
            return;

        rockWall.GetCameraTarget(out Vector3 targetPosition, out float targetSize);
        if (cannonRoot != null)
            targetPosition.x = Mathf.Max(targetPosition.x, cannonRoot.position.x + 9.5f);

        sceneCamera.transform.position = Vector3.SmoothDamp(sceneCamera.transform.position, targetPosition, ref positionVelocity, 0.55f);
        sceneCamera.orthographicSize = Mathf.SmoothDamp(sceneCamera.orthographicSize, targetSize, ref sizeVelocity, 0.55f);
    }

    private void SnapToTarget()
    {
        if (sceneCamera == null || rockWall == null)
            return;

        rockWall.GetCameraTarget(out Vector3 targetPosition, out float targetSize);
        if (cannonRoot != null)
            targetPosition.x = Mathf.Max(targetPosition.x, cannonRoot.position.x + 9.5f);

        sceneCamera.transform.position = targetPosition;
        sceneCamera.orthographicSize = targetSize;
    }
}

