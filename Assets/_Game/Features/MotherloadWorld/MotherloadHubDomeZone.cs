using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MotherloadHubDomeZone : MonoBehaviour
{
    [SerializeField] private float maxDownwardSpeed = 0.85f;
    [SerializeField] private float brakeAcceleration = 28f;

    public void Configure(float maxDownwardSpeed, float brakeAcceleration)
    {
        this.maxDownwardSpeed = Mathf.Max(0f, maxDownwardSpeed);
        this.brakeAcceleration = Mathf.Max(0f, brakeAcceleration);
        EnsureTrigger();
    }

    private void Awake()
    {
        EnsureTrigger();
    }

    private void OnValidate()
    {
        EnsureTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ApplyBrake(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ApplyBrake(other);
    }

    private void ApplyBrake(Collider2D other)
    {
        CannonAim cannon = other != null ? other.GetComponentInParent<CannonAim>() : null;
        if (cannon != null)
            cannon.ApplyHubDomeBrake(maxDownwardSpeed, brakeAcceleration);
    }

    private void EnsureTrigger()
    {
        Collider2D domeCollider = GetComponent<Collider2D>();
        if (domeCollider != null)
            domeCollider.isTrigger = true;
    }
}
