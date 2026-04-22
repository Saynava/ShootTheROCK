using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ShootTheRockPrototypeMarkers : MonoBehaviour
{
    [Header("Pocket Sizes")]
    [SerializeField] private Vector2 playerPocketSize = new Vector2(2.8f, 2.8f);
    [SerializeField] private Vector2 ballPocketSize = new Vector2(2.2f, 2.8f);
    [SerializeField] private Vector2 goalPocketSize = new Vector2(3f, 2.4f);

    [Header("Marker References")]
    [SerializeField] private Transform playerStartMarker;
    [SerializeField] private Transform ballStartMarker;
    [SerializeField] private Transform goalMarker;

    public Transform PlayerStartMarker => playerStartMarker;
    public Transform BallStartMarker => ballStartMarker;
    public Transform GoalMarker => goalMarker;
    public Vector2 PlayerPocketSize => playerPocketSize;
    public Vector2 BallPocketSize => ballPocketSize;
    public Vector2 GoalPocketSize => goalPocketSize;

    private void Reset()
    {
        EnsureMarkers();
    }

    [ContextMenu("Create Missing Markers")]
    public void EnsureMarkers()
    {
        playerStartMarker = EnsureMarker(playerStartMarker, "PlayerStartMarker", new Vector3(-8f, 6f, 0f));
        ballStartMarker = EnsureMarker(ballStartMarker, "BallStartMarker", new Vector3(-2f, -2f, 0f));
        goalMarker = EnsureMarker(goalMarker, "GoalMarker", new Vector3(-10f, -8f, 0f));
    }

    [ContextMenu("Reset Marker Positions")]
    public void ResetMarkerPositions()
    {
        EnsureMarkers();
        playerStartMarker.localPosition = new Vector3(-8f, 6f, 0f);
        ballStartMarker.localPosition = new Vector3(-2f, -2f, 0f);
        goalMarker.localPosition = new Vector3(-10f, -8f, 0f);
    }

    private Transform EnsureMarker(Transform existing, string markerName, Vector3 defaultLocalPosition)
    {
        if (existing == null)
        {
            Transform foundChild = transform.Find(markerName);
            if (foundChild != null)
                existing = foundChild;
        }

        if (existing != null)
            return existing;

        GameObject markerObject = new GameObject(markerName);
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localPosition = defaultLocalPosition;
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;
        return markerObject.transform;
    }

    private void OnDrawGizmos()
    {
        DrawMarker(playerStartMarker, playerPocketSize, new Color(0.25f, 0.8f, 1f, 0.95f));
        DrawMarker(ballStartMarker, ballPocketSize, new Color(1f, 0.85f, 0.2f, 0.95f));
        DrawMarker(goalMarker, goalPocketSize, new Color(0.2f, 1f, 0.35f, 0.95f));
    }

    private void DrawMarker(Transform marker, Vector2 pocketSize, Color color)
    {
        if (marker == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawSphere(marker.position, 0.22f);
        Gizmos.DrawWireCube(marker.position, new Vector3(pocketSize.x, pocketSize.y, 0.1f));
    }
}
