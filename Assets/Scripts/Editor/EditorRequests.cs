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
        private static readonly List<(string path, Action job, bool duringPlay)> Jobs = new List<(string, Action, bool)>();
        private static double nextProbe;

        static EditorRequests() => EditorApplication.update += Probe;

        // Relative paths resolve against the project folder. Jobs that only write files or import assets may
        // opt in to running during Play mode; scene-editing jobs must not.
        public static void Register(string path, Action job, bool duringPlay = false) => Jobs.Add((path, job, duringPlay));

        private static void Probe()
        {
            if (EditorApplication.timeSinceStartup < nextProbe || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextProbe = EditorApplication.timeSinceStartup + 1d;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            foreach ((string path, Action job, bool duringPlay) in Jobs)
            {
                if (playing && !duringPlay || !File.Exists(path)) continue;
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
