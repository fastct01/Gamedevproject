using StarterAssets;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drains stamina while sprinting, regenerates it otherwise, and blocks sprinting at zero.
/// Works with the Starter Assets First Person Controller without modifying it:
/// it runs before the controller and clears StarterAssetsInputs.sprint when exhausted.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina")]
    public float maxStamina = 100f;
    [Tooltip("Stamina lost per second while sprinting.")]
    public float drainPerSecond = 20f;
    [Tooltip("Stamina gained per second while not sprinting.")]
    public float regenPerSecond = 12f;
    [Tooltip("Seconds to wait after sprinting stops before regeneration starts.")]
    public float regenDelay = 1f;
    [Tooltip("After hitting zero, stamina must reach this amount before sprinting is allowed again.")]
    public float resumeThreshold = 20f;
    [Tooltip("Only drain stamina while actually moving.")]
    public bool requireMovementToDrain = true;

    [Header("Events")]
    public UnityEvent onExhausted;
    public UnityEvent onRecovered;

    public float Current { get; private set; }
    public float Normalized => maxStamina > 0f ? Current / maxStamina : 0f;
    public bool IsExhausted { get; private set; }
    public bool IsSprinting { get; private set; }

    StarterAssetsInputs _input;
    float _regenTimer;

    void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        Current = maxStamina;
    }

    void Update()
    {
        if (_input == null) return;

        bool wantsSprint = _input.sprint;
        bool moving = !requireMovementToDrain || _input.move != Vector2.zero;

        // Block sprinting while exhausted.
        if (IsExhausted) _input.sprint = false;

        IsSprinting = wantsSprint && moving && !IsExhausted;

        if (IsSprinting)
        {
            Current = Mathf.Max(0f, Current - drainPerSecond * Time.deltaTime);
            _regenTimer = regenDelay;

            if (Current <= 0f)
            {
                IsExhausted = true;
                _input.sprint = false;
                IsSprinting = false;
                onExhausted?.Invoke();
            }
        }
        else
        {
            if (_regenTimer > 0f) _regenTimer -= Time.deltaTime;
            else Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);

            if (IsExhausted && Current >= Mathf.Min(resumeThreshold, maxStamina))
            {
                IsExhausted = false;
                onRecovered?.Invoke();
            }
        }
    }
}
