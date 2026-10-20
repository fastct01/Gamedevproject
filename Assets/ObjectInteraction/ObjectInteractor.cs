using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grab / throw / push / pull. Put on the Player root (the object with the CharacterController).
/// Controls: Right Mouse click = pick up / drop (E also works), Left Mouse = throw,
/// hold Right Mouse on something too heavy to pick up = push/pull it.
/// Walking into a Pushable also shoves it.
/// </summary>
[DefaultExecutionOrder(-10)] // before the controller moves, so the step-offset tweak applies this frame
public class ObjectInteractor : MonoBehaviour
{
    [Header("Reach")]
    public Camera playerCamera;
    public float reach = 3f;
    public LayerMask interactMask = ~0;

    [Header("Grab / Throw")]
    public float maxGrabMass = 10f;
    [Tooltip("How far in front of the camera a held object floats (world units; the player is 0.09 tall).")]
    public float holdDistance = 0.14f;
    [Tooltip("How far below the centre of the view a held object sits.")]
    public float holdDrop = 0.025f;

    public float holdStiffness = 15f;
    public float throwSpeed = 14f;

    [Header("Push / Pull")]
    [Tooltip("How fast a dragged object follows the player (higher = tighter).")]
    public float dragStiffness = 10f;
    public float maxDragSpeed = 3f;
    [Tooltip("Objects up to this mass drag at full speed; heavier ones drag proportionally slower.")]
    public float dragEasyMass = 2.5f;
    [Tooltip("Force applied when walking into a Pushable.")]
    public float walkPushForce = 2f;
    [Tooltip("Stop the character automatically stepping up onto boxes/balls it should be pushing.")]
    public bool preventSteppingOnPushables = true;
    [Tooltip("Max push speed used if no Starter Assets controller is found.")]
    public float walkSpeedFallback = 0.8f;

