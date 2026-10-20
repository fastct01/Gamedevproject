using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The blaster tool. While it's equipped (from the hotbar) it shows in your hand and a crosshair appears;
/// left-click fires a bolt. Hits damage the monster (it staggers briefly); loose objects get knocked away.
/// Put on the Player.
/// </summary>
public class Blaster : MonoBehaviour
{
    public string itemId = "blaster";
    public Camera aimCamera;
    [Tooltip("Where it sits in view (player-local units relative to the camera).")]
    public Vector3 viewOffset = new Vector3(0.2f, -0.13f, 0.7f);
    public float viewScale = 1f;
    public float range = 8f;
    public float cooldown = 0.35f;
    [Tooltip("Damage per hit on the monster (it has 100 HP).")]
    public float damage = 25f;
    public float pushImpulse = 0.4f;
    public Color boltColor = new Color(0.3f, 0.95f, 1f);

    Inventory _inv;
    ObjectInteractor _interactor;
    Transform _view, _muzzle;
    LineRenderer _bolt;
    Light _flash;
    Collider[] _own;
    float _next, _boltT, _recoil;

    void Awake()
    {
        _inv = GetComponent<Inventory>();
        _interactor = GetComponent<ObjectInteractor>();
        if (aimCamera == null) aimCamera = GetComponentInChildren<Camera>();
        _own = GetComponentsInChildren<Collider>(true);

        _view = new GameObject("Blaster (held)").transform;
        _view.SetParent(aimCamera.transform, false);
        _view.localPosition = viewOffset;
        _view.localRotation = Quaternion.Euler(-2f, -6f, 0f);
        _view.localScale = Vector3.one * viewScale;
        BlasterModel.Create(_view, out _muzzle);
        _view.gameObject.SetActive(false);

        var boltGo = new GameObject("Blaster Bolt");
        _bolt = boltGo.AddComponent<LineRenderer>();
        _bolt.positionCount = 2;
        _bolt.useWorldSpace = true;
        _bolt.widthMultiplier = 0.004f;
        _bolt.numCapVertices = 2;
        _bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", boltColor * 3f);
        _bolt.sharedMaterial = m;
        _bolt.enabled = false;

        var fl = new GameObject("Muzzle Flash", typeof(Light));
        fl.transform.SetParent(_muzzle, false);
        _flash = fl.GetComponent<Light>();
        _flash.type = LightType.Point; _flash.range = 0.4f; _flash.color = boltColor; _flash.intensity = 0f;
        _flash.shadows = LightShadows.None;
    }

    bool Equipped => _inv != null && _inv.Equipped != null && _inv.Equipped.id == itemId;

    void Update()
    {
        bool show = Equipped;
        if (_view.gameObject.activeSelf != show) _view.gameObject.SetActive(show);

        _boltT = Mathf.MoveTowards(_boltT, 0f, Time.deltaTime * 8f);
        _bolt.enabled = _boltT > 0f;
        if (_bolt.enabled) _bolt.widthMultiplier = 0.004f * _boltT;
        _flash.intensity = 0.15f * _boltT;
        _recoil = Mathf.MoveTowards(_recoil, 0f, Time.deltaTime * 6f);
        _view.localPosition = viewOffset + new Vector3(0f, 0.01f, -0.06f) * _recoil;

        if (!show || _inv.IsOpen) return;
        if (_interactor != null && _interactor.HeldBody != null) return; // left click throws instead
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= _next) Fire();
    }

    void Fire()
    {
        _next = Time.time + cooldown;
        var cam = aimCamera.transform;
        Vector3 end = cam.position + cam.forward * range;
        RaycastHit best = default; bool any = false; float bestD = range;
        foreach (var h in Physics.RaycastAll(cam.position, cam.forward, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= bestD || System.Array.IndexOf(_own, h.collider) >= 0) continue;
            bestD = h.distance; best = h; any = true;
        }
        if (any)
        {
            end = best.point;
            var monster = best.collider.GetComponentInParent<MonsterAI>();
            if (monster != null) monster.TakeDamage(damage);
            else if (best.rigidbody != null && !best.rigidbody.isKinematic)
                best.rigidbody.AddForce(cam.forward * pushImpulse, ForceMode.Impulse);
        }
        _bolt.SetPosition(0, _muzzle.position);
        _bolt.SetPosition(1, end);
        _boltT = 1f;
        _recoil = 1f;
    }
}
