using Lightbringer.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.EditorTools
{
    internal static class EditorAssets
    {
        public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        public static InputActionAsset InputActions => AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

        // Project input plus URP shaders, as used by validation, diagnostics and preview sessions.
        public static void ConfigureSession(CampaignSession session, Material material)
            => session.Configure(InputActions, material, Shader.Find("Universal Render Pipeline/Unlit"));
    }
}
