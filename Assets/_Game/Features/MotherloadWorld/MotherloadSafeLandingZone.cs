using UnityEngine;

[DisallowMultipleComponent]
public sealed class MotherloadSafeLandingZone : MonoBehaviour
{
    [SerializeField] private float centerX;
    [SerializeField] private float halfWidth = 4.5f;
    [SerializeField] private float maxSafeContactY;

    public void Configure(float centerX, float halfWidth, float maxSafeContactY)
    {
        this.centerX = centerX;
        this.halfWidth = Mathf.Max(0f, halfWidth);
        this.maxSafeContactY = maxSafeContactY;
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        return Mathf.Abs(worldPoint.x - centerX) <= halfWidth && worldPoint.y <= maxSafeContactY;
    }
}
