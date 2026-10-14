using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Restrict watching to our small script folder. No scene saves or Play-mode changes occur here.
    [InitializeOnLoad]
    public static class GreyboxScriptAutoRefresh
    {
        private const string MenuPath = "Lightbringer/Auto Refresh Greybox Scripts";
        private const string EnabledKey = "Lightbringer.AutoRefreshScripts";
        private static string previousStamp;
        private static string pendingStamp;
        private static double nextProbe;

        static GreyboxScriptAutoRefresh()
        {
            previousStamp = GetStamp();
            EditorApplication.update += Probe;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            SessionState.SetBool(EnabledKey, !SessionState.GetBool(EnabledKey, true));
        }

        [MenuItem(MenuPath, true)]
        private static bool ShowToggle()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(EnabledKey, true));
            return true;
        }

        private static void Probe()
        {
            if (!SessionState.GetBool(EnabledKey, true) || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.timeSinceStartup < nextProbe)
                return;
            nextProbe = EditorApplication.timeSinceStartup + 2d;
            string stamp = GetStamp();
            if (stamp == previousStamp)
            {
                pendingStamp = null;
                return;
            }
            // Require two unchanged probes so a batch of script edits can finish before importing.
            if (pendingStamp != stamp)
            {
                pendingStamp = stamp;
                return;
            }
            previousStamp = stamp;
            pendingStamp = null;
            AssetDatabase.Refresh();
        }

        internal static string GetStamp()
        {
            string folder = Path.Combine(Application.dataPath, "Scripts");
            if (!Directory.Exists(folder))
                return string.Empty;
            try
            {
                return string.Join("|", Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path).Select(path => path + ":" + File.GetLastWriteTimeUtc(path).Ticks));
            }
            catch (IOException)
            {
                return previousStamp ?? string.Empty; // Retry if a file is being replaced.
            }
        }
    }
}
