using System;
using UnityEngine;

namespace Lightbringer.Core
{
    [CreateAssetMenu(menuName = "Lightbringer/Prologue Sequence")]
    public sealed class PrologueSequence : ScriptableObject
    {
        public const string ResourcePath = "Prologue/Sequence";
        public const string ViewedKey = "Lightbringer.PrologueViewed.v1";
        public const string MusicCredit = "‘Legionnaire’ — Scott Buckley\nCC BY 4.0 · www.scottbuckley.com.au\nhttps://creativecommons.org/licenses/by/4.0/";
        public AudioClip music;
        public PrologueSlide[] slides = Array.Empty<PrologueSlide>();

        public bool IsReady
        {
            get
            {
                if (slides == null || slides.Length == 0) return false;
                foreach (PrologueSlide slide in slides)
                    if (slide == null || slide.image == null || slide.captions == null || slide.captions.Length == 0)
                        return false;
                return true;
            }
        }
    }

    [Serializable]
    public sealed class PrologueSlide
    {
        public string id;
        public Texture2D image;
        [TextArea(2, 4)] public string[] captions;
    }
}
