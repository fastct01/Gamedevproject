using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opens when the player enters this object's trigger collider and closes when they leave.
/// Put it on a GameObject with a trigger BoxCollider that covers the area around the doorway.
/// </summary>
public class SlidingDoor : MonoBehaviour
{
    [System.Serializable]
    public class Panel
    {
        public Transform transform;
        [Tooltip("Local-space offset (relative to the panel's parent) when fully open.")]
        public Vector3 openOffset = new Vector3(1.2f, 0f, 0f);
        [HideInInspector] public Vector3 closedPos;
    }

    public Panel[] panels;
    public float speed = 3f;
    public AudioSource audioSource;
    public AudioClip openClip, closeClip;

    readonly HashSet<Collider> _inside = new HashSet<Collider>();
    bool _open;

    // Start (not Awake) so panels assigned right after AddComponent are picked up.
    void Start()
    {
        foreach (var p in panels) p.closedPos = p.transform.localPosition;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        _inside.Add(other);
        SetOpen(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!_inside.Remove(other)) return;
        if (_inside.Count == 0) SetOpen(false);
    }

    static bool IsPlayer(Collider c) => c.GetComponentInParent<CharacterController>() != null;

    void SetOpen(bool open)
    {
        if (_open == open) return;
        _open = open;
        var clip = open ? openClip : closeClip;
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void Update()
    {
        foreach (var p in panels)
        {
            Vector3 target = p.closedPos + (_open ? p.openOffset : Vector3.zero);
            p.transform.localPosition = Vector3.MoveTowards(p.transform.localPosition, target, speed * Time.deltaTime);
        }
    }
}
