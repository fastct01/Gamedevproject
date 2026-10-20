using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something that can be dropped/pushed into a matching SnapSocket (mirrors, power cores).
/// Picking it up or dragging it pulls it back out of the socket.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SnapItem : MonoBehaviour
{
    public static readonly List<SnapItem> All = new List<SnapItem>();

    [Tooltip("Must match the socket's 'Accepts' value.")]
    public string kind = "Mirror";

    public SnapSocket Socket { get; internal set; }
    public Rigidbody Body { get; private set; }
    /// <summary>Height of the pivot above the bottom of the collider (used to sit it on a socket).</summary>
    public float RestHeight { get; private set; }
    internal float releasedAt = -10f;

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        var col = GetComponentInChildren<Collider>();
        RestHeight = col != null ? transform.position.y - col.bounds.min.y : 0f;
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Sent by ObjectInteractor when the player grabs or starts dragging this object.
    void OnInteractStart()
    {
        if (Socket != null) Socket.Release();
    }
}
