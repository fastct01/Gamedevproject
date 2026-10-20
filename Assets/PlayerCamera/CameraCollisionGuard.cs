using UnityEngine;

/// <summary>
/// Stops the camera from ending up inside walls, boxes and other objects (e.g. when pushing a cube).
/// Each frame it checks the line from the player's centre to where the camera wants to be and,
/// if something is in the way, moves the camera back just far enough to stay clear of it.
/// Put it on the camera.
/// </summary>
[DefaultExecutionOrder(90)] // after the controller has positioned/rotated the camera
public class CameraCollisionGuard : MonoBehaviour
{
    [Tooltip("The player root (with the CharacterController). Found automatically if empty.")]
    public Transform player;
    [Tooltip("Minimum gap (world units) kept between the camera and any surface. Keep it bigger than the camera's near clip.")]
    public float clearance = 0.012f;
    public LayerMask collisionMask = ~0;

    Vector3 _desiredLocal;
    Collider[] _own;
    Camera _cam;

    void Awake()
    {
        _desiredLocal = transform.localPosition;
        _cam = GetComponent<Camera>();
        if (player == null)
        {
            var cc = GetComponentInParent<CharacterController>();
            player = cc != null ? cc.transform : transform.root;
        }
        _own = player.GetComponentsInChildren<Collider>(true);
    }

    void LateUpdate()
    {
        var parent = transform.parent;
        Vector3 desired = parent != null ? parent.TransformPoint(_desiredLocal) : _desiredLocal;

        // Safe starting point: straight above the player's centre at camera height (the capsule keeps
        // everything at least its radius away from here).
        Vector3 origin = new Vector3(player.position.x, desired.y, player.position.z);
        Vector3 toDesired = desired - origin;
        float dist = toDesired.magnitude;

        float radius = clearance;
        if (_cam != null) radius = Mathf.Max(clearance, _cam.nearClipPlane * 1.2f);

        float allowed = dist;
        if (dist > 1e-5f)
        {
            Vector3 dir = toDesired / dist;
            foreach (var hit in Physics.SphereCastAll(origin, radius, dir, dist, collisionMask, QueryTriggerInteraction.Ignore))
                if (hit.distance < allowed && !IsOwn(hit.collider)) allowed = hit.distance;
            transform.position = origin + dir * allowed;
        }
        else transform.position = desired;
    }

    bool IsOwn(Collider c)
    {
        foreach (var o in _own) if (o == c) return true;
        return false;
    }
}
