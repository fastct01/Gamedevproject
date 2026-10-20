using StarterAssets;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Global oxygen timer. Oxygen drains while the player is outside every OxygenZone, refills inside one,
/// and at zero the player suffocates: movement stops, onOutOfOxygen fires, and R restarts the scene.
/// Put it on the Player (the object with the CharacterController).
/// </summary>
public class OxygenSystem : MonoBehaviour
{
    [Header("Oxygen")]
    public float maxOxygen = 100f;
    [Tooltip("Seconds a full tank lasts outside a refill zone.")]
    public float fullTankSeconds = 180f;
    [Tooltip("Oxygen gained per second inside a refill zone.")]
    public float refillPerSecond = 30f;
    [Tooltip("Drain multiplier while sprinting (needs StaminaSystem on the player). 1 = no extra drain.")]
    public float sprintDrainMultiplier = 1.5f;
    [Range(0f, 1f)] public float lowOxygenThreshold = 0.25f;

    [Header("Failure")]
    public UnityEvent onOutOfOxygen;
    public UnityEvent onLowOxygen;
    [Tooltip("Press this to restart after running out.")]
    public Key restartKey = Key.R;

    public float Current { get; private set; }
    public float Normalized => maxOxygen > 0f ? Current / maxOxygen : 0f;
    public bool IsRefilling { get; private set; }
    public bool IsDepleted { get; private set; }
    public bool IsLow => Normalized <= lowOxygenThreshold && BoostRemaining <= 0f;
    /// <summary>Seconds left on an oxygen boost (no drain while it runs).</summary>
    public float BoostRemaining { get; private set; }
    /// <summary>Seconds left at the normal drain rate.</summary>
    public float SecondsRemaining => DrainPerSecond > 0f ? Current / DrainPerSecond : float.PositiveInfinity;

    float DrainPerSecond => fullTankSeconds > 0f ? maxOxygen / fullTankSeconds : 0f;

    StaminaSystem _stamina;
    bool _warnedLow;

    void Awake()
    {
        Current = maxOxygen;
        _stamina = GetComponent<StaminaSystem>();
    }

    void Start()
    {
        var inv = GetComponent<Inventory>();
        if (inv != null) inv.Used += OnItemUsed;
    }

    void OnItemUsed(InventoryItem item)
    {
        if (item == null || item.oxygenBoostSeconds <= 0f) return;
        StartBoost(item.oxygenBoostSeconds);
        var inv = GetComponent<Inventory>();
        if (inv != null) inv.Notify($"{item.displayName} active: unlimited oxygen for {Mathf.RoundToInt(item.oxygenBoostSeconds / 60f)} minutes");
    }

    /// <summary>Fill the tank and stop oxygen draining for this many seconds.</summary>
    public void StartBoost(float seconds)
    {
        Current = maxOxygen;
        BoostRemaining = Mathf.Max(BoostRemaining, seconds);
    }

    void Update()
    {
        if (IsDepleted)
        {
            var kb = Keyboard.current;
            if (kb != null && kb[restartKey].wasPressedThisFrame) Restart();
            return;
        }

        if (BoostRemaining > 0f)
        {
            BoostRemaining = Mathf.Max(0f, BoostRemaining - Time.deltaTime);
            Current = maxOxygen; // boost: no drain
            IsRefilling = false;
            return;
        }

        IsRefilling = InAnyZone();
        if (IsRefilling)
        {
            Current = Mathf.Min(maxOxygen, Current + refillPerSecond * Time.deltaTime);
            if (!IsLow) _warnedLow = false;
        }
        else
        {
            float mult = _stamina != null && _stamina.IsSprinting ? sprintDrainMultiplier : 1f;
            Current = Mathf.Max(0f, Current - DrainPerSecond * mult * Time.deltaTime);
        }

        if (IsLow && !_warnedLow)
        {
            _warnedLow = true;
            onLowOxygen?.Invoke();
        }

        if (Current <= 0f) Fail();
    }

    bool InAnyZone()
    {
        foreach (var z in OxygenZone.Active)
            if (z.isActiveAndEnabled && z.Contains(transform.position)) return true;
        return false;
    }

    void Fail()
    {
        IsDepleted = true;
        IsRefilling = false;

        // Freeze the player.
        var tpc = GetComponent<ThirdPersonController>();
        if (tpc != null) tpc.enabled = false;
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
        var input = GetComponent<StarterAssetsInputs>();
        if (input != null)
        {
            input.move = Vector2.zero;
            input.sprint = false;
            input.cursorInputForLook = false;
            input.cursorLocked = false;
        }
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;

        onOutOfOxygen?.Invoke();
    }

    public void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
#if UNITY_EDITOR
        else UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
    }

    /// <summary>Instantly refill (e.g. from a pickup).</summary>
    public void AddOxygen(float amount) => Current = Mathf.Clamp(Current + amount, 0f, maxOxygen);
}
