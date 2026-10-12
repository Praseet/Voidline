using UnityEngine;

[DisallowMultipleComponent]
public sealed class CorridorScroller : MonoBehaviour
{
    [SerializeField] private Transform[] segments;
    [SerializeField, Min(0f)] private float speed = 12f;
    [SerializeField, Min(1f)] private float segmentLength = 30f;
    [SerializeField] private float playerZ = 0f;
    [SerializeField, Min(0f)] private float cameraOffsetBehindPlayer = 7f;

    private Vector3[] initialLocalPositions;
    private Quaternion[] initialLocalRotations;
    private VoidlineRouteDirector routeDirector;
    private float distanceTravelled;

    public float Speed => speed;
    public float DistanceTravelled => distanceTravelled;
    public float SegmentLength => segmentLength;

    private void Awake()
    {
        CaptureInitialSegmentPose();
        routeDirector = GetComponent<VoidlineRouteDirector>();
    }

    public void SetSpeed(float worldUnitsPerSecond) => speed = Mathf.Max(0f, worldUnitsPerSecond);

    /// <summary>Explicit run reset also works when Enter Play Mode skips a scene reload.</summary>
    public void ResetForRun()
    {
        CaptureInitialSegmentPose();
        distanceTravelled = 0f;

        if (segments == null)
            return;

        for (int i = 0; i < segments.Length; i++)
        {
            Transform segment = segments[i];
            if (segment == null) continue;
            segment.gameObject.SetActive(true);
            segment.localPosition = initialLocalPositions[i];
            segment.localRotation = initialLocalRotations[i];
        }

        if (routeDirector == null)
            routeDirector = GetComponent<VoidlineRouteDirector>();
        ApplyRouteFrame();
    }

    private void CaptureInitialSegmentPose()
    {
        if (segments == null)
            return;

        if (initialLocalPositions != null &&
            initialLocalRotations != null &&
            initialLocalPositions.Length == segments.Length &&
            initialLocalRotations.Length == segments.Length)
            return;

        initialLocalPositions = new Vector3[segments.Length];
        initialLocalRotations = new Quaternion[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            Transform segment = segments[i];
            if (segment == null)
            {
                initialLocalPositions[i] = Vector3.zero;
                initialLocalRotations[i] = Quaternion.identity;
                continue;
            }

            initialLocalPositions[i] = segment.localPosition;
            initialLocalRotations[i] = segment.localRotation;
        }
    }

    private void Update()
    {
        if (segments == null || segments.Length == 0)
            return;

        float movement = speed * Time.deltaTime;
        if (movement <= 0f)
            return;

        distanceTravelled += movement;
        float recycleThreshold = playerZ - segmentLength * 0.5f - cameraOffsetBehindPlayer;
        for (int i = 0; i < segments.Length; i++)
        {
            Transform segment = segments[i];
            if (segment == null || !segment.gameObject.activeSelf)
                continue;

            segment.position += Vector3.back * movement;
            // The opening tunnel is a one-time introduction. Once it passes behind
            // the camera it is retired permanently, never looped into view again.
            if (segment.position.z < recycleThreshold)
                segment.gameObject.SetActive(false);
        }

        ApplyRouteFrame();
    }

    private void ApplyRouteFrame()
    {
        if (routeDirector == null)
            routeDirector = GetComponent<VoidlineRouteDirector>();
        if (routeDirector == null || segments == null)
            return;

        for (int i = 0; i < segments.Length; i++)
        {
            Transform segment = segments[i];
            if (segment == null || !segment.gameObject.activeSelf) continue;

            float absoluteDistance = Mathf.Max(0f, distanceTravelled + segment.position.z);
            Vector3 center = routeDirector.GetCenterOffsetAtDistance(absoluteDistance);
            segment.position = new Vector3(center.x, center.y, segment.position.z);
            segment.rotation = routeDirector.GetTrackRotationAtDistance(absoluteDistance);
        }
    }
}
