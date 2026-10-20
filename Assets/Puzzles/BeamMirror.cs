using UnityEngine;

/// <summary>
/// Reflects the beam off its mirror face (the face whose normal is transform.forward; flat box mirrors reflect
/// on both sides). A splitter also lets half the beam pass straight through.
/// </summary>
public class BeamMirror : MonoBehaviour
{
    public bool splitter;
}
