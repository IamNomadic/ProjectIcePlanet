using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HoverController : MonoBehaviour
{
    [Header("Hover")]
    [SerializeField] private bool hoverEnabled = true;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] private float gridRayRadius = 0.35f;
    [SerializeField] private float hoverForce = 200f;
    [SerializeField] private float hoverDamping = 5f;
    [SerializeField] private float maxHoverCorrection = 50f;
    [SerializeField] private float proximityMultiplier = 1.5f;
    [SerializeField] private float fallingThreshold = -0.1f;
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private float maxFallSpeed = 50f;
    [SerializeField] [Range(0f, 90f)] private float slopeLimitDegrees = 45f;
    [SerializeField] private bool requireAnyHit = true;

    // Landing recovery tuning
    [Header("Landing Recovery")]
    [SerializeField] private float landingRecoveryAssist = 0.5f;

    // Public state
    public bool IsGrounded { get; private set; } = false;
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public bool IsRecoveringLanding { get; private set; } = false;

    private Rigidbody rb;
    private Collider col;

    // internal
    private bool wasFalling = false;
    private bool landingRecovering = false;
    private bool landingBurstDone = false;
    private float rayStartVerticalOffset = 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (hoverEnabled)
            rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (hoverEnabled)
        {
            ApplyHoverBehaviour();
        }
        else
        {
            // fallback gravity when hover disabled
            CheckGrounded3x3(out bool grounded, out Vector3 normal, out _, out _);
            IsGrounded = grounded;
            GroundNormal = normal;

            if (!IsGrounded)
                rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
            else if (rb.velocity.y > 0f)
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        }

        // clamp fall speed
        if (rb.velocity.y < -maxFallSpeed)
            rb.velocity = new Vector3(rb.velocity.x, -maxFallSpeed, rb.velocity.z);
    }

    private void ApplyHoverBehaviour()
    {
        if (col == null) col = GetComponent<Collider>();
        float colliderHalfHeight = col.bounds.extents.y;

        // hoverHeight is 90% of groundCheckDistance
        float hoverHeight = Mathf.Max(0f, groundCheckDistance * 0.9f);
        float targetDist = hoverHeight + 0.01f;
        float rayMax = col.bounds.extents.y + groundCheckDistance + hoverHeight + 0.5f;

        // perform 3x3 raycasts around the bottom
        Vector3 boundsCenter = col.bounds.center;
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        float[] offsets = { -1f, 0f, 1f };

        int hitCount = 0;
        float accWeightedDist = 0f;
        float accWeight = 0f;
        Vector3 accNormal = Vector3.zero;

        for (int xi = 0; xi < 3; xi++)
        {
            for (int zi = 0; zi < 3; zi++)
            {
                Vector3 localOffset = right * (offsets[xi] * gridRayRadius) + forward * (offsets[zi] * gridRayRadius);
                Vector3 origin = boundsCenter + localOffset + Vector3.up * rayStartVerticalOffset;

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayMax, groundLayer))
                {
                    hitCount++;
                    float distFromCenter = localOffset.magnitude;
                    float proximity = 1f - Mathf.Clamp01(distFromCenter / gridRayRadius);
                    float weight = Mathf.Lerp(0.25f, 1f, proximity);

                    float distFromBottom = hit.distance - colliderHalfHeight - rayStartVerticalOffset;

                    accWeightedDist += distFromBottom * weight;
                    accWeight += weight;
                    accNormal += hit.normal;
                }
            }
        }

        // no ground detected -> airborne
        if (hitCount == 0 || accWeight <= 0f)
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
            rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
            if (rb.velocity.y < fallingThreshold) wasFalling = true;
            landingRecovering = false;
            landingBurstDone = false;
            return;
        }

        float avgDist = accWeightedDist / accWeight;
        Vector3 avgNormal = (accNormal / hitCount).normalized;

        float distanceError = targetDist - avgDist;
        float verticalVel = rb.velocity.y;

        // safety clamp to avoid clipping
        float minClearance = 0.005f;
        if (avgDist <= minClearance && verticalVel < 0f)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            verticalVel = 0f;
        }

        float pTerm = Mathf.Clamp(distanceError * hoverForce, -maxHoverCorrection, maxHoverCorrection);
        float proximityFactor = Mathf.Clamp01(1f - (avgDist / (targetDist * 2f + 0.0001f)));
        pTerm *= 1f + proximityFactor * (proximityMultiplier - 1f);

        float dTerm = -hoverDamping * verticalVel;
        float desiredAcc = pTerm + dTerm;

        if (distanceError < 0f)
        {
            desiredAcc = Mathf.Min(desiredAcc, Mathf.Max(-Mathf.Abs(pTerm) * 0.2f, -Mathf.Abs(hoverForce) * 0.1f));
        }

        float slopeAngle = Vector3.Angle(avgNormal, Vector3.up);
        bool slopeWalkable = slopeAngle <= slopeLimitDegrees;
        bool closeEnough = Mathf.Abs(distanceError) <= (groundCheckDistance + 0.05f);
        IsGrounded = hitCount > 0 && slopeWalkable && closeEnough;
        GroundNormal = avgNormal;

        rb.AddForce(Vector3.up * desiredAcc, ForceMode.Acceleration);

        bool currentlyFalling = verticalVel < fallingThreshold;
        if (currentlyFalling) wasFalling = true;

        if (wasFalling && (verticalVel >= 0f || IsGrounded))
        {
            landingRecovering = true;
            wasFalling = false;
            landingBurstDone = false;
        }

        if (landingRecovering)
        {
            float normalized = (hoverHeight > 0f) ? Mathf.Clamp01(distanceError / hoverHeight) : 0f;
            float ramp = Mathf.Sqrt(Mathf.Max(0f, normalized));
            float ongoing = Mathf.Max(0f, distanceError) * hoverForce * (landingRecoveryAssist + 0.5f * proximityFactor) * ramp;
            if (ongoing > 0f)
                rb.AddForce(Vector3.up * ongoing, ForceMode.Acceleration);

            if (distanceError <= 0.01f || normalized <= 0.01f)
            {
                landingRecovering = false;
                landingBurstDone = false;
            }
        }

        if (avgDist <= 0.002f && rb.velocity.y < 0f)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        }

        IsRecoveringLanding = landingRecovering;
    }

    // Public API --------------------------------------------------------------

    // Manually trigger a one-time upward impulse on landing (useful if enemy wants a landing 'bounce')
    public void ApplyLandingBurst(float multiplier = 1.0f)
    {
        if (landingBurstDone) return;
        float burst = hoverForce * 0.02f * multiplier;
        rb.AddForce(Vector3.up * burst, ForceMode.VelocityChange);
        landingBurstDone = true;
    }

    public void SetHoverEnabled(bool enabled)
    {
        hoverEnabled = enabled;
        rb.useGravity = !enabled;
    }

    // Optional: utility exposing last average distance and slope if needed
    public bool CheckGrounded3x3(out bool grounded, out Vector3 normal, out float avgDistance, out int hits)
    {
        // lightweight wrapper that replicates the check pattern and returns values
        grounded = false;
        normal = Vector3.up;
        avgDistance = float.MaxValue;
        hits = 0;

        if (col == null) col = GetComponent<Collider>();
        Vector3 boundsCenter = col.bounds.center;
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        float[] offsets = { -1f, 0f, 1f };
        float rayMax = col.bounds.extents.y + groundCheckDistance + 0.5f;

        float accDist = 0f;
        int accHits = 0;
        Vector3 accNormal = Vector3.zero;

        for (int xi = 0; xi < 3; xi++)
        {
            for (int zi = 0; zi < 3; zi++)
            {
                Vector3 localOffset = right * (offsets[xi] * gridRayRadius) + forward * (offsets[zi] * gridRayRadius);
                Vector3 origin = boundsCenter + localOffset + Vector3.up * rayStartVerticalOffset;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayMax, groundLayer))
                {
                    accHits++;
                    accNormal += hit.normal;
                    float distFromBottom = hit.distance - col.bounds.extents.y - rayStartVerticalOffset;
                    accDist += distFromBottom;
                }
            }
        }

        if (accHits == 0)
        {
            hits = 0;
            avgDistance = float.MaxValue;
            normal = Vector3.up;
            grounded = false;
            return false;
        }

        avgDistance = accDist / accHits;
        normal = (accNormal / accHits).normalized;
        hits = accHits;
        float slopeAngle = Vector3.Angle(normal, Vector3.up);
        grounded = (accHits > 0) && (slopeAngle <= slopeLimitDegrees);
        return grounded;
    }
}
