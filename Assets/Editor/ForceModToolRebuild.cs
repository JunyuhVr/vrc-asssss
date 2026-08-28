using UnityEditor;
using UnityEngine;
// trigger recompile

[InitializeOnLoad]
public static class ForceModToolRebuild
{
    static ForceModToolRebuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorPrefs.SetInt("Chromix.ModTool.BuildVersion", 0);
            Debug.Log("[ForceRebuild] Reset ModTool build key, triggering rebuild...");
            ChromixModToolBuilder.RebuildFromMenu();
        };
    }
}
