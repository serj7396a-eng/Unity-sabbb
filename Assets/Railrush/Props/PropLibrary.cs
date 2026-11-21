using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railrush.Props
{
    public sealed class PropLibrary : MonoBehaviour
    {
        private const float TrainLength = 10.2f;
        private const float RoofHeight = 3.42f;
        private const float RampLength = 7.8f;
        private const float RampLanding = 0.58f;

        private readonly Dictionary<string, GameObject> templates = new(StringComparer.Ordinal);
        private readonly List<Mesh> generatedMeshes = new();
        private readonly List<Material> generatedMaterials = new();
        private Transform templateRoot;
        private Shader shader;
        private Mesh cube;
        private Mesh cylinder;
        private Mesh coinRing;
        private Mesh orbitRing;
        private Mesh arrowGem;
        private Mesh magnetCurve;
        private Mesh trainShell;
        private Mesh rampDeck;
        private Material carbon;
        private Material steel;
        private Material silver;
        private Material blue;
        private Material glass;
        private Material teal;
        private Material acid;
        private Material coral;
        private Material violet;
        private Material amber;
        private Material gold;
        private Material paleGold;

        public void Build()
        {
            if (templateRoot != null)
                return;

            shader = Resources.Load<Shader>("RailrushMetroProp");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("Railrush prop shader is unavailable.");

            templateRoot = new GameObject("Railrush Prop Templates").transform;
            templateRoot.SetParent(transform, false);
            templateRoot.gameObject.SetActive(false);
            BuildMaterials();
            BuildMeshes();

            AddTemplate("train", true, batch => BuildTrain(batch, false));
            AddTemplate("ramp_train", true, batch => BuildTrain(batch, true));
            AddTemplate("hurdle", true, BuildHurdle);
            AddTemplate("gate", true, BuildGate);
            AddTemplate("coin", false, BuildCoin);
            AddTemplate("jet", false, BuildJet);
            AddTemplate("board", false, BuildBoard);
            AddTemplate("boots", false, BuildBoots);
            AddTemplate("magnet", false, BuildMagnet);
            AddTemplate("double", false, BuildDouble);
        }

        public GameObject Create(string kind)
        {
            Build();
            if (kind == null || !templates.TryGetValue(kind, out var template))
                return null;

            var prop = Instantiate(template, Vector3.zero, Quaternion.identity);
            prop.name = kind;
            prop.SetActive(true);
            return prop;
        }

        private void OnDestroy()
        {
            foreach (var mesh in generatedMeshes)
                Destroy(mesh);
            foreach (var material in generatedMaterials)
                Destroy(material);
        }

        private void BuildMaterials()
        {
            carbon = Surface("Graphite polymer", "182637", 0.24f, 0.18f);
            steel = Surface("Gunmetal", "465a73", 0.58f, 0.54f);
            silver = Surface("Ceramic shell", "bec8d4", 0.34f, 0.55f);
            blue = Surface("Indigo enamel", "3651a2", 0.45f, 0.56f);
            glass = Surface("Night glass", "173249", 0.42f, 0.82f, "163056", 0.17f);
            teal = Surface("Glacial cyan", "74eaf4", 0.12f, 0.45f, "20bbdd", 0.65f);
            acid = Surface("Signal lime", "daef7c", 0.18f, 0.46f, "c4ea4c", 0.59f);
            coral = Surface("Brake red", "fb655f", 0.08f, 0.48f, "f33547", 0.59f);
            violet = Surface("Ion violet", "b179eb", 0.18f, 0.59f, "a659ed", 0.56f);
            amber = Surface("Safety orange", "f6a958", 0.3f, 0.37f, "ed762b", 0.28f);
            gold = Surface("Brushed gold", "d69031", 0.65f, 0.62f, "e8972b", 0.2f);
            paleGold = Surface("Gold highlight", "fff2a3", 0.42f, 0.67f, "f4be52", 0.64f);
        }

        private Material Surface(string label, string hex, float metal, float polish, string glow = null, float intensity = 0f)
        {
            var material = new Material(shader) { name = label, enableInstancing = true };
            material.SetColor("_Color", ReadColor(hex));
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Glossiness", polish);
            if (glow != null)
            {
                material.SetColor("_EmissionColor", ReadColor(glow) * intensity);
                material.EnableKeyword("_EMISSION");
            }
            generatedMaterials.Add(material);
            return material;
        }

        private static Color ReadColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        private void BuildMeshes()
        {
            cube = Profile("Box", new[]
            {
                new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f)
            }, -0.5f, 0.5f);
            var circle = new Vector2[24];
            for (var i = 0; i < circle.Length; i++)
            {
                var angle = 2f * Mathf.PI * i / circle.Length;
                circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
            }
            cylinder = Profile("Round shell", circle, -1f, 1f);
            trainShell = Profile("Orbital train shell", new[]
            {
                new Vector2(-0.83f, 0.43f), new Vector2(0.83f, 0.43f), new Vector2(1.03f, 0.66f),
                new Vector2(1.03f, 2.94f), new Vector2(0.91f, 3.18f), new Vector2(0.69f, RoofHeight),
                new Vector2(-0.69f, RoofHeight), new Vector2(-0.91f, 3.18f), new Vector2(-1.03f, 2.94f),
                new Vector2(-1.03f, 0.66f)
            }, 0.08f, TrainLength - 0.08f);
            rampDeck = Profile("Service ramp deck", new[]
            {
                new Vector2(-RampLength, 0f), new Vector2(-RampLanding, 3.24f), new Vector2(-RampLanding, RoofHeight),
                new Vector2(-RampLength, 0.025f)
            }, -0.81f, 0.81f);
            coinRing = Arc("Coin beveled rim", 0.225f, 0.057f, 0f, 360f, 24, 8);
            orbitRing = Arc("Pickup ellipse", 0.346f, 0.014f, 0f, 360f, 24, 5);
            magnetCurve = Arc("Magnet horseshoe", 0.17f, 0.073f, 180f, 180f, 14, 7);
            arrowGem = Profile("Octagonal pickup prism", new[]
            {
                new Vector2(-0.17f, -0.28f), new Vector2(0.17f, -0.28f), new Vector2(0.26f, -0.15f),
                new Vector2(0.26f, 0.15f), new Vector2(0.17f, 0.28f), new Vector2(-0.17f, 0.28f),
                new Vector2(-0.26f, 0.15f), new Vector2(-0.26f, -0.15f)
            }, -0.09f, 0.09f);
        }

        private void BuildTrain(Batch batch, bool ramp)
        {
            var livery = ramp ? steel : blue;
            var marker = ramp ? amber : teal;
            batch.Add(trainShell, silver, Vector3.zero, Vector3.one, Quaternion.identity);
            batch.Box(livery, 0f, 0.73f, TrainLength * 0.5f, 2.02f, 0.47f, TrainLength - 0.11f);
            batch.Box(carbon, 0f, 0.38f, TrainLength * 0.5f, 1.71f, 0.28f, TrainLength - 0.21f);
            batch.Box(steel, 0f, 3.36f, TrainLength * 0.5f, 1.27f, 0.12f, TrainLength);

            for (var direction = -1; direction <= 1; direction += 2)
            {
                for (var segment = 0; segment < 5; segment++)
                {
                    var z = 1.09f + segment * 2.02f;
                    batch.Box(carbon, direction * 1.035f, 2.17f, z, 0.068f, 0.96f, 1.51f);
                    batch.Box(glass, direction * 1.074f, 2.17f, z, 0.023f, 0.83f, 1.36f);
                    batch.Box(marker, direction * 1.087f, 2.56f, z, 0.013f, 0.029f, 1.26f);
                    batch.Box(steel, direction * 1.088f, 2.17f, z, 0.014f, 0.88f, 0.042f);
                    batch.Box(livery, direction * 1.049f, 1.01f, z, 0.075f, 0.18f, 1.67f);
                }
                batch.Box(livery, direction * 1.053f, 1.39f, 5.1f, 0.063f, 0.24f, 10.02f);
                batch.Box(marker, direction * 1.086f, 1.44f, 5.1f, 0.014f, 0.039f, 9.92f);
                batch.Box(steel, direction * 0.91f, 0.36f, 5.1f, 0.11f, 0.15f, 8.76f);
                for (var axle = 0; axle < 4; axle++)
                {
                    var z = 1.38f + (axle % 2) * 0.92f + (axle / 2) * 6.03f;
                    Disc(batch, carbon, new Vector3(direction * 0.749f, 0.26f, z), 0.52f, 0.15f, Quaternion.Euler(0f, 0f, 90f));
                    Disc(batch, steel, new Vector3(direction * 0.834f, 0.26f, z), 0.31f, 0.033f, Quaternion.Euler(0f, 0f, 90f));
                }
            }

            BuildTrainFace(batch, 0.08f, 1, marker, livery);
            BuildTrainFace(batch, TrainLength - 0.08f, -1, coral, livery);
            for (var slat = 0; slat < 12; slat++)
            {
                var z = 2.7f + slat * 0.41f;
                batch.Box(carbon, 0f, 3.415f, z, 0.67f, 0.01f, 0.071f);
            }
            if (ramp)
                BuildRamp(batch);
        }

        private void BuildTrainFace(Batch batch, float z, int inward, Material signal, Material livery)
        {
            var outward = -inward;
            batch.Box(livery, 0f, 1.8f, z, 1.89f, 2.75f, 0.13f);
            batch.Box(carbon, 0f, 2.19f, z + outward * 0.044f, 1.79f, 1.14f, 0.045f);
            batch.Box(glass, 0f, 2.21f, z + outward * 0.056f, 1.65f, 0.96f, 0.018f);
            batch.Box(steel, 0f, 2.21f, z + outward * 0.065f, 0.052f, 0.97f, 0.012f);
            batch.Box(carbon, 0f, 2.9f, z + outward * 0.061f, 0.86f, 0.24f, 0.021f);
            batch.Box(signal, 0f, 2.9f, z + outward * 0.061f, 0.61f, 0.048f, 0.024f);
            batch.Box(steel, 0f, 0.36f, z + outward * 0.026f, 1.72f, 0.24f, 0.074f);
            batch.Box(carbon, 0f, 0.21f, z + inward * 0.02f, 0.41f, 0.21f, 0.15f);
            for (var side = -1; side <= 1; side += 2)
            {
                batch.Box(carbon, side * 0.67f, 0.87f, z + outward * 0.055f, 0.43f, 0.26f, 0.024f);
                batch.Box(signal, side * 0.67f, 0.89f, z + outward * 0.065f, 0.34f, 0.084f, 0.013f);
                batch.Box(silver, side * 0.67f, 0.99f, z + outward * 0.063f, 0.331f, 0.024f, 0.011f);
                batch.Box(coral, side * 0.81f, 0.59f, z + outward * 0.06f, 0.13f, 0.058f, 0.015f);
            }
        }

        private void BuildRamp(Batch batch)
        {
            batch.Add(rampDeck, steel, Vector3.zero, Vector3.one, Quaternion.Euler(0f, -90f, 0f));
            batch.Box(steel, 0f, 3.33f, -RampLanding * 0.5f, 1.62f, 0.18f, RampLanding);
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 0.77f;
                batch.Beam(carbon, new Vector3(x, 0.022f, -7.75f), new Vector3(x, 3.34f, -RampLanding), 0.13f, 0.13f);
                batch.Beam(amber, new Vector3(x, 0.034f, -7.73f), new Vector3(x, 3.394f, -RampLanding), 0.051f, 0.037f);
                batch.Box(carbon, x, 3.34f, -RampLanding * 0.5f, 0.13f, 0.13f, RampLanding);
                batch.Box(amber, x, 3.398f, -RampLanding * 0.5f, 0.051f, 0.036f, RampLanding);
                for (var brace = 1; brace <= 5; brace++)
                {
                    var z = -7.64f + brace * 1.34f;
                    var y = RampTop(z) - 0.16f;
                    batch.Beam(carbon, new Vector3(x, 0.08f, z), new Vector3(x, y, z), 0.086f, 0.11f);
                    batch.Beam(steel, new Vector3(x, 0.09f, z - 0.9f), new Vector3(x, y, z), 0.078f, 0.077f);
                }
            }
            for (var tread = 1; tread <= 16; tread++)
            {
                var z = -RampLength + tread * 0.447f;
                batch.Box(tread % 4 == 0 ? amber : carbon, 0f, RampTop(z) + 0.006f, z, 1.39f, 0.022f, 0.069f);
            }
            for (var mark = 0; mark < 3; mark++)
            {
                var tipZ = -5.1f + mark * 1.54f;
                var tailZ = tipZ - 0.46f;
                var tip = new Vector3(0f, RampTop(tipZ) + 0.025f, tipZ);
                batch.Beam(teal, new Vector3(-0.32f, RampTop(tailZ) + 0.025f, tailZ), tip, 0.065f, 0.025f);
                batch.Beam(teal, new Vector3(0.32f, RampTop(tailZ) + 0.025f, tailZ), tip, 0.065f, 0.025f);
            }
        }

        private static float RampTop(float z)
        {
            return Mathf.Lerp(0.025f, RoofHeight, (z + RampLength) / (RampLength - RampLanding));
        }

        private void BuildHurdle(Batch batch)
        {
            batch.Box(carbon, 0f, 0.48f, 0f, 2.06f, 0.28f, 0.28f);
            batch.Box(amber, 0f, 0.47f, -0.148f, 1.89f, 0.19f, 0.023f);
            batch.Box(amber, 0f, 0.47f, 0.148f, 1.89f, 0.19f, 0.023f);
            for (var patch = -3; patch <= 3; patch++)
            {
                var x = patch * 0.245f;
                batch.Box(carbon, x, 0.47f, -0.164f, 0.078f, 0.2f, 0.018f, 24f);
                batch.Box(carbon, x, 0.47f, 0.164f, 0.078f, 0.2f, 0.018f, 24f);
            }
            for (var side = -1; side <= 1; side += 2)
            {
                batch.Box(carbon, side * 0.92f, 0.075f, 0f, 0.33f, 0.15f, 0.63f);
                batch.Box(steel, side * 0.92f, 0.32f, 0f, 0.18f, 0.38f, 0.2f);
                batch.Box(teal, side * 0.92f, 0.529f, -0.171f, 0.102f, 0.048f, 0.014f);
            }
        }

        private void BuildGate(Batch batch)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 0.96f;
                batch.Box(carbon, x, 0.081f, 0f, 0.35f, 0.162f, 0.66f);
                batch.Box(steel, x, 1.245f, 0f, 0.194f, 2.49f, 0.34f);
                batch.Box(carbon, x, 1.23f, -0.18f, 0.226f, 0.76f, 0.05f);
                batch.Box(teal, x, 1.24f, -0.21f, 0.057f, 0.65f, 0.018f);
                batch.Box(amber, x, 2.516f, 0f, 0.23f, 0.068f, 0.37f);
            }
            batch.Box(carbon, 0f, 1.098f, 0f, 2.08f, 0.596f, 0.362f);
            batch.Box(blue, 0f, 1.12f, -0.193f, 1.81f, 0.468f, 0.026f);
            batch.Box(steel, 0f, 2.383f, 0f, 2.08f, 0.207f, 0.35f);
            batch.Box(glass, 0f, 1.866f, 0.019f, 1.72f, 0.91f, 0.104f);
            batch.Box(amber, 0f, 0.831f, -0.195f, 1.79f, 0.062f, 0.029f);
            batch.Box(amber, 0f, 0.831f, 0.195f, 1.79f, 0.062f, 0.029f);
            for (var arrow = -1; arrow <= 1; arrow++)
            {
                var x = arrow * 0.47f;
                GlyphBar(batch, acid, new Vector2(x - 0.112f, 1.24f), new Vector2(x, 1.08f), -0.214f, 0.047f);
                GlyphBar(batch, acid, new Vector2(x + 0.112f, 1.24f), new Vector2(x, 1.08f), -0.214f, 0.047f);
            }
        }

        private void BuildCoin(Batch batch)
        {
            batch.Add(coinRing, paleGold, Vector3.zero, Vector3.one, Quaternion.identity);
            Disc(batch, gold, Vector3.zero, 0.422f, 0.086f, Quaternion.Euler(90f, 0f, 0f));
            Disc(batch, paleGold, Vector3.zero, 0.319f, 0.091f, Quaternion.Euler(90f, 0f, 0f));
            Disc(batch, gold, Vector3.zero, 0.284f, 0.101f, Quaternion.Euler(90f, 0f, 0f));
            for (var side = -1; side <= 1; side += 2)
            {
                var z = side * 0.054f;
                GlyphBar(batch, paleGold, new Vector2(0.043f, 0.107f), new Vector2(-0.045f, 0.012f), z, 0.035f);
                GlyphBar(batch, paleGold, new Vector2(-0.05f, 0.009f), new Vector2(0.044f, 0.01f), z, 0.034f);
                GlyphBar(batch, paleGold, new Vector2(0.045f, 0.006f), new Vector2(-0.028f, -0.102f), z, 0.035f);
            }
        }

        private void BuildJet(Batch batch)
        {
            PickupRing(batch, teal);
            batch.Box(carbon, 0f, 0.033f, 0f, 0.32f, 0.38f, 0.16f);
            batch.Box(silver, 0f, 0.084f, -0.091f, 0.175f, 0.22f, 0.035f);
            batch.Box(teal, 0f, 0.13f, -0.117f, 0.12f, 0.033f, 0.021f);
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 0.17f;
                var pivot = Quaternion.Euler(0f, 0f, side * -12f);
                Disc(batch, violet, new Vector3(x, 0.019f, 0f), 0.138f, 0.313f, pivot);
                Disc(batch, steel, new Vector3(x + side * 0.034f, -0.156f, 0f), 0.11f, 0.084f, pivot);
                Disc(batch, teal, new Vector3(x + side * 0.042f, -0.201f, 0f), 0.073f, 0.056f, pivot);
                Disc(batch, silver, new Vector3(x - side * 0.031f, 0.163f, 0f), 0.104f, 0.071f, pivot);
            }
        }

        private void BuildBoard(Batch batch)
        {
            PickupRing(batch, violet);
            var lean = Quaternion.Euler(20f, 0f, -27f);
            batch.Add(arrowGem, violet, Vector3.zero, new Vector3(0.69f, 1.13f, 0.37f), lean);
            batch.Add(arrowGem, carbon, lean * new Vector3(0f, 0f, -0.037f), new Vector3(0.58f, 1.02f, 0.13f), lean);
            batch.Add(arrowGem, teal, lean * new Vector3(0f, 0f, -0.051f), new Vector3(0.19f, 0.75f, 0.12f), lean);
            for (var tip = -1; tip <= 1; tip += 2)
            {
                var y = tip * 0.18f;
                var rotation = lean * Quaternion.Euler(90f, 0f, 0f);
                Disc(batch, steel, lean * new Vector3(0f, y, 0.041f), 0.176f, 0.053f, rotation);
                Disc(batch, amber, lean * new Vector3(0f, y, 0.069f), 0.1f, 0.017f, rotation);
            }
        }

        private void BuildBoots(Batch batch)
        {
            PickupRing(batch, acid);
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 0.133f;
                var rotation = Quaternion.Euler(-12f, side * 22f, side * -8f);
                var origin = new Vector3(x, 0.035f * side, 0f);
                batch.Add(cube, carbon, origin + rotation * new Vector3(0f, -0.138f, -0.05f), new Vector3(0.193f, 0.074f, 0.31f), rotation);
                batch.Add(cube, acid, origin + rotation * new Vector3(0f, -0.054f, -0.035f), new Vector3(0.176f, 0.108f, 0.29f), rotation);
                batch.Add(cube, steel, origin + rotation * new Vector3(0f, 0.075f, 0.027f), new Vector3(0.168f, 0.224f, 0.19f), rotation);
                batch.Add(cube, acid, origin + rotation * new Vector3(0f, 0.137f, -0.067f), new Vector3(0.124f, 0.048f, 0.032f), rotation);
                batch.Add(cube, teal, origin + rotation * new Vector3(0f, -0.15f, -0.206f), new Vector3(0.141f, 0.03f, 0.025f), rotation);
            }
        }

        private void BuildMagnet(Batch batch)
        {
            PickupRing(batch, coral);
            batch.Add(magnetCurve, coral, Vector3.zero, Vector3.one, Quaternion.identity);
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 0.17f;
                batch.Box(coral, x, 0.046f, 0f, 0.147f, 0.124f, 0.146f);
                batch.Box(silver, x, 0.137f, 0f, 0.153f, 0.088f, 0.151f);
                batch.Box(teal, x, 0.178f, 0f, 0.124f, 0.02f, 0.121f);
            }
            GlyphBar(batch, acid, new Vector2(0.042f, 0.179f), new Vector2(-0.041f, 0.086f), -0.004f, 0.023f);
            GlyphBar(batch, acid, new Vector2(-0.042f, 0.086f), new Vector2(0.023f, 0.086f), -0.004f, 0.022f);
            GlyphBar(batch, acid, new Vector2(0.023f, 0.088f), new Vector2(-0.014f, 0.016f), -0.004f, 0.023f);
        }

        private void BuildDouble(Batch batch)
        {
            PickupRing(batch, violet);
            batch.Add(arrowGem, violet, Vector3.zero, Vector3.one, Quaternion.identity);
            batch.Add(arrowGem, blue, Vector3.zero, new Vector3(0.83f, 0.83f, 1.13f), Quaternion.identity);
            for (var side = -1; side <= 1; side += 2)
            {
                var z = side * 0.112f;
                var x = side * -0.076f;
                GlyphBar(batch, paleGold, new Vector2(x - 0.054f, 0.1f), new Vector2(x + 0.054f, 0.1f), z, 0.026f);
                GlyphBar(batch, paleGold, new Vector2(x + 0.054f, 0.10f), new Vector2(x + 0.054f, 0.009f), z, 0.026f);
                GlyphBar(batch, paleGold, new Vector2(x - 0.054f, 0.004f), new Vector2(x + 0.054f, 0.004f), z, 0.026f);
                GlyphBar(batch, paleGold, new Vector2(x - 0.054f, 0.004f), new Vector2(x - 0.054f, -0.095f), z, 0.026f);
                GlyphBar(batch, paleGold, new Vector2(x - 0.054f, -0.095f), new Vector2(x + 0.054f, -0.095f), z, 0.026f);
                GlyphBar(batch, paleGold, new Vector2(-x - 0.047f, -0.056f), new Vector2(-x + 0.047f, 0.064f), z, 0.024f);
                GlyphBar(batch, paleGold, new Vector2(-x + 0.047f, -0.056f), new Vector2(-x - 0.047f, 0.064f), z, 0.024f);
            }
        }

        private void PickupRing(Batch batch, Material material)
        {
            batch.Add(orbitRing, material, new Vector3(0f, -0.024f, 0f), Vector3.one, Quaternion.Euler(58f, 12f, -16f));
        }

        private void Disc(Batch batch, Material material, Vector3 center, float diameter, float depth, Quaternion rotation)
        {
            batch.Add(cylinder, material, center, new Vector3(diameter, diameter, depth * 0.5f), rotation * Quaternion.Euler(-90f, 0f, 0f));
        }

        private static void GlyphBar(Batch batch, Material material, Vector2 start, Vector2 end, float z, float width)
        {
            var span = end - start;
            var center = (start + end) * 0.5f;
            var tilt = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
            batch.Box(material, center.x, center.y, z, span.magnitude + width * 0.25f, width, 0.018f, tilt);
        }

        private void AddTemplate(string kind, bool castShadows, Action<Batch> build)
        {
            var root = new GameObject(kind);
            root.transform.SetParent(templateRoot, false);
            var batch = new Batch(cube);
            build(batch);
            foreach (var pair in batch.Geometry)
            {
                var mesh = new Mesh { name = kind + " / " + pair.Key.name };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.UploadMeshData(true);
                generatedMeshes.Add(mesh);

                var shell = new GameObject(pair.Key.name);
                shell.transform.SetParent(root.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = shell.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = castShadows && (pair.Key == carbon || pair.Key == silver || pair.Key == steel)
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
                renderer.receiveShadows = castShadows;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            templates.Add(kind, root);
        }

        private Mesh Profile(string label, Vector2[] points, float near, float far)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var side = 0; side < 2; side++)
            {
                var offset = vertices.Count;
                var z = side == 0 ? near : far;
                foreach (var point in points)
                    vertices.Add(new Vector3(point.x, point.y, z));
                for (var j = 1; j < points.Length - 1; j++)
                {
                    triangles.Add(offset);
                    triangles.Add(offset + j + (side == 0 ? 1 : 0));
                    triangles.Add(offset + j + (side == 0 ? 0 : 1));
                }
            }
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                var offset = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, near));
                vertices.Add(new Vector3(b.x, b.y, near));
                vertices.Add(new Vector3(b.x, b.y, far));
                vertices.Add(new Vector3(a.x, a.y, far));
                AddQuad(triangles, offset, offset + 1, offset + 2, offset + 3);
            }
            return BuildMesh(label, vertices, triangles);
        }

        private Mesh Arc(string label, float radius, float tube, float start, float sweep, int slices, int sections)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var row = 0; row <= slices; row++)
            {
                var radians = (start + sweep * row / slices) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
                for (var side = 0; side <= sections; side++)
                {
                    var ring = 2f * Mathf.PI * side / sections;
                    var normal = outward * Mathf.Cos(ring) + Vector3.forward * Mathf.Sin(ring);
                    vertices.Add(outward * radius + normal * tube);
                    normals.Add(normal);
                    if (row == slices || side == sections)
                        continue;
                    var index = row * (sections + 1) + side;
                    var next = index + sections + 1;
                    AddQuad(triangles, index, next, next + 1, index + 1);
                }
            }
            var mesh = BuildMesh(label, vertices, triangles);
            mesh.SetNormals(normals);
            return mesh;
        }

        private Mesh BuildMesh(string label, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = label };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            return mesh;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private sealed class Batch
        {
            private readonly Mesh cube;
            public readonly Dictionary<Material, List<CombineInstance>> Geometry = new();

            public Batch(Mesh cube)
            {
                this.cube = cube;
            }

            public void Add(Mesh shape, Material material, Vector3 center, Vector3 size, Quaternion rotation)
            {
                if (!Geometry.TryGetValue(material, out var entries))
                {
                    entries = new List<CombineInstance>();
                    Geometry.Add(material, entries);
                }
                entries.Add(new CombineInstance
                {
                    mesh = shape,
                    transform = Matrix4x4.TRS(center, rotation, size)
                });
            }

            public void Box(Material material, float x, float y, float z, float width, float height, float depth, float tilt = 0f)
            {
                Add(cube, material, new Vector3(x, y, z), new Vector3(width, height, depth), Quaternion.Euler(0f, 0f, tilt));
            }

            public void Beam(Material material, Vector3 start, Vector3 end, float width, float height)
            {
                var span = end - start;
                var axis = Mathf.Abs(Vector3.Dot(span.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                Add(cube, material, (start + end) * 0.5f, new Vector3(width, height, span.magnitude), Quaternion.LookRotation(span, axis));
            }
        }
    }
}
