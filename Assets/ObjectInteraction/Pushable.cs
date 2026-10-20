using UnityEngine;

/// <summary>
/// Marks a heavy object the player can shove by walking into it, or push/pull by holding Right Mouse.
/// Needs a Rigidbody (give it a high mass / drag so it feels heavy) and a Collider.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pushable : MonoBehaviour
{
    [Tooltip("Scales how strongly walking into this object shoves it.")]
    public float walkPushMultiplier = 1f;
}
