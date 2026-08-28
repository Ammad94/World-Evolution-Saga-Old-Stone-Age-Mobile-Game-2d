using UnityEngine;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Quits the app from the runtime assembly. The UnityEditor namespace is only
    /// referenced by editor assemblies, so UnityEditor.EditorApplication cannot be
    /// used directly in Assets/Scripts — it would fail to compile (CS0234) and
    /// trigger Unity's "Enter Safe Mode" dialog. Play mode is therefore stopped
    /// through reflection in the Editor, while builds use Application.Quit.
    /// </summary>
    public static class EditorOnlyQuitter
    {
        public static void Quit()
        {
#if UNITY_EDITOR
            var editorApp = System.Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            var isPlaying = editorApp?.GetProperty("isPlaying");
            if (isPlaying != null)
            {
                isPlaying.SetValue(null, false);
                return;
            }
#endif
            Application.Quit();
        }
    }
}
