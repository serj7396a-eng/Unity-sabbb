using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railrush.World
{
    /// <summary>A hardware-batched scenery loop. Gameplay objects live on a separate track.</summary>
    public sealed class NightTrack : MonoBehaviour
    {
        public const float SegmentLength = 36f;
        public const float DistrictLength = SegmentLength * 2f;
        private const int SegmentCount = 8;
        private const float LoopLength = SegmentCount * SegmentLength;
        private static readonly float[] Lanes = { -2.55f, 0f, 2.55f };
        private static readonly string[] DistrictNames =
        {
            "LUNE TERMINAL", "AFTERGLOW MARKET", "AQUEDUCT WALK", "COPPER WORKS"
        };

        private static readonly Paint Base = Surface(0x171927, 0f, .72f);
        private static readonly Paint Rail = Surface(0xABBFD7, .22f, .95f);
        private static readonly Paint RailEdge = Surface(0x4C516E, .06f, .65f);
        private static readonly Paint Timber = Surface(0x3D3146, 0f, .26f);
        private static readonly Paint Platform = Surface(0x4E516E, 0f, .68f);
        private static readonly Paint Grout = Surface(0x1B192D, 0f, .25f);
        private static readonly Paint Structure = Surface(0x232641, 0f, .30f);
        private static readonly Paint Glass = Surface(0x344761, .14f, .39f);
        private static readonly Paint Cyan = Surface(0x49EFFD, 1.0f);
        private static readonly Paint Violet = Surface(0xC86DFF, 1.0f);
        private static readonly Paint Lime = Surface(0xD4FF59, .87f);
        private static readonly Paint Amber = Surface(0xFFAB79, .84f);
        private static readonly Paint Porcelain = Surface(0xC1E9FF, .62f);
        private static readonly Paint Dark = Surface(0x0D1224, 0f, .25f);

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Transform[] segments = new Transform[SegmentCount];
        private readonly Transform[] vehicles = new Transform[4];
        private readonly List<Transform> rotors = new List<Transform>();
        private Material scenery;
        private Font lettering;
        private bool built;

        public static int DistrictAt(float distance)
        {
            return Mathf.Min(DistrictNames.Length - 1, Mathf.FloorToInt(Mathf.Repeat(distance, LoopLength) / DistrictLength));
        }

        public static string DistrictLabel(float distance)
        {
            return DistrictNames[DistrictAt(distance)];
        }

        public void Build()
        {
            if (built)
                return;

            built = true;
            var shader = Resources.Load<Shader>("RailrushTrack") ?? Shader.Find("Railrush/NightTrack");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            scenery = new Material(shader) { name = "Night Track • Baked Surfaces" };
            lettering = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int index = 0; index < SegmentCount; index++)
            {
                GameObject segment = new GameObject(DistrictNames[index / 2] + " / " + index);
                segment.transform.SetParent(transform, false);
                segments[index] = segment.transform;

                var geometry = new Geometry();
                BuildRailway(geometry);
                BuildDistancedStreet(geometry, segment.transform, index);
                AddMesh(segment.transform, geometry, "Batched district geometry");
            }

            BuildDistantSkyline();
            BuildTraffic();
            RenderDistance(0f, 0f, 2);
        }

        public void RenderDistance(float distance, float clock, int quality)
        {
            if (!built)
                return;

            for (int index = 0; index < segments.Length; index++)
            {
                float relative = Mathf.Repeat(index * SegmentLength - distance + SegmentLength, LoopLength) - SegmentLength;
                segments[index].localPosition = new Vector3(0f, 0f, relative);
            }

            for (int index = 0; index < rotors.Count; index++)
            {
                float sign = (index & 1) == 0 ? 1f : -1f;
                rotors[index].localRotation = Quaternion.Euler(0f, 0f, Mathf.Repeat(clock * 190f * sign + index * 29f, 360f));
            }

            int visible = quality >= 2 ? vehicles.Length : quality == 1 ? 2 : 0;
            for (int index = 0; index < vehicles.Length; index++)
            {
                Transform vehicle = vehicles[index];
                bool active = index < visible;
                if (vehicle.gameObject.activeSelf != active)
                    vehicle.gameObject.SetActive(active);
                if (!active)
                    continue;

                float phase = clock * (0.64f + index * .16f) + index * 2.38f;
                float x = Mathf.Sin(phase) * (5.3f + index * 1.1f);
                float y = 10.7f + index * 1.35f + Mathf.Sin(phase * 1.29f) * .48f;
                float z = Mathf.Repeat(44f + index * 26f - distance * .21f - clock * (5.7f + index * 1.45f), 142f) - 10f;
                vehicle.localPosition = new Vector3(x, y, z);
                vehicle.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 3f, 171f + Mathf.Cos(phase) * 14f, -Mathf.Cos(phase) * 18f);
            }
        }

        private void BuildRailway(Geometry geometry)
        {
            geometry.Box(0f, -.26f, 18f, 15f, .33f, SegmentLength, Base);
            geometry.Box(0f, -.09f, 18f, 8.21f, .18f, SegmentLength, Base);
            foreach (float lane in Lanes)
            {
                for (int tie = 0; tie < 36; tie++)
                {
                    float z = tie + .43f;
                    geometry.Box(lane, -.004f, z, 1.92f, .057f, .25f, Timber);
                    if (tie % 2 == 0)
                    {
                        geometry.Box(lane - .71f, .033f, z, .16f, .046f, .27f, RailEdge);
                        geometry.Box(lane + .71f, .033f, z, .16f, .046f, .27f, RailEdge);
                    }
                }

                foreach (int side in new[] { -1, 1 })
                {
                    float x = lane + side * .68f;
                    geometry.Box(x, .042f, 18f, .095f, .105f, SegmentLength, RailEdge);
                    geometry.Box(x, .101f, 18f, .064f, .026f, SegmentLength, Rail);
                }
            }

            foreach (int side in new[] { -1, 1 })
            {
                geometry.Box(side * 5.54f, .061f, 18f, 2.91f, .31f, SegmentLength, Platform);
                geometry.Box(side * 4.21f, .22f, 18f, .14f, .049f, SegmentLength, RailEdge);
                geometry.Box(side * 4.03f, .068f, 18f, .035f, .046f, SegmentLength, side > 0 ? Cyan : Violet);
                for (int index = 0; index < 18; index++)
                {
                    float z = .67f + index * 2f;
                    geometry.Box(side * 4.23f, .252f, z, .134f, .018f, .57f, Lime);
                    geometry.Box(side * 5.72f, .219f, z + .18f, 2.31f, .01f, .018f, Grout);
                }
            }
        }

        private void BuildDistancedStreet(Geometry geometry, Transform segment, int index)
        {
            int district = index / 2;
            var random = new System.Random(1641 + index * 39);
            Paint accent = district == 0 ? Cyan : district == 1 ? Violet : district == 2 ? Cyan : Amber;
            foreach (int side in new[] { -1, 1 })
            {
                for (int block = 0; block < 3; block++)
                {
                    float z = 6f + 12f * block;
                    float width = Between(random, 4.4f, 8.2f);
                    float depth = Between(random, 8.8f, 11.6f);
                    float height = Between(random, district == 2 ? 5.1f : 8.8f, district == 2 ? 12.4f : 21.5f);
                    float x = side * (7.66f + width * .5f + Between(random, 0f, 1.05f));
                    MakeBuilding(geometry, random, district, side, x, z, width, depth, height);
                }

                for (int marker = 0; marker < 3; marker++)
                {
                    float z = 1.7f + marker * 12f;
                    MakeLamp(geometry, side, z, side < 0 ? Violet : accent);
                    MakeGuardRail(geometry, side, z + 3.6f);
                }
                MakeSign(geometry, segment, side, 11.2f, DistrictNames[district], accent);
                switch (district)
                {
                    case 0:
                        MakeBoardingPlatform(geometry, side, 19.3f);
                        break;
                    case 1:
                        MakeMarketStall(geometry, side, 21.1f);
                        MakeMarketStall(geometry, side, 28.8f);
                        break;
                    case 2:
                        MakeAqueduct(geometry, side);
                        break;
                    default:
                        MakeMachinery(geometry, segment, side, 20.9f);
                        break;
                }
            }

            if (district == 0 && (index & 1) == 0)
                MakeStationGantry(geometry, segment, 23.5f);
            else if (district == 1)
                MakeLanternSpan(geometry, 15.6f);
            else if (district == 2)
                MakeCatwalk(geometry, 24.5f, Cyan, 9.3f);
            else if (district == 3)
                MakeCatwalk(geometry, 18.7f, Amber, 9.1f);
        }

        private static void MakeBuilding(Geometry geometry, System.Random random, int district, int side,
            float x, float z, float width, float depth, float height)
        {
            var palettes = new uint[,]
            {
                { 0x303A5C, 0x344B61, 0x272E48 },
                { 0x42304E, 0x4B344A, 0x363656 },
                { 0x213F52, 0x2C4D64, 0x30485E },
                { 0x473A4A, 0x54414E, 0x3E394E }
            };
            Paint wall = Surface(palettes[district, random.Next(3)], 0f, .26f);
            float inside = x - side * (width / 2f + .014f);
            float front = z - depth / 2f - .018f;
            geometry.Box(x, height / 2f, z, width, height, depth, wall);
            geometry.Box(x, height + .15f, z, width + .26f, .27f, depth + .30f, Structure);
            geometry.Box(x, .53f, z, width + .15f, 1.06f, depth + .13f, Structure);
            if (random.NextDouble() > .44)
            {
                float roofHeight = Between(random, .9f, 2.8f);
                geometry.Box(x, height + roofHeight / 2f, z + .42f, width * .45f, roofHeight, depth * .52f, wall);
                geometry.Box(x + side * .51f, height + roofHeight + .69f, z + .76f, .072f, 1.4f, .072f, RailEdge);
                geometry.Box(x + side * .51f, height + roofHeight + 1.40f, z + .76f, .14f, .14f, .14f, Amber);
            }

            int rows = Mathf.Clamp(Mathf.FloorToInt(height / 2.05f), 2, 9);
            for (int floor = 1; floor < rows; floor++)
            {
                float y = 1.36f + floor * 1.87f;
                geometry.Box(inside, y - .53f, z, .18f, .12f, depth * .96f, Structure);
                for (int column = 0; column < 4; column++)
                {
                    float placement = z + (column - 1.5f) * depth / 4.1f;
                    geometry.Box(inside - side * .038f, y, placement, .043f, .73f, depth / 7.8f, Window(random, district));
                }

                int frontColumns = width > 6.5f ? 3 : 2;
                for (int column = 0; column < frontColumns; column++)
                {
                    float placement = x + (column - (frontColumns - 1) * .5f) * width / (frontColumns + .25f);
                    geometry.Box(placement, y, front, .82f, .71f, .041f, Window(random, district));
                }
            }

            Paint trim = district == 3 ? Amber : side > 0 ? Cyan : Violet;
            geometry.Box(inside - side * .068f, 2.87f, z, .07f, .043f, depth * .91f, trim);
            if (district == 0 || district == 2)
                geometry.Box(inside - side * .06f, height - .47f, z, .067f, .05f, depth * .80f, trim);
            for (int column = -1; column <= 1; column++)
            {
                geometry.Box(inside - side * .065f, height * .52f, z + column * depth * .37f,
                    .105f, height * .87f, .14f, Structure);
            }

            geometry.Box(inside - side * .043f, 1.62f, z, .052f, 1.62f, depth * .56f, Glass);
        }

        private static Paint Window(System.Random random, int district)
        {
            double light = random.NextDouble();
            if (light < .12)
                return district == 3 ? Amber : Surface(0xFACDB8, .65f);
            if (light < .37)
                return Surface(district == 3 ? 0xA57073u : 0x7A8FD5u, .40f);
            if (light < .59)
                return Surface(district == 2 ? 0x75CFDBu : 0xB9659Bu, .44f);
            return Glass;
        }

        private static void MakeLamp(Geometry geometry, int side, float z, Paint accent)
        {
            float x = side * 5.36f;
            geometry.Box(x, .41f, z, .27f, .40f, .27f, Structure);
            geometry.Box(x, 2.38f, z, .087f, 4.26f, .097f, RailEdge);
            geometry.Box(x - side * .62f, 4.48f, z, 1.33f, .094f, .097f, RailEdge);
            geometry.Box(x - side * 1.17f, 4.43f, z, .49f, .118f, .35f, Structure);
            geometry.Box(x - side * 1.17f, 4.361f, z, .39f, .027f, .28f, accent);
            geometry.Box(x, 1.06f, z - .059f, .039f, .50f, .016f, accent);
        }

        private static void MakeGuardRail(Geometry geometry, int side, float z)
        {
            float x = side * 6.25f;
            geometry.Box(x, .76f, z - 1.32f, .088f, 1.08f, .082f, RailEdge);
            geometry.Box(x, .76f, z + 1.32f, .088f, 1.08f, .082f, RailEdge);
            geometry.Box(x, 1.28f, z, .076f, .055f, 2.74f, RailEdge);
            geometry.Box(x, .89f, z, .065f, .053f, 2.74f, RailEdge);
        }

        private void MakeSign(Geometry geometry, Transform segment, int side, float z, string caption, Paint color)
        {
            float x = side * 5.88f;
            geometry.Box(x, 1.96f, z, 1.62f, 1.15f, .14f, Structure);
            geometry.Box(x, 1.96f, z - .081f, 1.48f, 1.02f, .036f, Dark);
            geometry.Box(x, 2.47f, z - .091f, 1.48f, .047f, .023f, color);
            geometry.Box(x, 1.44f, z - .091f, 1.48f, .047f, .023f, color);
            geometry.Box(x, .76f, z, .12f, 1.11f, .12f, RailEdge);
            int split = caption.IndexOf(' ');
            AddLettering(segment, caption.Substring(0, split) + "\n" + caption.Substring(split + 1),
                new Vector3(x, 1.95f, z - .113f), 1.38f, .58f, new Color(.84f, .93f, 1f));
        }

        private static void MakeBoardingPlatform(Geometry geometry, int side, float z)
        {
            float x = side * 6.58f;
            geometry.Box(x, 2.83f, z, 1.9f, .14f, 7.1f, Structure);
            geometry.Box(x - side * .91f, 2.73f, z, .055f, .058f, 6.9f, Cyan);
            for (int part = -1; part <= 1; part++)
            {
                float pz = z + part * 2.22f;
                geometry.Box(x, 1.17f, pz, .09f, 1.92f, .075f, RailEdge);
                geometry.Box(x + side * .039f, 1.34f, pz + .92f, .044f, 1.66f, 1.69f, Glass);
                geometry.Box(x - side * .33f, .67f, pz + .89f, .61f, .14f, 1.38f, Structure);
                geometry.Box(x - side * .63f, .60f, pz + .89f, .047f, .047f, 1.35f, Violet);
            }
        }

        private static void MakeMarketStall(Geometry geometry, int side, float z)
        {
            float x = side * 6.32f;
            geometry.Box(x, .79f, z, 1.32f, 1.14f, 2.14f, Structure);
            geometry.Box(x, 1.40f, z, 1.45f, .16f, 2.25f, Amber);
            geometry.Box(x, 2.56f, z, 1.75f, .124f, 2.56f, Surface(0xAE426F, .47f), Quaternion.Euler(0f, 0f, side * 7.5f));
            geometry.Box(x - side * .86f, 2.46f, z, .10f, .082f, 2.51f, Violet);
            for (int index = -1; index <= 1; index++)
            {
                float itemZ = z + index * .54f;
                geometry.Box(x - side * .36f, 1.55f, itemZ, .32f, .16f, .38f, Glass);
                geometry.Box(x - side * .36f, 1.65f, itemZ, .23f, .065f, .27f, index == 0 ? Amber : Cyan);
            }
        }

        private static void MakeLanternSpan(Geometry geometry, float z)
        {
            for (int index = -5; index <= 5; index++)
            {
                float x = index * 1.1f;
                float y = 9.45f + Mathf.Abs(index) * .055f;
                geometry.Box(x, y + .30f, z, 1.12f, .028f, .033f, Grout);
                geometry.Box(x, y + .10f, z, .05f, .35f, .06f, Structure);
                geometry.Box(x, y - .10f, z, .20f, .23f, .19f, index % 2 == 0 ? Amber : Violet);
                geometry.Box(x, y - .25f, z, .12f, .055f, .12f, Amber);
            }
        }

        private static void MakeAqueduct(Geometry geometry, int side)
        {
            float x = side * 6.65f;
            geometry.Box(x, .228f, 18f, .62f, .028f, SegmentLength, Surface(0x317E9D, .31f, 1f));
            geometry.Box(x - side * .33f, .31f, 18f, .08f, .164f, SegmentLength, Structure);
            geometry.Box(x + side * .34f, .26f, 18f, .05f, .078f, SegmentLength, Cyan);
            for (int part = 0; part < 6; part++)
            {
                float z = 3f + part * 6f;
                geometry.Box(x + side * .36f, .81f, z, .15f, 1.19f, .16f, Structure);
                geometry.Box(x + side * .37f, 1.45f, z, .13f, .12f, .15f, Cyan);
                geometry.Box(x, .261f, z + .52f, .63f, .035f, 1.5f, RailEdge);
            }
        }

        private void MakeStationGantry(Geometry geometry, Transform segment, float z)
        {
            foreach (int side in new[] { -1, 1 })
            {
                geometry.Box(side * 6.11f, 4.04f, z, .25f, 7.64f, .46f, Structure);
                geometry.Box(side * 6.10f, 4.04f, z - .244f, .060f, 7.4f, .025f, side > 0 ? Cyan : Violet);
                geometry.Box(side * 5.65f, 8.19f, z, .25f, 1.4f, .46f, Structure, Quaternion.Euler(0, 0, side * 45f));
                geometry.Box(side * 5.65f, 8.20f, z - .245f, .058f, 1.39f, .024f,
                    side > 0 ? Cyan : Violet, Quaternion.Euler(0, 0, side * 45f));
            }
            geometry.Box(0f, 8.72f, z, 10.86f, .24f, .45f, Structure);
            geometry.Box(0f, 8.72f, z - .265f, 3.83f, .53f, .081f, Dark);
            geometry.Box(0f, 8.48f, z - .312f, 3.75f, .034f, .022f, Cyan);
            AddLettering(segment, "LUNE TERMINAL  /  07", new Vector3(0f, 8.73f, z - .322f), 3.65f, .35f, new Color(.80f, .95f, 1f));
        }

        private static void MakeCatwalk(Geometry geometry, float z, Paint accent, float y)
        {
            foreach (int side in new[] { -1, 1 })
            {
                geometry.Box(side * 6.82f, y / 2f, z, .23f, y, .25f, Structure);
                geometry.Box(side * 6.81f, y - .55f, z - .135f, .05f, .65f, .027f, accent);
            }
            geometry.Box(0f, y, z, 13.8f, .31f, 1.2f, Structure);
            geometry.Box(0f, y + .16f, z - .60f, 13.5f, .046f, .049f, accent);
            for (int index = -5; index <= 5; index++)
            {
                geometry.Box(index * 1.19f, y + .50f, z - .55f, .046f, .69f, .045f, RailEdge);
                geometry.Box(index * 1.19f, y - .19f, z - .28f, .22f, .046f, .11f, (index & 1) == 0 ? accent : Lime);
            }
            geometry.Box(0f, y + .83f, z - .55f, 13.5f, .045f, .045f, RailEdge);
        }

        private void MakeMachinery(Geometry geometry, Transform segment, int side, float z)
        {
            float x = side * 6.69f;
            geometry.Box(x, 1.90f, z, .92f, 3.29f, 1.13f, Structure);
            geometry.Box(x, 1.94f, z - .58f, .79f, 1.01f, .051f, Dark);
            for (int section = 0; section < 5; section++)
                geometry.Box(x, .70f + section * .185f, z - .584f, .66f, .051f, .031f, section % 3 == 0 ? Amber : RailEdge);
            geometry.Box(x, 3.64f, z, .72f, .24f, .77f, RailEdge);
            geometry.Box(x, 3.8f, z, .28f, .12f, .34f, Amber);

            GameObject rotor = new GameObject("Foundry exhaust rotor");
            rotor.transform.SetParent(segment, false);
            rotor.transform.localPosition = new Vector3(x, 2.16f, z - .62f);
            var blades = new Geometry();
            blades.Box(0, 0, 0, .17f, .17f, .094f, Amber);
            for (int blade = 0; blade < 5; blade++)
            {
                Quaternion spin = Quaternion.Euler(0f, 0f, blade * 72f + 17f);
                Vector3 p = spin * new Vector3(.23f, 0f, 0f);
                blades.Box(p.x, p.y, 0f, .33f, .102f, .057f, RailEdge, spin);
            }
            AddMesh(rotor.transform, blades, "Baked fan blades");
            rotors.Add(rotor.transform);
        }

        private void BuildDistantSkyline()
        {
            var geometry = new Geometry();
            var random = new System.Random(6128);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < 15; index++)
                {
                    float z = 44f + index * 7.4f;
                    float x = side * Between(random, 17.0f, 36f);
                    float width = Between(random, 4.0f, 10.0f);
                    float height = Between(random, 12f, 35f);
                    geometry.Box(x, height / 2f - 2f, z, width, height, 7.9f, Surface(0x231E3F));
                    if (index % 3 == 0)
                    {
                        geometry.Box(x, height - 2f, z, width * .68f, .055f, 4.6f, Violet);
                        geometry.Box(x, height - 4.2f, z - 3.98f, width * .53f, .17f, .064f, Cyan);
                    }
                }
            }
            AddMesh(transform, geometry, "Distant parallax silhouette");
        }

        private void BuildTraffic()
        {
            for (int index = 0; index < vehicles.Length; index++)
            {
                GameObject vehicle = new GameObject(index < 2 ? "Overhead courier taxi" : "Maintenance drone");
                vehicle.transform.SetParent(transform, false);
                vehicles[index] = vehicle.transform;
                bool taxi = index < 2;
                float width = taxi ? 2.3f : 1.28f;
                float length = taxi ? 3.5f : 1.43f;
                Paint trim = (index & 1) == 0 ? Cyan : Violet;

                var shape = new Geometry();
                shape.Box(0, 0, 0, width, .36f, length, Structure);
                shape.Box(0, .28f, -.21f, width * .72f, .36f, length * .58f, Glass);
                shape.Box(0, .16f, length * .35f, width * .76f, .15f, length * .35f, RailEdge, Quaternion.Euler(8, 0, 0));
                shape.Box(0, -.13f, length * .51f, width * .79f, .081f, .065f, Porcelain);
                shape.Box(0, -.12f, -length * .507f, width * .82f, .075f, .067f, Amber);
                foreach (int side in new[] { -1, 1 })
                {
                    shape.Box(side * width * .52f, -.12f, -.18f, .27f, .26f, length * .83f, RailEdge);
                    shape.Box(side * width * .52f, -.276f, -.18f, .20f, .039f, length * .68f, trim);
                    shape.Box(side * width * .52f, -.12f, -length * .56f, .14f, .072f, .45f, Amber);
                }
                AddMesh(vehicle.transform, shape, "Batched hover chassis");
            }
        }

        private void AddLettering(Transform parent, string text, Vector3 at, float width, float height, Color color)
        {
            if (lettering == null)
                return;

            GameObject label = new GameObject(text.Replace('\n', ' '));
            Transform placement = label.transform;
            placement.SetParent(parent, false);
            placement.localPosition = at;
            TextMesh letteringMesh = label.AddComponent<TextMesh>();
            letteringMesh.text = text;
            letteringMesh.anchor = TextAnchor.MiddleCenter;
            letteringMesh.alignment = TextAlignment.Center;
            letteringMesh.fontSize = 104;
            letteringMesh.characterSize = .1f;
            letteringMesh.font = lettering;
            letteringMesh.color = color;
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = lettering.material;
            Vector3 actual = renderer.localBounds.size;
            if (actual.x > .001f && actual.y > .001f)
                placement.localScale = Vector3.one * Mathf.Min(width / actual.x, height / actual.y);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void AddMesh(Transform parent, Geometry geometry, string title)
        {
            GameObject part = new GameObject(title);
            part.transform.SetParent(parent, false);
            Mesh mesh = geometry.Export(title);
            meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = scenery;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes)
                Destroy(mesh);
            if (scenery != null)
                Destroy(scenery);
        }

        private static float Between(System.Random random, float low, float high)
        {
            return low + (high - low) * (float)random.NextDouble();
        }

        private static Paint Surface(uint color, float emission = 0f, float wetness = 0f)
        {
            return new Paint(new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255), new Vector2(emission, wetness));
        }

        private readonly struct Paint
        {
            internal readonly Color32 Color;
            internal readonly Vector2 Optical;

            internal Paint(Color32 color, Vector2 optical)
            {
                Color = color;
                Optical = optical;
            }
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> points = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<Vector2> optics = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            internal void Box(float x, float y, float z, float width, float height, float depth, Paint paint, Quaternion? turning = null)
            {
                Quaternion rotation = turning ?? Quaternion.identity;
                Vector3 center = new Vector3(x, y, z);
                Vector3 u = rotation * Vector3.right * (width * .5f);
                Vector3 v = rotation * Vector3.up * (height * .5f);
                Vector3 w = rotation * Vector3.forward * (depth * .5f);

                Face(center + u, -w, v, rotation * Vector3.right, paint);
                Face(center - u, w, v, rotation * Vector3.left, paint);
                Face(center + v, w, u, rotation * Vector3.up, paint);
                Face(center - v, u, w, rotation * Vector3.down, paint);
                Face(center + w, u, v, rotation * Vector3.forward, paint);
                Face(center - w, -u, v, rotation * Vector3.back, paint);
            }

            private void Face(Vector3 midpoint, Vector3 u, Vector3 v, Vector3 normal, Paint paint)
            {
                int start = points.Count;
                points.Add(midpoint - u - v);
                points.Add(midpoint + u - v);
                points.Add(midpoint + u + v);
                points.Add(midpoint - u + v);
                for (int corner = 0; corner < 4; corner++)
                {
                    normals.Add(normal);
                    colors.Add(paint.Color);
                    optics.Add(paint.Optical);
                }
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            internal Mesh Export(string title)
            {
                var mesh = new Mesh { name = title };
                if (points.Count > 65535)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(points);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetUVs(1, optics);
                mesh.SetTriangles(triangles, 0, true);
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
