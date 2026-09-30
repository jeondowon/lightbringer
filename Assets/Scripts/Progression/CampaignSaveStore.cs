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
                Error = "Recovered campaign from backup.";
                return progress;
            }
            CanSave = false;
            Error = "Save could not be read. Original files are preserved; this session will not overwrite them.";
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
                string[] required = { "version", "unlockedStage", "cleared", "gold", "level", "experience",
                    "pendingLevels", "choices", "ranks", "equipmentLevels", "loadout" };
                foreach (string field in required) if (!json.Contains("\"" + field + "\"")) return false;
                progress = JsonUtility.FromJson<CampaignProgress>(json);
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
                Error = "Campaign save failed: " + exception.Message;
                return false;
            }
        }
    }
}
