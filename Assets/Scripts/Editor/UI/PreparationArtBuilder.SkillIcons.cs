using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Skill icons: painted medallion illustrations (Docs/ArtReference/Skills/Skill_*_v1.png, generated on a white
    // background, prompts in Docs/ArtReference/Prompts.md) keyed to transparency into Assets/UI/Art/Skill_*.png.
    // Request file: Docs/Validation/BuildSkillIcons.request.
    public static partial class PreparationArtBuilder
    {
        private const string SkillRequest = "Docs/Validation/BuildSkillIcons.request";
        private const string SkillSources = "Docs/ArtReference/Skills/";
        private const int SkillIconHeight = 512;
        private static readonly string[] SkillIconNames = { "LightBeam", "Heal", "Ice", "Shockwave", "Meteor" };

        [MenuItem("Lightbringer/UI/Build Skill Icons")]
        public static void BuildSkillIcons()
        {
            Directory.CreateDirectory(Full(Folder));
            Written.Clear();
            foreach (string name in SkillIconNames)
                Illustration($"{SkillSources}Skill_{name}_v1.png", "Skill_" + name, SkillIconHeight, false, false, true);
            ImportWritten();
            Debug.Log($"Skill icons built: {Written.Count} of {SkillIconNames.Length} in {Folder}");
        }
    }
}
