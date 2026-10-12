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
        // Trace the actual painted streams from the villagers down their clothes and across the lane.
        private static readonly Vector2[] QuarrelInkLeft =
        {
            P(259f, 292f), P(283f, 330f), P(321f, 360f), P(332f, 395f), P(359f, 432f),
            P(400f, 464f), P(430f, 496f), P(424f, 532f), P(452f, 557f), P(488f, 583f),
            P(494f, 627f), P(523f, 666f), P(541f, 707f), P(581f, 742f), P(637f, 767f),
            P(702f, 789f), P(813f, 798f), P(915f, 785f), P(1017f, 800f), P(1101f, 819f),
            P(1203f, 822f), P(1306f, 803f), P(1410f, 799f), P(1512f, 774f), P(1594f, 757f),
        };
        private static readonly Vector2[] QuarrelInkWoman =
        {
            P(1171f, 511f), P(1191f, 552f), P(1200f, 599f), P(1202f, 642f),
            P(1224f, 681f), P(1260f, 711f), P(1310f, 737f), P(1376f, 757f), P(1450f, 774f),
        };
        private static readonly Vector2[] QuarrelInkMan =
        {
            P(1388f, 477f), P(1376f, 520f), P(1354f, 563f), P(1333f, 608f),
            P(1334f, 644f), P(1355f, 681f), P(1351f, 720f), P(1390f, 746f), P(1450f, 768f),
        };
        private static readonly Vector2[] QuarrelInkFar =
        {
            P(1576f, 558f), P(1564f, 601f), P(1570f, 645f), P(1551f, 684f),
            P(1562f, 721f), P(1600f, 744f), P(1633f, 776f), P(1608f, 797f), P(1510f, 814f),
        };
        // Second caption: the separate streams gather and creep along the street out of the picture.
        private static readonly Vector2[] GatheringLane =
        {
            P(637f, 767f), P(702f, 789f), P(813f, 798f), P(915f, 785f), P(1017f, 800f),
            P(1101f, 819f), P(1203f, 822f), P(1306f, 803f), P(1410f, 799f),
            P(1512f, 774f), P(1594f, 757f), P(1660f, 751f), P(1730f, 737f),
        };
        private static readonly Vector2[] GatheringJunction =
        {
            P(1351f, 720f), P(1390f, 746f), P(1450f, 774f), P(1510f, 797f),
            P(1580f, 790f), P(1633f, 776f), P(1690f, 754f), P(1730f, 737f),
        };

        private void BirthOfDarkness()
        {
            // Keep the same moving strands and phase across both captions; the second thickens them
            // and feeds a wider current along the painted lane instead of replacing them with faint smoke.
            float gather = captionIndex >= 1 ? CaptionRamp(1.5f) : 0f;
            fx.DarkInkFlow(QuarrelInkLeft, Mathf.Lerp(23f, 33f, gather), 68f, 1f, 51);
            fx.DarkInkFlow(QuarrelInkWoman, Mathf.Lerp(17f, 25f, gather), 53f, 1f, 52);
            fx.DarkInkFlow(QuarrelInkMan, Mathf.Lerp(15f, 23f, gather), 58f, 1f, 53);
            fx.DarkInkFlow(QuarrelInkFar, Mathf.Lerp(15f, 22f, gather), 50f, 0.9f, 54);
            fx.DarkWisps(QuarrelStream, 40, 70f, 1f, 1f, 7);
            fx.DarkWisps(QuarrelWoman, 8, 60f, 0.8f, 0.8f, 8);
            fx.DarkWisps(QuarrelMan, 8, 60f, 0.8f, 0.8f, 9);
            if (captionIndex >= 1)
            {
                fx.DarkInkFlow(GatheringLane, 34f, 84f, gather, 55);
                fx.DarkInkFlow(GatheringJunction, 25f, 76f, gather, 56);
            }
            fx.Lamp(P(213f, 318f), 1.1f, 1.1f, 1);
            fx.Lamp(P(1105f, 385f), 0.6f, 0.7f, 2);
            fx.Lamp(P(1580f, 290f), 0.6f, 0.7f, 3);
            fx.Lamp(P(1500f, 540f), 0.5f, 0.6f, 4);
        }

        // 00D: the Demon King's red eyes, darkness boiling off its body.
        private static readonly Vector2[] DemonBody = { P(900f, 600f), P(1000f, 450f), P(1100f, 260f), P(1200f, 450f), P(1350f, 600f) };
        private static readonly Vector2[] DemonVillage = { P(60f, 560f), P(100f, 568f), P(170f, 572f), P(205f, 578f) };

        // Trace the painted charcoal currents into the body, then curl around its lower silhouette,
        // chest and shoulders. The crown and red eyes stay clear of the added ink.
        private static readonly Vector2[] DemonFlowLeft =
        {
            P(-40f, 659f), P(70f, 646f), P(154f, 669f), P(206f, 655f), P(264f, 680f),
            P(321f, 692f), P(366f, 720f), P(406f, 709f), P(464f, 741f), P(511f, 727f),
            P(574f, 746f), P(624f, 768f), P(685f, 738f), P(746f, 705f), P(804f, 668f),
            P(850f, 626f), P(909f, 588f), P(961f, 547f), P(1007f, 498f), P(1052f, 439f),
        };
        private static readonly Vector2[] DemonFlowLow =
        {
            P(-40f, 850f), P(61f, 818f), P(131f, 819f), P(214f, 848f), P(289f, 833f),
            P(365f, 814f), P(449f, 829f), P(522f, 813f), P(594f, 781f), P(664f, 759f),
            P(723f, 718f), P(787f, 704f), P(829f, 665f), P(883f, 656f), P(923f, 615f),
            P(954f, 569f), P(986f, 531f), P(1009f, 478f), P(1050f, 424f), P(1080f, 365f),
        };
        private static readonly Vector2[] DemonFlowRight =
        {
            P(1712f, 560f), P(1630f, 543f), P(1567f, 563f), P(1501f, 537f),
            P(1440f, 520f), P(1390f, 485f), P(1332f, 477f), P(1280f, 436f),
            P(1246f, 404f), P(1204f, 379f), P(1176f, 343f), P(1140f, 318f),
        };
        private static readonly Vector2[] DemonLowerWrap =
        {
            P(888f, 685f), P(938f, 651f), P(976f, 604f), P(960f, 559f),
            P(982f, 521f), P(1028f, 498f), P(1090f, 485f), P(1152f, 471f),
            P(1210f, 441f), P(1240f, 400f), P(1231f, 361f), P(1200f, 321f),
        };
        private static readonly Vector2[] DemonChestWrap =
        {
            P(1320f, 509f), P(1279f, 468f), P(1232f, 451f), P(1183f, 421f),
            P(1127f, 402f), P(1070f, 373f), P(1030f, 338f), P(1026f, 302f),
            P(1058f, 267f), P(1095f, 247f), P(1140f, 230f), P(1180f, 204f),
        };
        private static readonly Vector2[] DemonShoulderWrap =
        {
            P(809f, 384f), P(849f, 347f), P(884f, 311f), P(933f, 284f),
            P(969f, 249f), P(1010f, 227f), P(1055f, 209f), P(1085f, 230f),
            P(1123f, 236f), P(1160f, 213f), P(1200f, 237f), P(1240f, 270f),
            P(1284f, 287f), P(1320f, 323f), P(1362f, 363f), P(1389f, 412f),
        };

        private void DemonKingBirth()
        {
            fx.DarkInkFlow(DemonFlowLeft, 34f, 76f, 1f, 61);
            fx.DarkInkFlow(DemonFlowLow, 40f, 82f, 1f, 62);
            fx.DarkInkFlow(DemonFlowRight, 32f, 67f, 1f, 63);
            fx.DarkInkFlow(DemonLowerWrap, 38f, 72f, 1f, 64);
            fx.DarkInkFlow(DemonChestWrap, 30f, 64f, 1f, 65);
            fx.DarkInkFlow(DemonShoulderWrap, 28f, 60f, 1f, 66);
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

        // 05A: darkness leaks from the thief and the two passers-by who turn away, gathering along
        // the cobbles. Keep faces, the stolen purse, the victim's reaching hand and the coins readable.
        private static readonly Vector2[] Coins = { P(585f, 744f), P(625f, 753f), P(552f, 759f), P(599f, 766f), P(627f, 781f), P(582f, 795f) };
        private static readonly Vector2[] GreedThiefCloak =
        {
            P(1260f, 292f), P(1229f, 316f), P(1190f, 338f), P(1155f, 365f),
            P(1130f, 406f), P(1090f, 452f), P(1074f, 489f), P(1040f, 531f),
            P(1021f, 578f), P(981f, 623f), P(930f, 651f), P(867f, 669f),
            P(810f, 691f), P(755f, 700f), P(710f, 682f),
        };
        private static readonly Vector2[] GreedThiefHem =
        {
            P(1306f, 428f), P(1320f, 477f), P(1301f, 524f), P(1274f, 563f),
            P(1290f, 608f), P(1263f, 653f), P(1250f, 702f), P(1228f, 747f),
            P(1242f, 785f), P(1290f, 808f), P(1355f, 811f), P(1420f, 788f),
            P(1480f, 779f), P(1540f, 795f), P(1624f, 783f), P(1720f, 754f),
        };
        private static readonly Vector2[] GreedPasserMan =
        {
            P(587f, 336f), P(566f, 370f), P(571f, 410f), P(594f, 443f),
            P(582f, 482f), P(594f, 524f), P(590f, 563f), P(616f, 591f),
            P(665f, 603f), P(716f, 627f), P(771f, 638f), P(818f, 665f), P(867f, 669f),
        };
        private static readonly Vector2[] GreedPasserWoman =
        {
            P(707f, 348f), P(726f, 381f), P(720f, 421f), P(702f, 457f),
            P(709f, 497f), P(730f, 534f), P(726f, 570f), P(751f, 601f),
            P(793f, 623f), P(818f, 665f), P(867f, 669f),
        };
        private static readonly Vector2[] GreedStreet =
        {
            P(716f, 627f), P(771f, 638f), P(818f, 665f), P(867f, 669f),
            P(930f, 687f), P(946f, 724f), P(975f, 760f), P(1020f, 788f),
            P(1100f, 790f), P(1160f, 822f), P(1230f, 834f), P(1290f, 808f),
            P(1355f, 811f), P(1420f, 788f), P(1480f, 779f), P(1540f, 795f),
            P(1624f, 783f), P(1720f, 754f),
        };

        private void Greed()
        {
            float darkness = 0.6f + 0.4f * CaptionRamp(2f);
            fx.DarkInkFlow(GreedThiefCloak, 28f, 66f, darkness, 71);
            fx.DarkInkFlow(GreedThiefHem, 25f, 61f, darkness, 72);
            fx.DarkInkFlow(GreedPasserMan, 17f, 49f, darkness, 73);
            fx.DarkInkFlow(GreedPasserWoman, 17f, 53f, darkness, 74);
            fx.DarkInkFlow(GreedStreet, 34f, 76f, darkness, 75);
            fx.Lamp(P(684f, 75f), 0.7f, 0.8f, 1);
            for (int i = 0; i < Coins.Length; i++) fx.Glint(Coins[i], 7f, Warm, 4.5f + i * 0.7f, 120 + i);
        }

        // 05B: hatred seeps from both fighters' chests, coils down their clothes and joins on the
        // street. Keep their faces and hands, the intervening man and the sheltered child unobscured.
        private static readonly Vector2[] BrawlFeet = { P(480f, 780f), P(620f, 700f), P(760f, 760f), P(900f, 720f), P(1050f, 790f) };
        private static readonly Vector2[] BrawlWindows = { P(72f, 288f), P(825f, 150f), P(1088f, 380f), P(380f, 490f) };
        private static readonly Vector2[] BrawlLeftInk =
        {
            P(640f, 347f), P(612f, 381f), P(578f, 416f), P(570f, 457f),
            P(599f, 496f), P(613f, 538f), P(658f, 569f), P(704f, 606f),
            P(730f, 652f), P(748f, 695f), P(752f, 740f), P(794f, 777f),
            P(852f, 786f), P(910f, 772f),
        };
        private static readonly Vector2[] BrawlLeftHem =
        {
            P(574f, 457f), P(535f, 488f), P(508f, 528f), P(474f, 555f),
            P(489f, 597f), P(463f, 635f), P(448f, 677f), P(417f, 714f),
            P(406f, 758f), P(388f, 807f), P(421f, 840f), P(480f, 851f),
            P(551f, 831f), P(624f, 816f), P(696f, 827f),
        };
        private static readonly Vector2[] BrawlRightInk =
        {
            P(960f, 389f), P(991f, 426f), P(1004f, 465f), P(979f, 501f),
            P(947f, 539f), P(968f, 579f), P(944f, 614f), P(903f, 645f),
            P(921f, 683f), P(891f, 716f), P(880f, 750f), P(910f, 772f),
            P(977f, 778f), P(1038f, 761f), P(1104f, 742f),
        };
        private static readonly Vector2[] BrawlStreetInk =
        {
            P(298f, 842f), P(365f, 826f), P(421f, 840f), P(480f, 851f),
            P(551f, 831f), P(624f, 816f), P(696f, 827f), P(766f, 813f),
            P(823f, 801f), P(871f, 775f), P(910f, 772f), P(977f, 778f),
            P(1038f, 761f), P(1104f, 742f), P(1150f, 710f), P(1168f, 665f),
        };

        private void Hatred()
        {
            float darkness = 0.65f + 0.35f * CaptionRamp(1.8f);
            fx.DarkInkFlow(BrawlLeftInk, 29f, 70f, darkness, 81);
            fx.DarkInkFlow(BrawlLeftHem, 25f, 65f, darkness, 82);
            fx.DarkInkFlow(BrawlRightInk, 31f, 74f, darkness, 83);
            fx.DarkInkFlow(BrawlStreetInk, 37f, 82f, darkness, 84);
            fx.DarkWisps(BrawlFeet, 24, 100f, 1.2f, 0.85f, 13);
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
            fx.Glint(P(482f, 552f), 14f, Warm, 6f, 141);
            fx.Glint(P(896f, 661f), 18f, Warm, 7f, 142);
            fx.Glint(P(1395f, 82f), 28f, Gold, 5f, 143);
        }

        // 07(ForgottenBeacon): first show the dead crystal, then carry light from her hand up the
        // column. The crystal ignites once and stays lit; revisiting the page restarts the awakening.
        private void ForgottenBeacon()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(elapsed * 1.1f);
            float charge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 2.35f, elapsed));
            float lit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.1f, 3.7f, elapsed));
            float ignition = Mathf.Sin(Mathf.InverseLerp(2.1f, 4.1f, elapsed) * Mathf.PI);
            fx.Glow(P(510f, 480f), P(36f, 36f), new Color(0.65f, 0.78f, 1f, (0.14f + charge * 0.3f) * pulse));
            Vector2 risingLight = Vector2.Lerp(P(475f, 478f), P(460f, 224f), charge);
            fx.Glow(risingLight, P(22f, 42f), new Color(1f, 0.84f, 0.54f, charge * (1f - lit) * 0.75f));
            fx.Spill(P(461f, 425f), P(86f, 205f), Gold, lit * 0.16f, 71);
            fx.Spill(P(466f, 756f), P(250f, 90f), Gold, lit * 0.14f, 72);
            fx.Spill(P(558f, 519f), P(95f, 120f), Warm, lit * 0.1f, 73);
            fx.Beacon(P(460f, 224f), 1.35f, lit * (1.8f + 0.45f * ignition), 74);
            // The broad round source brightens with the original ignition timing, then stays lit.
            fx.Glow(P(460f, 224f), P(118f, 118f), new Color(1f, 0.76f, 0.35f, lit * 0.65f));
            fx.Glow(P(460f, 224f), P(72f, 72f), new Color(1f, 0.93f, 0.68f, lit * 0.98f));
            fx.Glow(P(460f, 224f), P(48f, 48f), new Color(1f, 0.99f, 0.9f, lit));
            fx.MagicGem(P(730f, 449f), 1.15f, 7);
        }

        // 09: the beacon beside her stays lit after its awakening. All distant beacons remain dark.
        private void Journey()
        {
            var mist = new Color(0.62f, 0.64f, 0.74f);
            fx.Fog(P(650f, 520f), P(350f, 70f), 7, mist, 0.12f, 9f, 3);
            fx.Fog(P(1350f, 600f), P(200f, 40f), 4, mist, 0.1f, 7f, 4);
            fx.Beacon(P(66f, 265f), 1.15f, 1.8f, 75);
            // A broad, steady round source covers the dark crystal even at the flicker's minimum.
            fx.Glow(P(66f, 265f), P(118f, 118f), new Color(1f, 0.76f, 0.35f, 0.65f));
            fx.Glow(P(66f, 265f), P(72f, 72f), new Color(1f, 0.93f, 0.68f, 0.98f));
            fx.Glow(P(66f, 265f), P(48f, 48f), new Color(1f, 0.99f, 0.9f, 1f));
            fx.MagicGem(P(400f, 543f), 0.9f, 8);
        }
    }
}
