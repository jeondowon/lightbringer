using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Lightbringer.Core
{
    // Graphics tiers so the game stays smooth on weaker machines (integrated GPUs, base Apple Silicon Macs).
    // Each tier is a Unity quality level (Low / Medium / High) with its own URP asset: MSAA, shadow cascades
    // and resolution, soft-shadow quality and SSAO step down together. On top of that the internal render
    // resolution is capped to a pixel budget, so a Retina or 4K screen does not multiply the GPU cost.
    // Presentation only: no gameplay value depends on the tier.
    //
    // Each session starts from a tier detected from the hardware and steps down automatically if battles
    // run slow. F9 cycles the tier by hand; that pick is remembered per machine (PlayerPrefs) and stops
    // auto adjustment. F10 shows a frame-time readout.
    //
    // Frames the display never shows only heat the machine (loud laptop fans), so the frame rate is capped:
    // 60 by default, the monitor's refresh rate, or unlimited (pause menu, remembered per machine). The cap
    // uses Application.targetFrameRate rather than vSync: no added input latency, and the quality levels,
    // which the Editor shares with Play mode, stay untouched.
    public static class GraphicsQuality
    {
        public enum Tier { Low, Medium, High }
        public enum FrameLimit { Fps60, Display, Unlimited }

        private const string TierKey = "Lightbringer.Graphics.Tier";
        private const string ManualKey = "Lightbringer.Graphics.Manual";
        private const string FrameLimitKey = "Lightbringer.Graphics.FrameLimit";
        // Internal render pixels per tier: 1600x900, 1920x1080, 2560x1440.
        private static readonly float[] PixelBudget = { 1600f * 900f, 1920f * 1080f, 2560f * 1440f };

        public static Tier Current { get; private set; } = Tier.High;
        public static bool IsManual { get; private set; }
        public static FrameLimit Limit { get; private set; } = FrameLimit.Fps60;
        public static float RenderScale { get; private set; } = 1f;
        // Lets the benchmark measure a fixed tier without auto adjustment interfering.
        public static bool AutoAdjust { get; set; } = true;
        public static event Action Changed;

        private static bool initialized;
        private static int appliedLevel = -1;
        private static Vector2Int appliedScreen;
        private static readonly Dictionary<UniversalRenderPipelineAsset, float> originalScales = new Dictionary<UniversalRenderPipelineAsset, float>();
        private static int originalLevel = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject("Graphics Quality");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GraphicsQualityController>();
        }

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            originalLevel = QualitySettings.GetQualityLevel();
            Application.quitting += Restore;
            IsManual = PlayerPrefs.GetInt(ManualKey, 0) == 1 && PlayerPrefs.HasKey(TierKey);
            SetFrameLimit((FrameLimit)Mathf.Clamp(PlayerPrefs.GetInt(FrameLimitKey, 0), 0, 2), false);
            Apply(IsManual ? (Tier)Mathf.Clamp(PlayerPrefs.GetInt(TierKey), 0, 2) : Detect(), false, false);
        }

        // remember = false keeps the choice for this session only (validation and the benchmark run unlimited).
        public static void SetFrameLimit(FrameLimit limit, bool remember = true)
        {
            Initialize();
            Limit = limit;
            if (remember) PlayerPrefs.SetInt(FrameLimitKey, (int)limit);
            int display = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = limit == FrameLimit.Fps60 ? 60 : limit == FrameLimit.Display ? Mathf.Max(30, display) : -1;
            Changed?.Invoke();
        }

        // Rough first guess from the GPU name; auto adjustment corrects it after a few seconds of battle.
        public static Tier Detect()
        {
            string gpu = SystemInfo.graphicsDeviceName ?? string.Empty;
            bool mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
            // Base chips (e.g. "Apple M1") start on Low; Pro on Medium; Max and Ultra on High.
            if (gpu.Contains("Apple"))
                return gpu.Contains("Max") || gpu.Contains("Ultra") ? Tier.High : gpu.Contains("Pro") ? Tier.Medium : Tier.Low;
            if (mac) return Tier.Low; // Intel Macs
            bool integrated = (gpu.Contains("Intel") && !gpu.Contains("Arc"))
                || (gpu.Contains("Radeon") && gpu.Contains("Graphics") && !gpu.Contains("RX"));
            if (integrated || SystemInfo.graphicsMemorySize < 3000) return Tier.Medium;
            return Tier.High;
        }

        public static void Cycle() => Apply((Tier)(((int)Current + 2) % 3), true);

        // Only a manual pick is saved. Detected and auto-lowered tiers last for this session, so a slow
        // moment (on battery, say) never locks the machine into a low tier. remember = false keeps even a
        // manual pick session-only (the benchmark measures every tier).
        public static void Apply(Tier tier, bool manual, bool remember = true)
        {
            Current = tier;
            if (manual) IsManual = true;
            if (manual && remember)
            {
                PlayerPrefs.SetInt(TierKey, (int)tier);
                PlayerPrefs.SetInt(ManualKey, 1);
            }
            int level = Array.IndexOf(QualitySettings.names, tier.ToString());
            // Projects without the tier levels keep their single level; resolution still scales.
            if (level >= 0 && level != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(level, true);
            appliedLevel = QualitySettings.GetQualityLevel();
            UpdateResolution(true);
            Changed?.Invoke();
        }

        // Called every second by the controller: a resized window or a different display needs a new scale.
        public static void UpdateResolution(bool force = false)
        {
            Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && screen == appliedScreen && appliedLevel == QualitySettings.GetQualityLevel()) return;
            appliedScreen = screen;
            appliedLevel = QualitySettings.GetQualityLevel();
            if (!(QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp) || screen.x <= 0 || screen.y <= 0) return;
            if (!originalScales.ContainsKey(urp)) originalScales[urp] = urp.renderScale;
            float pixels = (float)screen.x * screen.y;
            RenderScale = Mathf.Clamp(Mathf.Sqrt(PixelBudget[(int)Current] / pixels), 0.5f, 1f);
            // Snap so tiny window resizes do not reallocate render targets.
            RenderScale = Mathf.Min(1f, Mathf.Round(RenderScale * 20f) / 20f);
            urp.renderScale = RenderScale;
        }

        // The Editor shares the URP assets and quality level with Play mode: put them back when Play stops.
        private static void Restore()
        {
            foreach (KeyValuePair<UniversalRenderPipelineAsset, float> entry in originalScales)
                if (entry.Key != null) entry.Key.renderScale = entry.Value;
            originalScales.Clear();
#if UNITY_EDITOR
            if (originalLevel >= 0 && originalLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(originalLevel, true);
            Application.targetFrameRate = -1;
#endif
            Application.quitting -= Restore;
            initialized = false;
            Changed = null;
        }

        // Steps one tier down after sustained slow battle frames. Never steps up, never overrides a manual pick.
        internal static void ReportSlow()
        {
            if (!AutoAdjust || IsManual || Current == Tier.Low) return;
            Tier lower = Current - 1;
            Debug.Log($"[Graphics] Sustained low frame rate on {Current}; switching to {lower}. Press F9 to choose a tier manually.");
            Apply(lower, false);
        }
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class GraphicsQualityController : MonoBehaviour
    {
        // Frames slower than ~45 fps for most of a 6 s window during live play trigger one step down.
        // A frame cap (battery saver, vsync) reads as slow too; the drop lasts only for this session.
        private const float SlowFrame = 1f / 45f;
        private const float Window = 6f;
        private const float SettleAfterChange = 4f;

        private float windowTime, slowTime, settle = SettleAfterChange, resolutionCheck;
        private bool showStats;
        private float statsTimer, smoothedMs;
        private string statsText = string.Empty;
        private GUIStyle statsStyle;

        private void Awake()
        {
            GraphicsQuality.Initialize();
            GraphicsQuality.Changed += OnChanged;
        }

        private void OnDestroy() => GraphicsQuality.Changed -= OnChanged;

        private void OnChanged() { settle = SettleAfterChange; windowTime = slowTime = 0f; }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f9Key.wasPressedThisFrame) GraphicsQuality.Cycle();
                if (keyboard.f10Key.wasPressedThisFrame) showStats = !showStats;
            }
            resolutionCheck -= delta;
            if (resolutionCheck <= 0f) { resolutionCheck = 1f; GraphicsQuality.UpdateResolution(); }
            smoothedMs = Mathf.Lerp(smoothedMs, delta * 1000f, 0.05f);
            if (showStats && (statsTimer -= delta) <= 0f)
            {
                statsTimer = 0.25f;
                statsText = $"{smoothedMs:0.0} ms  ({1000f / Mathf.Max(0.01f, smoothedMs):0} fps)\n"
                    + $"{GraphicsQuality.Current}{(GraphicsQuality.IsManual ? "" : " (auto)")}  scale {GraphicsQuality.RenderScale:0.00}  limit {GraphicsQuality.Limit}  [F9 tier]";
            }
            Watch(delta);
        }

        // Only judge live play: a focused window, time running, and the battle camera rendering.
        private void Watch(float delta)
        {
            bool live = Application.isFocused && Time.timeScale > 0f && Camera.main != null && delta < 0.25f;
            if (!live || (settle -= delta) > 0f) return;
            windowTime += delta;
            if (delta > SlowFrame) slowTime += delta;
            if (windowTime < Window) return;
            if (slowTime > windowTime * 0.6f) GraphicsQuality.ReportSlow();
            windowTime = slowTime = 0f;
        }

        private void OnGUI()
        {
            if (!showStats) return;
            if (statsStyle == null)
                statsStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Label(new Rect(12f, 8f, 480f, 44f), statsText, statsStyle);
        }
    }
}
