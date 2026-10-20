using UnityEngine;

/// <summary>Marks a small object the player can pick up and throw. Needs a Rigidbody and a Collider.</summary>
[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    [Tooltip("Multiplies the player's throw speed for this object (lighter things can fly further).")]
    public float throwMultiplier = 1f;
}
