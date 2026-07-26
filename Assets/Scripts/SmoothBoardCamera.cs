using UnityEngine;

/// <summary>
/// Smooth side-view camera that follows a surfboard without inheriting
/// the board's roll, pitch, or yaw.
/// </summary>
[DisallowMultipleComponent]
public sealed class SmoothBoardCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool findSurfboardAutomatically = true;

    [Header("Follow Offset")]
    [SerializeField] private Vector3 followOffset = new Vector3(1.8f, 0.8f, -10f);
    [SerializeField] private bool lookAhead = true;
    [SerializeField, Min(0f)] private float horizontalLookAhead = 1.4f;
    [SerializeField, Min(0f)] private float verticalLookAhead = 0.35f;
    [SerializeField, Min(0.01f)] private float velocityForMaximumLookAhead = 8f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.22f;
    [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.28f;
    [SerializeField, Min(0f)] private float maximumFollowSpeed = 40f;

    [Header("Camera Bounds")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Vector2 minimumPosition = new Vector2(-100f, -20f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(100f, 50f);

    [Header("Axis Control")]
    [SerializeField] private bool followHorizontal = true;
    [SerializeField] private bool followVertical = true;
    [SerializeField] private bool lockCameraZ = true;
    [SerializeField] private float lockedZ = -10f;

    private Rigidbody targetBody;
    private Vector3 followVelocity;
    private Vector3 smoothedLookAhead;
    private Vector3 lookAheadVelocity;

    private void Awake()
    {
        TryFindTarget();
        CacheTargetBody();

        if (lockCameraZ)
            lockedZ = transform.position.z;
    }

    private void OnEnable()
    {
        followVelocity = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            TryFindTarget();

            if (target == null)
                return;

            CacheTargetBody();
        }

        Vector3 desiredLookAhead = CalculateLookAhead();

        smoothedLookAhead = Vector3.SmoothDamp(
            smoothedLookAhead,
            desiredLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);

        Vector3 desiredPosition = target.position + followOffset + smoothedLookAhead;

        if (!followHorizontal)
            desiredPosition.x = transform.position.x;

        if (!followVertical)
            desiredPosition.y = transform.position.y;

        if (lockCameraZ)
            desiredPosition.z = lockedZ;

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(
                desiredPosition.x,
                minimumPosition.x,
                maximumPosition.x);

            desiredPosition.y = Mathf.Clamp(
                desiredPosition.y,
                minimumPosition.y,
                maximumPosition.y);
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            positionSmoothTime,
            maximumFollowSpeed,
            Time.deltaTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        CacheTargetBody();

        followVelocity = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
        smoothedLookAhead = Vector3.zero;
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        Vector3 snapPosition = target.position + followOffset;

        if (lockCameraZ)
            snapPosition.z = lockedZ;

        transform.position = snapPosition;
        followVelocity = Vector3.zero;
    }

    private Vector3 CalculateLookAhead()
    {
        if (!lookAhead)
            return Vector3.zero;

        Vector3 velocity = targetBody != null
            ? targetBody.linearVelocity
            : Vector3.zero;

        float speedScale = Mathf.Clamp01(
            velocity.magnitude / velocityForMaximumLookAhead);

        Vector3 ahead = new Vector3(
            Mathf.Sign(velocity.x) * horizontalLookAhead * speedScale,
            Mathf.Clamp(
                velocity.y / velocityForMaximumLookAhead,
                -1f,
                1f) * verticalLookAhead,
            0f);

        return ahead;
    }

    private void TryFindTarget()
    {
        if (target != null || !findSurfboardAutomatically)
            return;

        // Avoid a compile-time dependency on SurfboardController.
        // This works even when that script lives in another assembly or namespace.
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name == "SurfboardController")
            {
                target = behaviour.transform;
                return;
            }
        }

        // Optional fallback when the board object is named or tagged clearly.
        GameObject namedBoard = GameObject.Find("Surfboard");

        if (namedBoard != null)
            target = namedBoard.transform;
    }

    private void CacheTargetBody()
    {
        targetBody = target != null
            ? target.GetComponent<Rigidbody>()
            : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        positionSmoothTime = Mathf.Max(0.01f, positionSmoothTime);
        lookAheadSmoothTime = Mathf.Max(0.01f, lookAheadSmoothTime);
        velocityForMaximumLookAhead =
            Mathf.Max(0.01f, velocityForMaximumLookAhead);

        if (maximumPosition.x < minimumPosition.x)
            maximumPosition.x = minimumPosition.x;

        if (maximumPosition.y < minimumPosition.y)
            maximumPosition.y = minimumPosition.y;
    }
#endif
}
