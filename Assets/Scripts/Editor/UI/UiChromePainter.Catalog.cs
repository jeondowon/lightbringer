using System;

namespace Lightbringer.EditorTools
{
    public static partial class UiChromePainter
    {
        // Every texture under Assets/UI/Art/Chrome. Button heights must match the USS heights, and panel, dialog and
        // card slice widths the Story*Slice constants (see LightbringerHUD.uss).
        public static void PaintAll(Action<string, UiImage> save)
        {
            save("Ui_Panel", StoryPanel(StoryPanelSize, StoryPanelSlice, false));
            save("Ui_Dialog", StoryPanel(StoryDialogSize, StoryDialogSlice, true));
            save("Ui_Card", StoryCard(StoryCardSize, StoryCardSlice, false));
            save("Ui_CardActive", StoryCard(StoryCardSize, StoryCardSlice, true));
            save("Ui_BtnPrimary", StoryButton(320, 56, ButtonPrimary));
            save("Ui_BtnPrimary44", StoryButton(320, 44, ButtonPrimary));
            save("Ui_BtnSecondary", StoryButton(320, 44, ButtonSecondary));
            save("Ui_BtnSecondary32", StoryButton(240, 32, ButtonSecondary));
            save("Ui_BtnDanger", StoryButton(320, 48, ButtonDanger));
            save("Ui_Toast", StoryToast(512, 44));
            save("Ui_Gloss", StoryGaugeFill(128, 32));
            save("Ui_Divider", StoryDivider(512, 24));
            save("Ui_Shard", Shard(64));
            save("Ui_Medallion", Medallion(256, 4));
            save("Ui_Socket", Medallion(160, 8));
            save("Ui_Rays", StoryGlow(512));
            save("Ui_Seal", StorySeal(256));
            save("Ui_HudBanner", StoryHudBanner(1024, 136, 84));
            save("Ui_Tag", StoryTag(112, 28, 0));
            save("Ui_TagActive", StoryTag(112, 28, 1));
            save("Ui_TagAlert", StoryTag(112, 28, 2));
            save("Ui_IconAlly", StoryIcon(64, 0));
            save("Ui_IconEnemy", StoryIcon(64, 1));
            save("Ui_IconWave", StoryIcon(64, 2));
            save("Map_Compass", Compass(192));
            save("Map_Parchment", StoryMapPage(244, 270, 8));
            save("Map_BeaconDark", StoryBeacon(0));
            save("Map_BeaconReady", StoryBeacon(1));
            save("Map_BeaconLit", StoryBeacon(2));
        }
    }
}
