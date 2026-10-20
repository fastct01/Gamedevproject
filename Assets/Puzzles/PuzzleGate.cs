using UnityEngine;

/// <summary>A gate/lid that slides open (and optionally closed) when a puzzle tells it to.</summary>
public class PuzzleGate : MonoBehaviour
{
    [Tooltip("The moving part. Defaults to this transform.")]
    public Transform panel;
    [Tooltip("Local-space offset of the panel when fully open.")]
    public Vector3 openOffset = new Vector3(0.5f, 0f, 0f);
    public float duration = 1.5f;
    public bool isOpen;
    public AudioSource audioSource;
    public AudioClip openClip;

    Vector3 _closed;
    float _t;

    void Awake()
    {
        if (panel == null) panel = transform;
        _closed = panel.localPosition;
        _t = isOpen ? 1f : 0f;
        Apply();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
    }

    public void Close() => isOpen = false;

    void Update()
    {
        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(_t, target)) return;
        _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
        Apply();
    }

    void Apply()
    {
        float e = _t * _t * (3f - 2f * _t);
        panel.localPosition = _closed + openOffset * e;
    }
}
