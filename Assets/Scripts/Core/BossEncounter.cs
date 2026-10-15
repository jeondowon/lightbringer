using System.Collections.Generic;
using Lightbringer.Combat;
using Lightbringer.Units;
using Lightbringer.Visuals;
using UnityEngine;

namespace Lightbringer.Core
{
    public enum BossEncounterPhase { Assault, Stronghold, Rupture, Arrival, Dialogue, Fight, Interlude, RetreatDialogue, Retreat, Complete }

    // Owns the Warlord encounters (8, 16) and the final battle (24); neither a base death nor the cinematic grants victory.
    // The finale adds the Demon King: a sky darkness whose health is an abyss core standing on the ruined stronghold,
    // so troops advancing along their Paths can strike it. Story order (Storyline.md): the Warlord wards the core until
    // he falls, his remaining light speaks (Interlude), then the Demon King is fought alone.
    public sealed class BossEncounter : MonoBehaviour
    {
        public const float WaveSeconds = 4.2f, ArrivalSeconds = 5.5f;
        public const float RetreatFadeSeconds = 1.2f;
        // The reveal shroud gathers at the ruin while the camera follows the wave, so it is already thick when the
        // view cuts back; it is blown apart PartAt seconds into the arrival.
        private const float ShroudLead = 1.2f, PartAt = 1f;
        // The Warlord stands this far in front of the ruined citadel.
        private static readonly Vector3 RevealOffset = Vector3.zero;
        private readonly BattleSimulation cinematic = new BattleSimulation();
        private readonly List<Combatant> victims = new List<Combatant>();
        private PrototypeBattle battle;
        private ArtStyleLibrary art;
        private Vector3 centre, cameraStart, posePosition;
        private Quaternion cameraRotation, poseRotation;
        private float elapsed, waveRadius, front, cameraTravel, savedFov, shake, shakeClock;
        private bool heroShield, baseShield;
        private BossArrivalVfx wave, reveal;
        private BossShockwaveVfx shock;
        private BossCombat bossCombat;
        private Light sun;
        private float sunIntensity;
        private DemonKingSky sky;
        private DemonKingCombat demonKing;
        // Once a boss stands on the field, every newly raised unit hunts across it: allies the boss, enemies the hero.
        private bool hunting;
        public BossEncounterPhase Phase { get; private set; }
        public bool IsRetreating => Phase == BossEncounterPhase.RetreatDialogue || Phase == BossEncounterPhase.Retreat;
        public bool IsInterlude => Phase == BossEncounterPhase.Interlude;
        public bool IsDialogue => Phase == BossEncounterPhase.Dialogue || Phase == BossEncounterPhase.RetreatDialogue || IsInterlude;
        public bool IsStoryPanel => IsDialogue || Phase == BossEncounterPhase.Retreat;
        public bool IsCinematic => Phase == BossEncounterPhase.Rupture || Phase == BossEncounterPhase.Arrival || IsStoryPanel;
        public float StoryOpacity => Phase == BossEncounterPhase.Retreat ? 1 - Mathf.Clamp01(elapsed / RetreatFadeSeconds) : 1;
        public int DialogueIndex { get; private set; }
        public int FallenAllies { get; private set; }
        public Combatant Boss => battle != null ? battle.Waves.Boss : null;
        public bool IsRematch => battle != null && battle.Stage == 16;
        public bool IsFinale => battle != null && EnemyCatalog.IsFinale(battle.Stage);
        // The finale's dialogue plays over the live battlefield so the Demon King in the sky is seen, not drawn;
        // only the Warlord's fall is a storybook page.
        public bool IsIllustrated => !IsFinale || IsInterlude;
        // While the Warlord stands, the abyss core cannot be harmed (the Demon King still casts from the sky).
        public bool IsCoreWarded => demonKing != null && demonKing.Warded;
        public Combatant DemonCore => demonKing != null ? demonKing.Core : null;
        public DemonKingCombat DemonKing => demonKing;
        public DemonKingSky Sky => sky;
        public string ChapterTitle => IsFinale ? "두 어둠" : "어둠의 군주";
        private Vector3 RevealPoint => centre + (IsFinale ? Vector3.back * 7 : RevealOffset);
        private float CinematicFov => IsFinale ? 60 : 48;
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
        // The Warlord's identity is not revealed in Act 2 (Storyline.md): the rematch opens without naming him.
        public static readonly string[] RematchLines = {
            "끝내 이 성채까지 왔구나. 되살린 봉화의 빛으로 나를 꺾을 수 있다고 믿느냐?",
            "이번엔 물러서지 않아. 마왕에게 가는 길, 여기서 열고 말겠어.",
            "길을 원한다면 내 검을 넘어 보아라.",
            "우린 함께 여기까지 왔어. 이번에도 모두를 지키며 너를 넘어서겠어."
        };
        // Act 2 ending: kneeling, sword close-up, she knows her teacher's sword, he turns without answering and leaves.
        // An empty line is a silent page.
        public static readonly string[] RematchRetreatSpeakers = { "", "", "빛의 지휘관", "빛의 지휘관", "빛의 지휘관", "" };
        public static readonly string[] RematchRetreatLines = {
            "",
            "",
            "그 검… 스승님의 검인데.",
            "네가 왜 그 검을 가지고 있지?",
            "잠깐!",
            ""
        };
        public static readonly string[] FinaleSpeakers = { "어둠의 군주", "마왕", "빛의 지휘관", "어둠의 군주", "빛의 지휘관" };
        public static readonly string[] FinaleLines = {
            "여기까지 올 줄 알았다. 하지만 이번에 너를 맞는 것은 나 혼자가 아니다.",
            "봉화를 되살린 작은 빛이여. 이 하늘 아래에서 네 빛은 모두 꺼진다.",
            "하늘을 뒤덮은 어둠… 저것이 마왕이구나.",
            "그 검의 주인이 누구였는지 알고 싶다면… 나를 넘어 보아라.",
            "모두, 군주부터 막아! 그가 서 있는 한 마왕의 핵에는 닿지 않아!"
        };
        // Act 3: the Warlord's body is gone but a small light remains and speaks in the teacher's voice.
        public static readonly string[] InterludeSpeakers = { "익숙한 목소리", "빛의 지휘관" };
        public static readonly string[] InterludeLines = {
            "아직… 끝나지 않았다. 내 빛은 놈에게 묶여 있다.",
            "스승님…?"
        };
        public static readonly string[] FinaleEndSpeakers = { "빛의 지휘관", "빛의 지휘관" };
        public static readonly string[] FinaleEndLines = {
            "하늘이 걷히고 있어. 모든 봉화에 다시 빛이 돌아와.",
            "스승님… 이제 당신을 묶던 어둠도 사라졌어요."
        };
        // One illustration per line (Resources paths); the finale's own dialogue has none.
        private static readonly string[] ArrivalPictures =
            { "BossStory/Confrontation_v1", "BossStory/Confrontation_v1", "BossStory/WarlordClose_v1", "BossStory/HeroResolve_v1" };
        private static readonly string[] RetreatPictures =
            { "BossStory/WarlordDefeated_v1", "BossStory/Retreat_v1", "BossStory/Aftermath_v1" };
        private static readonly string[] RematchRetreatPictures =
        {
            "BossStory/WarlordDefeated_v1", StoryCatalog.SwordCloseUp, StoryCatalog.HeroRecognition,
            "BossStory/WarlordClose_v1", "BossStory/Retreat_v1", "BossStory/Aftermath_v1"
        };
        private static readonly string[] InterludePictures = { StoryCatalog.WarlordDissolution, StoryCatalog.WarlordDissolution };
        public string[] CurrentSpeakers => IsInterlude ? InterludeSpeakers
            : IsFinale ? (IsRetreating ? FinaleEndSpeakers : FinaleSpeakers)
            : IsRetreating ? (IsRematch ? RematchRetreatSpeakers : RetreatSpeakers) : Speakers;
        public string[] CurrentLines => IsInterlude ? InterludeLines
            : IsFinale ? (IsRetreating ? FinaleEndLines : FinaleLines)
            : IsRematch ? (IsRetreating ? RematchRetreatLines : RematchLines)
            : IsRetreating ? RetreatLines : Lines;
        public string[] CurrentPictures => IsInterlude ? InterludePictures : IsFinale ? null
            : IsRetreating ? (IsRematch ? RematchRetreatPictures : RetreatPictures) : ArrivalPictures;
        // Leaving this retreat line starts the Warlord's escape through the rift (BossPortalEscape); -1 for none.
        public int EscapeLine => IsFinale || !IsRetreating ? -1 : IsRematch ? 4 : 1;
        public string ObjectiveText => Phase == BossEncounterPhase.Assault ? "마지막 공세를 모두 격퇴하세요"
            : Phase == BossEncounterPhase.Stronghold ? "적 본진을 파괴하세요"
            : Phase == BossEncounterPhase.Fight ? FightObjective
            : IsInterlude ? "어둠의 군주가 쓰러집니다"
            : IsFinale ? (IsRetreating ? "두 어둠이 걷힙니다" : "두 어둠이 모습을 드러냅니다")
            : IsRetreating ? "어둠의 군주가 물러납니다" : "어둠의 군주가 나타납니다";