    Rigidbody _held;
    /// <summary>The object currently held with E (null if none).</summary>
    public Rigidbody HeldBody => _held;
    /// <summary>The object currently being pushed/pulled with right mouse (null if none).</summary>
    public Rigidbody DraggedBody => _dragged;
    bool _heldHadGravity;
    Rigidbody _dragged;
    Vector3 _dragOffset;
    Collider[] _playerColliders;
    CharacterController _cc;
    float _defaultStepOffset;
    StarterAssets.StarterAssetsInputs _inputs;
    StarterAssets.ThirdPersonController _tpc;
    StarterAssets.FirstPersonController _fpc;
    bool _speedCapped;
    Inventory _inventory;
    float _baseWalk, _baseSprint;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        _playerColliders = GetComponentsInChildren<Collider>();
        _cc = GetComponent<CharacterController>();
        if (_cc != null) _defaultStepOffset = _cc.stepOffset;
        _inputs = GetComponent<StarterAssets.StarterAssetsInputs>();
        _tpc = GetComponent<StarterAssets.ThirdPersonController>();
        _fpc = GetComponent<StarterAssets.FirstPersonController>();
    }

    void Update()
    {
        UpdateStepOffset();

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        if (_inventory == null) _inventory = GetComponent<Inventory>();
        if (_inventory != null && _inventory.IsOpen) return; // clicks belong to the inventory screen

        // Right click (or E): pick up / drop - the same click toggles.
        // Right click on something too heavy to pick up starts a push/pull drag instead (hold to keep dragging).
        bool rightClick = mouse.rightButton.wasPressedThisFrame;
        if (rightClick || kb.eKey.wasPressedThisFrame)
        {
            if (_held != null) Drop();
            else if (!TryGrab() && rightClick) TryStartDrag();
        }

        // Left mouse: throw
        if (_held != null && mouse.leftButton.wasPressedThisFrame)
        {
            var g = _held.GetComponent<Grabbable>();
            float mult = g != null ? g.throwMultiplier : 1f;
            Release(playerCamera.transform.forward * throwSpeed * mult);
        }

        if (_dragged != null && !mouse.rightButton.isPressed) { _dragged = null; ClearDragSpeedCap(); }
    }

    void FixedUpdate()
    {
        if (_held != null)
        {
            var camT = playerCamera.transform;
            Vector3 target = camT.position + camT.forward * holdDistance - camT.up * holdDrop;
            Vector3 toTarget = target - _held.position;
            _held.velocity = toTarget * holdStiffness; // never drops on its own - only when you let go
            _held.angularVelocity = Vector3.zero;
        }

        if (_dragged != null)
        {
            Vector3 target = transform.position + _dragOffset;
            Vector3 toTarget = target - _dragged.position;
            toTarget.y = 0f;
            // Move with the player straight away (their velocity) plus a pull that closes any gap.
            // The grip never breaks on its own - only when you let go of the mouse button.
            Vector3 follow = _cc != null ? new Vector3(_cc.velocity.x, 0f, _cc.velocity.z) : Vector3.zero;
            Vector3 v = Vector3.ClampMagnitude(follow + toTarget * dragStiffness, DragSpeedLimit(_dragged));
            _dragged.velocity = new Vector3(v.x, _dragged.velocity.y, v.z);
        }
    }

    bool TryGrab()
    {
        if (!Raycast(out var hit)) return false;
        // Items go straight into the inventory.
        var pickup = hit.collider.GetComponentInParent<ItemPickup>();
        if (pickup != null) return pickup.Collect(gameObject);
        var g = hit.collider.GetComponentInParent<Grabbable>();
        if (g == null) return false;
        var rb = g.GetComponent<Rigidbody>();
        if (rb.mass > maxGrabMass) return false;

        rb.SendMessage("OnInteractStart", SendMessageOptions.DontRequireReceiver);
        _held = rb;
        _heldHadGravity = rb.useGravity;
        rb.useGravity = false;
        SetIgnorePlayer(rb, true);
        return true;
    }

    // Let go of the held object.
    void Drop() => Release(Vector3.zero);

    void Release(Vector3 velocity)
    {
        _held.useGravity = _heldHadGravity;
        SetIgnorePlayer(_held, false);
        _held.velocity = velocity;
        _held = null;
    }

    void TryStartDrag()
    {
        if (!Raycast(out var hit)) return;
        var p = hit.collider.GetComponentInParent<Pushable>();
        if (p == null) return;
        p.SendMessage("OnInteractStart", SendMessageOptions.DontRequireReceiver);
        _dragged = p.GetComponent<Rigidbody>();
        _dragOffset = _dragged.position - transform.position;
        _dragOffset.y = 0f;
        ApplyDragSpeedCap();
    }

    // Heavy things drag slower: full speed up to dragEasyMass, then slowing with weight (min 20%).
    float DragSpeedLimit(Rigidbody rb) =>
        maxDragSpeed * Mathf.Clamp(dragEasyMass / Mathf.Max(0.0001f, rb.mass), 0.2f, 1f);

    // While dragging something heavy you walk at its speed, so it can never fall behind and slip away.
    void ApplyDragSpeedCap()
    {
        if (_tpc == null && _fpc == null) return;
        if (!_speedCapped)
        {
            _baseWalk = _tpc != null ? _tpc.MoveSpeed : _fpc.MoveSpeed;
            _baseSprint = _tpc != null ? _tpc.SprintSpeed : _fpc.SprintSpeed;
            _speedCapped = true;
        }
        float cap = _dragged != null ? DragSpeedLimit(_dragged) : float.MaxValue;
        float walk = Mathf.Min(_baseWalk, cap), sprint = Mathf.Min(_baseSprint, cap);
        if (_tpc != null) { _tpc.MoveSpeed = walk; _tpc.SprintSpeed = sprint; }
        if (_fpc != null) { _fpc.MoveSpeed = walk; _fpc.SprintSpeed = sprint; }
    }

    void ClearDragSpeedCap()
    {
        if (!_speedCapped) return;
        if (_tpc != null) { _tpc.MoveSpeed = _baseWalk; _tpc.SprintSpeed = _baseSprint; }
        if (_fpc != null) { _fpc.MoveSpeed = _baseWalk; _fpc.SprintSpeed = _baseSprint; }
        _speedCapped = false;
    }

    void OnDisable() => ClearDragSpeedCap();

    bool Raycast(out RaycastHit hit)
    {
        var t = playerCamera.transform;
        return Physics.Raycast(t.position, t.forward, out hit, reach, interactMask, QueryTriggerInteraction.Ignore);
    }

    float IntendedSpeed()
    {
        if (_inputs == null) return walkSpeedFallback;
        float speed = walkSpeedFallback;
        if (_tpc != null) speed = _inputs.sprint ? _tpc.SprintSpeed : _tpc.MoveSpeed;
        else if (_fpc != null) speed = _inputs.sprint ? _fpc.SprintSpeed : _fpc.MoveSpeed;
        return speed * Mathf.Clamp01(_inputs.move.magnitude);
    }

    // The controller climbs anything lower than its step offset - including small push boxes.
    // While a Pushable is right at your feet, turn stepping off so you push it instead of hopping on top.
    void UpdateStepOffset()
    {
        if (!preventSteppingOnPushables || _cc == null) return;
        Vector3 s = transform.lossyScale;
        float radius = _cc.radius * Mathf.Max(s.x, s.z);
        float halfHeight = _cc.height * s.y * 0.5f;
        Vector3 feet = transform.TransformPoint(_cc.center) + Vector3.down * Mathf.Max(0f, halfHeight - radius);

        bool pushableNearby = false;
        foreach (var c in Physics.OverlapSphere(feet, radius * 2f, ~0, QueryTriggerInteraction.Ignore))
            if (c.GetComponentInParent<Pushable>() != null) { pushableNearby = true; break; }

        _cc.stepOffset = pushableNearby ? 0f : _defaultStepOffset;
    }

    void SetIgnorePlayer(Rigidbody rb, bool ignore)
    {
        foreach (var a in rb.GetComponentsInChildren<Collider>())
            foreach (var b in _playerColliders)
                Physics.IgnoreCollision(a, b, ignore);
    }

    // CharacterController doesn't push rigidbodies by itself, so shove Pushables when walking into them.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var p = hit.collider.GetComponentInParent<Pushable>();
        if (p == null || hit.moveDirection.y < -0.3f) return;
        var rb = p.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic || rb == _dragged) return;
        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;

        // Heavier objects gain speed more slowly (impulse / mass), and nothing is pushed faster than you walk.
        Vector3 v = rb.velocity;
        Vector3 horizontal = new Vector3(v.x, 0f, v.z) + dir * (walkPushForce * p.walkPushMultiplier / rb.mass);
        // Cap at the speed you're trying to walk (the controller's actual speed is ~0 while a box blocks it).
        horizontal = Vector3.ClampMagnitude(horizontal, IntendedSpeed());
        rb.velocity = new Vector3(horizontal.x, v.y, horizontal.z);
    }
}
