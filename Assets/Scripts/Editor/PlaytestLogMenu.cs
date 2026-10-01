using System.Collections.Generic;
using System.IO;
using System.Text;
using Lightbringer.Core;
using Lightbringer.Units;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Turns the per-battle playtest log into a per-stage Markdown report under Docs/Playtest.
    public static class PlaytestLogMenu
    {
        [MenuItem("Lightbringer/Playtest/Summarize Playtest Log")]
        private static void Summarize()
        {
            string report = WriteSummary(PlaytestRecorder.DefaultLogPath, out int count);
            if (report == null)
            {
                EditorUtility.DisplayDialog("Playtest Log", "No playtest records yet.\nPlay the campaign and return to preparation after a battle.", "OK");
                return;
            }
            Debug.Log($"Playtest summary written from {count} battle(s): {report}");
            EditorUtility.RevealInFinder(report);
        }

        [MenuItem("Lightbringer/Playtest/Open Playtest Log Folder")]
        private static void OpenFolder()
        {
            string folder = Path.GetDirectoryName(PlaytestRecorder.DefaultLogPath);
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static List<PlaytestRecord> Load(string path)
        {
            List<PlaytestRecord> records = new List<PlaytestRecord>();
            if (!File.Exists(path)) return records;
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    PlaytestRecord record = JsonUtility.FromJson<PlaytestRecord>(line);
                    if (record != null && record.stage > 0) records.Add(record);
                }
                catch (System.ArgumentException) { Debug.LogWarning("Skipped an unreadable playtest log line."); }
            }
            return records;
        }

        // Returns the report path, or null when there is nothing to summarize.
        public static string WriteSummary(string logPath, out int count)
        {
            List<PlaytestRecord> records = Load(logPath);
            count = records.Count;
            if (count == 0) return null;
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Playtest");
            Directory.CreateDirectory(folder);
            string report = Path.Combine(folder, "PlaytestSummary.md");
            File.WriteAllText(report, BuildReport(records), new UTF8Encoding(false));
            return report;
        }

        public static string BuildReport(List<PlaytestRecord> records)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Lightbringer Playtest Summary");
            text.AppendLine();
            text.AppendLine($"Generated {System.DateTime.Now:yyyy-MM-dd HH:mm} from {records.Count} battle(s). " +
                "Abandoned battles are listed but excluded from win rate.");
            text.AppendLine();
            text.AppendLine("## By stage");
            text.AppendLine();
            text.AppendLine("| Stage | Runs | Win / Loss / Quit | Avg min | Fun (avg of rated) | Food wasted | Summoned | Lost | Allies in aura | Lowest HP | Base left on loss |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            List<string> flags = new List<string>();
            for (int stage = 1; stage <= 8; stage++)
            {
                List<PlaytestRecord> runs = records.FindAll(r => r.stage == stage);
                if (runs.Count == 0) continue;
                int wins = 0, losses = 0, quits = 0, rated = 0;
                float minutes = 0, fun = 0, produced = 0, wasted = 0, summoned = 0, lost = 0, aura = 0, lowest = 0, baseLeft = 0;
                foreach (PlaytestRecord run in runs)
                {
                    if (run.result == "victory") wins++;
                    else if (run.result == "defeat") { losses++; baseLeft += run.enemyBaseHealthPercent; }
                    else quits++;
                    if (run.funRating > 0) { rated++; fun += run.funRating; }
                    minutes += run.durationSeconds / 60f;
                    produced += run.foodProduced; wasted += run.foodWasted;
                    summoned += run.alliesSummoned; lost += run.alliesLost;
                    aura += run.averageAlliesInAura; lowest += run.lowestHeroHealthPercent;
                }
                int n = runs.Count;
                float wastedPercent = produced > 0 ? 100f * wasted / produced : 0f;
                text.AppendLine($"| {stage} | {n} | {wins} / {losses} / {quits} | {minutes / n:0.0} | " +
                    $"{(rated > 0 ? (fun / rated).ToString("0.0") : "-")} | {wastedPercent:0}% | {summoned / n:0.0} | {lost / n:0.0} | " +
                    $"{aura / n:0.0} | {lowest / n:0}% | {(losses > 0 ? (baseLeft / losses).ToString("0") + "%" : "-")} |");
                // Heuristic hints only; the player's own notes decide the balance change.
                if (wastedPercent > 25f) flags.Add($"Stage {stage}: {wastedPercent:0}% of Food was lost at the cap — Food may outpace useful summons, or the cap is too low.");
                if (wins > 0 && losses == 0 && lowest / n > 70f) flags.Add($"Stage {stage}: never lost and hero stayed above {lowest / n:0}% HP — may be too easy.");
                if (losses + wins > 0 && losses > wins) flags.Add($"Stage {stage}: more defeats than victories — check difficulty spike.");
                if (minutes / n > 8f) flags.Add($"Stage {stage}: average {minutes / n:0.0} min — may drag.");
                if (aura / n < 1f) flags.Add($"Stage {stage}: fewer than 1 ally in the aura on average — aura may not matter enough yet.");
            }

            text.AppendLine();
            text.AppendLine("## Troop usage (all battles)");
            text.AppendLine();
            int[] usage = new int[UnitCatalog.Count];
            int total = 0;
            foreach (PlaytestRecord run in records)
                for (int i = 0; i < usage.Length && run.summonsByUnit != null && i < run.summonsByUnit.Length; i++)
                { usage[i] += run.summonsByUnit[i]; total += run.summonsByUnit[i]; }
            text.AppendLine("| Troop | Cost | Summons | Share |");
            text.AppendLine("|---|---|---|---|");
            for (int i = 0; i < usage.Length; i++)
                text.AppendLine($"| {UnitCatalog.Names[i]} | {UnitCatalog.Cost((UnitKind)i):0} | {usage[i]} | {(total > 0 ? 100f * usage[i] / total : 0):0}% |");

            text.AppendLine();
            text.AppendLine("## Multi-front battles (stage 3+)");
            text.AppendLine();
            text.AppendLine("Share of summons and hero time per Path. A front with no hero time is one the player never supported in person.");
            text.AppendLine();
            text.AppendLine("| Stage | Summons P1 / P2 / P3 | Hero time P1 / P2 / P3 |");
            text.AppendLine("|---|---|---|");
            foreach (PlaytestRecord run in records)
            {
                if (run.stage < 3 || run.summonsByPath == null || run.heroSecondsNearPath == null) continue;
                text.AppendLine($"| {run.stage} ({run.result}) | {string.Join(" / ", run.summonsByPath)} | " +
                    $"{run.heroSecondsNearPath[0]:0}s / {run.heroSecondsNearPath[1]:0}s / {run.heroSecondsNearPath[2]:0}s |");
            }

            text.AppendLine();
            text.AppendLine("## Automatic hints");
            text.AppendLine();
            if (flags.Count == 0) text.AppendLine("- No thresholds tripped.");
            foreach (string flag in flags) text.AppendLine("- " + flag);

            text.AppendLine();
            text.AppendLine("## Player notes");
            text.AppendLine();
            bool any = false;
            foreach (PlaytestRecord run in records)
            {
                if (string.IsNullOrWhiteSpace(run.note)) continue;
                any = true;
                string rating = run.funRating > 0 ? $"fun {run.funRating}/5" : "unrated";
                text.AppendLine($"- Stage {run.stage}, {run.result}, {rating}: {run.note.Replace("\n", " ").Trim()}");
            }
            if (!any) text.AppendLine("- No notes yet.");
            return text.ToString();
        }
    }
}
