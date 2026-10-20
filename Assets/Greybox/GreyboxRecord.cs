using UnityEngine;

/// <summary>
/// Remembers what Greybox mode changed on an object so it can be put back exactly.
/// Added and removed by Tools > Greybox; you don't need to touch it.
/// </summary>
[DisallowMultipleComponent]
public class GreyboxRecord : MonoBehaviour
{
    public bool rendererWasEnabled = true;
    [Tooltip("Set when only the material was swapped (Unity primitive with a textured material).")]
    public bool materialSwapOnly;
    public Material[] originalMaterials;
    public MeshCollider[] disabledColliders;
    public GameObject proxy;
}
