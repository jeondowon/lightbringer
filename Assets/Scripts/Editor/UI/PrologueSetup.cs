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
            public Entry(string id, string file, params string[] captions) { Id = id; File = file; Captions = captions; }
        }
        public static readonly Entry[] Entries = {
            new Entry("00A", "Prologue_00A_Creation_Storybook_v1", "태초에, 빛이 있었다.", "빛은 세상을 빚고, 그 안에 생명을 불어넣었다."),
            new Entry("00B", "Prologue_00B_Peace_Storybook_v3", "생명은 빛의 온기 속에서 자라났고,\n세상은 오랜 평화를 누렸다."),
            new Entry("00C", "Prologue_00C_BirthOfDarkness_Storybook_v1", "그러나 평화가 길어질수록, 사람들은 빛의 온기를 잊어 갔다.\n이웃에게 문을 닫았고, 서로를 탓하며 미워하기 시작했다.", "빛을 등진 그들의 마음은 어둠이 되었다."),
            new Entry("00D", "Prologue_00D_DemonKingBirth_Storybook_v1", "짙어진 어둠은 이윽고 스스로의 의지를 품었고,\n그 의지가 형체를 이루어 마왕이 되었다."),
            new Entry("00E", "Prologue_00E_UndeadRising_Storybook_v1", "힘을 키운 마왕은 자신의 군대를 만들었고,\n어둠의 군대는 세상을 뒤덮었다.\n쓰러진 이들마저 죽음의 군세가 되어 일어났다."),
            new Entry("01", "Prologue_01_Invasion_Storybook_v4", "도시는 불길에 휩싸였고,\n세상은 혼란과 공포로 물들었다."),
            new Entry("02", "Prologue_02_KnightOfLight_ColorManga_v3", "모든 희망이 스러져 가던 그때,\n빛의 검을 든 한 용사가 나타났다."),
            new Entry("02B", "Prologue_02B_KnightVictory_Storybook_v3", "용사는 어둠의 군세를 물리치며\n빼앗긴 땅을 하나씩 되찾아 나갔다."),
            new Entry("03", "Prologue_03_Beacons_Storybook_v3", "그는 자신이 되찾은 땅 위에 봉화를 세워,\n어둠이 다시 스며들지 못하도록 했다."),
            new Entry("03(V5)", "Prologue_03_Beacons_Storybook_v5", "봉화의 불빛 아래로 사람들이 돌아왔고,\n빛의 수호 아래 사람들은 다시 삶을 이어나갈 수 있었다."),
            new Entry("03B", "Prologue_03B_BeaconWar_Storybook_v1", "용사는 봉화를 하나씩 세워가며,\n세상을 다시 밝히기 위한 긴 투쟁을 이어갔다."),
            new Entry("04A", "Prologue_04A_ThroneHall_Storybook_v1", "오랜 전쟁 끝에, 용사는 마침내 마왕의 성에 이르렀다."),
            new Entry("04", "Prologue_04_LastBattle_Storybook_v2", "빛의 용사와 어둠의 마왕이 칼날을 맞부딪힌 순간,\n성을 집어삼킬 만큼 거대한 빛이 터져 나왔다."),
            new Entry("04B", "Prologue_04B_EmptyCastle_Storybook_v1", "이윽고 빛이 걷혔을 때, 용사와 마왕은 더 이상 보이지 않았다."),
            new Entry("05", "Prologue_05_ForgottenPromise_Storybook_v1", "마왕이 사라지고 평화가 돌아오자,\n사람들은 사라진 용사를 기리며 봉화를 지켰다.", "그러나 세월이 흐르며 용사는 잊혀졌고,\n인간들은 어둠에 대한 경계심마저 잃어 갔다."),
            new Entry("05A", "Prologue_05A_Greed_Storybook_v1", "인간의 탐욕은 이웃의 것을 탐하게 했고,\n그들의 이기심은 서로의 고통을 외면했다."),
            new Entry("05B", "Prologue_05B_Hatred_Storybook_v1", "증오와 욕망이 인간들을 잠식하며,\n사라진 줄 알았던 어둠은 다시 힘을 길렀다."),
            new Entry("06", "Prologue_06_DarknessReturns_Storybook_v1", "인간의 죄와 용사의 부재로 인해 봉화의 불은 꺼졌고,\n어둠의 군세는 다시 눈을 뜨게 되었다."),
            new Entry("07", "Prologue_07_LightStrikes_Storybook_v2", "바로 그때, 땅 위로 빛이 쏟아져 어둠의 군세를 물리쳤다."),
            new Entry("08", "Prologue_08_FirstStep_Storybook_v2", "그 빛 속에서, 누군가가 발걸음을 내디뎠다."),
            new Entry("07(ForgottenBeacon)", "Prologue_07_ForgottenBeacon_Storybook_v1", "모두가 떠난 자리,\n그녀는 꺼져 버린 봉화의 빛을 되살렸다."),
            new Entry("09", "Prologue_09_Journey_Storybook_v2", "그리고 그 너머에는,\n아직 밝혀야 할 수많은 봉화가 기다리고 있었다."),
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

        // The slides in order; every slide's artwork must exist under Docs/ArtReference/Prologue.
        public static Entry[] Resolve()
        {
            foreach (Entry entry in Entries)
                if (!File.Exists("Docs/ArtReference/Prologue/" + entry.File + ".png"))
                    throw new FileNotFoundException("Missing prologue artwork: " + entry.File);
            return Entries;
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

