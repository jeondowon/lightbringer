using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Core;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Explicit editorial order: never infer it by sorting filenames (03 and 07 each occur twice).
    [InitializeOnLoad]
    public static class PrologueSetup
    {
        public const string AssetPath = "Assets/UI/Resources/Prologue/Sequence.asset";
        public const string ImageFolder = "Assets/UI/Prologue";
        // What Build does when a slide's artwork has not been delivered yet.
        public enum MissingArt { Required, MergeIntoNext, MergeIntoPrevious }

        public sealed class Entry
        {
            public readonly string Id, File, OlderFile;
            public readonly string[] Captions;
            public readonly MissingArt Missing;
            public Entry(string id, string file, params string[] captions)
                : this(id, file, null, MissingArt.Required, captions) { }
            private Entry(string id, string file, string olderFile, MissingArt missing, string[] captions)
            { Id = id; File = file; OlderFile = olderFile; Missing = missing; Captions = captions; }

            // A newer revision used once its file exists; until then the older artwork stands in.
            public static Entry Revised(string id, string file, string olderFile, params string[] captions) =>
                new Entry(id, file, olderFile, MissingArt.Required, captions);

            // A planned page: until its artwork exists, its captions join a neighbouring slide so the story stays whole.
            public static Entry Planned(string id, string file, MissingArt missing, params string[] captions) =>
                new Entry(id, file, null, missing, captions);
        }
        public static readonly Entry[] Entries = {
            new Entry("00A", "Prologue_00A_Creation_Storybook_v1", "태초에, 빛이 있었다.", "빛은 세상을 빚고, 그 안에 생명을 불어넣었다."),
            Entry.Revised("00B", "Prologue_00B_Peace_Storybook_v3", "Prologue_00B_Peace_Storybook_v2", "생명은 빛의 온기 속에서 자라났고,\n세상은 오랜 평화를 누렸다."),
            new Entry("00C", "Prologue_00C_BirthOfDarkness_Storybook_v1", "그러나 평화가 길어질수록, 사람들은 빛의 온기를 잊어 갔다.\n이웃에게 문을 닫았고, 서로를 탓하며 미워하기 시작했다.", "빛을 등진 마음들은 하나둘 모여, 마침내 어둠이 되었다."),
            new Entry("00D", "Prologue_00D_DemonKingBirth_Storybook_v1", "짙어진 어둠은 이윽고 스스로의 의지를 품었고,\n그 의지가 형체를 이루어 마왕이 되었다."),
            Entry.Planned("00E", "Prologue_00E_UndeadRising_Storybook_v1", MissingArt.MergeIntoNext, "마왕이 이끄는 어둠은 세상을 덮쳤다.\n쓰러진 이들마저 죽음의 군세가 되어 일어났다."),
            new Entry("01", "Prologue_01_Invasion_Storybook_v4", "도시는 불길에 휩싸였고,\n사람들은 정든 고향을 버리고 달아나야만 했다."),
            new Entry("02", "Prologue_02_KnightOfLight_ColorManga_v3", "모든 희망이 스러져 가던 그때,\n빛의 검을 든 한 용사가 나타났다."),
            new Entry("02B", "Prologue_02B_KnightVictory_Storybook_v3", "용사는 어둠의 군세를 물리치며\n빼앗긴 땅을 하나씩 되찾아 나갔다."),
            new Entry("03", "Prologue_03_Beacons_Storybook_v3", "그는 되찾은 땅마다 봉화를 세우고,\n어둠이 다시 스며들지 못하도록 제 빛을 나누어 두었다."),
            new Entry("03(V5)", "Prologue_03_Beacons_Storybook_v5", "봉화의 불빛 아래로 사람들이 돌아왔고,\n폐허가 된 마을에도 삶이 이어졌다."),
            Entry.Planned("03B", "Prologue_03B_BeaconWar_Storybook_v1", MissingArt.MergeIntoPrevious, "용사는 봉화를 하나씩 세워가며,\n세상을 다시 밝히기 위한 긴 전쟁을 계속했다."),
            Entry.Planned("04A", "Prologue_04A_ThroneHall_Storybook_v1", MissingArt.MergeIntoNext, "오랜 전쟁 끝에, 용사는 마침내 마왕의 성에 이르렀다."),
            new Entry("04", "Prologue_04_LastBattle_Storybook_v2", "빛의 용사와 어둠의 마왕이 칼날을 맞부딪힌 순간,\n성을 집어삼킬 만큼 거대한 빛이 터져 나왔다."),
            Entry.Planned("04B", "Prologue_04B_EmptyCastle_Storybook_v1", MissingArt.MergeIntoPrevious, "이윽고 빛이 걷혔을 때, 용사도 마왕도 흔적 없이 사라져 있었다.\n그 뒤로 두 사람의 모습을 본 이는 아무도 없었다."),
            new Entry("05", "Prologue_05_ForgottenPromise_Storybook_v1", "평화가 돌아오자, 사람들은 용사를 기리며 봉화를 지켰다.", "그러나 세월이 흐르며 그 약속은 차츰 희미해졌고,\n사람들은 어둠이 영영 사라졌다고 믿게 되었다."),
            new Entry("05A", "Prologue_05A_Greed_Storybook_v1", "그사이 탐욕은 이웃의 것을 탐하게 했고,\n사람들은 서로의 고통을 외면했다."),
            new Entry("05B", "Prologue_05B_Hatred_Storybook_v1", "증오가 마음을 채워 가는 동안,\n사라진 줄 알았던 어둠은 다시 힘을 길렀다."),
            new Entry("06", "Prologue_06_DarknessReturns_Storybook_v1", "그리고 마침내 봉화가 꺼진 밤, 죽은 자들이 눈을 떴다.", "불빛이 사라진 길을 따라 어둠이 번져 갔다.\n이번에는, 빛의 용사도 나타나지 않았다."),
            new Entry("07", "Prologue_07_LightStrikes_Storybook_v2", "바로 그때, 어둠에 잠긴 땅 위로 한 줄기 빛이 쏟아졌다."),
            new Entry("08", "Prologue_08_FirstStep_Storybook_v1", "그 빛 속에서, 한 사람이 쓰러진 어둠을 딛고 첫걸음을 내디뎠다."),
            new Entry("07(ForgottenBeacon)", "Prologue_07_ForgottenBeacon_Storybook_v1", "모두가 떠난 자리,\n그 사람은 꺼져 버린 봉화 앞에서 걸음을 멈추었다."),
            new Entry("09", "Prologue_09_Journey_Storybook_v1", "그리고 그 너머에는,\n아직 밝혀야 할 수많은 봉화가 기다리고 있었다."),
        };

        static PrologueSetup()
        {
            EditorRequests.Register("Docs/Validation/BuildPrologue.request", BuildAndValidate);
        }

        [MenuItem("Lightbringer/Prologue/Build and Validate")]
        public static void BuildAndValidate()
        {
            try
            {
                Build();
                PrologueValidation.Run();
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Docs/Validation");
                File.WriteAllText("Docs/Validation/PrologueChecks.txt", "FAIL | " + DateTime.UtcNow.ToString("O") + "\n" + error);
                throw;
            }
        }

        private static bool SourceExists(string file) =>
            file != null && File.Exists("Docs/ArtReference/Prologue/" + file + ".png");

        // The slides actually built from the artwork on disk, in order. Planned pages without artwork
        // hand their captions to a neighbour; required artwork that is missing is an error.
        public static Entry[] Resolve()
        {
            var resolved = new List<Entry>();
            var carried = new List<string>();
            foreach (Entry entry in Entries)
            {
                string file = SourceExists(entry.File) ? entry.File : SourceExists(entry.OlderFile) ? entry.OlderFile : null;
                if (file == null)
                {
                    if (entry.Missing == MissingArt.Required)
                        throw new FileNotFoundException("Missing prologue artwork: " + entry.File);
                    if (entry.Missing == MissingArt.MergeIntoNext) carried.AddRange(entry.Captions);
                    else
                    {
                        if (resolved.Count == 0) throw new InvalidOperationException("No slide before planned page " + entry.Id);
                        Entry previous = resolved[resolved.Count - 1];
                        resolved[resolved.Count - 1] = new Entry(previous.Id, previous.File, previous.Captions.Concat(entry.Captions).ToArray());
                    }
                    continue;
                }
                resolved.Add(new Entry(entry.Id, file, carried.Concat(entry.Captions).ToArray()));
                carried.Clear();
            }
            if (carried.Count > 0) throw new InvalidOperationException("Planned prologue captions have no following slide.");
            return resolved.ToArray();
        }

        public static PrologueSequence Build()
        {
            // Check all source files before creating or modifying any Unity asset.
            Entry[] slides = Resolve();
            AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(BattleMusicImport.ProloguePath);
            if (music == null) throw new InvalidOperationException("Missing prologue music.");
            Directory.CreateDirectory(ImageFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            foreach (Entry entry in slides)
                File.Copy("Docs/ArtReference/Prologue/" + entry.File + ".png", ImageFolder + "/" + entry.File + ".png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PrologueSequence sequence = AssetDatabase.LoadAssetAtPath<PrologueSequence>(AssetPath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<PrologueSequence>();
                AssetDatabase.CreateAsset(sequence, AssetPath);
            }
            sequence.slides = new PrologueSlide[slides.Length];
            for (int i = 0; i < slides.Length; i++)
            {
                Entry entry = slides[i];
                string path = ImageFolder + "/" + entry.File + ".png";
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                sequence.slides[i] = new PrologueSlide {
                    id = entry.Id, image = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    captions = (string[])entry.Captions.Clone()
                };
            }
            sequence.music = music;
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
            return sequence;
        }
    }
}

