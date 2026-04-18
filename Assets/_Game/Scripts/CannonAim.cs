using UnityEngine;
using UnityEngine.InputSystem;

public class CannonAim : MonoBehaviour
{
    private Camera sceneCamera;

    public void Initialize(Camera targetCamera)
    {
        sceneCamera = targetCamera;
    }

    private void Update()
    {
        if (sceneCamera == null)
            return;

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
}

