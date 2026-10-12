using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Lightbringer.Progression
{
    public sealed class CampaignSaveStore
    {
        private readonly string path;
        public string Error { get; private set; } = "";
        public bool CanSave { get; private set; } = true;
        public CampaignSaveStore(string filePath) => path = filePath;

        public CampaignProgress Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new CampaignProgress();
            if (TryRead(path, out CampaignProgress progress)) return progress;
            if (TryRead(path + ".bak", out progress))
            {
                Error = "백업에서 캠페인 데이터를 복구했습니다.";
                return progress;
            }
            CanSave = false;
            Error = "저장 데이터를 읽을 수 없습니다. 원본 보호를 위해 이번 진행 내용은 저장하지 않습니다.";
            return new CampaignProgress();
        }

        private static bool TryRead(string file, out CampaignProgress progress)
        {
            progress = null;
            try
            {
                if (!File.Exists(file)) return false;
                string json = File.ReadAllText(file, Encoding.UTF8);
                // JsonUtility supplies field defaults for missing fields; reject incomplete records first.
                string[] required = { "version", "unlockedStage", "cleared", "level", "experience",
                    "pendingLevels", "choices", "ranks", "loadout" };
                foreach (string field in required) if (!json.Contains("\"" + field + "\"")) return false;
                // Version 3 keeps skills in skillLevels; older saves keep gear in equipmentLevels.
                if (!json.Contains("\"skillLevels\"") && !json.Contains("\"equipmentLevels\"")) return false;
                progress = JsonUtility.FromJson<CampaignProgress>(json);
                progress?.UpgradeSavedData();
                return progress != null && progress.IsValid();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { return false; }
        }

        public bool Save(CampaignProgress progress)
        {
            if (!CanSave || progress == null || !progress.IsValid()) return false;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(JsonUtility.ToJson(progress, true));
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    if (!TryRead(path, out _))
                        File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), true);
                    File.Replace(temporary, path, path + ".bak");
                }
                else File.Move(temporary, path);
                Error = "";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                Error = "캠페인 저장 실패: " + exception.Message;
                return false;
            }
        }
    }
}
