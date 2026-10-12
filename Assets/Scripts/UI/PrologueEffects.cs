using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lightbringer.UI
{
    // Living light over each prologue illustration. Positions are artwork pixels (1672x941) of the slide image,
    // which the picture shows scale-to-fit inside the 16:9 frame. Keyed by PrologueSlide.id.
    internal sealed class PrologueEffects : VisualElement
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.48f);
        private static readonly Color Warm = new Color(1f, 0.9f, 0.7f);
        private static readonly Color UndeadViolet = new Color(0.66f, 0.42f, 1f);

        private readonly SpriteFx fx = new SpriteFx { CaptionFadeStart = 0.74f };
        private Action scene;
        private int captionIndex;
        private float elapsed, captionTime, repaintTime;

        public PrologueEffects()
        {
            name = "Prologue Effects";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0f;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
        }

        public void Show(string slideId)
        {
            scene = SceneFor(slideId);
            elapsed = 0f;
            MarkDirtyRepaint();
        }

        // Some pages change their effect with the caption; the new beat eases in from the caption change.
        public void SetCaption(int index)
        {
            captionIndex = index;
            captionTime = 0f;
        }

        private float CaptionRamp(float seconds) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(captionTime / seconds));

        public void Tick(float deltaTime)
        {
            if (scene == null) return;
            elapsed += deltaTime;
            captionTime += deltaTime;
            repaintTime += deltaTime;
            if (repaintTime < 1f / 60f) return;
            repaintTime = 0f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (scene == null || !fx.Begin(contentRect, elapsed, false)) return;
            scene();
            fx.End(context);
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);

        private Action SceneFor(string id)
        {
            switch (id)
            {
                case "00A": return Creation;
                case "00B": return Peace;
                case "00C": return BirthOfDarkness;
                case "00D": return DemonKingBirth;
                case "01": return Invasion;
                case "02": return KnightOfLight;
                case "02B": return KnightVictory;
                case "03": return Beacons;
                case "03(V5)": return VillageBeacon;
                case "04": return LastBattle;
                case "04B": return EmptyCastle;
                case "05": return ForgottenPromise;
                case "05A": return Greed;
                case "05B": return Hatred;
                case "06": return DarknessReturns;
                case "07": return LightStrikes;
                case "08": return FirstStep;
                case "07(ForgottenBeacon)": return ForgottenBeacon;
                case "09": return Journey;
                case "00E": return UndeadRising;
                case "03B": return BeaconWar;
                case "04A": return ThroneHall;
                default: return null;
            }
        }

        // 00A: the first light, pouring golden dust over the world and glinting on the river.
        private static readonly Vector2[] CreationRiver =
            { P(760f, 505f), P(850f, 522f), P(940f, 600f), P(1010f, 640f), P(1060f, 690f), P(1150f, 700f), P(960f, 742f), P(870f, 770f) };

        private void Creation()
        {
            fx.Beacon(P(830f, 97f), 1.3f, 1f, 1);
            fx.Motes(P(830f, 300f), P(430f, 190f), 42, Gold, P(0f, 70f), 18f, 1f, 9f, 3);
            for (int i = 0; i < CreationRiver.Length; i++)
                fx.Glint(CreationRiver[i], 11f, Warm, 3.5f + i * 0.4f, 50 + i);
        }

        // 00B: an evening of peace — sunlight falls through the clouds onto the valley; warm windows,
        // fireflies over the flowers, the river catching the light.
        private static readonly Vector2[] PeaceWindows = { P(113f, 400f), P(222f, 440f), P(490f, 505f), P(722f, 590f) };
        private static readonly Vector2[] PeaceDistant = { P(1395f, 370f), P(1385f, 460f), P(1460f, 465f), P(1530f, 440f), P(1520f, 690f) };
        private static readonly Vector2[] PeaceRiver = { P(1050f, 420f), P(1110f, 480f), P(1200f, 560f), P(1215f, 640f) };

        private void Peace()
        {
            // Follows the break in the clouds painted into v3, so the moving shafts sit on the painted ones.
            fx.Spill(P(1150f, 420f), P(480f, 140f), new Color(1f, 0.78f, 0.48f), 0.08f, 6);
            fx.SunRays(P(1080f, 95f), 600f, 0.28f, 6);
            fx.Lamp(P(318f, 415f), 1f, 1f, 1);
            for (int i = 0; i < PeaceWindows.Length; i++) fx.Lamp(PeaceWindows[i], 0.8f, 0.75f, 10 + i);
            for (int i = 0; i < PeaceDistant.Length; i++) fx.Lamp(PeaceDistant[i], 0.4f, 0.6f, 20 + i);
            for (int i = 0; i < PeaceRiver.Length; i++) fx.Glint(PeaceRiver[i], 9f, Warm, 4f + i * 0.5f, 30 + i);
            var firefly = new Color(0.86f, 1f, 0.55f);
            fx.Motes(P(220f, 760f), P(220f, 130f), 14, firefly, P(0f, -8f), 30f, 0.9f, 7f, 4);
            fx.Motes(P(720f, 700f), P(320f, 90f), 10, firefly, P(0f, -8f), 30f, 0.8f, 7f, 5);
        }

        // 00C: a lantern in a quarrel, and darkness rising from the black streams between people.
        private static readonly Vector2[] QuarrelStream =
        {
            P(270f, 360f), P(330f, 430f), P(420f, 520f), P(470f, 620f), P(540f, 700f), P(600f, 760f),
            P(720f, 790f), P(900f, 800f), P(1100f, 790f), P(1300f, 770f), P(1450f, 760f), P(1550f, 750f),
        };
        private static readonly Vector2[] QuarrelWoman = { P(1180f, 520f), P(1200f, 600f), P(1240f, 680f) };
        private static readonly Vector2[] QuarrelMan = { P(1380f, 560f), P(1420f, 640f), P(1460f, 700f) };
        // Second caption: the separate streams gather and creep along the street out of the picture.
        private static readonly Vector2[] GatheringStream =
        {
            P(270f, 360f), P(330f, 430f), P(420f, 520f), P(470f, 620f), P(540f, 700f), P(600f, 760f),
            P(720f, 790f), P(900f, 800f), P(1100f, 790f), P(1300f, 770f), P(1450f, 760f), P(1560f, 750f), P(1700f, 735f),
        };
        private static readonly Vector2[] GatheringWoman = { P(1180f, 520f), P(1200f, 600f), P(1240f, 690f), P(1330f, 765f), P(1460f, 758f), P(1700f, 735f) };
        private static readonly Vector2[] GatheringMan = { P(1380f, 560f), P(1420f, 640f), P(1470f, 710f), P(1560f, 750f), P(1700f, 735f) };

        private void BirthOfDarkness()
        {
            fx.DarkWisps(QuarrelStream, 40, 70f, 1f, 1f, 7);
            fx.DarkWisps(QuarrelWoman, 8, 60f, 0.8f, 0.8f, 8);
            fx.DarkWisps(QuarrelMan, 8, 60f, 0.8f, 0.8f, 9);
            if (captionIndex >= 1)
            {
                float gather = CaptionRamp(1.5f);
                fx.DarkFlow(GatheringStream, 44, 38f, 1.1f, gather, 31);
                fx.DarkFlow(GatheringWoman, 12, 34f, 0.9f, gather, 32);
                fx.DarkFlow(GatheringMan, 12, 34f, 0.9f, gather, 33);
            }
            fx.Lamp(P(213f, 318f), 1.1f, 1.1f, 1);
            fx.Lamp(P(1105f, 385f), 0.6f, 0.7f, 2);
            fx.Lamp(P(1580f, 290f), 0.6f, 0.7f, 3);
            fx.Lamp(P(1500f, 540f), 0.5f, 0.6f, 4);
        }

        // 00D: the Demon King's red eyes, darkness boiling off its body.
        private static readonly Vector2[] DemonBody = { P(900f, 600f), P(1000f, 450f), P(1100f, 260f), P(1200f, 450f), P(1350f, 600f) };
        private static readonly Vector2[] DemonVillage = { P(60f, 560f), P(100f, 568f), P(170f, 572f), P(205f, 578f) };

        // Darkness creeping across the land from both sides into the Demon King's body.
        private static readonly Vector2[] DemonFlowLeft = { P(-40f, 760f), P(200f, 720f), P(420f, 700f), P(620f, 720f), P(800f, 690f), P(950f, 620f), P(1050f, 560f) };
        private static readonly Vector2[] DemonFlowLow = { P(-40f, 900f), P(300f, 860f), P(520f, 830f), P(700f, 780f), P(880f, 700f), P(1000f, 600f) };
        private static readonly Vector2[] DemonFlowRight = { P(1712f, 640f), P(1500f, 620f), P(1350f, 610f), P(1220f, 580f), P(1130f, 520f) };

        private void DemonKingBirth()
        {
            fx.DarkFlow(DemonFlowLeft, 34, 42f, 1.2f, 1f, 34);
            fx.DarkFlow(DemonFlowLow, 30, 46f, 1.3f, 0.9f, 35);
            fx.DarkFlow(DemonFlowRight, 22, 40f, 1.1f, 0.9f, 36);
            fx.DarkWisps(DemonBody, 46, 160f, 1.6f, 1f, 11);
            for (int i = 0; i < DemonVillage.Length; i++) fx.Lamp(DemonVillage[i], 0.35f, 0.6f, 40 + i);
            var red = new Color(1f, 0.22f, 0.08f);
            fx.Eye(P(1110f, 122f), 1.5f, red, 1);
            fx.Eye(P(1138f, 118f), 1.5f, red, 1);
        }

        // 00E: darkness pours from the Demon King across the old battlefield and the fallen rise where it touches.
        private static readonly Vector2[] RisingFlowLeft = { P(950f, 290f), P(830f, 380f), P(700f, 480f), P(520f, 580f), P(320f, 660f), P(120f, 720f) };
        private static readonly Vector2[] RisingFlowCentre = { P(1130f, 380f), P(1060f, 500f), P(980f, 610f), P(920f, 700f), P(860f, 790f) };
        private static readonly Vector2[] RisingFlowRight = { P(1300f, 380f), P(1340f, 500f), P(1400f, 620f), P(1480f, 720f) };
        private static readonly Vector2[] RisingGraves = { P(500f, 700f), P(740f, 640f), P(930f, 760f), P(1220f, 700f), P(300f, 640f) };
        private static readonly Vector2[] RisingEyes =
        {
            P(512f, 636f), P(528f, 638f), P(956f, 712f), P(979f, 720f),
            P(741f, 540f), P(895f, 595f), P(904f, 596f), P(1044f, 602f), P(1140f, 600f),
        };

        private void UndeadRising()
        {
            fx.Fog(P(900f, 560f), P(800f, 50f), 8, new Color(0.56f, 0.54f, 0.68f), 0.1f, 10f, 5);
            fx.DarkFlow(RisingFlowLeft, 36, 44f, 1.3f, 1f, 41);
            fx.DarkFlow(RisingFlowCentre, 26, 40f, 1.2f, 1f, 42);
            fx.DarkFlow(RisingFlowRight, 18, 40f, 1.1f, 0.9f, 43);
            fx.DarkWisps(RisingGraves, 24, 90f, 1f, 0.8f, 44);
            for (int i = 0; i < RisingEyes.Length; i++)
                fx.Eye(RisingEyes[i], i < 4 ? 1f : 0.7f, UndeadViolet, i / 2 + 3);
            var red = new Color(1f, 0.22f, 0.08f);
            fx.Eye(P(1177f, 96f), 1.5f, red, 2);
            fx.Eye(P(1202f, 90f), 1.5f, red, 2);
        }

        // 01: the burning city — every roof fire flickers and throws embers; ash drifts past the fleeing people.
        private static readonly (Vector2 at, float size, int embers)[] InvasionFires =
        {
            (P(100f, 60f), 0.8f, 10), (P(70f, 85f), 0.6f, 6), (P(245f, 180f), 0.6f, 8), (P(300f, 205f), 0.6f, 8),
            (P(320f, 250f), 0.6f, 8), (P(415f, 250f), 0.7f, 10), (P(465f, 250f), 0.6f, 8), (P(520f, 330f), 0.6f, 8),
            (P(625f, 190f), 0.4f, 5), (P(675f, 215f), 0.4f, 5), (P(745f, 330f), 0.6f, 8), (P(830f, 350f), 0.6f, 8),
            (P(975f, 385f), 0.5f, 6), (P(680f, 410f), 0.5f, 6), (P(1545f, 100f), 0.4f, 5), (P(1590f, 650f), 0.6f, 8),
        };

        private void Invasion()
        {
            for (int i = 0; i < InvasionFires.Length; i++)
                fx.Fire(InvasionFires[i].at, InvasionFires[i].size, InvasionFires[i].embers, 60 + i);
            // As on the title screen: embers lift off the burning roofs into the sky, and a few drift past up close.
            for (int i = 0; i < 40; i++)
            {
                Vector2 origin = P(30f + SpriteFx.Hash(300 + i) * 950f, 230f + SpriteFx.Hash(340 + i) * 170f);
                fx.Ember(origin, 300 + i, 1.1f, 220f + SpriteFx.Hash(380 + i) * 220f, 40f, 3f, false);
            }
            for (int i = 0; i < 10; i++)
            {
                float seed = SpriteFx.Hash(420 + i);
                fx.Ember(P(200f + seed * 1300f, 980f), 420 + i, 1.5f, 420f + seed * 180f, 300f, 8f, true);
            }
            fx.Motes(P(836f, 470f), P(836f, 470f), 14, new Color(0.78f, 0.62f, 0.5f, 0.8f), P(70f, -50f), 20f, 0.9f, 11f, 12, true);
        }

        // 02: the Knight of Light raises the radiant sword over the undead host; a far town burns.
        private static readonly Vector2[] KnightFarFires = { P(1390f, 385f), P(1440f, 383f), P(1505f, 383f), P(1540f, 380f) };

        private void KnightOfLight()
        {
            for (int i = 0; i < KnightFarFires.Length; i++) fx.Fire(KnightFarFires[i], 0.4f, 6, 90 + i);
            fx.Rays(P(458f, 95f), 260f, 0.6f, 2);
            fx.Blade(P(455f, 178f), P(458f, 15f), 9f, 1f, 1);
        }

        // 02B: the blade cleaves a skeleton apart in a spray of light.
        private void KnightVictory()
        {
            fx.Blade(P(870f, 437f), P(1378f, 682f), 10f, 1f, 2);
            fx.Burst(P(1225f, 470f), 22, 120f, Gold, 2.6f, 4);
        }

        // 03: the first beacon lights the road home; two more burn on the far hills.
        private static readonly Vector2[] BeaconsWindows = { P(98f, 370f), P(210f, 425f), P(255f, 432f) };

        private void Beacons()
        {
            for (int i = 0; i < BeaconsWindows.Length; i++) fx.Lamp(BeaconsWindows[i], 0.55f, 0.7f, 70 + i);
            fx.Spill(P(480f, 585f), P(250f, 58f), new Color(1f, 0.72f, 0.36f), 0.24f, 2);
            fx.Beacon(P(510f, 165f), 0.9f, 1f, 2);
            fx.Beacon(P(1280f, 368f), 0.17f, 0.9f, 3);
            fx.Beacon(P(1565f, 375f), 0.15f, 0.85f, 4);
            fx.Blade(P(468f, 515f), P(505f, 598f), 4f, 0.8f, 3);
        }

        // 03(V5): the beacon in the rebuilt square; the knight leaves through the arch.
        private static readonly Vector2[] SquareWindows = { P(733f, 45f), P(1575f, 170f), P(1640f, 780f) };

        private void VillageBeacon()
        {
            for (int i = 0; i < SquareWindows.Length; i++) fx.Lamp(SquareWindows[i], 0.6f, 0.75f, 80 + i);
            fx.Spill(P(960f, 620f), P(330f, 130f), new Color(1f, 0.72f, 0.36f), 0.22f, 4);
            fx.Beacon(P(1113f, 225f), 1f, 1f, 4);
            fx.Blade(P(362f, 212f), P(395f, 287f), 3.5f, 0.8f, 4);
        }

        // 03B: the hero fights on beside a new beacon while a chain of lit beacons stretches across the mountains.
        private static readonly Vector2[] BeaconChain = { P(598f, 140f), P(835f, 185f), P(1028f, 238f), P(1322f, 245f), P(1478f, 280f), P(1612f, 232f) };

        private void BeaconWar()
        {
            fx.Spill(P(300f, 520f), P(260f, 110f), new Color(1f, 0.72f, 0.36f), 0.2f, 9);
            fx.Beacon(P(300f, 140f), 0.85f, 1f, 9);
            for (int i = 0; i < BeaconChain.Length; i++)
                fx.Beacon(BeaconChain[i], 0.09f + 0.02f * (BeaconChain.Length - i) / BeaconChain.Length, 0.8f, 10 + i);
            fx.Blade(P(800f, 525f), P(1085f, 700f), 9f, 1f, 9);
            fx.Burst(P(1040f, 520f), 18, 110f, Gold, 2.6f, 9);
        }

        // 04A: the hero at the head of the throne hall, the Demon King's eyes waiting in the dark.
        private void ThroneHall()
        {
            fx.Motes(P(836f, 300f), P(700f, 260f), 26, new Color(0.7f, 0.76f, 0.9f, 0.55f), P(0f, 30f), 16f, 0.8f, 12f, 18);
            fx.Spill(P(800f, 790f), P(160f, 40f), new Color(1f, 0.78f, 0.44f), 0.2f, 10);
            fx.Blade(P(745f, 600f), P(880f, 780f), 7f, 0.85f, 10);
            var red = new Color(1f, 0.22f, 0.08f);
            fx.Eye(P(1082f, 319f), 1.1f, red, 4);
            fx.Eye(P(1092f, 318f), 1.1f, red, 4);
        }

        // 04: sword meets Demon King and the light keeps swelling, building toward the white flash into 04B.
        private void LastBattle()
        {
            float surge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 6f));
            fx.Glow(P(930f, 395f), P(520f, 420f) * (0.8f + 0.4f * surge), new Color(1f, 0.92f, 0.74f, 0.08f + 0.14f * surge));
            fx.Beacon(P(930f, 395f), 1.6f + 0.7f * surge, 1.1f + 0.35f * surge, 5);
            fx.Burst(P(930f, 395f), 36, 320f + 120f * surge, Gold, 3.2f - 0.8f * surge, 5);
            fx.Blade(P(785f, 465f), P(930f, 400f), 6f, 1f, 5);
        }

        // 04B: the light has lifted; a last pale glow fades where it burst and dust settles over the empty hall.
        private void EmptyCastle()
        {
            float afterglow = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 5f));
            fx.Glow(P(930f, 400f), P(260f, 220f), new Color(1f, 0.95f, 0.84f, 0.06f + 0.22f * afterglow));
            fx.Motes(P(930f, 430f), P(380f, 240f), 34, new Color(0.95f, 0.92f, 0.82f, 0.7f), P(0f, 45f), 14f, 0.9f, 10f, 17);
        }

        // 05: peace again — the beacon burns low as the promise fades; the market glows.
        private static readonly Vector2[] MarketLamps = { P(637f, 570f), P(1460f, 555f) };
        private static readonly Vector2[] MarketWindows = { P(1597f, 370f), P(1065f, 425f), P(1090f, 425f), P(1255f, 585f), P(965f, 625f) };

        private void ForgottenPromise()
        {
            for (int i = 0; i < MarketLamps.Length; i++) fx.Lamp(MarketLamps[i], 1f, 1f, 100 + i);
            for (int i = 0; i < MarketWindows.Length; i++) fx.Lamp(MarketWindows[i], 0.6f, 0.7f, 110 + i);
            fx.Beacon(P(355f, 215f), 0.55f, 0.45f, 6);
        }

        // 05A: a lit window above, scattered coins catching the light below.
        private static readonly Vector2[] Coins = { P(585f, 744f), P(625f, 753f), P(552f, 759f), P(599f, 766f), P(627f, 781f), P(582f, 795f) };

        private void Greed()
        {
            fx.Lamp(P(684f, 75f), 0.7f, 0.8f, 1);
            for (int i = 0; i < Coins.Length; i++) fx.Glint(Coins[i], 7f, Warm, 4.5f + i * 0.7f, 120 + i);
        }

        // 05B: a brawl under the lantern, faint darkness gathering at the fighters' feet.
        private static readonly Vector2[] BrawlFeet = { P(480f, 780f), P(620f, 700f), P(760f, 760f), P(900f, 720f), P(1050f, 790f) };
        private static readonly Vector2[] BrawlWindows = { P(72f, 288f), P(825f, 150f), P(1088f, 380f), P(380f, 490f) };

        private void Hatred()
        {
            fx.DarkWisps(BrawlFeet, 14, 120f, 1f, 0.55f, 13);
            for (int i = 0; i < BrawlWindows.Length; i++) fx.Lamp(BrawlWindows[i], 0.5f, 0.6f, 130 + i);
            fx.Lamp(P(1193f, 160f), 1.3f, 1.1f, 2);
        }

        // 06: the beacon is dark; fog crawls over the graves and the dead open violet eyes.
        private static readonly Vector2[] UndeadEyes =
        {
            P(908f, 622f), P(930f, 628f), P(1256f, 467f), P(1507f, 488f), P(1521f, 492f),
            P(655f, 566f), P(662f, 566f), P(772f, 583f), P(778f, 583f), P(1028f, 571f), P(1035f, 571f),
        };

        private void DarknessReturns()
        {
            var mist = new Color(0.6f, 0.62f, 0.74f);
            fx.Fog(P(900f, 560f), P(620f, 45f), 6, mist, 0.1f, 10f, 1);
            fx.Fog(P(1000f, 650f), P(720f, 70f), 9, mist, 0.14f, 14f, 2);
            for (int i = 0; i < UndeadEyes.Length; i++)
                fx.Eye(UndeadEyes[i], i < 5 ? 1f : 0.7f, UndeadViolet, i / 2);
        }

        // 07: shafts of light pour through the clouds and the undead crumble where they land.
        private void LightStrikes()
        {
            fx.Shaft(P(470f, 0f), P(420f, 650f), 45f, 130f, 0.35f, 1, 14);
            fx.Shaft(P(880f, 0f), P(730f, 690f), 55f, 130f, 0.35f, 2, 14);
            fx.Shaft(P(990f, 100f), P(1060f, 520f), 30f, 80f, 0.3f, 3, 8);
            fx.Shaft(P(1270f, 0f), P(1420f, 660f), 55f, 120f, 0.35f, 4, 14);
            fx.Burst(P(360f, 640f), 10, 60f, Gold, 2.4f, 21);
            fx.Burst(P(670f, 705f), 10, 60f, Gold, 2.4f, 22);
            fx.Burst(P(1170f, 710f), 10, 60f, Gold, 2.4f, 23);
            fx.Burst(P(1290f, 740f), 10, 60f, Gold, 2.4f, 24);
        }

        // 08: the last of the darkness leaves the fallen; light glances off the new hero's armour.
        private static readonly Vector2[] FallenSkeleton = { P(1180f, 520f), P(1300f, 480f), P(1420f, 560f), P(1550f, 600f), P(1280f, 640f) };

        private void FirstStep()
        {
            fx.DarkWisps(FallenSkeleton, 22, 110f, 1.2f, 0.8f, 15);
            fx.Glint(P(372f, 548f), 14f, Warm, 6f, 141);
            fx.Glint(P(782f, 640f), 18f, Warm, 7f, 142);
            fx.Glint(P(1395f, 82f), 28f, Gold, 5f, 143);
        }

        // 07(ForgottenBeacon): the hero's staff glows blue beside the dead beacon she touches.
        private void ForgottenBeacon()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(elapsed * 1.1f);
            fx.Glow(P(512f, 466f), P(26f, 26f), new Color(0.5f, 0.72f, 1f, 0.16f * pulse));
            fx.MagicGem(P(730f, 449f), 1.15f, 7);
        }

        // 09: fog in the valley, the staff's blue light, and far beacons waiting to be lit.
        private static readonly Vector2[] FarBeacons = { P(540f, 352f), P(940f, 292f), P(1482f, 196f), P(1555f, 432f) };

        private void Journey()
        {
            var mist = new Color(0.62f, 0.64f, 0.74f);
            fx.Fog(P(650f, 520f), P(350f, 70f), 7, mist, 0.12f, 9f, 3);
            fx.Fog(P(1350f, 600f), P(200f, 40f), 4, mist, 0.1f, 7f, 4);
            for (int i = 0; i < FarBeacons.Length; i++) fx.Glint(FarBeacons[i], 14f, Warm, 7f + i * 1.3f, 150 + i);
            fx.MagicGem(P(400f, 543f), 0.9f, 8);
        }
    }
}
