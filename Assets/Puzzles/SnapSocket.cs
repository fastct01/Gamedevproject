using UnityEngine;

/// <summary>
/// A spot on the ground that locks a matching SnapItem in place when it's pushed/dropped close enough.
/// Rotatable sockets let the player turn the item in steps (see MirrorRotateInput).
/// </summary>
public class SnapSocket : MonoBehaviour
{
    [Tooltip("SnapItem.kind this socket takes.")]
    public string accepts = "Mirror";
    public float snapRadius = 0.1f;
    [Tooltip("Max height difference between item and socket to snap.")]
    public float heightTolerance = 0.3f;
    [Tooltip("Yaw (degrees) the item is set to when snapped.")]
    public float yaw;
    public bool rotatable = true;
    public float rotateStep = 45f;
    [Tooltip("Optional pad graphic that turns to match the current angle (e.g. a mirror-shaped pad).")]
    public Transform padVisual;

    public SnapItem Current { get; private set; }

    ObjectInteractor _interactor;

    void Start()
    {
        _interactor = FindFirstObjectByType<ObjectInteractor>();
        AlignPad();
    }

    void AlignPad()
    {
        if (padVisual != null) padVisual.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void Update()
    {
        if (Current != null) return;
        foreach (var item in SnapItem.All)
        {
            if (item == null || item.kind != accepts || item.Socket != null) continue;
            if (Time.time - item.releasedAt < 0.4f) continue;
            var rb = item.Body;
            if (_interactor != null && (rb == _interactor.HeldBody || rb == _interactor.DraggedBody)) continue;
            Vector3 d = item.transform.position - transform.position;
            if (Mathf.Abs(d.y) > heightTolerance) continue;
            d.y = 0f;
            if (d.magnitude > snapRadius) continue;
            if (!rb.isKinematic && rb.linearVelocity.magnitude > 0.6f) continue;
            Snap(item);
            break;
        }
    }

    void Snap(SnapItem item)
    {
        Current = item;
        item.Socket = this;
        var rb = item.Body;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        // Keep the angle the player gave it (snapped to the rotation step); fixed sockets use their own angle.
        if (rotatable && rotateStep > 0f)
            yaw = Mathf.Repeat(Mathf.Round(item.transform.eulerAngles.y / rotateStep) * rotateStep, 360f);
        Place();
    }

    void Place()
    {
        AlignPad();
        if (Current == null) return;
        Current.transform.SetPositionAndRotation(
            transform.position + Vector3.up * Current.RestHeight,
            Quaternion.Euler(0f, yaw, 0f));
    }

    public void Rotate(int direction)
    {
        if (!rotatable || Current == null) return;
        yaw = Mathf.Repeat(yaw + direction * rotateStep, 360f);
        Place();
    }

    public void Release()
    {
        if (Current == null) return;
        var item = Current;
        Current = null;
        item.Socket = null;
        item.releasedAt = Time.time;
        item.Body.isKinematic = false;
    }
}
