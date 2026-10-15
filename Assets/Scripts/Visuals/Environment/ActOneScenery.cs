using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Act 1 (stages 2-8) stage identity from existing art only: TriForge forest pieces, the battlefield props and the
    // Act 2 stone landmarks, plus a lighting/panorama variation per stage. The journey runs from the bright frontier
    // coast toward the Warlord's citadel, darkening from stage 6 on. Stage 1 keeps its authored glade untouched.
    // Only battlefield-local copies of the palette and backdrop are edited.
    public sealed class ActOneScenery : MonoBehaviour
    {
        private readonly List<Object> owned = new List<Object>();
        private ArtStyleLibrary palette;
        private ForestEnvironmentProfile forest;
        private BattlefieldEnvironment terrain;
        private System.Random random;
        public int Stage { get; private set; }
        public int Landmarks { get; private set; }
        public static bool AppliesTo(int stage) => stage >= 2 && stage <= 8;

        public static ArtStyleLibrary CreatePalette(Transform root, ArtStyleLibrary source, int stage)
        {
            if (!AppliesTo(stage) || source == null) return source;
            ActOneScenery scenery = root.gameObject.AddComponent<ActOneScenery>();
            scenery.Stage = stage;
            ArtStyleLibrary s = scenery.palette = scenery.Own(Instantiate(source), "Act One Stage " + stage + " Palette");
            BattlefieldBackdropProfile backdrop = source.FindBackdrop(stage);
            if (backdrop != null)
            {
                backdrop = scenery.Own(Instantiate(backdrop), backdrop.name + " (Stage " + stage + ")");
                // Turn the shared forest-valley panorama so each stage looks out on a different stretch of the region.
                backdrop.rotation = Mathf.Repeat(backdrop.rotation + (stage - 1) * 47f, 360f);
                s.backdrops = new[] { backdrop };
            }
            scenery.forest = source.forestEnvironment != null && source.forestEnvironment.AppliesTo(stage)
                ? source.forestEnvironment
                : backdrop != null && backdrop.forest != null && backdrop.forest.IsReady ? backdrop.forest : null;
            switch (stage)
            {
                case 2: // Coastal hill: clear, high sun, sea haze.
                    s.sunEuler = new Vector3(46f, -60f, 0f);
                    s.sunIntensity *= 1.05f;
                    s.cloudShadowStrength *= .7f;
                    Haze(s, backdrop, new Color(.62f, .7f, .78f), 1.1f);
                    break;
                case 3: // Deep forest: dim, green-tinted shade.
                    s.sunEuler = new Vector3(52f, 20f, 0f);
                    s.sunIntensity *= .8f;
                    s.ambientSky = Color.Lerp(s.ambientSky, new Color(.36f, .5f, .38f), .45f);
                    s.ambientEquator = Color.Lerp(s.ambientEquator, new Color(.3f, .4f, .3f), .4f);
                    s.cloudShadowStrength = Mathf.Min(1f, s.cloudShadowStrength * 1.5f);
                    Haze(s, backdrop, new Color(.42f, .5f, .45f), .75f);
                    break;
                case 4: // Frontier wall: plain afternoon.
                    s.sunEuler = new Vector3(34f, 25f, 0f);
                    break;
                case 5: // Knights' camp: warm late afternoon.
                    s.sunEuler = new Vector3(26f, -80f, 0f);
                    s.sunColor = Color.Lerp(s.sunColor, new Color(1f, .8f, .58f), .5f);
                    Haze(s, backdrop, new Color(.68f, .64f, .58f), 1f);
                    break;
                case 6: // Corrupted pass: low sunset, the Warlord's shadow begins.
                    s.sunEuler = new Vector3(17f, 70f, 0f);
                    s.sunColor = new Color(1f, .7f, .5f);
                    s.sunIntensity *= .9f;
                    s.ambientSky *= .85f;
                    s.ambientEquator = Color.Lerp(s.ambientEquator, new Color(.5f, .38f, .34f), .35f);
                    Haze(s, backdrop, new Color(.6f, .5f, .48f), .9f, .9f);
                    break;
                case 7: // Citadel approach: dusk under gathering cloud.
                    s.sunEuler = new Vector3(22f, 110f, 0f);
                    s.sunColor = new Color(.86f, .76f, .78f);
                    s.sunIntensity *= .72f;
                    s.ambientSky *= .75f;
                    s.ambientEquator *= .8f;
                    s.cloudShadowStrength = Mathf.Min(1f, s.cloudShadowStrength * 1.6f);
                    s.grassLight = Color.Lerp(s.grassLight, s.dirt, .2f);
                    Haze(s, backdrop, new Color(.46f, .44f, .5f), .85f, .8f);
                    break;
                case 8: // The black citadel: dark, cold and violet, leading into Act 2's overcast.
                    s.sunEuler = new Vector3(30f, 140f, 0f);
                    s.sunColor = new Color(.74f, .74f, .88f);
                    s.sunIntensity *= .6f;
                    s.ambientSky = Color.Lerp(s.ambientSky, new Color(.3f, .3f, .42f), .55f);
                    s.ambientEquator = Color.Lerp(s.ambientEquator, new Color(.28f, .26f, .32f), .5f);
                    s.ambientGround *= .8f;
                    s.cloudShadowStrength = Mathf.Min(1f, s.cloudShadowStrength * 1.8f);
                    s.grassLight = Color.Lerp(s.grassLight, s.dirt, .3f);
                    s.grassDark = Color.Lerp(s.grassDark, s.corruptStone, .2f);
                    Haze(s, backdrop, new Color(.32f, .31f, .38f), .8f, .65f);
                    break;
            }
            return s;
        }

        // Fog colour/distance live on the backdrop when there is one (BattlefieldStyling prefers it).
        private static void Haze(ArtStyleLibrary s, BattlefieldBackdropProfile backdrop, Color fog, float distance,
            float exposure = 1f)
        {
            s.fogColor = fog;
            s.fogStart *= distance;
            s.fogEnd *= distance;
            if (backdrop == null) return;
            backdrop.fogColor = fog;
            backdrop.fogStart *= distance;
            backdrop.fogEnd *= distance;
            backdrop.exposure *= exposure;
        }

        private T Own<T>(T item, string name) where T : Object
        {
            item.name = name;
            item.hideFlags = HideFlags.DontSave;
            owned.Add(item);
            return item;
        }

        // Called after the shared camps and shrine ruins, before the forest, so the forest grows around these.
        internal void Dress(BattlefieldEnvironment environment)
        {
            terrain = environment;
            random = new System.Random(Stage * 6151);
            switch (Stage)
            {
                case 2: // 해안 감시탑 언덕: the old coastal watchtower on its rocky hill.
                    Tower("Coastal Watchtower", new Vector2(18f, 30f), 5.5f, -110f);
                    Prop(BattlefieldPropKind.RuinColumn, new Vector2(12f, 38f), 1f, 20f);
                    Prop(BattlefieldPropKind.RuinColumn, new Vector2(24f, 22f), .8f, -35f);
                    Wall("Fallen Lookout Wall", new Vector2(-19f, 48f), 3.5f, 15f);
                    Boulders(14, 2f, 3.6f);
                    Scatter(forest != null ? forest.stone : null, 40, .8f, 1.6f, 5f);
                    break;
                case 3: // 어둠이 고인 숲길: two trails through close woodland and a woodcutters' clearing.
                    Grove(26, .75f, 1.1f, 7.5f);
                    Prop(BattlefieldPropKind.AlliedChest, new Vector2(0f, 21f), 1f, 20f);
                    Prop(BattlefieldPropKind.AlliedBarrel, new Vector2(1.6f, 22.5f), 1f, -10f);
                    Prop(BattlefieldPropKind.AlliedBarrel, new Vector2(-1.4f, 23f), .9f, 35f);
                    Scatter(forest != null ? forest.sapling : null, 45, .8f, 1.4f, 6f);
                    Scatter(forest != null ? forest.plant : null, 120, .8f, 1.5f, 4.5f);
                    break;
                case 4: // 무너진 국경 성벽: the old frontier wall, broken where the two roads pass.
                    Wall("Frontier Wall Centre", new Vector2(0f, 44f), 4.5f, 0f);
                    Wall("Frontier Wall West", new Vector2(-25f, 44f), 4.5f, 180f);
                    Wall("Frontier Wall East", new Vector2(25f, 44f), 4.5f, 0f);
                    Prop(BattlefieldPropKind.RuinArch, new Vector2(0f, 20f), 1f, 0f);
                    Tower("Frontier Gate Tower", new Vector2(-28f, 62f), 4.5f, 60f);
                    Scatter(forest != null ? forest.stone : null, 30, .7f, 1.4f, 5f);
                    break;
                case 5: // 버려진 기사단 야영지: the order's abandoned camp around its memorial.
                    Prop(BattlefieldPropKind.AlliedMonument, new Vector2(0f, 31f), 1.4f, 180f);
                    Supplies(new Vector2(-3.5f, 25f));
                    Supplies(new Vector2(4f, 37f));
                    Supplies(new Vector2(-28f, 34f));
                    Supplies(new Vector2(28f, 26f));
                    Prop(BattlefieldPropKind.RuinArch, new Vector2(0f, 49f), 1.1f, 0f);
                    Scatter(forest != null ? forest.plant : null, 60, .8f, 1.4f, 4.5f);
                    break;
                case 6: // 타락한 제단 고개: the Warlord's altars among the boulders of the pass.
                    Prop(BattlefieldPropKind.EnemyAltar, new Vector2(-11f, 32f), 1.3f, 160f);
                    Prop(BattlefieldPropKind.EnemyAltar, new Vector2(11f, 40f), 1.3f, 200f);
                    Fences(new Vector2(-11f, 32f), 4.5f);
                    Fences(new Vector2(11f, 40f), 4.5f);
                    Boulders(12, 2.4f, 4f);
                    break;
                case 7: // 검은 성채 진입로: enemy-held watchtowers guarding the last road to the citadel.
                    Tower("Approach Watchtower West", new Vector2(-29f, 48f), 5f, 75f);
                    Tower("Approach Watchtower East", new Vector2(29f, 40f), 5f, -75f);
                    Wall("Broken Approach Wall", new Vector2(-29f, 18f), 3.5f, 90f);
                    Fences(new Vector2(-29f, 48f), 6.5f);
                    Fences(new Vector2(29f, 40f), 6.5f);
                    Boulders(8, 2f, 3.4f);
                    break;
                case 8: // 검은 성채: corruption spreading out from the Warlord's citadel.
                    Prop(BattlefieldPropKind.EnemyAltar, new Vector2(10f, 46f), 1.3f, 180f);
                    Fences(new Vector2(10f, 46f), 4.5f);
                    Wall("Siege Ruins West", new Vector2(-29f, 30f), 4f, 80f);
                    Wall("Siege Ruins East", new Vector2(29f, 24f), 4f, -80f);
                    break;
            }
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        // Nearest clear ground to the desired spot: the whole footprint stays off every Path, gate and keep.
        private bool FindSite(Vector2 desired, float radius, out Vector2 site)
        {
            for (int i = 0; i < 81; i++)
            {
                int ring = i == 0 ? 0 : (i - 1) / 8 + 1;
                float angle = (i % 8) * Mathf.PI * .25f;
                site = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 1.5f;
                if (Mathf.Abs(site.x) + radius > 34f || !terrain.ClearForProp(site, radius)) continue;
                return true;
            }
            site = desired;
            return false;
        }

        private GameObject Put(GameObject prefab, string label, Vector2 point, float radius, float scale, float yaw)
        {
            GameObject item = Instantiate(prefab, terrain.transform, false);
            item.name = label;
            item.transform.localPosition = new Vector3(point.x, terrain.SurfaceHeight(point.x, point.y) - .05f, point.y);
            item.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            item.transform.localScale = Vector3.one * scale;
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            terrain.ReserveProp(point, radius + .4f);
            return item;
        }

        // Act 2 stone landmarks have unit horizontal radius (ActTwoEnvironmentProfile).
        private void Tower(string label, Vector2 desired, float radius, float yaw) =>
            Landmark(palette.actTwoEnvironment != null ? palette.actTwoEnvironment.watchtower : null, label, desired, radius, yaw);

        private void Wall(string label, Vector2 desired, float radius, float yaw) =>
            Landmark(palette.actTwoEnvironment != null ? palette.actTwoEnvironment.brokenWall : null, label, desired, radius, yaw);

        private void Landmark(GameObject prefab, string label, Vector2 desired, float radius, float yaw)
        {
            if (prefab == null || !FindSite(desired, radius, out Vector2 site)) return;
            Put(prefab, label, site, radius, radius * .9f, yaw);
            Landmarks++;
        }

        private void Prop(BattlefieldPropKind kind, Vector2 desired, float scale, float yaw)
        {
            BattlefieldPropAsset asset = palette.battlefieldProps != null ? palette.battlefieldProps.Find(kind) : null;
            if (asset == null || asset.prefab == null || !FindSite(desired, asset.Radius * scale, out Vector2 site)) return;
            Put(asset.prefab, kind.ToString(), site, asset.Radius * scale, scale, yaw);
            Landmarks++;
        }

        private void Supplies(Vector2 centre)
        {
            Prop(BattlefieldPropKind.AlliedChest, centre, 1f, Range(-30f, 30f));
            Prop(BattlefieldPropKind.AlliedBarrel, centre + new Vector2(1.7f, .6f), 1f, Range(0f, 360f));
            Prop(BattlefieldPropKind.AlliedBarrel, centre + new Vector2(1.2f, -1.3f), .85f, Range(0f, 360f));
        }

        // Short palisade pieces flanking a structure, never a wall across the approach.
        private void Fences(Vector2 centre, float distance)
        {
            foreach (int side in new[] { -1, 1 })
                Prop(BattlefieldPropKind.EnemyBoneFence, centre + new Vector2(side * distance, -1.5f), .8f, side * 70f);
        }

        // Large forest rocks on the open flanks.
        private void Boulders(int count, float minScale, float maxScale)
        {
            if (forest == null || forest.rock == null) return;
            for (int i = 0, placed = 0; i < count * 8 && placed < count; i++)
            {
                Vector2 point = new Vector2(Range(-33f, 33f), Range(-6f, 70f));
                float scale = Range(minScale, maxScale), radius = scale * 1.1f;
                if (!terrain.ClearOfPlay(point, 5f + radius, 12f) || !terrain.ClearOfProps(point, radius)) continue;
                Put(forest.rock, "Boulder", point, radius, scale, Range(0f, 360f)).transform.localPosition += Vector3.down * .25f * scale;
                placed++;
            }
        }

        // Trees between and beside the Paths so the woodland closes in on the trails.
        private void Grove(int count, float minScale, float maxScale, float laneClearance)
        {
            if (forest == null || forest.tree == null) return;
            for (int i = 0, placed = 0; i < count * 8 && placed < count; i++)
            {
                Vector2 point = new Vector2(Range(-33f, 33f), Range(0f, 66f));
                if (!terrain.ClearOfPlay(point, laneClearance, 15f) || !terrain.ClearOfProps(point, 3.5f)) continue;
                GameObject tree = Put(forest.tree, "Trail Tree", point, 2.5f, Range(minScale, maxScale), Range(0f, 360f));
                tree.transform.localPosition += Vector3.down * .1f;
                ForestGroundContact contact = tree.GetComponent<ForestGroundContact>();
                if (contact != null) contact.Ground(terrain);
                placed++;
            }
        }

        // Small ground cover; does not reserve space, so the forest dressing may still grow nearby.
        private void Scatter(GameObject prefab, int count, float minScale, float maxScale, float laneClearance)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 point = new Vector2(Range(-33f, 33f), Range(-6f, 70f));
                if (!terrain.ClearOfPlay(point, laneClearance, 11f) || !terrain.ClearOfProps(point, .5f)) continue;
                GameObject item = Instantiate(prefab, terrain.transform, false);
                item.name = prefab.name;
                item.transform.localPosition = new Vector3(point.x, terrain.SurfaceHeight(point.x, point.y) - .04f, point.y);
                item.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                item.transform.localScale = Vector3.one * Range(minScale, maxScale);
            }
        }

        private void OnDestroy()
        {
            foreach (Object item in owned)
                if (item != null)
                {
                    if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
                }
            owned.Clear();
        }
    }
}
