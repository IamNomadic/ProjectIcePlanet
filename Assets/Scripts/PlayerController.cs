using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Quake Movement Settings")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float sprintSpeed = 16f;
    [SerializeField] private float groundAcceleration = 14f;
    [SerializeField] private float airAcceleration = 2f;
    [SerializeField] private float groundFriction = 6f;
    [SerializeField] private float stopSpeed = 100f;
    [SerializeField] private bool sprintRequiresForward = true;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private bool autoBunnyHop = false;
    [SerializeField] private float jumpCooldownDuration = 0.2f;

    [Header("Hover (enabled)")]
    [SerializeField] private bool hoverEnabled = true;
    [SerializeField] private float hoverForce = 200f;
    [SerializeField] private float hoverDamping = 10f;
    [SerializeField] private float maxHoverCorrection = 50f;
    [SerializeField] private float fallingThreshold = -0.1f;

    [Header("Slope Handling")]
    [SerializeField] private float slideForce = 20f;
    [Range(0f, 1f)] [SerializeField] private float steepSlopeUpwardDamping = 0.1f;

    [Header("High Speed Safety")]
    [Tooltip("Look ahead time in seconds. Higher = safer but might stop you early near walls.")]
    [SerializeField] private float predictionTime = 0.05f;
    [Tooltip("How hard to push back when detecting a high speed collision.")]
    [SerializeField] private float wallRepulsionForce = 10f;
    [Tooltip("The radius of the predictive sphere (should be slightly smaller than collider).")]
    [SerializeField] private float predictionRadius = 0.3f;

    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float gridRayRadius = 0.35f;
    [Range(0f, 90f)] [SerializeField] private float slopeLimitDegrees = 45f;

    [Header("Gravity")]
    [SerializeField] private float gravityScale = 2.5f;

    [Header("Camera")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 0.05f;
    [SerializeField] private float controllerSensitivity = 180f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -89f;
    [SerializeField] private float maxPitch = 89f;
    [SerializeField] private bool lockCursor = true;

    // References
    private Rigidbody rb;
    private PlayerInput playerInput;
    private Collider col;
    private InputAction sprintAction;

    // Inputs
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isSprinting;

    // Jump State
    private bool jumpKeyHeld;
    private bool jumpQueued;
    private float jumpCooldownTimer = 0f;

    // Physics State
    private bool isGrounded;
    private bool isTouchingSlope;
    private float yaw, pitch;
    private Vector3 groundNormal = Vector3.up;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        col = GetComponent<Collider>();

        rb.drag = 0f;
        rb.angularDrag = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Vital for high speed

        if (hoverEnabled) rb.useGravity = false;

        playerInput.actions["Jump"].performed += OnJump;
        playerInput.actions["Jump"].canceled += OnJumpCanceled;

        sprintAction = playerInput.actions.FindAction("Sprint");

        if (cameraPivot == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cameraPivot = cam.transform;
        }

        yaw = transform.eulerAngles.y;
        if (cameraPivot != null)
        {
            float x = cameraPivot.localEulerAngles.x;
            pitch = (x > 180f) ? x - 360f : x;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDestroy()
    {
        if (playerInput != null)
        {
            playerInput.actions["Jump"].performed -= OnJump;
            playerInput.actions["Jump"].canceled -= OnJumpCanceled;
        }
    }

    private void Update()
    {
        moveInput = playerInput.actions["Move"].ReadValue<Vector2>();
        lookInput = playerInput.actions["Look"].ReadValue<Vector2>();

        if (sprintAction != null) isSprinting = sprintAction.IsPressed();

        if (autoBunnyHop && jumpKeyHeld)
        {
            jumpQueued = true;
        }

        HandleLook();
    }

    private void FixedUpdate()
    {
        // 1. Prevent High Speed Tunneling (Run this FIRST)
        PreventHighSpeedTunneling();

        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer -= Time.fixedDeltaTime;
        }

        // 2. Ground Check & Hover
        bool canGroundCheck = (jumpCooldownTimer <= 0f);

        if (canGroundCheck)
        {
            if (hoverEnabled) ApplyHoverBehaviour();
            else CheckIfGrounded_3x3();
        }
        else
        {
            isGrounded = false;
            isTouchingSlope = false;
            groundNormal = Vector3.up;
        }

        // Apply Gravity
        if (!isGrounded || !hoverEnabled)
        {
            rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
        }

        // 3. Movement Logic
        if (isGrounded)
        {
            if (!jumpQueued) ApplyFriction(1.0f);
            HandleGroundMovement();
            if (jumpQueued) PerformJump();
        }
        else
        {
            HandleAirMovement();
        }
    }

    // ==========================================================================================
    // NEW: ANTI-TUNNELING / PROPORTIONAL PUSHBACK
    // ==========================================================================================

    private void PreventHighSpeedTunneling()
    {
        float speed = rb.velocity.magnitude;

        // Only run this calculation if we are actually moving fast enough to clip
        if (speed < 5f) return;

        // Calculate how far we will travel in the next few frames
        float predictDist = speed * predictionTime;

        // SphereCast forward from the center of the player
        Vector3 origin = col.bounds.center;
        Vector3 direction = rb.velocity.normalized;

        if (Physics.SphereCast(origin, predictionRadius, direction, out RaycastHit hit, predictDist, groundLayer))
        {
            // Check if the thing we are about to hit is a steep slope/wall
            float angle = Vector3.Angle(hit.normal, Vector3.up);

            if (angle > slopeLimitDegrees)
            {
                // We are about to hit a wall at high speed. 

                // 1. Calculate Velocity INTO the wall (Dot Product)
                // If negative, we are moving towards it.
                float velocityIntoWall = Vector3.Dot(rb.velocity, hit.normal);

                if (velocityIntoWall < 0)
                {
                    // 2. Clip Velocity (The Quake/Source Engine method)
                    // Projects velocity onto the wall plane, preserving speed but redirecting it parallel to the wall.
                    // This prevents the "force" that pushes you through the mesh.
                    Vector3 projectedVel = Vector3.ProjectOnPlane(rb.velocity, hit.normal);

                    // Apply the redirection immediately
                    rb.velocity = projectedVel;

                    // 3. Proportional Pushback (Repulsion)
                    // Apply a force based on how fast we were hitting the wall
                    // logic: -velocityIntoWall is positive magnitude of impact.
                    float impactForce = -velocityIntoWall;
                    rb.AddForce(hit.normal * impactForce * wallRepulsionForce, ForceMode.Impulse);
                }
            }
        }
    }

    // ==========================================================================================
    // MOVEMENT
    // ==========================================================================================

    private void HandleGroundMovement()
    {
        Vector3 wishDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

        float currentWishSpeed = maxSpeed;
        if (isSprinting)
        {
            if (!sprintRequiresForward || moveInput.y > 0.1f)
                currentWishSpeed = sprintSpeed;
        }

        Accelerate(wishDir, currentWishSpeed, groundAcceleration);
    }

    private void HandleAirMovement()
    {
        Vector3 wishDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

        // Air-steering deflection to prevent sticking to walls in air
        if (isTouchingSlope && !isGrounded)
        {
            if (Vector3.Dot(wishDir, groundNormal) < 0)
            {
                wishDir = Vector3.ProjectOnPlane(wishDir, groundNormal).normalized;
            }
        }

        Accelerate(wishDir, maxSpeed, airAcceleration);
    }

    private void ApplyFriction(float t)
    {
        Vector3 vec = rb.velocity;
        vec.y = 0f;
        float speed = vec.magnitude;
        float drop = 0f;

        if (speed > 0.01f)
        {
            float control = (speed < stopSpeed) ? stopSpeed : speed;
            drop += control * groundFriction * Time.fixedDeltaTime * t;
        }

        float newSpeed = speed - drop;
        if (newSpeed < 0f) newSpeed = 0f;
        if (speed > 0f) newSpeed /= speed;

        rb.velocity = new Vector3(rb.velocity.x * newSpeed, rb.velocity.y, rb.velocity.z * newSpeed);
    }

    private void Accelerate(Vector3 wishDir, float wishSpeed, float accel)
    {
        float currentSpeed = Vector3.Dot(rb.velocity, wishDir);
        float addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0) return;

        float accelSpeed = accel * Time.fixedDeltaTime * wishSpeed;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        rb.AddForce(wishDir * accelSpeed, ForceMode.VelocityChange);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpKeyHeld = true;
        jumpQueued = true;
    }

    public void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpKeyHeld = false;
        jumpQueued = false;
    }

    private void PerformJump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpCooldownTimer = jumpCooldownDuration;
        isGrounded = false;
        jumpQueued = false;
    }

    private void ApplyHoverBehaviour()
    {
        if (col == null) col = GetComponent<Collider>();

        float colliderHeight = col.bounds.extents.y;
        float hoverTarget = groundCheckDistance * 0.9f;
        float rayLength = colliderHeight + hoverTarget + 0.5f;

        Vector3 center = col.bounds.center;
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        float[] offsets = { -1f, 0f, 1f };

        int hits = 0;
        float avgDistError = 0f;
        Vector3 avgNormal = Vector3.zero;

        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                Vector3 offset = (right * offsets[x] + forward * offsets[z]) * gridRayRadius;
                Vector3 origin = center + offset;

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
                {
                    hits++;
                    avgNormal += hit.normal;
                    float dist = hit.distance - colliderHeight;
                    float error = hoverTarget - dist;
                    avgDistError += error;
                }
            }
        }

        if (hits == 0)
        {
            isGrounded = false;
            isTouchingSlope = false;
            groundNormal = Vector3.up;
            return;
        }

        avgNormal /= hits;
        avgNormal.Normalize();
        avgDistError /= hits;
        groundNormal = avgNormal;

        float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
        bool isSteep = slopeAngle > slopeLimitDegrees;

        isGrounded = !isSteep;
        isTouchingSlope = true;

        // --- HOVER FORCE ---
        float force = avgDistError * hoverForce;
        force -= rb.velocity.y * hoverDamping;
        force = Mathf.Clamp(force, -maxHoverCorrection, maxHoverCorrection);

        // If steep, push outward (normal) rather than upward.
        Vector3 liftDirection = isSteep ? groundNormal : Vector3.up;
        rb.AddForce(liftDirection * force, ForceMode.Acceleration);

        // --- STEEP SLOPE HANDLING ---
        if (isSteep)
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            rb.AddForce(slideDir * slideForce, ForceMode.Acceleration);

            if (rb.velocity.y > 0)
            {
                Vector3 dampedVel = rb.velocity;
                dampedVel.y *= (1f - steepSlopeUpwardDamping);
                rb.velocity = dampedVel;
            }
        }
    }

    private void HandleLook()
    {
        if (cameraPivot == null) return;

        string scheme = playerInput.currentControlScheme;
        float invert = invertY ? -1f : 1f;

        if (scheme == "Keyboard&Mouse")
        {
            yaw += lookInput.x * mouseSensitivity;
            pitch += (-lookInput.y * invert) * mouseSensitivity;
        }
        else
        {
            yaw += lookInput.x * controllerSensitivity * Time.deltaTime;
            pitch += (-lookInput.y * invert) * controllerSensitivity * Time.deltaTime;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void CheckIfGrounded_3x3()
    {
        Vector3 center = col.bounds.center;
        float dist = col.bounds.extents.y + groundCheckDistance;
        isGrounded = Physics.BoxCast(center, Vector3.one * gridRayRadius, Vector3.down, transform.rotation, dist, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
        if (col == null) col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = isGrounded ? Color.green : (isTouchingSlope ? Color.yellow : Color.red);
        Gizmos.DrawWireCube(col.bounds.center + Vector3.down * (col.bounds.extents.y + groundCheckDistance / 2), new Vector3(gridRayRadius * 2, groundCheckDistance, gridRayRadius * 2));

        // Draw prediction sphere
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 origin = col.bounds.center;
            Vector3 direction = rb.velocity.normalized;
            float dist = rb.velocity.magnitude * predictionTime;
            Gizmos.DrawWireSphere(origin + direction * dist, predictionRadius);
        }
    }
}