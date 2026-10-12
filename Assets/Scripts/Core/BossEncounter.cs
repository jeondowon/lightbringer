using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEngine;

namespace Lightbringer.Core
{
    public enum BossEncounterPhase { Assault, Stronghold, Rupture, Arrival, Dialogue, Fight, RetreatDialogue, Retreat, Complete }

    // Owns the first Warlord encounter; neither a base death nor the cinematic grants victory.
    public sealed class BossEncounter : MonoBehaviour
    {
        public const float WaveSeconds = 2.8f, ArrivalSeconds = 2.4f;
        public const float RetreatFadeSeconds = 1.2f;
        private readonly BattleSimulation cinematic = new BattleSimulation();
        private readonly List<Combatant> victims = new List<Combatant>();
        private PrototypeBattle battle;
        private ArtStyleLibrary art;
        private Vector3 centre, cameraStart;
        private Quaternion cameraRotation;
        private float elapsed, waveRadius, savedFov;
        private bool heroShield, baseShield;
        private BossVfx wave;
        private BossCombat bossCombat;
        private Light sun;
        private float sunIntensity;
        public BossEncounterPhase Phase { get; private set; }
        public bool IsRetreating => Phase == BossEncounterPhase.RetreatDialogue || Phase == BossEncounterPhase.Retreat;
        public bool IsDialogue => Phase == BossEncounterPhase.Dialogue || Phase == BossEncounterPhase.RetreatDialogue;
        public bool IsStoryPanel => IsDialogue || Phase == BossEncounterPhase.Retreat;
        public bool IsCinematic => Phase == BossEncounterPhase.Rupture || Phase == BossEncounterPhase.Arrival || IsStoryPanel;
        public float StoryOpacity => Phase == BossEncounterPhase.Retreat ? 1 - Mathf.Clamp01(elapsed / RetreatFadeSeconds) : 1;
        public int DialogueIndex { get; private set; }
        public int FallenAllies { get; private set; }
        public Combatant Boss => battle != null ? battle.Waves.Boss : null;
        public static readonly string[] Speakers = { "어둠의 군주", "빛의 지휘관", "어둠의 군주", "빛의 지휘관" };
        public static readonly string[] Lines =
        {
            "꺼진 봉화들을 밝히고 다닌 자가 너였나. 네 진격도 여기까지다.",
            "내 병사들에게 무슨 짓을…! 네가 이 어둠의 주인인가?",
            "나는 그분의 검이다. 마왕께 이르는 길은 여기서 끝난다.",
            "누가 네 뒤에 있든, 우린 멈추지 않아. 쓰러진 이들을 위해서라도 이 땅을 되찾겠다."
        };
        public static readonly string[] RetreatSpeakers = { "어둠의 군주", "어둠의 군주", "빛의 지휘관" };
        public static readonly string[] RetreatLines =
        {
            "이 빛은… 너를 얕보았군.",
            "이것으로 끝이라 생각하지 마라. 우리는 다시 만날 것이다.",
            "물러났군… 모두의 희생을 잊지 않겠어. 봉화에 다시 빛을 밝히자."
        };
        public string[] CurrentSpeakers => IsRetreating ? RetreatSpeakers : Speakers;
        public string[] CurrentLines => IsRetreating ? RetreatLines : Lines;
        public string ObjectiveText => Phase == BossEncounterPhase.Assault ? "마지막 공세를 모두 격퇴하세요"
            : Phase == BossEncounterPhase.Stronghold ? "적 본진을 파괴하세요"
            : Phase == BossEncounterPhase.Fight ? "어둠의 군주를 격퇴하세요"
            : IsRetreating ? "어둠의 군주가 물러납니다" : "어둠의 군주가 나타납니다";

        public void Configure(PrototypeBattle value, ArtStyleLibrary style)
        {
            battle = value; art = style;
            centre = battle.Objective.EnemyBase.transform.position;
            centre.y = battle.Root.transform.position.y;
            battle.Waves.AssaultEnded += OnAssaultEnded;
            battle.Objective.BaseDestroyed += OnBaseDestroyed;
            battle.Objective.FinalBossDefeated += BeginRetreat;
            battle.Objective.Completed += OnCompleted;
        }

