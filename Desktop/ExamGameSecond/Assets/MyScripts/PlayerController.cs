using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.2f; // how far below the cube's feet to check for the terrain

    [Header("Look Settings")]
    public float mouseSensitivity = 0.15f;
    public float minPitch = -70f;
    public float maxPitch = 80f;
    public bool invertY = false;
    [Tooltip("Camera-only pivot (child of this transform) that pitch is applied to - Cinemachine's TrackingTarget. Mouse X still turns this whole transform (and so the camera, which tracks a child of it) - only pitch needs to stay off the CharacterController itself, or it would tip over.")]
    public string cameraAimChildName = "CameraAim";

    [Header("Camera Jolt")]
    [Tooltip("Degrees of roll (tilt-right) kick applied to CameraAim by TriggerCameraJolt (e.g. called by MiasmaFogArea on each damage tick). If it visually tilts left instead of right, flip the sign.")]
    public float joltStrength = 4f;
    [Tooltip("Seconds to ease into the tilt. The ease-back to level takes the same amount of time again.")]
    public float joltDuration = 0.15f;

    [Header("Knockback")]
    [Tooltip("Seconds a knockback push (e.g. Mobai.PerformAttack landing a hit) takes to cover its full distance.")]
    public float knockbackDuration = 0.2f;

    [Header("Interaction & Inventory")]
    [Tooltip("E key - hook up interaction logic here later (e.g. picking up items, opening doors)")]
    public UnityEvent onInteract;
    [Tooltip("Right mouse click - a second interact trigger for things E doesn't suit (e.g. repeated chopping); InteractableHotspot listens to both.")]
    public UnityEvent onSecondaryInteract;
    [Tooltip("Hook up inventory logic here later (e.g. opening/closing the inventory UI)")]
    public UnityEvent onToggleInventory;
    [Tooltip("Hook up quest full view logic here later (e.g. opening/closing the full quest UI)")]
    public UnityEvent onToggleQuestFullView;
    [Tooltip("Esc key - wired by UInavigator to force-close every open browsing panel (Inventory/Quest Full View/an NPC's Trade shop) and open the Main Menu, or close the Main Menu if that's what's currently open.")]
    public UnityEvent onEscape;

    // Parameter names on the player model's own AnimatorController (e.g. Sloth's
    // SmallController.controller) - Speed/Grounded/VerticalSpeed drive its Idle/Walk/Run/
    // GoingUp/GoingDown/Land state machine. Sprint is left at its default (false) - there's
    // no sprint key yet.
    private const string SpeedAnimatorParam = "Speed";
    private const string GroundedAnimatorParam = "Grounded";
    private const string VerticalSpeedAnimatorParam = "VerticalSpeed";

    private CharacterController controller;
    private Animator animator; // found on a child (the player model's own root) - null on any model that doesn't have one
    private Vector3 velocity;
    private Transform cameraAim;
    private float pitch;
    private bool inputLocked;
    private bool blockToggleKeys; // I/Q suppressed - pushed by UInavigator, true during any modal NPC dialogue (girl/wizard/dog's pickup-confirm)
    private bool blockInteractKey; // E suppressed too - true only during girl/wizard dialogue; the dog's own pickup-confirm needs a second E press to complete, so it's deliberately excluded
    private float joltRoll;
    private Coroutine joltCoroutine;
    private Coroutine knockbackCoroutine;
    private float lastMoveMagnitude; // 0-1, set by Move() each frame, read by UpdateAnimator()
    private bool isGrounded; // set by Jump() each frame, read by UpdateAnimator()

    // True while WASD input is actually held (lastMoveMagnitude > ~0) - read by PlayerFootsteps
    // to know when to start/stop its footstep loop.
    public bool IsMoving => lastMoveMagnitude > 0.01f;

    // True while the ground-check sphere cast (see IsTouchingGround, run every frame by Jump())
    // is actually touching solid ground - false both mid-jump/fall AND while WaterFloater is
    // holding the player up over deep water (the sphere cast from the player's floated height
    // finds nothing within groundCheckDistance below it). Read by PlayerFootsteps so footsteps
    // stop the instant either happens and resume the instant the player is grounded again.
    public bool IsGrounded => isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraAim = transform.Find(cameraAimChildName);
        animator = GetComponentInChildren<Animator>();
    }

    // Movement/look freeze for a modal moment like the girl's dialogue panel, the dog's
    // pickup confirm, or the wizard's Trade shop (see GirlNpcBehavior/DogNpcBehavior/
    // TradeManager) - stronger than the cursor-unlock-only gating Look() already does for
    // panels like inventory, which still let the player walk. E/I/Q/Esc are all deliberately
    // exempt (see Update) - they're how a locked interaction gets answered (E) or backed out
    // of/switched away from (I/Q/Esc, via UInavigator) in the first place.
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
    }

    // Pushed every frame by UInavigator (it's the one component that actually knows about
    // girl/wizard/dog dialogue state) - a hard source-level block, not just a convention every
    // E/I/Q listener has to remember to check individually. Fixes the wizard-dialogue cursor
    // bug at its root: pressing E while his conversation is up could reach an unrelated
    // listener (a nearby mob/resource hotspot, MagicStickAbility, etc.) that has no idea a
    // modal dialogue is active, since none of them are gated on IsModalDialogueActive() the way
    // UInavigator's own panel toggles are - blocking the key here means none of them ever see
    // it in the first place. Esc is deliberately never touched by either flag - it must always
    // stay live so a stuck panel can still be escaped.
    public void SetModalDialogueState(bool blockToggleKeys, bool blockInteractKey)
    {
        this.blockToggleKeys = blockToggleKeys;
        this.blockInteractKey = blockInteractKey;
    }

    // Diagnostic camera kick - tilts CameraAim to the right (roll) then eases back level,
    // unrelated to normal look input. Used to visually confirm a periodic event is actually
    // firing (e.g. MiasmaFogArea calling this once per damage tick) even when its actual
    // effect isn't otherwise obvious on screen. CameraAim's rotation feeds Cinemachine (via
    // CinemachineBrain on the actual camera), so the vcam picks this up like any other look
    // change rather than needing to be pushed to it directly.
    public void TriggerCameraJolt()
    {
        Debug.Log("[Miasma] TriggerCameraJolt called - tilting camera right and back.");
        if (joltCoroutine != null) StopCoroutine(joltCoroutine);
        joltCoroutine = StartCoroutine(CameraJoltRoutine());
    }

    private IEnumerator CameraJoltRoutine()
    {
        float halfDuration = joltDuration;

        // Ease out to the tilt.
        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            joltRoll = Mathf.SmoothStep(0f, joltStrength, t / halfDuration);
            yield return null;
        }
        joltRoll = joltStrength;

        // Ease back to level.
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            joltRoll = Mathf.SmoothStep(joltStrength, 0f, t / halfDuration);
            yield return null;
        }

        joltRoll = 0f;
        joltCoroutine = null;
    }

    // Pushes the player horizontally away from fromPosition over knockbackDuration seconds -
    // called by Mobai.PerformAttack right alongside TriggerCameraJolt when a mob lands a hit.
    // Routed through controller.Move() every frame (not a raw transform.position set) since
    // CharacterController owns/overwrites translation itself each frame it's active.
    public void ApplyKnockback(Vector3 fromPosition, float distance)
    {
        Vector3 direction = transform.position - fromPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
        direction.Normalize();

        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, distance));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float distance)
    {
        float speed = distance / knockbackDuration;
        float elapsed = 0f;
        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            controller.Move(direction * speed * Time.deltaTime);
            yield return null;
        }

        knockbackCoroutine = null;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Esc always stays live, even while blockToggleKeys/blockInteractKey are both true -
        // it's the one key that must always be able to back out of a stuck panel.
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            onEscape.Invoke();
        }

        // E normally stays live even while inputLocked - a lock exists BECAUSE the player is
        // mid interaction (an NPC dialogue, or a Trade shop - see TradeManager), and E is how
        // most of those get answered/advanced or (for the dog's two-stage pickup-confirm)
        // completed. blockInteractKey overrides that for girl/wizard specifically, since
        // neither of them needs a further E press once their dialogue is already open - see
        // SetModalDialogueState.
        if (!blockInteractKey && keyboard.eKey.wasPressedThisFrame)
        {
            onInteract.Invoke();
        }

        // I/Q normally stay live even while inputLocked too, so UInavigator's own cross-panel
        // switching can force-close/switch out of whatever's open - but each of those handlers
        // already refuses to act during a modal NPC dialogue anyway (see UInavigator.
        // IsModalDialogueActive), so blocking them here as well is pure defense-in-depth: no
        // future listener on either event can accidentally fire during one, even if it forgets
        // to check that itself.
        if (!blockToggleKeys)
        {
            if (keyboard.iKey.wasPressedThisFrame)
            {
                onToggleInventory.Invoke();
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                onToggleQuestFullView.Invoke();
            }
        }

        if (inputLocked) return;

        Look();
        Move(keyboard);
        Jump(keyboard);
        UpdateAnimator();

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            Debug.Log("[Interact] Right mouse button pressed.");
            onSecondaryInteract.Invoke();
        }
    }

    // Mouse X turns this whole transform (yaw) - which the CharacterController can
    // safely do without tipping over, and which the camera picks up automatically
    // since it tracks a child of this transform. Mouse Y only pitches cameraAim, a
    // camera-only child, so looking up/down never affects the character's own body.
    // Paused while the cursor isn't locked (e.g. a UI panel like the inventory is
    // open and needs the cursor free to click with) - see UInavigator.RefreshCursorLock.
    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();

        transform.Rotate(Vector3.up, delta.x * mouseSensitivity);

        if (cameraAim == null) return;

        float pitchInput = (invertY ? delta.y : -delta.y) * mouseSensitivity;
        pitch = Mathf.Clamp(pitch + pitchInput, minPitch, maxPitch);
        cameraAim.localRotation = Quaternion.Euler(pitch, 0f, joltRoll);
    }

    // WASD moves relative to the current look direction: W/S forward and backward,
    // A/D strafe left and right - none of them turn the player, mouse-look does that.
    void Move(Keyboard keyboard)
    {
        float moveX = 0f;
        float moveZ = 0f;

        if (keyboard.wKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed) moveZ -= 1f;
        if (keyboard.dKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed) moveX -= 1f;

        Vector3 inputDirection = new(moveX, 0f, moveZ);
        if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();
        lastMoveMagnitude = inputDirection.magnitude;

        Vector3 move = transform.forward * inputDirection.z + transform.right * inputDirection.x;
        controller.Move(moveSpeed * Time.deltaTime * move);
    }

    // Space bar jump with simple gravity
    void Jump(Keyboard keyboard)
    {
        isGrounded = IsTouchingGround();

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // small downward force to keep the controller grounded
        }

        if (isGrounded && keyboard.spaceKey.wasPressedThisFrame)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // Drives the player model's own AnimatorController (see the param name consts above).
    // Speed is fed as the raw 0-1 move-input magnitude, NOT multiplied by moveSpeed - Sloth's
    // SmallController.controller's Idle/Walk/Run transitions use tiny thresholds (0.01/0.1),
    // confirming it was authored to compare against a normalized 0-1 value, not real
    // units/sec (feeding moveSpeed's raw 20 in here was also silently multiplying Walk/Run's
    // own playback rate via their SpeedParameterActive - see SmallController.controller,
    // fixed there directly since a state's own animation-speed multiplier isn't something
    // this generic controller script should be reaching into). Grounded/VerticalSpeed come
    // straight from this frame's own Jump()/gravity state - without Grounded, most such
    // controllers park permanently on a falling/airborne state, which is exactly what an
    // unwired model looks like.
    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(SpeedAnimatorParam, lastMoveMagnitude);
        animator.SetBool(GroundedAnimatorParam, isGrounded);
        animator.SetFloat(VerticalSpeedAnimatorParam, velocity.y);
    }

    // Casts a small sphere down from the cube's feet to check if it is touching the terrain right now,
    // independent of whether the controller moved this frame (unlike CharacterController.isGrounded)
    bool IsTouchingGround()
    {
        Vector3 feet = transform.position + controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);
        return Physics.SphereCast(feet, controller.radius * 0.9f, Vector3.down, out _, groundCheckDistance);
    }
}