        private string FightObjective
        {
            get
            {
                if (!IsFinale) return "어둠의 군주를 격퇴하세요";
                bool lord = Boss != null && Boss.IsAlive, core = DemonCore != null && DemonCore.IsAlive;
                return lord && core ? "어둠의 군주를 쓰러뜨려 마왕의 핵을 드러내세요"
                    : lord ? "남은 어둠의 군주를 쓰러뜨리세요" : "마왕의 핵을 파괴하세요";
            }
        }

        public void Configure(PrototypeBattle value, ArtStyleLibrary style)
        {
            battle = value; art = style;
            centre = battle.Objective.EnemyBase.transform.position;
            centre.y = battle.Root.transform.position.y;
            battle.Waves.AssaultEnded += OnAssaultEnded;
            battle.Objective.BaseDestroyed += OnBaseDestroyed;
            battle.Objective.FinalBossDefeated += BeginRetreat;
            battle.Objective.BossFellEarly += OnBossFellEarly;
            battle.Objective.Completed += OnCompleted;
            battle.Waves.Spawned += OnEnemySpawned;
            if (battle.Summoner != null) battle.Summoner.Summoned += OnAllySummoned;
            // The storm bank already looms over the final citadel; its eyes open when the stronghold falls.
            if (IsFinale) sky = DemonKingSky.Build(art, battle.Root.transform);
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
            // Every arrival opens with the rupture: the shockwave runs to the far corners of the field and wipes the army.
            waveRadius = FieldReach() + 4;
            front = 0;
            foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
                if (unit.IsAlive && unit.Faction == Faction.Allied && unit != battle.Hero && unit != battle.AlliedBase
                    && unit.GetComponent<UnitPathFollower>() != null)
                {
                    victims.Add(unit);
                    waveRadius = Mathf.Max(waveRadius, GroundDistance(unit.transform.position, centre) + 3);
                }
            cameraTravel = Mathf.Max(0, GroundDistance(battle.AlliedBase.transform.position, centre) - 8);
            if (battle.Camera != null)
            {
                cameraStart = battle.Camera.transform.position;
                cameraRotation = battle.Camera.transform.rotation;
                savedFov = battle.Camera.fieldOfView;
                posePosition = cameraStart;
                poseRotation = cameraRotation;
            }
            shake = 1;
            if (Application.isPlaying)
            {
                Lightbringer.Audio.BossArrivalAudio.Begin(transform);
                wave = BossArrivalVfx.Spawn(art, battle.Root.transform, centre, BossArrivalVfx.Kind.Rupture, battle.Camera);
                shock = BossShockwaveVfx.Spawn(art, battle.Root.transform, centre, waveRadius);
            }
        }

