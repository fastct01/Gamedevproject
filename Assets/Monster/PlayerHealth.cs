using StarterAssets;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Player health. Damage knocks the player back a little; at 0 the player dies and R restarts.
/// Heals inside oxygen zones marked "Restores Health" (the ship), and takes damage from long falls.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    [Tooltip("Health regained per second after not being hurt for Regen Delay seconds (0 = no regen).")]
    public float regenPerSecond = 0f;
    public float regenDelay = 5f;
    public Key restartKey = Key.R;

    [Header("Healing zones")]
    [Tooltip("Health per second while standing in an oxygen zone that has 'Restores Health' ticked (the ship).")]
    public float zoneHealPerSecond = 12f;

    [Header("Fall damage (world units; the player is 0.09 tall)")]
    public bool fallDamage = true;
    [Tooltip("Falls shorter than this don't hurt. A normal jump drops about 0.2.")]
    public float safeFallHeight = 0.36f;
    [Tooltip("Falls this high or more are fatal. Damage scales up between the two.")]
    public float lethalFallHeight = 1.1f;
    public UnityEvent onDamaged;
    public UnityEvent onDied;

    public float Current { get; private set; }
    public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
    public bool IsDead { get; private set; }
    /// <summary>Time.time of the last hit (for UI flashes).</summary>
    public float LastHitTime { get; private set; } = -10f;
    /// <summary>True while a healing zone is topping health up (for the UI).</summary>
    public bool IsHealing { get; private set; }

    CharacterController _cc;
    Vector3 _knock;
    ThirdPersonController _tpc;
    FirstPersonController _fpc;
    bool _wasGrounded = true;
    float _fallPeakY;
    float _ignoreFallUntil;

    void Awake()
    {
        Current = maxHealth;
        _cc = GetComponent<CharacterController>();
        _tpc = GetComponent<ThirdPersonController>();
        _fpc = GetComponent<FirstPersonController>();
        _fallPeakY = transform.position.y;
        _ignoreFallUntil = Time.time + 1f; // don't count settling onto the ground at spawn
    }

    public void TakeDamage(float amount, Vector3 knockbackVelocity = default)
    {
        if (IsDead || amount <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        LastHitTime = Time.time;
        _knock = knockbackVelocity;
        onDamaged?.Invoke();
        if (Current <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    void Update()
    {
        if (IsDead)
        {
            var kb = Keyboard.current;
            if (kb != null && kb[restartKey].wasPressedThisFrame) Restart();
            return;
        }

        if (_knock.sqrMagnitude > 1e-6f && _cc != null && _cc.enabled)
        {
            _cc.Move(_knock * Time.deltaTime);
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, Time.deltaTime * 4f);
        }

        if (regenPerSecond > 0f && Time.time - LastHitTime > regenDelay)
            Current = Mathf.Min(maxHealth, Current + regenPerSecond * Time.deltaTime);

        // Heal in zones that restore health (the ship's blue circle).
        IsHealing = false;
        if (zoneHealPerSecond > 0f && Current < maxHealth)
            foreach (var z in OxygenZone.Active)
                if (z != null && z.restoresHealth && z.isActiveAndEnabled && z.Contains(transform.position))
                {
                    Current = Mathf.Min(maxHealth, Current + zoneHealPerSecond * Time.deltaTime);
                    IsHealing = true;
                    break;
                }
    }

    void LateUpdate()
    {
        if (IsDead || !fallDamage) return;
        bool grounded = _tpc != null ? _tpc.Grounded : _fpc != null ? _fpc.Grounded : (_cc != null && _cc.isGrounded);
        float y = transform.position.y;

        if (!grounded)
        {
            if (_wasGrounded) _fallPeakY = y;            // just left the ground
            else _fallPeakY = Mathf.Max(_fallPeakY, y);  // track the highest point (jump apex)
        }
        else if (!_wasGrounded)
        {
            float drop = _fallPeakY - y;
            if (drop > safeFallHeight && Time.time > _ignoreFallUntil)
            {
                float t = Mathf.InverseLerp(safeFallHeight, lethalFallHeight, drop);
                TakeDamage(Mathf.Lerp(5f, maxHealth, t));
            }
        }
        _wasGrounded = grounded;
    }

    void Die()
    {
        IsDead = true;
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
        onDied?.Invoke();
    }

    public void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
#if UNITY_EDITOR
        else UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
    }
}
