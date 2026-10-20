using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// R turns a mirror 45°: the one you're holding, or the one you're looking at on a socket. Put on the Player.
/// A held mirror keeps its angle when you drop it on a socket.
/// </summary>
public class MirrorRotateInput : MonoBehaviour
{
    public Camera aimCamera;
    public float reach = 1.5f;
    public Key rotateKey = Key.R;

    Collider[] _own;
    ObjectInteractor _interactor;

    void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponentInChildren<Camera>();
        _own = GetComponentsInChildren<Collider>(true);
        _interactor = GetComponent<ObjectInteractor>();
        if (_interactor != null) reach = Mathf.Max(reach, _interactor.reach);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || aimCamera == null) return;
        if (!kb[rotateKey].wasPressedThisFrame) return;
        const int dir = 1;

        // Holding something: turn it in your hands.
        if (_interactor != null && _interactor.HeldBody != null)
        {
            var held = _interactor.HeldBody;
            held.rotation = Quaternion.Euler(0f, 45f * dir, 0f) * held.rotation;
            held.transform.rotation = held.rotation;
            return;
        }

        var t = aimCamera.transform;
        float best = float.MaxValue;
        SnapSocket target = null;
        foreach (var h in Physics.RaycastAll(t.position, t.forward, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= best || System.Array.IndexOf(_own, h.collider) >= 0) continue;
            best = h.distance;
            var item = h.collider.GetComponentInParent<SnapItem>();
            target = item != null ? item.Socket : h.collider.GetComponentInParent<SnapSocket>();
        }
        if (target != null) target.Rotate(dir);
    }
}
