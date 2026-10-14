using UnityEngine;

namespace Lightbringer.Visuals
{
    [CreateAssetMenu(menuName = "Lightbringer/Battlefield Backdrop", fileName = "BattlefieldBackdrop")]
    public sealed class BattlefieldBackdropProfile : ScriptableObject
    {
        [Min(1)] public int firstStage = 1;
        [Min(1)] public int lastStage = 8;
        [Tooltip("360 degree equirectangular image (2:1). Replace this to change the distant scenery.")]
        public Texture2D panorama;
        [Range(0f, 360f)] public float rotation = 18f;
        [Range(0.2f, 2f)] public float exposure = 1f;
        [Range(-0.2f, 0.2f)] public float horizonOffset;
        [Range(0f, 0.1f)] public float cloudMotion = 0.012f;
        [Header("Blend with the 3D battlefield")]
        public Color fogColor = new Color(0.46f, 0.52f, 0.57f);
        [Min(0f)] public float fogStart = 65f;
        [Min(1f)] public float fogEnd = 205f;
        [Tooltip("Optional nearby forest for this stage range. Its own stage range is ignored here.")]
        public ForestEnvironmentProfile forest;

        public bool AppliesTo(int stage) => panorama != null && stage >= firstStage && stage <= lastStage;

        public void ApplyTo(Material sky)
        {
            sky.SetTexture("_Panorama", panorama);
            sky.SetFloat("_PanoramaEnabled", 1f);
            sky.SetFloat("_PanoramaRotation", rotation);
            sky.SetFloat("_PanoramaExposure", exposure);
            sky.SetFloat("_HorizonOffset", horizonOffset);
            sky.SetFloat("_CloudMotion", cloudMotion);
            sky.SetColor("_HorizonColor", fogColor);
        }
    }
}