        // Ground distance from the citadel to the farthest corner of the playable field.
        private float FieldReach()
        {
            float reach = 0;
            foreach (float x in new[] { -BattlefieldLayout.HalfWidth, BattlefieldLayout.HalfWidth })
                foreach (float z in new[] { BattlefieldLayout.MinZ, BattlefieldLayout.MaxZ })
                    reach = Mathf.Max(reach, GroundDistance(battle.Root.transform.TransformPoint(new Vector3(x, 0, z)), centre));
            return reach;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (!isActiveAndEnabled || delta <= 0 || battle == null || battle.Objective.HasEnded || !IsCinematic) return;
            elapsed += delta;
            // Light returns as the two darknesses fall in the finale; every other scene dims under the Warlord.
            float daylight = IsFinale && IsRetreating ? 1.15f : .3f;
            if (sun != null) sun.intensity = Mathf.Lerp(sun.intensity, sunIntensity * daylight, 1 - Mathf.Exp(-delta * (daylight > 1 ? .8f : 2)));
            if (Phase == BossEncounterPhase.Rupture)
            {
                float t = Mathf.Clamp01(elapsed / WaveSeconds);
                // Fast off the blast, easing as it runs out over the emptied field towards the allied gate.
                front = waveRadius * (0.55f * t + 0.45f * (1 - (1 - t) * (1 - t)));
                if (wave != null) wave.SetFront(front);
                if (shock != null) shock.SetFront(front);
                // A low rumble under the advancing wave on top of the initial blast.
                shake = Mathf.Max(shake, 0.5f - 0.3f * t);
                for (int i = victims.Count - 1; i >= 0; i--)
                {
                    Combatant unit = victims[i];
                    if (unit == null || !unit.IsAlive) { victims.RemoveAt(i); continue; }
                    if (GroundDistance(unit.transform.position, centre) > front) continue;
                    // The shockwave throws a copy of the ally's model; its own hit/death feedback would double it.
                    if (shock != null) shock.Fling(unit);
                    if (unit.TryGetComponent(out HitReaction reaction)) reaction.enabled = false;
                    unit.Invulnerable = false;
                    unit.TakeDamage(unit.MaximumHealth * 10);
                    FallenAllies++;
                    victims.RemoveAt(i);
                }
                if (reveal == null && elapsed >= WaveSeconds - ShroudLead && Application.isPlaying)
                    reveal = BossArrivalVfx.Spawn(art, battle.Root.transform, RevealPoint, BossArrivalVfx.Kind.Reveal, battle.Camera);
                if (elapsed >= WaveSeconds)
                {
                    // The Warlord already stands in front of the ruin, hidden in the shroud.
                    Combatant boss = battle.Waves.SpawnFinalBoss(RevealPoint + Vector3.up * 0.85f);
                    if (boss == null) return; // Retry while the surviving army occupies the arrival slots.
                    Phase = BossEncounterPhase.Arrival; elapsed = 0;
                    boss.Invulnerable = true;
                    boss.GetComponent<UnitCombat>().enabled = false;
                    boss.GetComponent<UnitPathFollower>().enabled = false;
                    bossCombat = boss.gameObject.AddComponent<BossCombat>();
                    bossCombat.Configure(battle, art);
                    bossCombat.enabled = false;
                    battle.Objective.TrackFinalBoss(boss, waitForOutro: true);
                    if (IsFinale) SummonDemonKing();
                    hunting = true;
                    // Cut from the wave's far edge back to the smoke-filled ruin.
                    shake = 0;
                    SnapCamera();
                }
            }
            else if (Phase == BossEncounterPhase.Arrival)
            {
                if (reveal != null && elapsed >= PartAt)
                {
                    reveal.Part();
                    reveal = null;
                    shake = Mathf.Max(shake, 0.3f);
                }
                if (elapsed >= ArrivalSeconds)
                {
                    Phase = BossEncounterPhase.Dialogue; elapsed = 0; DialogueIndex = 0;
                    reveal = null;
                    // The dark pool settles under the Warlord as the dialogue begins.
                    Combatant boss = Boss;
                    if (boss != null && Application.isPlaying)
                        BossVfx.Spawn(boss.transform, boss.transform.position - Vector3.up * .78f, BossVfx.Shape.Aura, 4f, -1);
                }
            }
            else if (Phase == BossEncounterPhase.Retreat && elapsed >= RetreatFadeSeconds)
            {
                battle.Objective.CompleteBossVictory();
            }
            // Storybook panels replace camera cuts while anyone is speaking, including the retreat. The finale's dialogue
            // keeps the live view, framing the Warlord, the core and the eyes in the sky.
            if ((!IsStoryPanel || !IsIllustrated) && Phase != BossEncounterPhase.Complete) UpdateCamera(delta);
        }

