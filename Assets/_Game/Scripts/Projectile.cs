using UnityEngine;

public class Projectile : MonoBehaviour
{
    private float lifetime = 4f;
    private Rigidbody2D body;
    private bool didHit;
    private float blastRadiusScale = 1f;

    public void Initialize(Vector2 direction, float speed, RockWall rockWall, Rigidbody2D body, float blastRadiusScale)
    {
        this.body = body;
        this.blastRadiusScale = Mathf.Max(0.25f, blastRadiusScale);
        if (this.body != null)
            this.body.linearVelocity = direction.normalized * speed;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (didHit)
            return;

        RockWall hitWall = other.GetComponent<RockWall>();
        if (hitWall == null)
            return;

        didHit = true;
        Vector2 impactDirection = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
            ? body.linearVelocity.normalized
            : Vector2.right;

        Vector2 queryPoint = (Vector2)transform.position - (impactDirection * 0.18f);
        Vector2 hitPoint = other.ClosestPoint(queryPoint);
        if ((hitPoint - queryPoint).sqrMagnitude <= 0.0001f)
            hitPoint = other.ClosestPoint((Vector2)transform.position - (impactDirection * 0.45f));

        hitWall.ApplyHit(hitPoint, impactDirection, blastRadiusScale);
        Destroy(gameObject);
    }
}
