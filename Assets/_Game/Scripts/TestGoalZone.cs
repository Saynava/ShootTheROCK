using UnityEngine;

public class TestGoalZone : MonoBehaviour
{
    private static readonly Color IdleColor = new Color(0.18f, 0.95f, 0.35f, 0.42f);
    private static readonly Color CompleteColor = new Color(1f, 0.82f, 0.16f, 0.7f);

    private string targetObjectName = "TestGoalBall";
    private SpriteRenderer spriteRenderer;
    private bool completed;

    public void Initialize(string targetName)
    {
        targetObjectName = string.IsNullOrWhiteSpace(targetName) ? "TestGoalBall" : targetName;
        CacheComponents();
        completed = false;
        RefreshVisual();
    }

    private void Awake()
    {
        CacheComponents();
        RefreshVisual();
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completed)
            return;

        GameObject candidate = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (candidate == null || candidate.name != targetObjectName)
            return;

        completed = true;
        RefreshVisual();

        Rigidbody2D body = candidate.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.Sleep();
        }

        Debug.Log($"Test goal complete: {targetObjectName} reached {name}");
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = completed ? CompleteColor : IdleColor;
    }
}
