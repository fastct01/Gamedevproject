using UnityEngine;
using UnityEngine.Events;

/// <summary>Opens the well lid and switches on its rewards once every receiver is lit at the same time.</summary>
public class Lightwell : MonoBehaviour
{
    public BeamReceiver[] receivers;
    public PuzzleGate lid;
    public GameObject[] activateOnSolve;
    public UnityEvent onSolved;

    public bool Solved { get; private set; }

    void Update()
    {
        if (Solved || receivers == null || receivers.Length == 0) return;
        foreach (var r in receivers) if (r == null || !r.IsLit) return;
        Solved = true;
        if (lid != null) lid.Open();
        foreach (var g in activateOnSolve) if (g != null) g.SetActive(true);
        onSolved?.Invoke();
    }
}
