using System;
using System.Collections.Generic;
using System.IO;
using Lightbringer.Combat;
using Lightbringer.Pathing;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.Core
{
    // One battle of objective playtest data plus the player's own fun rating and note.
    [Serializable]
    public sealed class PlaytestRecord
    {
        public string session;
        public string startedAt;
        public int stage;
        public string result;
        public float durationSeconds;
        public int heroLevelStart;
        public int heroLevelEnd;
        public float foodProduced;
        public float foodSpent;
        public float foodWasted;
        public float secondsAtFoodCap;
        public float averageManaPercent;
        public float lowestHeroHealthPercent = 100f;
        public float enemyBaseHealthPercent = 100f;
        public int wavesSpawned;
        public int totalWaves;
        public int enemiesDefeated;
        public int alliesSummoned;
        public int alliesLost;
        public float averageAlliesInAura;
        public float firstSummonSeconds = -1f;
        public int[] summonsByUnit = new int[UnitCatalog.Count];
        public int[] summonsByPath = new int[3];
        public float[] heroSecondsNearPath = new float[3];
        public int funRating;
        public string note = "";
    }

    public sealed class PlaytestRecorder
    {
        private const float SampleInterval = 0.5f;
        private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        private readonly List<Combatant> allies = new List<Combatant>();
        private readonly string logPath;
        private PrototypeBattle battle;
        private CampaignProgress progress;
        private FoodResource food;
        private ManaResource mana;
        private float sampleTimer;
        private int samples;
        private float manaSum;
        private float auraSum;
        private bool finished;
        public PlaytestRecord Current { get; private set; }

        // A null path keeps the recorder in memory only (validation sessions).
        public PlaytestRecorder(string path) => logPath = path;

        public static string DefaultLogPath =>
            Path.Combine(Application.persistentDataPath, "Lightbringer", "playtest", "playtest-log.jsonl");

        public void Begin(PrototypeBattle target, int stage, CampaignProgress profile)
        {
            Discard();
            battle = target; progress = profile; finished = false;
            sampleTimer = 0f; samples = 0; manaSum = 0f; auraSum = 0f;
            allies.Clear();
            Current = new PlaytestRecord
            {
                session = SessionId, startedAt = DateTime.Now.ToString("s"), stage = stage,
                result = "in-progress", heroLevelStart = profile.level, heroLevelEnd = profile.level,
                totalWaves = target.Waves.TotalWaves
            };
            food = target.Hero.GetComponent<FoodResource>();
            mana = target.Hero.GetComponent<ManaResource>();
            target.Summoner.Summoned += OnSummoned;
            target.Waves.Spawned += OnEnemySpawned;
        }

        private void OnSummoned(Combatant unit)
        {
            if (Current == null || unit == null) return;
            Current.alliesSummoned++;
            Current.summonsByUnit[(int)battle.Summoner.SelectedUnit]++;
            int lane = Array.IndexOf(battle.Paths, battle.Summoner.SelectedPath);
            if (lane >= 0 && lane < Current.summonsByPath.Length) Current.summonsByPath[lane]++;
            if (Current.firstSummonSeconds < 0f) Current.firstSummonSeconds = Current.durationSeconds;
            allies.Add(unit);
            unit.Died += OnAllyDied;
        }

        private void OnAllyDied(Combatant unit)
        {
            if (Current != null && !finished) Current.alliesLost++;
            allies.Remove(unit);
        }

        private void OnEnemySpawned(Combatant enemy) => enemy.Died += OnEnemyDied;

        private void OnEnemyDied(Combatant enemy)
        {
            if (Current != null && !finished && enemy.LastAttacker != null && enemy.LastAttacker.Faction == Faction.Allied)
                Current.enemiesDefeated++;
        }

        // Only advances while the battle is running; level-up choices pause the clock.
        public void Tick(float delta, bool paused)
        {
            if (Current == null || finished || paused || delta <= 0f || battle == null || battle.Hero == null) return;
            Current.durationSeconds += delta;
            if (food != null && food.CurrentFood >= food.MaximumFood - 0.01f) Current.secondsAtFoodCap += delta;
            Current.lowestHeroHealthPercent = Mathf.Min(Current.lowestHeroHealthPercent,
                100f * battle.Hero.CurrentHealth / battle.Hero.MaximumHealth);
            sampleTimer -= delta;
            if (sampleTimer > 0f) return;
            sampleTimer = SampleInterval;
            Sample();
        }

        private void Sample()
        {
            samples++;
            if (mana != null && mana.Maximum > 0f) manaSum += 100f * mana.Current / mana.Maximum;
            Vector3 hero = battle.Hero.transform.position;
            Lightbringer.Aura.HeroAura aura = battle.Hero.GetComponent<Lightbringer.Aura.HeroAura>();
            if (aura != null)
            {
                int covered = 0;
                float radius = aura.Radius * aura.Radius;
                foreach (Combatant ally in allies)
                    if (ally != null && ally.IsAlive && (ally.transform.position - hero).sqrMagnitude <= radius) covered++;
                auraSum += covered;
            }
            int nearest = NearestPath(hero);
            if (nearest >= 0 && nearest < Current.heroSecondsNearPath.Length)
                Current.heroSecondsNearPath[nearest] += SampleInterval;
        }

        private int NearestPath(Vector3 position)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int lane = 0; lane < battle.Paths.Length; lane++)
            {
                WaypointPath path = battle.Paths[lane];
                if (path == null || !path.IsValid) continue;
                float distance = path.SqrDistanceTo(position);
                if (distance < bestDistance) { bestDistance = distance; best = lane; }
            }
            return best;
        }

        public void Finish(string result)
        {
            if (Current == null || finished) return;
            finished = true;
            Current.result = result;
            Current.heroLevelEnd = progress != null ? progress.level : Current.heroLevelStart;
            if (food != null)
            { Current.foodProduced = food.TotalProduced; Current.foodSpent = food.TotalSpent; Current.foodWasted = food.TotalWasted; }
            if (samples > 0) { Current.averageManaPercent = manaSum / samples; Current.averageAlliesInAura = auraSum / samples; }
            Combatant enemyBase = battle != null && battle.Objective != null ? battle.Objective.EnemyBase : null;
            Current.enemyBaseHealthPercent = enemyBase == null || enemyBase.CurrentHealth <= 0f
                ? 0f : 100f * enemyBase.CurrentHealth / enemyBase.MaximumHealth;
            if (battle != null && battle.Waves != null) Current.wavesSpawned = battle.Waves.WavesSpawned;
            Unsubscribe();
        }

        public void SetFeedback(int rating, string note)
        {
            if (Current == null) return;
            Current.funRating = Mathf.Clamp(rating, 0, 5);
            Current.note = note ?? "";
        }

        // Writes the record once. Returns an error message, or empty on success.
        public string Commit()
        {
            if (Current == null) return "";
            if (!finished) Finish("abandoned");
            PlaytestRecord record = Current;
            // The base-kill EXP can arrive after the victory callback.
            if (progress != null) record.heroLevelEnd = progress.level;
            Current = null;
            battle = null;
            if (string.IsNullOrEmpty(logPath)) return "";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, JsonUtility.ToJson(record) + "\n");
                return "";
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Playtest log could not be written: " + exception.Message);
                return "Playtest log could not be written.";
            }
        }

        private void Discard()
        {
            Unsubscribe();
            Current = null;
            battle = null;
        }

        private void Unsubscribe()
        {
            if (battle == null) return;
            if (battle.Summoner != null) battle.Summoner.Summoned -= OnSummoned;
            if (battle.Waves != null) battle.Waves.Spawned -= OnEnemySpawned;
        }
    }
}
