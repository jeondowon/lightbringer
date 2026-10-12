using System;
using System.IO;
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
        public sealed class Entry
        {
            public readonly string Id, File;
            public readonly string[] Captions;
            public Entry(string id, string file, params string[] captions)
            { Id = id; File = file; Captions = captions; }
        }
        public static readonly Entry[] Entries = {
            new Entry("00A", "Prologue_00A_Creation_Storybook_v1", "태초에, 빛이 있었다.", "빛은 세상을 빚고, 그 안에 생명을 불어넣었다."),
            new Entry("00B", "Prologue_00B_Peace_Storybook_v2", "생명은 빛의 온기 속에서 자라났고,\n세상은 오랜 평화를 누렸다."),
            new Entry("00C", "Prologue_00C_BirthOfDarkness_Storybook_v1", "그러나 평화가 길어질수록\n빛을 독차지하려는 욕망과 서로를 향한 증오가 싹텄다.", "빛을 등진 마음들은 하나둘 모여, 마침내 어둠이 되었다."),
            new Entry("00D", "Prologue_00D_DemonKingBirth_Storybook_v1", "어둠은 자라 의지를 품었고,\n그 끝에서 마왕이 태어났다."),
            new Entry("01", "Prologue_01_Invasion_Storybook_v4", "마왕이 이끄는 어둠은 세상을 덮쳤다.\n쓰러진 이들마저 죽음의 군세가 되어 일어났다.", "도시는 불타올랐고, 살아남은 이들은 고향을 등져야 했다."),
            new Entry("02", "Prologue_02_KnightOfLight_ColorManga_v3", "모든 희망이 스러져 가던 그때,\n빛의 검을 든 한 기사가 나타났다."),
            new Entry("02B", "Prologue_02B_KnightVictory_Storybook_v3", "기사는 어둠의 군세를 물리치며\n빼앗긴 땅을 하나씩 되찾아 나갔다."),
            new Entry("03", "Prologue_03_Beacons_Storybook_v3", "그리고 되찾은 땅마다 봉화를 세우고,\n어둠이 다시 스며들지 못하도록 제 빛을 나누어 두었다."),
            new Entry("03(V5)", "Prologue_03_Beacons_Storybook_v5", "봉화의 불빛 아래로 사람들이 돌아왔고,\n폐허가 된 마을에도 삶이 이어졌다.", "기사는 그들을 뒤로한 채, 끝나지 않은 전쟁터로 걸음을 옮겼다."),
            new Entry("04", "Prologue_04_LastBattle_Storybook_v2", "오랜 전쟁 끝에, 기사는 마침내 마왕의 성에 이르렀다.", "성을 집어삼킨 눈부신 빛이 걷혔을 때,\n기사도 마왕도 흔적 없이 사라지고 없었다."),
            new Entry("05", "Prologue_05_ForgottenPromise_Storybook_v1", "평화가 돌아오자, 사람들은 기사를 기리며 봉화를 지켰다.", "그러나 세월이 흐르며 그 약속은 차츰 희미해졌고,\n사람들은 어둠이 영영 사라졌다고 믿게 되었다."),
            new Entry("05A", "Prologue_05A_Greed_Storybook_v1", "그사이 탐욕은 이웃의 것을 탐하게 했고,\n사람들은 서로의 고통을 외면했다."),
            new Entry("05B", "Prologue_05B_Hatred_Storybook_v1", "증오가 마음을 채워 가는 동안,\n사라진 줄 알았던 어둠은 다시 힘을 길렀다."),
            new Entry("06", "Prologue_06_DarknessReturns_Storybook_v1", "그리고 마침내 봉화가 꺼진 밤, 죽은 자들이 눈을 떴다.", "불빛이 사라진 길을 따라 어둠이 번져 갔다.\n이번에는, 빛의 기사도 나타나지 않았다."),
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

        public static PrologueSequence Build()
        {
            // Check all source files before creating or modifying any Unity asset.
            foreach (Entry entry in Entries)
                if (!File.Exists("Docs/ArtReference/Prologue/" + entry.File + ".png"))
                    throw new FileNotFoundException("Missing prologue artwork: " + entry.File);
            AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(BattleMusicImport.ProloguePath);
            if (music == null) throw new InvalidOperationException("Missing prologue music.");
            Directory.CreateDirectory(ImageFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            foreach (Entry entry in Entries)
                File.Copy("Docs/ArtReference/Prologue/" + entry.File + ".png", ImageFolder + "/" + entry.File + ".png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PrologueSequence sequence = AssetDatabase.LoadAssetAtPath<PrologueSequence>(AssetPath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<PrologueSequence>();
                AssetDatabase.CreateAsset(sequence, AssetPath);
            }
            sequence.slides = new PrologueSlide[Entries.Length];
            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];
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

