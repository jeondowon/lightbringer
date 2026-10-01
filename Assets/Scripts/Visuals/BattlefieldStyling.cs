using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lightbringer.Visuals
{
    // Applies the Art Pass lighting mood to a generated battlefield: warm sun with soft shadows,
    // trilight ambient, distance haze that hides the ground edge, and optional post-processing.
    public static class BattlefieldStyling
    {
        public static void Apply(ArtStyleLibrary style, Light sun, Renderer ground, Camera camera, Transform root)
        {
            if (style == null) return;
            if (ground != null && style.groundMaterial != null)
            {
                ground.SetPropertyBlock(null);
                ground.sharedMaterial = style.groundMaterial;
            }
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(style.sunEuler);
                sun.color = style.sunColor;
                sun.intensity = style.sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
            }
            // Drifting cloud shadows shared by the terrain, grass and toon shaders (strength 0 = off).
            Shader.SetGlobalVector("_LB_CloudParams", new Vector4(style.cloudShadowStrength, style.cloudScale, style.cloudDrift.x, style.cloudDrift.y));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = style.ambientSky;
            RenderSettings.ambientEquatorColor = style.ambientEquator;
            RenderSettings.ambientGroundColor = style.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = style.fogColor;
            RenderSettings.fogStartDistance = style.fogStart;
            RenderSettings.fogEndDistance = Mathf.Max(style.fogStart + 1f, style.fogEnd);
            if (camera != null)
            {
                // The gradient sky (set by the environment) meets the fog colour at the horizon.
                camera.clearFlags = style.skyMaterial != null && RenderSettings.skybox != null
                    ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.backgroundColor = style.fogColor;
            }
            if (style.postProcessing != null && root != null)
            {
                GameObject item = new GameObject("Art Style Post Processing");
                item.transform.SetParent(root, false);
                Volume volume = item.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = style.postProcessing;
                if (camera != null) camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }
    }
}
