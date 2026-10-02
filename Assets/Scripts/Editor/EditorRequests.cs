using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Editor jobs can be triggered from outside Unity by creating a request file; the file is removed and
    // the job runs once the Editor is idle. One throttled poller serves every registered request.
    [InitializeOnLoad]
    internal static class EditorRequests
    {
        private static readonly List<(string path, Action job)> Jobs = new List<(string, Action)>();
        private static double nextProbe;

        static EditorRequests() => EditorApplication.update += Probe;

        // Relative paths resolve against the project folder.
        public static void Register(string path, Action job) => Jobs.Add((path, job));

        private static void Probe()
        {
            if (EditorApplication.timeSinceStartup < nextProbe || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextProbe = EditorApplication.timeSinceStartup + 1d;
            foreach ((string path, Action job) in Jobs)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    File.Delete(path);
                    job();
                }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
    }
}
