using StarterAssets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Red pill monster. Walks a loop around its territory; while the player is inside the territory with their
/// flashlight on, it chases them, faster than walking but slower than sprinting. Touching the player deals damage.
/// Turning the flashlight off makes it lose you after a few seconds: it checks where it last saw you, then goes
/// back to its loop.
/// It moves on a NavMesh limited to its territory, so it goes around objects and never leaves.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    public MonsterTerritory territory;

    [Header("Movement")]
    public float patrolSpeed = 0.45f;
    [Tooltip("If on, chase speed sits between the player's walk and sprint speeds (see Chase Blend).")]
    public bool chaseSpeedFromPlayer = true;
    [Range(0.05f, 0.95f)] public float chaseBlend = 0.35f;
    [Tooltip("Used when Chase Speed From Player is off.")]
    public float chaseSpeed = 1.2f;
    [Tooltip("How far inside the territory edge the patrol loop runs.")]
    public float patrolInset = 0.9f;
    public int patrolPoints = 10;

    [Header("Attack")]
    public float damage = 20f;
    [Tooltip("Centre-to-centre distance at which it hits (player radius + monster radius + a little).")]
    public float attackRange = 0.075f;
    public float attackCooldown = 1f;
    public float knockback = 0.6f;

    [Header("Darkness")]
    [Tooltip("The monster hunts by your flashlight: with it off, it loses track of you.")]
    public bool needsFlashlightToSee = true;
    [Tooltip("Seconds it still knows where you are after you switch the flashlight off.")]
    public float trackAfterDarkSeconds = 5f;
    [Tooltip("After losing you it checks where it last saw you for this long, then goes back to its loop.")]
    public float searchTime = 2.5f;

    [Header("Look")]
    public Renderer bodyRenderer;
    public Light glow;

    [Header("Health")]
    public float maxHealth = 100f;
    [Tooltip("Seconds it staggers (stops) after being hit.")]
    public float hitStagger = 0.5f;
    [Tooltip("Seconds after dying before it comes back (at the far side of its loop).")]
    public float respawnSeconds = 25f;
    public bool showHealthBar = true;

    public float Health { get; private set; }
    public bool IsDead { get; private set; }

    public bool IsChasing { get; private set; }
    public bool IsStunned => Time.time < _stunnedUntil;

    float _stunnedUntil;
    Collider _collider;
    Transform[] _parts;
    Vector3[] _partScales;
    Transform _barRoot;
    RectTransform _barFill;
    float _barShown = 1f, _barShowUntil, _deadAt = -10f, _respawnAt = -10f;

    /// <summary>Freeze in place for a few seconds (e.g. hit by the blaster).</summary>
    public void Stun(float seconds)
    {
        _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + seconds);
        _flash = 1f;
        if (_agent != null && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.velocity = Vector3.zero; }
    }

    NavMeshAgent _agent;
    Transform _player;
    PlayerHealth _health;
    StaminaSystem _stamina;
    ThirdPersonController _tpc;
    FirstPersonController _fpc;
    PlayerFlashlight _flashlight;
    float _playerWalk = 0.8f, _playerSprint = 2f; // the player's normal speeds, read once at start
    float _lastLitTime = -999f;
    bool _searching;
    float _searchTimer;
    Vector3 _lastSeen;
    Vector3[] _loop;
    int _next;
    float _repath, _lastAttack = -10f, _flash;
    Material _mat;
    Color _baseEmission;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.enabled = false; // enabled once our NavMesh exists
        _collider = GetComponent<Collider>();
        if (bodyRenderer != null)
        {
            _mat = bodyRenderer.material;
            _baseEmission = _mat.GetColor("_EmissionColor");
        }
        Health = maxHealth;
        // Everything visible scales down when it dies and back up when it respawns.
        _parts = new Transform[transform.childCount];
        _partScales = new Vector3[transform.childCount];
        for (int i = 0; i < transform.childCount; i++) { _parts[i] = transform.GetChild(i); _partScales[i] = _parts[i].localScale; }
        if (showHealthBar) BuildHealthBar();
    }

    // ------------------------------------------------------------ health
    /// <summary>Take damage (e.g. from the blaster). Staggers briefly; at 0 HP it dies and respawns later.</summary>
    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        Health = Mathf.Max(0f, Health - amount);
        _barShowUntil = Time.time + 4f;
        if (Health <= 0f) { Die(); return; }
        Stun(hitStagger);
    }

    void Die()
    {
        IsDead = true;
        IsChasing = false;
        _searching = false;
        _deadAt = Time.time;
        if (_agent.isOnNavMesh) { _agent.isStopped = true; _agent.velocity = Vector3.zero; }
        if (_collider != null) _collider.enabled = false;
        if (glow != null) glow.enabled = false;
    }

    void Respawn()
    {
        // Come back at the loop point furthest from the player.
        Vector3 spawn = transform.position;
        if (_loop != null && _loop.Length > 0)
        {
            float best = -1f;
            int bi = 0;
            for (int i = 0; i < _loop.Length; i++)
            {
                float d = _player != null ? (_loop[i] - _player.position).sqrMagnitude : i;
                if (d > best) { best = d; bi = i; }
            }
            spawn = _loop[bi];
            _next = bi;
        }
        _agent.Warp(spawn);
        _agent.isStopped = false;
        Health = maxHealth;
        _barShown = 1f;
        IsDead = false;
        _respawnAt = Time.time;
        if (_collider != null) _collider.enabled = true;
        if (glow != null) glow.enabled = true;
        _lastLitTime = -999f;
        GoPatrol();
    }

    // Shrinks away when dead, grows back in when respawning.
    void UpdateDeathScale()
    {
        float k = 1f;
        if (IsDead) k = 1f - Mathf.Clamp01((Time.time - _deadAt) / 0.6f);
        else if (Time.time - _respawnAt < 0.8f) k = Mathf.Clamp01((Time.time - _respawnAt) / 0.8f);
        for (int i = 0; i < _parts.Length; i++)
            if (_parts[i] != null && _parts[i] != _barRoot) _parts[i].localScale = _partScales[i] * k;
    }

    void BuildHealthBar()
    {
        var go = new GameObject("HP Bar", typeof(RectTransform), typeof(Canvas));
        _barRoot = go.transform;
        _barRoot.SetParent(transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)_barRoot;
        rt.sizeDelta = new Vector2(100f, 14f);
        rt.localScale = Vector3.one * 0.0008f; // 0.08 x 0.011 world units
        rt.localPosition = new Vector3(0f, (territory != null ? territory.agentHeight : 0.12f) + 0.03f, 0f);
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(_barRoot, false);
        var brt = (RectTransform)bg.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(_barRoot, false);
        _barFill = (RectTransform)fill.transform;
        _barFill.anchorMin = Vector2.zero; _barFill.anchorMax = Vector2.one;
        _barFill.offsetMin = new Vector2(2f, 2f); _barFill.offsetMax = new Vector2(-2f, -2f);
        fill.GetComponent<Image>().color = new Color(0.95f, 0.15f, 0.1f);
    }

    void LateUpdate()
    {
        UpdateDeathScale();
        if (_barRoot == null) return;
        bool show = !IsDead && (IsChasing || Health < maxHealth || Time.time < _barShowUntil);
        if (_barRoot.gameObject.activeSelf != show) _barRoot.gameObject.SetActive(show);
        if (!show) return;
        _barShown = Mathf.MoveTowards(_barShown, Health / maxHealth, Time.deltaTime * 2f);
        _barFill.anchorMax = new Vector2(_barShown, 1f);
        var cam = Camera.main != null ? Camera.main : (_player != null ? _player.GetComponentInChildren<Camera>() : null);
        if (cam != null) _barRoot.rotation = Quaternion.LookRotation(_barRoot.position - cam.transform.position, Vector3.up);
    }

    void Start()
    {
        if (territory == null) territory = FindFirstObjectByType<MonsterTerritory>();
        var cc = FindFirstObjectByType<CharacterController>();
        if (cc != null)
        {
            _player = cc.transform;
            _health = cc.GetComponent<PlayerHealth>();
            _stamina = cc.GetComponent<StaminaSystem>();
            _tpc = cc.GetComponent<ThirdPersonController>();
            _fpc = cc.GetComponent<FirstPersonController>();
            _flashlight = cc.GetComponent<PlayerFlashlight>();
            if (_tpc != null) { _playerWalk = _tpc.MoveSpeed; _playerSprint = _tpc.SprintSpeed; }
            else if (_fpc != null) { _playerWalk = _fpc.MoveSpeed; _playerSprint = _fpc.SprintSpeed; }
        }

        if (territory == null || !territory.IsBuilt) { Debug.LogWarning("Monster has no territory NavMesh."); enabled = false; return; }

        var filter = new NavMeshQueryFilter { agentTypeID = territory.AgentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, filter)) { Debug.LogWarning("Monster spawn is off the NavMesh."); enabled = false; return; }

        _agent.agentTypeID = territory.AgentTypeId;
        _agent.radius = territory.agentRadius;
        _agent.height = territory.agentHeight;
        _agent.baseOffset = 0f;
        _agent.acceleration = 6f;
        _agent.angularSpeed = 720f;
        _agent.autoBraking = false;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        transform.position = hit.position;
        _agent.enabled = true;
        _agent.Warp(hit.position);

        BuildLoop(filter);
        _next = NearestLoopIndex();
        GoPatrol();
    }

    void BuildLoop(NavMeshQueryFilter filter)
    {
        Bounds b = territory.Area;
        float minX = b.min.x + patrolInset, maxX = b.max.x - patrolInset;
        float minZ = b.min.z + patrolInset, maxZ = b.max.z - patrolInset;
        Vector3 c = b.center;
        float rx = (maxX - minX) * 0.5f, rz = (maxZ - minZ) * 0.5f;
        var pts = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < patrolPoints; i++)
        {
            // Rounded-square loop around the area.
            float a = i / (float)patrolPoints * Mathf.PI * 2f;
            float x = Mathf.Sign(Mathf.Cos(a)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 0.5f) * rx;
            float z = Mathf.Sign(Mathf.Sin(a)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.5f) * rz;
            // Sample at ground level (not mid-air) so points don't land on top of platforms or boxes.
            var p = new Vector3(c.x + x, transform.position.y, c.z + z);
            if (!NavMesh.SamplePosition(p, out var h, 1f, filter)) continue;
            // Keep only points the monster can actually walk to.
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, h.position, filter, path) && path.status == NavMeshPathStatus.PathComplete)
                pts.Add(h.position);
        }
        _loop = pts.ToArray();
    }

    int NearestLoopIndex()
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < _loop.Length; i++)
        {
            float d = (_loop[i] - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    void GoPatrol()
    {
        IsChasing = false;
        _agent.speed = patrolSpeed;
        _agent.stoppingDistance = 0f;
        if (_loop.Length > 0) _agent.SetDestination(_loop[_next]);
    }

    float ChaseSpeed()
    {
        if (!chaseSpeedFromPlayer) return chaseSpeed;
        return Mathf.Lerp(_playerWalk, _playerSprint, chaseBlend);
    }

    void Update()
    {
        if (!_agent.isOnNavMesh) return;
        if (IsDead)
        {
            if (Time.time - _deadAt >= respawnSeconds) Respawn();
            return;
        }
        if (IsStunned)
        {
            _agent.isStopped = true;
            _flash = Mathf.Max(_flash, 0.4f + 0.4f * Mathf.Sin(Time.time * 20f)); // flicker while stunned
            UpdateLook();
            return;
        }
        if (_agent.isStopped) _agent.isStopped = false;

        bool playerAlive = _player != null && (_health == null || !_health.IsDead);
        bool playerInArea = playerAlive && territory.Contains(_player.position);
        // It can only see you while your flashlight is on.
        bool lightOn = !needsFlashlightToSee || _flashlight == null || _flashlight.IsOn;
        if (lightOn) _lastLitTime = Time.time;
        // After the light goes out it still knows where you are for a few seconds.
        bool canSee = playerInArea && (lightOn || Time.time - _lastLitTime <= trackAfterDarkSeconds);

        if (canSee)
        {
            if (!IsChasing)
            {
                IsChasing = true;
                _searching = false;
                _agent.stoppingDistance = attackRange * 0.6f;
            }
            _lastSeen = _player.position;
            _agent.speed = ChaseSpeed();
            _repath -= Time.deltaTime;
            if (_repath <= 0f)
            {
                _repath = 0.15f;
                var filter = new NavMeshQueryFilter { agentTypeID = territory.AgentTypeId, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(_player.position, out var hit, 0.5f, filter)) _agent.SetDestination(hit.position);
            }
            TryAttack();
        }
        else if (IsChasing)
        {
            IsChasing = false;
            if (playerInArea)
            {
                // Light went out: it heads to where it last saw you and looks around, but can't find you.
                _searching = true;
                _searchTimer = searchTime;
                _agent.speed = patrolSpeed;
                _agent.stoppingDistance = 0f;
                var filter = new NavMeshQueryFilter { agentTypeID = territory.AgentTypeId, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(_lastSeen, out var hit, 0.5f, filter)) _agent.SetDestination(hit.position);
            }
            else
            {
                // You left its area: pick the loop back up from the nearest point.
                _next = NearestLoopIndex();
                GoPatrol();
            }
        }
        else if (_searching)
        {
            if (!_agent.pathPending && _agent.remainingDistance < 0.1f)
            {
                _searchTimer -= Time.deltaTime;
                transform.Rotate(0f, 120f * Time.deltaTime, 0f); // looking around
                if (_searchTimer <= 0f)
                {
                    _searching = false;
                    _next = NearestLoopIndex();
                    GoPatrol();
                }
            }
        }
        else if (_loop.Length > 0 && !_agent.pathPending && _agent.remainingDistance < 0.15f)
        {
            _next = (_next + 1) % _loop.Length;
            _agent.SetDestination(_loop[_next]);
        }

        UpdateLook();
    }

    void TryAttack()
    {
        if (_health == null || Time.time - _lastAttack < attackCooldown) return;
        Vector3 d = _player.position - transform.position;
        float dy = Mathf.Abs(d.y);
        d.y = 0f;
        if (d.magnitude > attackRange || dy > territory.agentHeight) return;
        _lastAttack = Time.time;
        _flash = 1f;
        _health.TakeDamage(damage, d.sqrMagnitude > 1e-6f ? d.normalized * knockback : Vector3.zero);
    }

    void UpdateLook()
    {
        _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 3f);
        float pulse = IsChasing ? 1.6f + 0.6f * Mathf.Sin(Time.time * 12f) : 1f;
        if (_mat != null) _mat.SetColor("_EmissionColor", _baseEmission * pulse + Color.white * _flash);
        if (glow != null) glow.intensity = (IsChasing ? 0.08f : 0.04f) * pulse;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (_loop == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0.2f);
        for (int i = 0; i < _loop.Length; i++) Gizmos.DrawLine(_loop[i], _loop[(i + 1) % _loop.Length]);
    }
}
