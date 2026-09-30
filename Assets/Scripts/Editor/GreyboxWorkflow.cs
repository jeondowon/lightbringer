using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    public static class GreyboxWorkflow
    {
        private const string MenuPath = "Lightbringer/Prepare and Play Greybox";

        [MenuItem(MenuPath, true)]
        private static bool CanPrepare()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating && PrefabStageUtility.GetCurrentPrefabStage() == null;
        }

        [MenuItem(MenuPath)]
        private static void PrepareAndPlay()
        {
            if (!CanPrepare() || !GreyboxValidation.RunChecks())
                return;
            if (!GreyboxPlayerSetup.TrySetup())
                return;
            Scene scene = SceneManager.GetActiveScene();
            // An existing scene saves directly; a new untitled scene uses Unity's Save dialog.
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogWarning("Greybox preparation stopped because the scene was not saved.");
                return;
            }
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }
    }
}
