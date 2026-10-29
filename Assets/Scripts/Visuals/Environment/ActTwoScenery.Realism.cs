using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lightbringer.Visuals
{
    // Act 2 (stages 9-16) photographic pass: physically plausible light per stage, textured and normal-mapped ground,
    // soft diffuse props without toon outlines, a filmic grade, and photoscanned (Poly Haven, CC0) rocks, wood and goods.
    // Act 3 shares the material detail and grading pipeline, then applies its ash-domain palette.
    public sealed partial class ActTwoScenery
    {
        // Time of day, weather and grade per stage. pitch/yaw: sun rotation; temperature: white balance (-100 cold .. 100 warm).
        private struct Mood
        {
            public float pitch, yaw, intensity, temperature, saturation, exposure;
            public Color sun, sky, equator, fog;
            public Mood(float pitch, float yaw, Color sun, float intensity, Color sky, Color equator, Color fog,
                float temperature, float saturation, float exposure)
            {
                this.pitch = pitch; this.yaw = yaw; this.sun = sun; this.intensity = intensity;
                this.sky = sky; this.equator = equator; this.fog = fog;
                this.temperature = temperature; this.saturation = saturation; this.exposure = exposure;
            }
        }

        // Act 2 is permanently overcast and grows darker towards the Warlord's citadel: no direct golden sun anywhere.
        private static Mood MoodFor(int stage)
        {
            if (stage >= 17)
            {
                float depth = Mathf.Clamp01((stage - 17) / 7f);
                return new Mood(54 - depth * 12, -45 + (stage - 17) * 13,
                    new Color(.72f, .75f, .81f), Mathf.Lerp(.65f, .45f, depth),
                    Color.Lerp(new Color(.45f, .48f, .54f), new Color(.37f, .40f, .47f), depth),
                    new Color(.33f, .34f, .37f), new Color(.25f, .26f, .30f),
                    -12 - depth * 4, -25 - depth * 7, .35f);
            }
            // t: 0 at stage 9 (grey daylight) .. 1 at stage 16 (heavy storm gloom).
            float t = Mathf.Clamp01((stage - 9) / 7f);
            // Overcast light is mostly sky fill: a weak diffuse sun with strong, cool ambient.
            return new Mood(
                Mathf.Lerp(62, 48, t), -35,
                Color.Lerp(new Color(.82f, .85f, .89f), new Color(.60f, .63f, .70f), t),
                Mathf.Lerp(.8f, .45f, t),
                Color.Lerp(new Color(.66f, .70f, .76f), new Color(.38f, .41f, .48f), t),
                Color.Lerp(new Color(.52f, .54f, .56f), new Color(.30f, .31f, .34f), t),
                Color.Lerp(new Color(.52f, .55f, .59f), new Color(.30f, .32f, .36f), t),
                Mathf.Lerp(-6, -14, t), Mathf.Lerp(-16, -28, t), Mathf.Lerp(.3f, .05f, t));
        }

        private void ApplyRealism()
        {
            ArtStyleLibrary s = palette;
            Mood mood = MoodFor(Stage);
            s.sunEuler = new Vector3(mood.pitch, mood.yaw, 0);
            s.sunColor = mood.sun;
            s.sunIntensity = mood.intensity;
            s.ambientSky = mood.sky;
            s.ambientEquator = mood.equator;
            s.ambientGround = new Color(.24f, .22f, .20f);
            s.fogColor = mood.fog;
            s.cloudShadowStrength = .18f;
            // Natural earth and stone tones instead of a painted palette.
            s.grassLight = new Color(.36f, .34f, .26f);
            s.grassDark = new Color(.23f, .23f, .19f);
            s.dirt = new Color(.36f, .31f, .25f);
            // Procedural stone stays darker than the scanned ground so paving and rubble never glare white.
            s.rock = new Color(.24f, .24f, .23f);
            s.ruinStone = new Color(.27f, .26f, .24f);
            s.bark = new Color(.17f, .15f, .13f);
            if (s.terrainMaterial != null)
            {
                Material t = s.terrainMaterial;
                t.SetFloat("_ForestTextureStrength", .92f);
                t.SetFloat("_ForestNormalStrength", 1.1f);
                t.SetFloat("_NaturalLight", 1);
                t.SetFloat("_StrokeStrength", 0);
                t.SetFloat("_PatchStrength", .1f);
                t.SetFloat("_BumpStrength", .05f);
                t.SetFloat("_GravelAmount", .35f);
                t.SetFloat("_RutStrength", .8f);
                t.SetFloat("_SoilBreakup", .9f);
                t.SetFloat("_StoneTextureStrength", 1);
                // A larger tile hides the repeat from the high battle camera.
                t.SetFloat("_ForestTextureScale", .24f);
                t.SetColor("_ForestGroundTint", Color.Lerp(new Color(.86f, .82f, .72f), mood.sun, .12f));
            }
            if (s.propsMaterial != null)
            {
                Material p = s.propsMaterial;
                // Wide, soft diffuse ramp and no ink outline or rim: the toon shader reads as plain matte material.
                p.SetFloat("_RampThreshold", -.15f);
                p.SetFloat("_RampSoftness", .7f);
                p.SetFloat("_OutlineWidth", 0);
                p.SetFloat("_RimStrength", 0);
                p.SetFloat("_Specular", .04f);
                p.SetFloat("_PatternStrength", .35f);
                p.SetColor("_BaseColor", new Color(.8f, .8f, .8f));
                p.SetColor("_ShadowColor", Color.Lerp(new Color(.46f, .48f, .52f), mood.sky, .35f));
            }
            if (s.skyMaterial != null)
            {
                s.skyMaterial.SetColor("_HorizonColor", mood.fog);
                s.skyMaterial.SetColor("_ZenithColor", Color.Lerp(mood.sky, Color.black, .35f));
                s.skyMaterial.SetColor("_SunColor", mood.sun * .85f);
                s.skyMaterial.SetFloat("_SunStrength", 0f);
            }
            s.postProcessing = BuildGrade(mood);
        }

        // Filmic tonemapping, restrained saturation, a per-stage white balance and a light grain; no split-toning.
        private VolumeProfile BuildGrade(Mood mood)
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = IsActThree ? "Act Three Ash Domain Grade" : "Act Two Photographic Grade";
            profile.hideFlags = HideFlags.DontSave;
            owned.Add(profile);
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(.22f);
            bloom.scatter.Override(.7f);
            ColorAdjustments adjust = profile.Add<ColorAdjustments>();
            adjust.postExposure.Override(mood.exposure);
            adjust.contrast.Override(12);
            adjust.saturation.Override(mood.saturation);
            WhiteBalance balance = profile.Add<WhiteBalance>();
            balance.temperature.Override(mood.temperature);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(.3f);
            vignette.smoothness.Override(.6f);
            FilmGrain grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(.12f);
            grain.response.Override(.8f);
            return profile;
        }

        // Silty, dark river water under an overcast sky (the toon water strips read neon blue otherwise).
        private static readonly Color MurkyWater = new Color(.07f, .09f, .09f);

        public int ScannedProps { get; private set; }
        // Act 2 with the photoscan set imported: scanned rocks, stumps and ground replace the toon dressing.
        public bool UsesPhotoscans => palette != null && palette.actTwoEnvironment != null
            && palette.actTwoEnvironment.Find(ActTwoScatterKind.Stump).Length > 0;

        private ActTwoScatterAsset[] ScanSet(ActTwoScatterKind kind)
            => palette.actTwoEnvironment != null ? palette.actTwoEnvironment.Find(kind) : new ActTwoScatterAsset[0];

        private GameObject PlaceScan(ActTwoScatterAsset asset, Vector2 point, float scale, float yaw, float tilt = 0, float sink = .06f)
        {
            GameObject item = Instantiate(asset.prefab, terrain.transform, false);
            item.name = asset.prefab.name;
            item.transform.localPosition = Ground(point.x, point.y) - Vector3.up * sink * scale;
            // Rest on the local slope so the downhill side never floats above the hillside.
            // Large rocks sample the slope across their footprint rather than at the pivot alone.
            float span = Mathf.Clamp(asset.radius * scale * .5f, 1, 5);
            float dx = terrain.SurfaceHeight(point.x + span, point.y) - terrain.SurfaceHeight(point.x - span, point.y);
            float dz = terrain.SurfaceHeight(point.x, point.y + span) - terrain.SurfaceHeight(point.x, point.y - span);
            Vector3 normal = new Vector3(-dx * .5f / span, 1, -dz * .5f / span).normalized;
            item.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(tilt, yaw, 0);
            item.transform.localScale = Vector3.one * scale;
            // Detail falls off with distance through the Mesh LOD chain baked into each scan (ActTwoPhotoSetup).
            terrain.ReserveProp(point, asset.radius * scale);
            if (Glossy(asset.kind)) Matte(item);
            ScannedProps++;
            return item;
        }

        // Scans of painted, varnished or iron-bound goods keep the gloss of their sunny capture. Under the overcast
        // sky that gloss mirrors the bright default reflection cubemap, so they glow against the dark field.
        private static bool Glossy(ActTwoScatterKind kind) => kind == ActTwoScatterKind.Shield || kind == ActTwoScatterKind.Crate
            || kind == ActTwoScatterKind.Barrel || kind == ActTwoScatterKind.Bucket || kind == ActTwoScatterKind.Gate;

        private readonly Dictionary<Material, Material> matteScans = new Dictionary<Material, Material>();

        private void Matte(GameObject item)
        {
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = MatteCopy(materials[i]);
                renderer.sharedMaterials = materials;
            }
        }

        // Battlefield-local weathered copy of a scan material (glTFast or URP Lit), shared by every instance:
        // rougher surfaces and mostly dielectric metal fittings.
        private Material MatteCopy(Material source)
        {
            if (source == null) return null;
            if (matteScans.TryGetValue(source, out Material result)) return result;
            result = Copy(source);
            // glTFast: smoothness = 1 - roughness map * roughnessFactor.
            if (result.HasProperty("roughnessFactor")) result.SetFloat("roughnessFactor", result.GetFloat("roughnessFactor") * 2.5f);
            if (result.HasProperty("metallicFactor")) result.SetFloat("metallicFactor", result.GetFloat("metallicFactor") * .35f);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", result.GetFloat("_Smoothness") * .35f);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", result.GetFloat("_Metallic") * .35f);
            matteScans[source] = result;
            return result;
        }

        // Scatters kind within the battlefield; returns how many were placed.
        private int ScatterScans(ActTwoScatterKind kind, int count, float minScale, float maxScale, float tilt = 0, float pad = .6f)
        {
            ActTwoScatterAsset[] set = ScanSet(kind);
            if (set.Length == 0) return 0;
            int placed = 0;
            for (int attempt = 0; attempt < count * 14 && placed < count; attempt++)
            {
                ActTwoScatterAsset asset = set[random.Next(set.Length)];
                // Wide rock clusters are capped so no single scan swallows a whole flank of the field.
                float scale = Mathf.Min(Range(minScale, maxScale), 3.5f / Mathf.Max(.1f, asset.radius));
                Vector2 point = new Vector2(Range(-31, 31), Range(-26, 88));
                if (!terrain.ClearForProp(point, asset.radius * scale + pad)) continue;
                PlaceScan(asset, point, scale, Range(0, 360), tilt == 0 ? 0 : Range(-tilt, tilt));
                placed++;
            }
            return placed;
        }

        private void BuildPhotoscans()
        {
            // Weathered rock outcrops break out of both valley slopes just outside the walkable field,
            // sunk deep so they read as bedrock rather than objects resting on the hill.
            ActTwoScatterAsset[] outcrops = ScanSet(ActTwoScatterKind.Boulder);
            if (outcrops.Length > 0)
                for (int side = -1; side <= 1; side += 2)
                    for (float z = -36; z < 100; z += 9)
                    {
                        ActTwoScatterAsset asset = outcrops[random.Next(outcrops.Length)];
                        float scale = Mathf.Min(Range(1.6f, 3f), 6f / Mathf.Max(.1f, asset.radius));
                        Vector2 point = new Vector2(side * Range(39, 52), z + Range(-2.5f, 2.5f));
                        if (!terrain.ClearOfProps(point, asset.radius * scale * .5f)) continue;
                        PlaceScan(asset, point, scale, Range(0, 360), Range(-8, 8), .38f);
                    }
            int dead = IsActThree ? 5 + (Stage - 17) / 2 : Stage - 9;
            ScatterScans(ActTwoScatterKind.Boulder, 8 + dead, 1.1f, 1.9f);
            ScatterScans(ActTwoScatterKind.Log, Stage == 20 ? 14 : 4 + dead / 2, 1f, 1.4f);
            ScatterScans(ActTwoScatterKind.Stump, 7, 1f, 1.5f);
            if (!IsActThree) ScatterScans(ActTwoScatterKind.Brush, 16 + dead * 2, 1.2f, 2.2f, 0, .2f);
            BuildBattleRelics(dead);
        }

        // Remnants of earlier battles: shields thrown flat or half-buried on a rim, swords and spears driven point
        // first into the ground, arms dropped in the mud. Most lie in small clusters where a line once broke.
        private void BuildBattleRelics(int dead)
        {
            ActTwoScatterAsset[] shields = ScanSet(ActTwoScatterKind.Shield);
            var arms = new List<ActTwoScatterAsset>();
            // Swords twice over: the commonest sidearm on the field.
            foreach (ActTwoScatterKind kind in new[] { ActTwoScatterKind.Sword, ActTwoScatterKind.Sword, ActTwoScatterKind.Spear, ActTwoScatterKind.Mace })
                arms.AddRange(ScanSet(kind));
            if (shields.Length == 0 && arms.Count == 0) return;
            ActTwoScatterAsset Pick(bool shield) => shield && shields.Length > 0 || arms.Count == 0
                ? shields[random.Next(shields.Length)] : arms[random.Next(arms.Count)];

            int clusters = 3 + dead / 2, placed = 0;
            for (int attempt = 0; attempt < clusters * 14 && placed < clusters; attempt++)
            {
                Vector2 centre = new Vector2(Range(-29, 29), Range(-22, 86));
                if (!terrain.ClearForProp(centre, 1.2f)) continue;
                placed++;
                int items = random.Next(2, 5);
                for (int i = 0; i < items; i++)
                {
                    float angle = Range(0, Mathf.PI * 2);
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i == 0 ? 0 : Range(.9f, 2.6f));
                    PlaceRelic(Pick(i == 0 || random.NextDouble() < .35), point);
                }
            }
            int singles = 2 + dead / 2;
            for (int attempt = 0; attempt < 40 && singles > 0; attempt++)
                if (PlaceRelic(Pick(random.NextDouble() < .5), new Vector2(Range(-30, 30), Range(-24, 87)))) singles--;
        }

        // Shields face +Z and long arms run along +Y with their flats on ±X (WeaponMeshes convention).
        private bool PlaceRelic(ActTwoScatterAsset asset, Vector2 point)
        {
            bool shield = asset.kind == ActTwoScatterKind.Shield;
            // The scanned kite shield is real-scale; the unit weapons are sized to the larger-than-life unit models.
            bool relic = System.Array.IndexOf(palette.actTwoEnvironment.relics, asset) >= 0;
            float scale = relic ? Range(1.3f, 1.6f) : Range(.8f, 1f);
            float reach = scale * (asset.length > 0 ? asset.length * .5f : asset.radius * 2.5f);
            if (!terrain.ClearForProp(point, reach + .1f)) return false;

            Quaternion pose;
            float sink, roll = (float)random.NextDouble(), side = random.Next(2) == 0 ? -1 : 1;
            if (shield && roll < .6f)
            {
                // Face down or face up, tipped so one rim digs into the dirt.
                pose = Quaternion.Euler(side * 90 + Range(-8, 8), 0, Range(-6, 6));
                sink = Range(.3f, .45f);
            }
            else if (shield)
            {
                // Toppled and half swallowed by the mud, leaning well off the vertical.
                pose = Quaternion.Euler(side * Range(38, 65), 0, Range(-15, 15));
                sink = Range(.3f, .5f);
            }
            else if (asset.kind == ActTwoScatterKind.Sword && roll < .55f || asset.kind == ActTwoScatterKind.Spear && roll < .35f)
            {
                // Driven point first into the ground, hilt or shaft raised at a slant.
                float lean = asset.kind == ActTwoScatterKind.Spear ? Range(22, 40) : Range(4, 20);
                pose = Quaternion.Euler(side * lean, 0, 180 + Range(-lean, lean) * .5f);
                sink = asset.kind == ActTwoScatterKind.Spear ? Range(.1f, .18f) : Range(.18f, .3f);
            }
            else
            {
                // Dropped flat on a blade face or along the haft.
                pose = Quaternion.Euler(Range(-6, 6), 0, side * 90 + Range(-6, 6));
                sink = Range(.3f, .5f);
            }

            GameObject item = Instantiate(asset.prefab, terrain.transform, false);
            item.name = asset.prefab.name;
            item.transform.localScale = Vector3.one * scale;
            item.transform.localRotation = Quaternion.Euler(0, Range(0, 360), 0) * pose;
            item.transform.localPosition = Ground(point.x, point.y);
            // Settle on the posed bounds, so base pivots (scans) and centre pivots (relics) bury alike.
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float surface = terrain.transform.TransformPoint(Ground(point.x, point.y)).y;
                item.transform.position += Vector3.up * (surface - bounds.min.y - bounds.size.y * sink);
            }
            terrain.ReserveProp(point, reach * .8f);
            // The relics are built already weathered (BattlefieldRelicSetup).
            if (!relic) Matte(item);
            ScannedProps++;
            return true;
        }

        private void BuildEnemyRearCover()
        {
            ActTwoScatterAsset[] cliffs = ScanSet(ActTwoScatterKind.Cliff);
            ActTwoScatterAsset[] rocks = ScanSet(ActTwoScatterKind.Boulder);
            if (rocks.Length == 0) return;
            // Reserve the rear outcrops before trees fill the hills. Two staggered depth bands
            // frame the enemy stronghold, with gaps rather than a continuous wall of repeated rocks.
            System.Random previousRandom = random;
            random = new System.Random(Stage * 6271 + 31);
            for (int cluster = 0; cluster < 14; cluster++)
            {
                Vector2 centre = new Vector2(-72 + cluster % 7 * 24 + Range(-5, 5),
                    (cluster < 7 ? 103 : 132) + Range(-5, 5));
                TrySlopeScan(cliffs.Length > 0 ? cliffs : rocks, centre, Range(5.5f, 8f), .3f);
                for (int fragment = 0; fragment < 3; fragment++)
                {
                    float angle = Range(0, Mathf.PI * 2);
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(9, 13);
                    TrySlopeScan(rocks, point, Range(2.2f, 4.2f), .25f);
                }
            }
            random = previousRandom;
        }

        private void BuildHillBoulders()
        {
            // Massive bedrock boulders break the repeating ground photo across both valley hills.
            // Only the closed scans scale up cleanly; the moss set would read as oversized pebbles.
            // Placed before the trees so the woodland grows around them; seeded apart from the rest.
            List<ActTwoScatterAsset> closed = new List<ActTwoScatterAsset>(ScanSet(ActTwoScatterKind.Cliff));
            foreach (ActTwoScatterAsset rock in ScanSet(ActTwoScatterKind.Boulder))
                if (rock.radius < 2.5f) closed.Add(rock);
            if (closed.Count == 0) return;
            System.Random previousRandom = random;
            random = new System.Random(Stage * 7919 + 57);
            for (int side = -1; side <= 1; side += 2)
                for (float z = -44; z < 128; z += 19)
                    TryHillBoulder(closed, new Vector2(side * Range(48, 88), z + Range(-6, 6)), Range(8f, 13f), 1f);
            // Rear hills: behind the enemy castle (the horizon of every advance) and behind the allied
            // keep, kept past the camera's farthest pull-back so none fills the view at the start.
            // The enemy rear already holds the cliff bands, so these may bury the edges of those.
            for (int band = 0; band < 2; band++)
                for (float x = -84; x <= 84; x += 25)
                {
                    float jitter = Range(-6, 6);
                    TryHillBoulder(closed, new Vector2(x + jitter, (band == 0 ? 114 : 137) + Range(-5, 5)), Range(8f, 13f), -1f);
                    TryHillBoulder(closed, new Vector2(x - jitter, (band == 0 ? -60 : -82) + Range(-5, 5)), Range(8f, 12f), 1f);
                }
            random = previousRandom;
        }

        // A positive pad keeps a gap to existing props; a negative one lets the boulder bury a neighbour's edge.
        private void TryHillBoulder(List<ActTwoScatterAsset> set, Vector2 point, float radius, float pad)
        {
            // Keep them up on the hillside rather than at its foot beside the field.
            if (terrain.Height(point.x, point.y) < 3.5f
                || !terrain.ClearOfPlay(point, 8f + radius, 16f + radius)
                || !terrain.ClearOfProps(point, pad < 0 ? radius * .6f : radius + pad)) return;
            ActTwoScatterAsset asset = set[random.Next(set.Count)];
            PlaceScan(asset, point, radius / Mathf.Max(.1f, asset.radius), Range(0, 360), Range(-6, 6), .32f);
            HillBoulders++;
        }

        public int HillBoulders { get; private set; }

        private void BuildSlopeCover()
        {
            // Break up exposed soil across the whole hillside, not just the foot of the valley.
            // Existing trees/landmarks keep their footprints; the extra dressing is Act 2 only.
            ActTwoScatterAsset[] rocks = ScanSet(ActTwoScatterKind.Boulder);
            ActTwoScatterAsset[] cliffs = ScanSet(ActTwoScatterKind.Cliff);
            ActTwoScatterAsset[] logs = ScanSet(ActTwoScatterKind.Log);
            ActTwoScatterAsset[] brush = ScanSet(ActTwoScatterKind.Brush);
            if (rocks.Length == 0) return;

            for (int cluster = 0; cluster < 24; cluster++)
            {
                // Independent jitter and depth avoid both mirrored sides and another regular row.
                Vector2 centre = cluster < 18
                    ? new Vector2((cluster % 2 == 0 ? -1 : 1) * Range(44, 82),
                        -42 + (cluster / 2) * 18 + Range(-8, 8))
                    : new Vector2(Range(-76, 76), Range(108, 133));
                ActTwoScatterAsset[] anchors = cluster % 3 == 0 && cliffs.Length > 0 ? cliffs : rocks;
                TrySlopeScan(anchors, centre, Range(3.8f, 6.4f), .35f);

                int fragments = random.Next(3, 6);
                for (int i = 0; i < fragments; i++)
                {
                    float angle = Range(0, Mathf.PI * 2);
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(6, 13);
                    ActTwoScatterAsset[] set = i == 0 && logs.Length > 0 ? logs
                        : i % 2 == 1 && brush.Length > 0 ? brush : rocks;
                    TrySlopeScan(set, point, Range(1.3f, 2.8f), set == rocks ? .24f : .06f);
                }
            }
        }

        private void TrySlopeScan(ActTwoScatterAsset[] set, Vector2 point, float radius, float sink)
        {
            // ClearForProp intentionally rejects slopes (for flat plinths), so check play space
            // and full prop footprints here and let PlaceScan align the decoration to the slope.
            if (!terrain.ClearOfPlay(point, 6.5f + radius, 14 + radius)
                || !terrain.ClearOfProps(point, radius + .4f)) return;
            ActTwoScatterAsset asset = set[random.Next(set.Length)];
            float scale = radius / Mathf.Max(.1f, asset.radius);
            PlaceScan(asset, point, scale, Range(0, 360), Range(-5, 5), sink);
        }

        // Crates, barrels and buckets around a depot or storeyard centre; keeps to the landmark's own footprint.
        private void ScanGoods(Vector3 centre, float radius, int count)
        {
            ActTwoScatterKind[] kinds = { ActTwoScatterKind.Crate, ActTwoScatterKind.Barrel, ActTwoScatterKind.Crate, ActTwoScatterKind.Bucket };
            for (int i = 0; i < count; i++)
            {
                ActTwoScatterAsset[] set = ScanSet(kinds[i % kinds.Length]);
                if (set.Length == 0) continue;
                ActTwoScatterAsset asset = set[random.Next(set.Length)];
                float angle = Range(0, Mathf.PI * 2), distance = Range(1.2f, radius);
                Vector2 point = new Vector2(centre.x, centre.z) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                // Hero-scale goods; the wide barrel pile stays at its own size so it fits inside the yard.
                float scale = asset.radius > 1.5f ? 1f : Range(1.5f, 1.9f);
                if (IsActThree && !terrain.ClearForProp(point, asset.radius * scale)) continue;
                PlaceScan(asset, point, scale, Range(0, 360));
            }
        }
    }
}