        public bool AdvanceDialogue()
        {
            if (!IsDialogue || elapsed < (IsRetreating || IsInterlude ? 0.75f : 0.25f) || battle.Objective.HasEnded) return false;
            elapsed = 0;
            if (++DialogueIndex < CurrentLines.Length) return true;
            if (IsInterlude) EndInterlude();
            else if (IsRetreating) Phase = BossEncounterPhase.Retreat;
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
            if (IsFinale)
            {
                if (sky != null) sky.Dissipate();
                if (battle.Camera != null)
                {
                    // Ease from the battle view toward the clearing sky.
                    posePosition = battle.Camera.transform.position;
                    poseRotation = battle.Camera.transform.rotation;
                }
            }
        }

        // The abyss core rises on the ruined stronghold, carrying the Demon King's health; the sky opens its eyes.
        private void SummonDemonKing()
        {
            Transform root = battle.Root.transform;
            var item = new GameObject("Abyss Core (Demon King)");
            item.transform.SetParent(root, false);
            item.transform.position = centre;
            BoxCollider surface = item.AddComponent<BoxCollider>();
            surface.center = new Vector3(0, 2.5f, 0);
            surface.size = new Vector3(3, 5, 3);
            Combatant core = item.AddComponent<Combatant>();
            core.UseTeamTint = false;
            core.Configure(Faction.Enemy, EnemyCatalog.DemonCoreHealth(battle.Stage));
            core.SetAttackSurface(surface);
            core.IsHeavy = true;
            core.StatusImmune = true;
            core.Invulnerable = true;
            AbyssCoreVisual.Build(item.transform, core, sky);
            battle.Waves.RegisterBossPart(core);
            battle.Objective.AddFinalBoss(core);
            var host = new GameObject("Demon King");
            host.transform.SetParent(root, false);
            demonKing = host.AddComponent<DemonKingCombat>();
            demonKing.Configure(battle, core, sky, bossCombat, art);
            demonKing.enabled = false;
            if (sky != null) { sky.BindCore(item.transform); sky.Reveal(); }
            if (Application.isPlaying)
                BossVfx.Spawn(item.transform, centre + Vector3.up * .02f, BossVfx.Shape.Aura, 4.5f, -1);
        }