        private void OnAssaultEnded()
        {
            if (Phase != BossEncounterPhase.Assault || battle.Objective.HasEnded) return;
            Phase = BossEncounterPhase.Stronghold;
            if (battle.Objective.EnemyBase != null) battle.Objective.EnemyBase.Invulnerable = false;
        }

        private void OnBaseDestroyed(Combatant stronghold)
        {
            if (Phase != BossEncounterPhase.Stronghold || battle.Objective.HasEnded) return;
            Phase = BossEncounterPhase.Rupture;
            elapsed = 0;
            cinematic.Pause(battle.Root.transform);
            heroShield = battle.Hero.Invulnerable;
            baseShield = battle.AlliedBase.Invulnerable;
            foreach (Light light in battle.Root.GetComponentsInChildren<Light>())
                if (light.type == LightType.Directional) { sun = light; sunIntensity = light.intensity; break; }
            battle.Hero.Invulnerable = true;
            battle.AlliedBase.Invulnerable = true;
            waveRadius = 25;
            foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
                if (unit.IsAlive && unit.Faction == Faction.Allied && unit != battle.Hero && unit != battle.AlliedBase
                    && unit.GetComponent<UnitPathFollower>() != null)
                {
                    victims.Add(unit);
                    waveRadius = Mathf.Max(waveRadius, GroundDistance(unit.transform.position, centre) + 3);
                }
            waveRadius = Mathf.Max(waveRadius, GroundDistance(battle.Hero.transform.position, centre) + 8);
            if (battle.Camera != null)
            {
                cameraStart = battle.Camera.transform.position;
                cameraRotation = battle.Camera.transform.rotation;
                savedFov = battle.Camera.fieldOfView;
            }
            if (Application.isPlaying)
            {
                BossVfx.Spawn(battle.Root.transform, centre, BossVfx.Shape.Collapse, 5, 4);
                wave = BossVfx.Spawn(battle.Root.transform, centre, BossVfx.Shape.Wave, 1, WaveSeconds + 0.4f);
            }
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0 || battle == null || battle.Objective.HasEnded || !IsCinematic) return;
            elapsed += delta;
            if (sun != null) sun.intensity = Mathf.Lerp(sun.intensity, sunIntensity * .3f, 1 - Mathf.Exp(-delta * 2));
            if (Phase == BossEncounterPhase.Rupture)
            {
                float radius = Mathf.Lerp(0, waveRadius, Mathf.Clamp01(elapsed / WaveSeconds));
                if (wave != null) wave.SetRadius(radius);
                for (int i = victims.Count - 1; i >= 0; i--)
                {
                    Combatant unit = victims[i];
                    if (unit == null || !unit.IsAlive) { victims.RemoveAt(i); continue; }
                    if (GroundDistance(unit.transform.position, centre) > radius) continue;
                    unit.Invulnerable = false;
                    unit.TakeDamage(unit.MaximumHealth * 10);
                    FallenAllies++;
                    victims.RemoveAt(i);
                }
                if (elapsed >= WaveSeconds)
                {
                    Phase = BossEncounterPhase.Arrival; elapsed = 0;
                    Combatant boss = battle.Waves.SpawnFinalBoss(centre + Vector3.back * 4 + Vector3.up * 0.85f);
                    if (boss == null) { Debug.LogError("Final boss could not spawn after the assault.", this); return; }
                    boss.Invulnerable = true;
                    boss.GetComponent<UnitCombat>().enabled = false;
                    boss.GetComponent<UnitPathFollower>().enabled = false;
                    bossCombat = boss.gameObject.AddComponent<BossCombat>();
                    bossCombat.Configure(battle, art);
                    bossCombat.enabled = false;
                    battle.Objective.TrackFinalBoss(boss, waitForOutro: true);
                    if (Application.isPlaying)
                    {
                        BossVfx.Spawn(boss.transform, boss.transform.position - Vector3.up * .78f, BossVfx.Shape.Aura, 4f, -1);
                        BossVfx.Spawn(battle.Root.transform, boss.transform.position - Vector3.up * 0.8f, BossVfx.Shape.Portal, 3.5f, ArrivalSeconds + 0.5f);
                    }
                }
            }
            else if (Phase == BossEncounterPhase.Arrival && elapsed >= ArrivalSeconds)
            {
                Phase = BossEncounterPhase.Dialogue; elapsed = 0; DialogueIndex = 0;
            }
            else if (Phase == BossEncounterPhase.Retreat && elapsed >= RetreatFadeSeconds)
            {
                battle.Objective.CompleteBossVictory();
            }
            // Storybook panels replace camera cuts while anyone is speaking, including the retreat.
            if (!IsStoryPanel && Phase != BossEncounterPhase.Complete) UpdateCamera(delta);
        }

