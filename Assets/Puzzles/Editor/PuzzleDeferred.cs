using UnityEditor;
using UnityEngine;

/// <summary>Runs a puzzle rebuild that was requested during Play mode as soon as the editor is back in Edit mode.</summary>
[InitializeOnLoad]
static class PuzzleDeferred
{
    // Value: name of the build to run, e.g. "LightwellSimple".
    const string Key = "Puzzles.PendingBuild";

    static PuzzleDeferred()
    {
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) Schedule(); };
        Schedule();
    }

    public static void Queue(string build)
    {
        SessionState.SetString(Key, build);
        Schedule();
    }

    static void Schedule()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        string build = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(build) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling) { EditorApplication.delayCall += Run; return; }
        SessionState.EraseString(Key);
        switch (build)
        {
            case "LightwellSimple": PuzzleBuilder.BuildLightwell(); break;
            case "LightwellAdvanced": PuzzleBuilder.BuildLightwellAdvanced(); break;
            case "WeighingGate": PuzzleBuilder.BuildWeighingGate(); break;
            case "ParkourReward": InventoryBuilder.PlaceParkourReward(); break;
        }
        Debug.Log("Puzzles: rebuilt '" + build + "'. Save the scene (Cmd+S) to keep it.");
    }
}