        // The surviving darkness, or null once both have fallen.
        private Combatant BossQuarry => Boss != null && Boss.IsAlive ? Boss : DemonCore != null && DemonCore.IsAlive ? DemonCore : null;

        private void OnEnemySpawned(Combatant unit)
        {
            if (hunting && unit != null && unit != Boss && unit.TryGetComponent(out UnitCombat combat)) combat.Quarry = battle.Hero;
        }

        private void OnAllySummoned(Combatant unit)
        {
            if (hunting && unit != null && unit.TryGetComponent(out UnitCombat combat)) combat.Quarry = BossQuarry;
        }

        private void OnBossFellEarly(Combatant fallen)
        {
            if (!IsFinale) return;
            // Allies hunting the fallen darkness turn on the survivor.
            Combatant survivor = BossQuarry;
            foreach (UnitCombat ally in battle.Root.GetComponentsInChildren<UnitCombat>())
                if (ally.Quarry == fallen) ally.Quarry = survivor;
            // The survivor strengthens one pattern; nobody heals.
            if (fallen == DemonCore && bossCombat != null) bossCombat.Bereaved = true;
            else if (demonKing != null) demonKing.Bereaved = true;
            if (fallen == Boss) OnWarlordFell();
        }

        // The core is warded while the Warlord stands, so in the finale he always falls first: the fight halts while the
        // light left in his place speaks in the teacher's voice (ActThree_01), then the Demon King is fought alone.
        private void OnWarlordFell()
        {
            if (Phase != BossEncounterPhase.Fight || battle.Objective.HasEnded) return;
            Phase = BossEncounterPhase.Interlude;
            elapsed = 0; DialogueIndex = 0;
            battle.Root.GetComponentInParent<CampaignSession>()?.UpdatePause();
            cinematic.Pause(battle.Root.transform);
            heroShield = battle.Hero.Invulnerable;
            baseShield = battle.AlliedBase.Invulnerable;
            battle.Hero.Invulnerable = true;
            battle.AlliedBase.Invulnerable = true;
        }