        public bool AdvanceDialogue()
        {
            if (!IsDialogue || elapsed < (IsRetreating ? 0.75f : 0.25f) || battle.Objective.HasEnded) return false;
            elapsed = 0;
            if (++DialogueIndex < CurrentLines.Length) return true;
            if (IsRetreating) Phase = BossEncounterPhase.Retreat;
            else BeginFight();
            return true;
        }

        private void BeginRetreat()
        {
            if (Phase != BossEncounterPhase.Fight || battle.Objective.HasEnded) return;
            Phase = BossEncounterPhase.RetreatDialogue;
            elapsed = 0; DialogueIndex = 0;
            // Rewards may queue growth on the lethal hit; acquire the cinematic pause before the next frame.
            battle.Root.GetComponentInParent<CampaignSession>()?.UpdatePause();
            cinematic.Pause(battle.Root.transform);
            heroShield = battle.Hero.Invulnerable;
            baseShield = battle.AlliedBase.Invulnerable;
            battle.Hero.Invulnerable = true;
            battle.AlliedBase.Invulnerable = true;
        }

        private void BeginFight()
        {
            Phase = BossEncounterPhase.Fight;
            RestoreProtection();
            if (battle.Camera != null)
            {
                battle.Camera.transform.SetPositionAndRotation(cameraStart, cameraRotation);
                battle.Camera.fieldOfView = savedFov;
            }
            cinematic.Resume(battle.Objective.HasEnded);
            if (Boss != null && Boss.IsAlive)
            {
                Boss.Invulnerable = false;
                Boss.GetComponent<UnitCombat>().enabled = true;
                Boss.GetComponent<UnitPathFollower>().enabled = true;
                bossCombat.enabled = true;
            }
            // A base kill can grant a level-up; apply that pause before anyone can attack this frame.
            battle.Root.GetComponentInParent<CampaignSession>()?.UpdatePause();
        }

        private void UpdateCamera(float delta)
        {
            if (battle.Camera == null) return;
            Vector3 focus = centre + Vector3.up * 3;
            Vector3 offset = new Vector3(12, 10, -20);
            if (Phase == BossEncounterPhase.Rupture) offset = new Vector3(20, 23, -35);
            Transform camera = battle.Camera.transform;
            float blend = 1 - Mathf.Exp(-delta * 3);
            camera.position = Vector3.Lerp(camera.position, focus + offset, blend);
            camera.rotation = Quaternion.Slerp(camera.rotation, Quaternion.LookRotation(focus - camera.position), blend);
            battle.Camera.fieldOfView = Mathf.Lerp(battle.Camera.fieldOfView, Phase == BossEncounterPhase.Rupture ? 65 : 48, blend);
        }

        private void RestoreProtection()
        {
            if (sun != null) sun.intensity = sunIntensity;
            if (battle.Hero != null) battle.Hero.Invulnerable = heroShield;
            if (battle.AlliedBase != null) battle.AlliedBase.Invulnerable = baseShield;
        }

        private void OnCompleted(bool victory)
        {
            if (IsCinematic) RestoreProtection();
            Phase = BossEncounterPhase.Complete;
            cinematic.Resume(true);
        }

        private void OnDestroy()
        {
            if (battle == null) return;
            if (battle.Waves != null) battle.Waves.AssaultEnded -= OnAssaultEnded;
            if (battle.Objective != null)
            {
                battle.Objective.BaseDestroyed -= OnBaseDestroyed;
                battle.Objective.FinalBossDefeated -= BeginRetreat;
                battle.Objective.Completed -= OnCompleted;
            }
        }

        private static float GroundDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
    }
}
