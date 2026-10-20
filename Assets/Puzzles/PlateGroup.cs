using UnityEngine;
using UnityEngine.Events;

/// <summary>Opens a gate (and switches on rewards) once every pressure plate has the right weight.</summary>
public class PlateGroup : MonoBehaviour
{
    public PressurePlate[] plates;
    public PuzzleGate gate;
    [Tooltip("Objects switched on when solved (e.g. an oxygen zone).")]
    public GameObject[] activateOnSolve;
    [Tooltip("Stay solved even if a weight is later removed.")]
    public bool latch = true;
    public UnityEvent onSolved;

    public bool Solved { get; private set; }

    void Update()
    {
        bool all = plates != null && plates.Length > 0;
        if (all)
            foreach (var p in plates)
                if (p == null || !p.IsSatisfied) { all = false; break; }

        if (all && !Solved)
        {
            Solved = true;
            if (gate != null) gate.Open();
            foreach (var g in activateOnSolve) if (g != null) g.SetActive(true);
            onSolved?.Invoke();
        }
        else if (!all && Solved && !latch)
        {
            Solved = false;
            if (gate != null) gate.Close();
            foreach (var g in activateOnSolve) if (g != null) g.SetActive(false);
        }
    }
}