        private void EndInterlude()
        {
            Phase = BossEncounterPhase.Fight;
            RestoreProtection();
            cinematic.Resume(battle.Objective.HasEnded);
            if (demonKing != null) demonKing.Warded = false;
            if (DemonCore != null && DemonCore.IsAlive) DemonCore.Invulnerable = false;
            battle.Root.GetComponentInParent<CampaignSession>()?.UpdatePause();
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
            if (demonKing != null && DemonCore != null && DemonCore.IsAlive)
            {
                // The core stays out of reach while the Warlord stands; the Demon King still casts from the sky.
                demonKing.Warded = Boss != null && Boss.IsAlive;
                DemonCore.Invulnerable = demonKing.Warded;
                demonKing.enabled = true;
            }
            // A base kill can grant a level-up; apply that pause before anyone can attack this frame.
            battle.Root.GetComponentInParent<CampaignSession>()?.UpdatePause();
        }

        private void UpdateCamera(float delta)
        {
            if (battle.Camera == null) return;
            Vector3 focus = CameraFocus(out Vector3 offset);
            Transform camera = battle.Camera.transform;
            // The wave is followed tightly; everything else eases in.
            float blend = 1 - Mathf.Exp(-delta * (Phase == BossEncounterPhase.Rupture ? 6 : 3));
            posePosition = Vector3.Lerp(posePosition, focus + offset, blend);
            poseRotation = Quaternion.Slerp(poseRotation, Quaternion.LookRotation(focus - posePosition), blend);
            // Shake rides on top of the smoothed pose so it never feeds back into the follow.
            shake = Mathf.Max(0, shake - delta * 0.9f);
            shakeClock += delta * 22;
            float amount = shake * shake;
            float pitch = (Mathf.PerlinNoise(shakeClock, 0.2f) - .5f) * 2, yaw = (Mathf.PerlinNoise(0.6f, shakeClock) - .5f) * 2;
            camera.SetPositionAndRotation(posePosition + Vector3.up * (pitch * amount * 0.35f),
                poseRotation * Quaternion.Euler(pitch * amount * 1.8f, yaw * amount * 1.8f, yaw * amount * 1.1f));
            battle.Camera.fieldOfView = Mathf.Lerp(battle.Camera.fieldOfView, Phase == BossEncounterPhase.Rupture ? 65 : CinematicFov, blend);
        }

        // During the rupture the view starts on the blast, then rides just ahead of the shockwave toward the allied
        // gate, looking back at the citadel so the allies it catches are in frame. Afterwards it frames the Warlord.
        private Vector3 CameraFocus(out Vector3 offset)
        {
            Vector3 focus = centre + Vector3.up * 3;
            offset = new Vector3(12, 10, -20);
            if (IsFinale && Phase != BossEncounterPhase.Rupture)
            {
                // The ending pulls back and tilts up into the clearing sky.
                if (IsRetreating)
                {
                    offset = new Vector3(14, 3, -48);
                    return centre + new Vector3(0, 16, 8);
                }
                // Low behind the Warlord, tilted up so the Demon King's head and eyes over the citadel crown the
                // frame; the ruined citadel is only a backdrop.
                offset = new Vector3(6, -11.5f, -36);
                return centre + new Vector3(0, 15, 8);
            }
            if (Phase != BossEncounterPhase.Rupture) return focus;
            offset = new Vector3(18, 24, -32);
            return focus + Vector3.back * Mathf.Clamp(front - 4, 0, cameraTravel);
        }

        private void SnapCamera()
        {
            if (battle.Camera == null) return;
            Vector3 focus = CameraFocus(out Vector3 offset);
            posePosition = focus + offset;
            poseRotation = Quaternion.LookRotation(focus - posePosition);
            battle.Camera.transform.SetPositionAndRotation(posePosition, poseRotation);
            battle.Camera.fieldOfView = CinematicFov;
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
            if (battle.Waves != null)
            {
                battle.Waves.AssaultEnded -= OnAssaultEnded;
                battle.Waves.Spawned -= OnEnemySpawned;
            }
            if (battle.Summoner != null) battle.Summoner.Summoned -= OnAllySummoned;
            if (battle.Objective != null)
            {
                battle.Objective.BaseDestroyed -= OnBaseDestroyed;
                battle.Objective.FinalBossDefeated -= BeginRetreat;
                battle.Objective.BossFellEarly -= OnBossFellEarly;
                battle.Objective.Completed -= OnCompleted;
            }
        }

        private static float GroundDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
    }
}
