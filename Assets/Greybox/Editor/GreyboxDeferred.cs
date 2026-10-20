using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs a queued Greybox re-apply as soon as the editor is back in Edit mode (scene edits made during
/// Play mode are thrown away, so changes requested while playing wait here until you stop).
/// Queue it with Tools > Greybox > Re-apply Greybox Mode.
/// </summary>
[InitializeOnLoad]
static class GreyboxDeferred
{
    // Value "pending;Name1,Name2" - the names are root objects to mark with GreyboxKeep first.
    const string Key = "Greybox.PendingReapply";

    static GreyboxDeferred()
    {
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) Schedule(); };
        Schedule();
    }

    [MenuItem("Tools/Greybox/Re-apply Greybox Mode")]
    static void Reapply() => Queue("");

    public static void Queue(string keepRootNames)
    {
        SessionState.SetString(Key, "pending;" + keepRootNames);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            Debug.Log("Greybox: will re-apply automatically when you stop Play mode.");
        Schedule();
    }

    static void Schedule()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        string value = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(value) || EditorApplication.isPlayingOrWillChangePlaymode) return; // retried on EnteredEditMode
        if (EditorApplication.isCompiling) { EditorApplication.delayCall += Run; return; }

        SessionState.EraseString(Key);
        GreyboxTool.Disable();

        string[] parts = value.Split(';');
        if (parts.Length > 1)
            foreach (var name in parts[1].Split(','))
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == name.Trim() && root.GetComponent<GreyboxKeep>() == null)
                        Undo.AddComponent<GreyboxKeep>(root);
            }

        GreyboxTool.Enable();
        foreach (var zone in Object.FindObjectsByType<OxygenZone>(FindObjectsSortMode.None))
        {
            zone.enabled = false;
            zone.enabled = true; // redraw the circle on the new ground
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Greybox: re-applied. Save the scene (Cmd+S) to keep it.");
    }
}
