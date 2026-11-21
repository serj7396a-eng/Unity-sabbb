using System.Collections.Generic;
using Railrush.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railrush.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RunnerVisual : MonoBehaviour
    {
        private const float ThighLength = 0.45f;
        private const float ShinLength = 0.43f;
        private const float AnkleClearance = 0.109f;

        private enum Paint
        {
            Carbon, Boot, Graphite, Aluminum, Violet, Cobalt, Shoulder, Mauve, Visor,
            Lime, Mint, Ice, Fuchsia, Signal, White
        }

        private static readonly Color32[] Palette =
        {
            new Color32(18, 21, 34, 255), new Color32(31, 37, 57, 255),
            new Color32(51, 64, 85, 255), new Color32(124, 145, 168, 255),
            new Color32(56, 40, 135, 255), new Color32(51, 73, 195, 255),
            new Color32(112, 92, 218, 255), new Color32(180, 130, 222, 255),
            new Color32(21, 47, 68, 255), new Color32(188, 240, 69, 255),
            new Color32(67, 252, 209, 255), new Color32(92, 209, 255, 255),
            new Color32(243, 79, 218, 255), new Color32(255, 84, 118, 255),
            new Color32(214, 232, 248, 255),
        };

        private static readonly Vector2[] BoardOutline =
        {
            new Vector2(-0.15f, -0.69f), new Vector2(0.14f, -0.69f),
            new Vector2(0.27f, -0.51f), new Vector2(0.34f, -0.19f),
            new Vector2(0.33f, 0.34f), new Vector2(0.24f, 0.58f),
            new Vector2(0.09f, 0.76f), new Vector2(-0.10f, 0.76f),
            new Vector2(-0.25f, 0.57f), new Vector2(-0.33f, 0.32f),
            new Vector2(-0.34f, -0.19f), new Vector2(-0.28f, -0.49f),
        };

        private readonly Transform[] arms = new Transform[2];
        private readonly Transform[] elbows = new Transform[2];
        private readonly Transform[] thighs = new Transform[2];
        private readonly Transform[] shins = new Transform[2];
        private readonly Transform[] ankles = new Transform[2];
        private readonly Transform[] coatTails = new Transform[2];
        private readonly Transform[] boostBoots = new Transform[2];
        private readonly List<Object> resources = new List<Object>();

        private Transform rig;
        private Transform chest;
        private Transform head;
        private Transform sling;
        private Transform board;
        private Transform boardWake;
        private Transform thruster;
        private Transform exhaust;
        private Material suitMaterial;
        private Material plasmaMaterial;
        private float lastTime = -1f;
        private float lastHeight;
        private float verticalVelocity;
        private float gait;
        private float bank;
        private float touchdown;
        private float duck;
        private float flight;
        private float glide;
        private float impact;
        private ImportedCourier courier;

        public void Build()
        {
            if (rig != null)
                return;

            CreateMaterials();
            rig = Pivot(transform, "Courier rig", Vector3.zero);
            chest = Pivot(rig, "Jacket and torso", new Vector3(0f, 0.036f, 0f));
            head = Pivot(chest, "Hood and visor", new Vector3(0f, 0.617f, 0.026f));
            sling = Pivot(chest, "Courier messenger pack", new Vector3(0f, 0.117f, -0.216f));
            BuildBody();
            BuildHead();
            BuildSling();

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                BuildArm(i, side);
                BuildLeg(i, side);
                BuildCoatTail(i, side);
            }

            BuildBoard();
            BuildThruster();
            var candidate = gameObject.AddComponent<ImportedCourier>();
            if (candidate.Build())
            {
                courier = candidate;
                foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                    if (!renderer.transform.IsChildOf(thruster) &&
                        !renderer.transform.IsChildOf(boostBoots[0]) && !renderer.transform.IsChildOf(boostBoots[1]))
                        renderer.enabled = false;
            }
            else Destroy(candidate);
            Pose(0f, 0f, 0f, 0f, false, false, false, false, false);
        }

        public void Pose(float time, float speed, float height, float lateralVelocity,
            bool sliding, bool flying, bool boarding, bool boots, bool crashed)
        {
            if (rig == null)
                Build();

            if (courier != null)
                courier.Pose(time, speed, height, lateralVelocity, sliding, flying, boarding, crashed);

            float dt = Mathf.Clamp(time - lastTime, 0f, 0.075f);
            if (lastTime < 0f || time < lastTime)
            {
                gait = bank = touchdown = duck = flight = glide = impact = verticalVelocity = 0f;
                lastHeight = height;
            }

            lastTime = time;
            if (dt > 0f)
            {
                verticalVelocity = Damp(verticalVelocity, (height - lastHeight) / dt, 19f, dt);
                if (lastHeight > 0.025f && height <= 0.002f && !flying)
                    touchdown = Mathf.Clamp(-verticalVelocity * 0.012f, 0.035f, 0.115f);
                lastHeight = height;
            }

            float effort = Mathf.Clamp01(speed / 36f);
            float run = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(speed / 5f));
            bank = Damp(bank, Mathf.Clamp(lateralVelocity / 12f, -1f, 1f), 17f, dt);
            touchdown = Mathf.MoveTowards(touchdown, 0f, dt * 0.50f);
            duck = Damp(duck, sliding && !flying ? 1f : 0f, sliding ? 42f : 19f, dt);
            flight = Damp(flight, height > 0.025f && !flying ? 1f : 0f, 25f, dt);
            glide = Damp(glide, boarding && !flying ? 1f : 0f, 26f, dt);
            impact = crashed ? Mathf.MoveTowards(impact, 1f, Mathf.Max(dt, Time.unscaledDeltaTime) * 3.2f) : 0f;

            float jump = flight * (1f - duck);
            float tuck = Mathf.Clamp01(height * 0.45f + verticalVelocity * 0.036f);
            float fall = Mathf.SmoothStep(0f, 1f, impact);
            float jet = flying ? 1f : 0f;
            float ride = glide * (1f - jet);
            if (!crashed)
                gait = Mathf.Repeat(gait + dt * run * (10.8f + effort * 5f) * (1f - jump * 0.67f), Mathf.PI * 2f);

            float breath = Mathf.Sin(time * 2.4f);
            float stride = Mathf.Sin(gait);
            float hip = Mathf.Lerp(0.914f + breath * 0.008f, 0.879f + Mathf.Abs(stride) * 0.043f, run) - touchdown;
            hip = Mathf.Lerp(hip, 0.87f + tuck * 0.043f, jump);
            hip = Mathf.Lerp(hip, 0.43f, duck);
            hip = Mathf.Lerp(hip, 0.84f, jet);
            hip = Mathf.Lerp(hip, 0.78f, ride);
            hip = Mathf.Lerp(hip, 0.73f - fall * 0.27f, crashed ? 1f : 0f);

            float bob = Mathf.Sin(time * 8.2f) * 0.016f;
            float boardLift = ride * (0.124f + bob);
            float pitch = Mathf.Lerp(2f, 10f + effort * 8f, run);
            pitch = Mathf.Lerp(pitch, 15f - tuck * 9f, jump);
            pitch = Mathf.Lerp(pitch, -68f, duck);
            pitch = Mathf.Lerp(pitch, 14f, jet);
            pitch = Mathf.Lerp(pitch, 14f + bank * 5f, ride);
            pitch = Mathf.Lerp(pitch, 30f - fall * 15f, crashed ? 1f : 0f);

            float twist = Mathf.Lerp(Mathf.Sin(time * 1.23f) * 3.6f, stride * 11f + bank * 8f, run);
            twist = Mathf.Lerp(twist, 20f - bank * 12f, ride);
            float roll = -bank * 16f + Mathf.Cos(gait) * run * 2.5f + fall * 29f;
            float glance = Mathf.Lerp(Mathf.Sin(time * 1.04f) * 13f, -twist * 0.42f + bank * 14f, run);
            float drag = Mathf.Lerp(4f, 16f + effort * 13f, run) + jump * tuck * 10f + jet * 13f;

            rig.localPosition = new Vector3(-bank * 0.057f, hip + boardLift, -fall * 0.31f);
            rig.localRotation = Quaternion.Euler(0f, bank * 5.8f - stride * run * 3.4f, roll);
            chest.localRotation = Quaternion.Euler(pitch, twist + fall * 18f, -roll * 0.28f);
            head.localRotation = Quaternion.Euler(-pitch * 0.71f + touchdown * 38f, glance, -roll * 0.28f);
            sling.localRotation = Quaternion.Euler(drag * -0.13f + touchdown * 29f, -bank * 5f, roll * -0.25f);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float step = gait + Mathf.PI * i;
                float swing = Mathf.Sin(step);
                float lift = Mathf.Max(0f, -Mathf.Cos(step));
                float footZ = Mathf.Lerp(side * 0.045f, -swing * (0.29f + effort * 0.087f), run);
                float footY = AnkleClearance - hip + 0.045f + run * Mathf.Pow(lift, 1.45f) * (0.23f + effort * 0.076f);
                float footAngle = -run * (lift * 30f - Mathf.Max(0f, -swing) * 10f);
                float shoulderAngle = Mathf.Lerp(side * 6f + breath * 2.7f, -swing * (43f + effort * 14f) - 3f, run);
                float elbowAngle = Mathf.Lerp(17f + breath * 4f, 64f + swing * 17f, run);
                float shoulderRoll = side * (9f + run * swing * 2.5f) - bank * 11f;

                footZ = Mathf.Lerp(footZ, 0.12f + side * (0.15f + tuck * 0.14f), jump);
                footY += jump * (0.16f + tuck * 0.20f - side * 0.11f);
                footAngle = Mathf.Lerp(footAngle, -side * 13f, jump);
                shoulderAngle = Mathf.Lerp(shoulderAngle, -14f + side * (26f + tuck * 12f), jump);
                elbowAngle = Mathf.Lerp(elbowAngle, 56f + side * 16f, jump);
                shoulderRoll = Mathf.Lerp(shoulderRoll, side * 22f - bank * 13f, jump);

                footZ = Mathf.Lerp(footZ, 0.59f + side * 0.15f, duck);
                footY += duck * (0.038f * i - run * lift * 0.19f);
                footAngle = Mathf.Lerp(footAngle, 14f, duck);
                shoulderAngle = Mathf.Lerp(shoulderAngle, 78f + side * 33f, duck);
                elbowAngle = Mathf.Lerp(elbowAngle, 44f + side * 8f, duck);
                shoulderRoll = Mathf.Lerp(shoulderRoll, side * 20f, duck);

                footZ = Mathf.Lerp(footZ, -0.15f + side * 0.13f + Mathf.Sin(time * 7.1f + i) * 0.036f, jet);
                footY = Mathf.Lerp(footY, -0.62f + side * 0.065f, jet);
                footAngle = Mathf.Lerp(footAngle, 24f, jet);
                shoulderAngle = Mathf.Lerp(shoulderAngle, 9f + side * 12f, jet);
                elbowAngle = Mathf.Lerp(elbowAngle, 44f + side * 10f, jet);
                shoulderRoll = Mathf.Lerp(shoulderRoll, side * 24f - bank * 16f, jet);

                footZ = Mathf.Lerp(footZ, side * 0.24f, ride);
                footY = Mathf.Lerp(footY, AnkleClearance - hip + 0.045f - side * bank * 0.019f, ride);
                footAngle = Mathf.Lerp(footAngle, bank * 6f, ride);
                shoulderAngle = Mathf.Lerp(shoulderAngle, -13f + side * 22f, ride);
                elbowAngle = Mathf.Lerp(elbowAngle, 38f - side * 11f, ride);
                shoulderRoll = Mathf.Lerp(shoulderRoll, side * 34f - bank * 16f, ride);

                footZ = Mathf.Lerp(footZ, side * (0.27f + fall * 0.15f), fall);
                shoulderAngle = Mathf.Lerp(shoulderAngle, -27f + side * 36f + fall * 53f, fall);
                elbowAngle = Mathf.Lerp(elbowAngle, 53f - fall * 25f, fall);
                shoulderRoll = Mathf.Lerp(shoulderRoll, side * (29f + fall * 18f), fall);

                SolveLeg(i, footY, footZ, footAngle, side * 2f - bank * 5f, ride * 16f);
                arms[i].localRotation = Quaternion.Euler(shoulderAngle, -side * (3f + Mathf.Max(0f, swing) * run * 7f), shoulderRoll);
                elbows[i].localRotation = Quaternion.Euler(-elbowAngle, 0f, 0f);
                coatTails[i].localRotation = Quaternion.Euler(drag + Mathf.Sin(time * (7f + effort * 10f) + i * 1.7f) * 5f - fall * 19f,
                    -bank * 13f, side * (4f + jump * tuck * 6f));
                SetActive(boostBoots[i], boots && !flying && !boarding && !crashed);
            }

            SetActive(board, boarding && !flying && !crashed);
            SetActive(thruster, flying && !crashed);
            SetActive(sling, !flying || crashed);
            if (board.gameObject.activeSelf)
            {
                board.localPosition = new Vector3(0f, 0.075f + bob, 0f);
                board.localRotation = Quaternion.Euler(2f + Mathf.Sin(time * 8.9f) * 3f, bank * 12f, -bank * 18f);
                if (boardWake != null)
                    boardWake.localScale = new Vector3(1f, 1f, 0.90f + Mathf.Sin(time * 17f) * 0.16f);
            }

            if (thruster.gameObject.activeSelf)
            {
                thruster.localRotation = Quaternion.Euler(Mathf.Sin(time * 12f) * 2.1f, 0f, bank * -4f);
                if (exhaust != null)
                {
                    exhaust.localScale = new Vector3(1f, 1f + Mathf.Sin(time * 43f) * 0.11f + Mathf.Sin(time * 29f) * 0.08f, 1f);
                    exhaust.localRotation = Quaternion.Euler(0f, 0f, -bank * 9f);
                }
            }
        }

        private void SolveLeg(int i, float y, float z, float footAngle, float roll, float toeOut)
        {
            float reach = Mathf.Clamp(new Vector2(y, z).magnitude, 0.13f, ThighLength + ShinLength - 0.004f);
            float cosine = (reach * reach - ThighLength * ThighLength - ShinLength * ShinLength) / (2f * ThighLength * ShinLength);
            float knee = Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f));
            float thigh = Mathf.Atan2(-z, -y) - Mathf.Atan2(ShinLength * Mathf.Sin(knee), ThighLength + ShinLength * Mathf.Cos(knee));
            thighs[i].localRotation = Quaternion.Euler(thigh * Mathf.Rad2Deg, 0f, roll);
            shins[i].localRotation = Quaternion.Euler(knee * Mathf.Rad2Deg, 0f, 0f);
            ankles[i].localRotation = Quaternion.Euler(footAngle - (thigh + knee) * Mathf.Rad2Deg, toeOut, 0f);
        }

        private void BuildBody()
        {
            var hips = new MeshBatch();
            hips.Shell(Paint.Boot, new Band(-0.13f, 0.22f, 0.149f), new Band(-0.085f, 0.254f, 0.173f),
                new Band(0.045f, 0.258f, 0.163f), new Band(0.087f, 0.221f, 0.140f));
            Bake(rig, hips);

            var body = new MeshBatch();
            body.Shell(Paint.Cobalt, new Band(-0.061f, 0.254f, 0.182f, -0.011f), new Band(0.04f, 0.272f, 0.184f, -0.012f),
                new Band(0.18f, 0.259f, 0.172f), new Band(0.36f, 0.313f, 0.199f),
                new Band(0.51f, 0.309f, 0.175f, 0.007f), new Band(0.581f, 0.174f, 0.137f, 0.015f));
            body.Shell(Paint.Violet, new Band(0.515f, 0.199f, 0.141f, 0.012f), new Band(0.592f, 0.171f, 0.141f, 0.013f),
                new Band(0.631f, 0.159f, 0.125f, 0.019f));

            body.Plate(new Vector3(0f, 0.071f, 0.004f), new Vector3(0.529f, 0.048f, 0.35f), 0.35f, Paint.Violet);
            body.Beam(new Vector3(0f, 0.10f, 0.18f), new Vector3(0f, 0.52f, 0.181f), 0.032f, 0.022f, Paint.Carbon);
            body.Beam(new Vector3(0.008f, 0.22f, 0.192f), new Vector3(0.008f, 0.47f, 0.198f), 0.009f, 0.017f, Paint.Mint);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                body.Beam(new Vector3(side * 0.264f, 0.48f, -0.146f), new Vector3(side * 0.19f, 0.31f, -0.224f), 0.049f, 0.036f, Paint.Lime);
                body.Beam(new Vector3(side * 0.19f, 0.31f, -0.224f), new Vector3(side * 0.221f, 0.104f, -0.182f), 0.042f, 0.031f, Paint.Lime);
                body.Beam(new Vector3(side * 0.239f, 0.484f, 0.154f), new Vector3(side * 0.141f, 0.107f, 0.17f), 0.037f, 0.025f, Paint.Lime);
                body.Plate(new Vector3(side * 0.217f, 0.136f, -0.203f), new Vector3(0.070f, 0.066f, 0.044f), 0.27f, Paint.Carbon);
                body.Plate(new Vector3(side * 0.217f, 0.14f, -0.229f), new Vector3(0.037f, 0.017f, 0.009f), 0.22f, Paint.Ice);
            }

            Bake(chest, body);
        }

        private void BuildHead()
        {
            var skull = new MeshBatch();
            skull.Shell(Paint.Violet, new Band(-0.013f, 0.123f, 0.122f, -0.003f), new Band(0.061f, 0.178f, 0.157f, -0.01f),
                new Band(0.145f, 0.205f, 0.179f, -0.015f), new Band(0.267f, 0.206f, 0.183f, -0.02f),
                new Band(0.343f, 0.149f, 0.152f, -0.025f), new Band(0.376f, 0.078f, 0.092f, -0.022f));
            skull.Round(new Vector3(0f, 0.145f, 0.122f), new Vector3(0.319f, 0.24f, 0.153f), Paint.Carbon);
            skull.Plate(new Vector3(0f, 0.19f, 0.163f), new Vector3(0.314f, 0.111f, 0.113f), 0.48f, Paint.Visor);
            skull.Beam(new Vector3(-0.126f, 0.195f, 0.219f), new Vector3(0.126f, 0.195f, 0.219f), 0.018f, 0.016f, Paint.Mint);
            skull.Beam(new Vector3(-0.013f, 0.333f, -0.145f), new Vector3(-0.013f, 0.197f, -0.199f), 0.031f, 0.016f, Paint.Shoulder);
            skull.Beam(new Vector3(-0.013f, 0.204f, -0.203f), new Vector3(-0.013f, 0.133f, -0.188f), 0.013f, 0.014f, Paint.Ice);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                skull.Round(new Vector3(side * 0.179f, 0.18f, -0.004f), new Vector3(0.122f, 0.149f, 0.154f), Paint.Graphite);
                skull.Round(new Vector3(side * 0.223f, 0.18f, -0.003f), new Vector3(0.058f, 0.111f, 0.112f), Paint.Carbon);
                skull.Beam(new Vector3(side * 0.23f, 0.148f, -0.051f), new Vector3(side * 0.237f, 0.202f, -0.045f), 0.014f, 0.018f, Paint.Mint);
                skull.Beam(new Vector3(side * 0.103f, 0.31f, -0.145f), new Vector3(side * 0.136f, 0.257f, -0.171f), 0.019f, 0.015f, Paint.Mauve);
            }

            Bake(head, skull);
        }

        private void BuildSling()
        {
            var pack = new MeshBatch();
            pack.Shell(Paint.Carbon, new Band(0.061f, 0.143f, 0.081f, -0.041f), new Band(0.11f, 0.207f, 0.108f, -0.044f),
                new Band(0.313f, 0.207f, 0.104f, -0.04f), new Band(0.375f, 0.141f, 0.074f, -0.019f));
            pack.Plate(new Vector3(0f, 0.225f, -0.119f), new Vector3(0.354f, 0.230f, 0.133f), 0.44f, Paint.Graphite);
            pack.Plate(new Vector3(0f, 0.222f, -0.186f), new Vector3(0.281f, 0.166f, 0.026f), 0.37f, Paint.Carbon);
            pack.Beam(new Vector3(-0.112f, 0.248f, -0.204f), new Vector3(-0.012f, 0.190f, -0.204f), 0.039f, 0.015f, Paint.Lime);
            pack.Beam(new Vector3(0.012f, 0.190f, -0.204f), new Vector3(0.112f, 0.248f, -0.204f), 0.039f, 0.015f, Paint.Lime);
            pack.Plate(new Vector3(0f, 0.139f, -0.190f), new Vector3(0.239f, 0.017f, 0.013f), 0.28f, Paint.Ice);
            for (int led = -2; led <= 2; led++)
                pack.Plate(new Vector3(led * 0.026f, 0.31f, -0.168f), new Vector3(0.013f, 0.011f, 0.010f), 0.35f, led == 2 ? Paint.Signal : Paint.Mint);

            Bake(sling, pack);
        }

        private void BuildArm(int i, float side)
        {
            arms[i] = Pivot(chest, i == 0 ? "Left shoulder" : "Right shoulder", new Vector3(side * 0.309f, 0.485f, 0f));
            elbows[i] = Pivot(arms[i], "Articulated elbow", new Vector3(0f, -0.300f, 0f));
            var sleeve = new MeshBatch();
            sleeve.Shell(Paint.Cobalt, new Band(-0.31f, 0.079f, 0.083f), new Band(-0.15f, 0.103f, 0.107f),
                new Band(-0.057f, 0.111f, 0.116f), new Band(0.019f, 0.109f, 0.106f));
            sleeve.Round(new Vector3(side * 0.026f, -0.041f, 0.002f), new Vector3(0.252f, 0.232f, 0.257f), Paint.Shoulder);
            sleeve.Beam(new Vector3(side * 0.046f, -0.081f, -0.109f), new Vector3(side * 0.042f, -0.235f, -0.089f), 0.027f, 0.016f, Paint.Mauve);
            sleeve.Beam(new Vector3(side * 0.117f, -0.058f, -0.042f), new Vector3(side * 0.098f, -0.15f, -0.047f), 0.012f, 0.016f, Paint.Mint);
            Bake(arms[i], sleeve);

            var forearm = new MeshBatch();
            forearm.Shell(Paint.Violet, new Band(-0.255f, 0.064f, 0.075f), new Band(-0.165f, 0.082f, 0.082f),
                new Band(0.01f, 0.088f, 0.084f), new Band(0.046f, 0.076f, 0.078f));
            forearm.Plate(new Vector3(0f, -0.236f, 0f), new Vector3(0.149f, 0.058f, 0.144f), 0.30f, Paint.Carbon);
            forearm.Plate(new Vector3(side * 0.038f, -0.233f, -0.078f), new Vector3(0.041f, 0.029f, 0.021f), 0.32f, Paint.Ice);
            forearm.Round(new Vector3(0f, -0.296f, 0.007f), new Vector3(0.123f, 0.126f, 0.152f), Paint.Carbon);
            forearm.Round(new Vector3(-side * 0.045f, -0.273f, 0.046f), new Vector3(0.058f, 0.096f, 0.074f), Paint.Boot);
            if (i == 0)
            {
                forearm.Plate(new Vector3(-0.048f, -0.148f, -0.078f), new Vector3(0.092f, 0.111f, 0.036f), 0.35f, Paint.Graphite);
                forearm.Plate(new Vector3(-0.049f, -0.15f, -0.099f), new Vector3(0.056f, 0.068f, 0.011f), 0.36f, Paint.Visor);
                forearm.Plate(new Vector3(-0.05f, -0.136f, -0.106f), new Vector3(0.026f, 0.009f, 0.006f), 0.25f, Paint.Mint);
            }

            Bake(elbows[i], forearm);
        }

        private void BuildLeg(int i, float side)
        {
            thighs[i] = Pivot(rig, i == 0 ? "Left hip" : "Right hip", new Vector3(side * 0.176f, -0.045f, 0f));
            shins[i] = Pivot(thighs[i], "Knee hinge", new Vector3(0f, -ThighLength, 0f));
            ankles[i] = Pivot(shins[i], "Ankle heel", new Vector3(0f, -ShinLength, 0f));
            boostBoots[i] = Pivot(ankles[i], "Boost boot modules", Vector3.zero);

            var trousers = new MeshBatch();
            trousers.Shell(Paint.Boot, new Band(-0.455f, 0.090f, 0.107f), new Band(-0.29f, 0.113f, 0.119f),
                new Band(-0.116f, 0.129f, 0.136f), new Band(0.01f, 0.132f, 0.141f), new Band(0.058f, 0.109f, 0.127f));
            trousers.Plate(new Vector3(side * 0.103f, -0.169f, -0.048f), new Vector3(0.084f, 0.164f, 0.154f), 0.34f, Paint.Graphite);
            trousers.Plate(new Vector3(side * 0.139f, -0.118f, -0.102f), new Vector3(0.023f, 0.038f, 0.025f), 0.28f, Paint.Lime);
            Bake(thighs[i], trousers);

            var shin = new MeshBatch();
            shin.Shell(Paint.Carbon, new Band(-0.427f, 0.068f, 0.073f), new Band(-0.248f, 0.082f, 0.085f, -0.009f),
                new Band(-0.089f, 0.098f, 0.098f), new Band(0.039f, 0.092f, 0.096f), new Band(0.085f, 0.079f, 0.084f));
            shin.Round(new Vector3(0f, -0.008f, 0.079f), new Vector3(0.190f, 0.196f, 0.102f), Paint.Graphite);
            shin.Beam(new Vector3(side * 0.047f, -0.119f, -0.095f), new Vector3(side * 0.047f, -0.273f, -0.093f), 0.020f, 0.019f, Paint.Ice);
            Bake(shins[i], shin);

            var shoe = new MeshBatch();
            shoe.Plate(new Vector3(0f, -0.08f, 0.073f), new Vector3(0.229f, 0.056f, 0.389f), 0.30f, Paint.White);
            shoe.Round(new Vector3(0f, -0.016f, 0.078f), new Vector3(0.218f, 0.157f, 0.373f), Paint.Boot);
            shoe.Round(new Vector3(0f, -0.011f, -0.071f), new Vector3(0.22f, 0.142f, 0.145f), Paint.Violet);
            shoe.Round(new Vector3(0f, -0.026f, 0.182f), new Vector3(0.205f, 0.118f, 0.164f), Paint.Graphite);
            shoe.Plate(new Vector3(0f, -0.021f, -0.145f), new Vector3(0.104f, 0.027f, 0.014f), 0.33f, Paint.Lime);
            shoe.Beam(new Vector3(side * 0.104f, -0.03f, -0.051f), new Vector3(side * 0.105f, -0.038f, 0.114f), 0.018f, 0.018f, Paint.Mint);
            Bake(ankles[i], shoe);

            var booster = new MeshBatch();
            booster.Plate(new Vector3(0f, -0.08f, 0.072f), new Vector3(0.245f, 0.038f, 0.402f), 0.30f, Paint.Lime);
            booster.Plate(new Vector3(0f, 0.014f, -0.154f), new Vector3(0.181f, 0.115f, 0.095f), 0.32f, Paint.Graphite);
            booster.Plate(new Vector3(0f, 0.019f, -0.206f), new Vector3(0.123f, 0.035f, 0.018f), 0.35f, Paint.Mint);
            booster.Beam(new Vector3(-0.118f, -0.024f, -0.125f), new Vector3(-0.117f, -0.048f, 0.175f), 0.021f, 0.022f, Paint.Lime);
            booster.Beam(new Vector3(0.118f, -0.024f, -0.125f), new Vector3(0.117f, -0.048f, 0.175f), 0.021f, 0.022f, Paint.Lime);
            Bake(boostBoots[i], booster, false);
        }

        private void BuildCoatTail(int i, float side)
        {
            coatTails[i] = Pivot(chest, i == 0 ? "Left split hem" : "Right split hem", new Vector3(side * 0.150f, 0.048f, -0.149f));
            var tail = new MeshBatch();
            tail.Shell(Paint.Cobalt, new Band(-0.259f, 0.102f, 0.031f, -0.079f), new Band(-0.197f, 0.126f, 0.035f, -0.054f),
                new Band(-0.028f, 0.131f, 0.052f, -0.001f), new Band(0.037f, 0.112f, 0.063f, 0.027f));
            tail.Beam(new Vector3(side * 0.031f, -0.064f, -0.059f), new Vector3(side * 0.048f, -0.205f, -0.087f), 0.024f, 0.015f, Paint.Mauve);
            tail.Beam(new Vector3(-0.086f, -0.232f, -0.101f), new Vector3(0.086f, -0.232f, -0.101f), 0.014f, 0.017f, Paint.Ice);
            Bake(coatTails[i], tail);
        }

        private void BuildBoard()
        {
            board = Pivot(transform, "Maglev deck", new Vector3(0f, 0.075f, 0f));
            var deck = new MeshBatch();
            deck.Deck(BoardOutline, new Vector3(0f, -0.016f, 0f), 0.105f, 1f, 0.32f, Paint.Graphite);
            deck.Deck(BoardOutline, new Vector3(0f, -0.031f, 0f), 0.043f, 1.03f, 0.34f, Paint.Fuchsia);
            deck.Deck(BoardOutline, new Vector3(0f, 0.034f, 0f), 0.045f, 0.93f, 0.31f, Paint.Carbon);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                deck.Plate(new Vector3(0f, 0.053f, side * 0.229f), new Vector3(0.47f, 0.020f, 0.37f), 0.38f, Paint.Violet,
                    Quaternion.Euler(0f, side * 8f, 0f));
                deck.Plate(new Vector3(side * 0.227f, -0.070f, -0.30f), new Vector3(0.129f, 0.063f, 0.26f), 0.36f, Paint.Boot,
                    Quaternion.Euler(0f, side * 6f, 0f));
                deck.Plate(new Vector3(side * 0.228f, -0.074f, -0.432f), new Vector3(0.093f, 0.038f, 0.023f), 0.37f, Paint.Mint);
                deck.Beam(new Vector3(side * 0.231f, 0.059f, -0.135f), new Vector3(side * 0.12f, 0.062f, 0.052f), 0.027f, 0.020f, Paint.White);
            }

            deck.Plate(new Vector3(0f, 0.063f, 0f), new Vector3(0.107f, 0.019f, 0.107f), 0.27f, Paint.Fuchsia,
                Quaternion.Euler(0f, 45f, 0f));
            Bake(board, deck, false);

            if (plasmaMaterial == null)
                return;

            var wake = new RibbonBatch();
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var start = new Vector3(side * 0.234f, -0.072f, -0.41f);
                var drift = new Vector3(side * 0.061f, -0.022f, -1.02f);
                wake.Add(start, drift, Vector3.right * 0.11f, new Color32(183, 49, 255, 135));
                wake.Add(start, drift * 0.79f, new Vector3(0.057f, 0.043f, 0f), new Color32(80, 236, 247, 210));
            }

            boardWake = BakeEffect(board, "Levitation wake", Vector3.zero, wake);
        }

        private void BuildThruster()
        {
            thruster = Pivot(chest, "Flight canisters", new Vector3(0f, 0.274f, -0.302f));
            var pack = new MeshBatch();
            pack.Plate(new Vector3(0f, 0.022f, -0.006f), new Vector3(0.468f, 0.342f, 0.208f), 0.45f, Paint.Carbon);
            pack.Plate(new Vector3(0f, 0.042f, -0.11f), new Vector3(0.377f, 0.250f, 0.058f), 0.36f, Paint.Graphite);
            pack.Plate(new Vector3(0f, 0.058f, -0.142f), new Vector3(0.107f, 0.107f, 0.020f), 0.30f, Paint.Ice,
                Quaternion.Euler(0f, 0f, 45f));
            pack.Plate(new Vector3(0f, 0.058f, -0.156f), new Vector3(0.055f, 0.055f, 0.010f), 0.28f, Paint.Carbon,
                Quaternion.Euler(0f, 0f, 45f));

            var flames = new RibbonBatch();
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float x = side * 0.281f;
                pack.ShellAt(new Vector3(x, 0f, -0.033f), Paint.Aluminum,
                    new Band(-0.237f, 0.097f, 0.099f), new Band(-0.153f, 0.124f, 0.115f),
                    new Band(0.154f, 0.12f, 0.115f), new Band(0.254f, 0.098f, 0.088f, 0.010f),
                    new Band(0.287f, 0.064f, 0.059f, 0.014f));
                pack.Plate(new Vector3(x, 0.021f, -0.127f), new Vector3(0.182f, 0.34f, 0.055f), 0.38f, Paint.Graphite);
                pack.Plate(new Vector3(x, 0.044f, -0.160f), new Vector3(0.022f, 0.225f, 0.018f), 0.30f, Paint.Mint);
                pack.Plate(new Vector3(x, 0.203f, -0.137f), new Vector3(0.094f, 0.037f, 0.022f), 0.27f, Paint.Ice);
                pack.ShellAt(new Vector3(x, -0.278f, -0.033f), Paint.Carbon,
                    new Band(-0.061f, 0.109f, 0.112f), new Band(-0.031f, 0.124f, 0.124f), new Band(0.034f, 0.103f, 0.108f));
                pack.ShellAt(new Vector3(x, -0.335f, -0.033f), Paint.Ice,
                    new Band(-0.019f, 0.076f, 0.076f), new Band(0.018f, 0.088f, 0.089f));

                var nozzle = new Vector3(x, 0f, -0.033f);
                var thrust = new Vector3(side * 0.067f, -0.96f, -0.29f);
                for (int blade = 0; blade < 3; blade++)
                {
                    float angle = (blade * 60f + 12f) * Mathf.Deg2Rad;
                    var across = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    flames.Add(nozzle, thrust, across * 0.133f, new Color32(17, 124, 255, 124));
                    flames.Add(nozzle, thrust * 0.61f, across * 0.059f, new Color32(113, 241, 255, 204));
                }
            }

            Bake(thruster, pack);
            if (plasmaMaterial != null)
                exhaust = BakeEffect(thruster, "Twin plasma exhaust", new Vector3(0f, -0.348f, 0f), flames);
        }

        private void CreateMaterials()
        {
            var suitShader = Resources.Load<Shader>("RailrushCourierSuit");
            var plasmaShader = Resources.Load<Shader>("RailrushCourierPlasma");
            if (suitShader == null || plasmaShader == null)
                throw new System.InvalidOperationException("Railrush courier shaders are missing from Resources.");

            suitMaterial = Track(new Material(suitShader) { name = "Courier suit and equipment" });
            suitMaterial.mainTexture = MakePalette(Palette, "Courier dye swatches");
            var emission = new Color32[Palette.Length];
            for (int i = 0; i < emission.Length; i++)
                emission[i] = new Color32(0, 0, 0, 255);
            for (int i = (int)Paint.Lime; i <= (int)Paint.Signal; i++)
                emission[i] = Palette[i];
            suitMaterial.SetTexture("_EmissionMap", MakePalette(emission, "Courier illuminated trim swatches"));

            plasmaMaterial = Track(new Material(plasmaShader) { name = "Additive plasma and maglev wake" });
            plasmaMaterial.mainTexture = MakePlasmaMask();
        }

        private Texture2D MakePalette(Color32[] colors, string label)
        {
            var texture = Track(new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false, false));
            texture.name = label;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            return texture;
        }

        private Texture2D MakePlasmaMask()
        {
            const int width = 20;
            const int length = 48;
            var texture = Track(new Texture2D(width, length, TextureFormat.RGBA32, false, false));
            texture.name = "Radial plasma falloff";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[width * length];
            for (int y = 0; y < length; y++)
            {
                float axis = y / (float)(length - 1);
                for (int x = 0; x < width; x++)
                {
                    float radial = Mathf.Max(0f, 1f - Mathf.Abs((x / (float)(width - 1)) * 2f - 1f));
                    float intensity = Mathf.Pow(radial, 2.4f) * Mathf.Pow(1f - axis, 1.45f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(intensity * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void Bake(Transform node, MeshBatch batch, bool shadows = true)
        {
            Mesh mesh = Track(batch.Create(node.name));
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = suitMaterial;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
        }

        private Transform BakeEffect(Transform parent, string label, Vector3 position, RibbonBatch ribbons)
        {
            var node = Pivot(parent, label, position);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = Track(ribbons.Create(label));
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = plasmaMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return node;
        }

        private static Transform Pivot(Transform parent, string label, Vector3 position)
        {
            Transform child = new GameObject(label).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.gameObject.layer = parent.gameObject.layer;
            return child;
        }

        private static float Damp(float from, float to, float decay, float dt)
        {
            return Mathf.Lerp(from, to, 1f - Mathf.Exp(-decay * dt));
        }

        private static void SetActive(Transform node, bool active)
        {
            if (node.gameObject.activeSelf != active)
                node.gameObject.SetActive(active);
        }

        private T Track<T>(T asset) where T : Object
        {
            resources.Add(asset);
            return asset;
        }

        private void OnDestroy()
        {
            foreach (Object asset in resources)
            {
                if (Application.isPlaying)
                    Destroy(asset);
                else
                    DestroyImmediate(asset);
            }
        }

        private readonly struct Band
        {
            public readonly float Y;
            public readonly float XRadius;
            public readonly float ZRadius;
            public readonly float Z;

            public Band(float y, float xRadius, float zRadius, float z = 0f)
            {
                Y = y;
                XRadius = xRadius;
                ZRadius = zRadius;
                Z = z;
            }
        }

        private sealed class MeshBatch
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> indices = new List<int>();

            public void Shell(Paint paint, params Band[] bands)
            {
                ShellAt(Vector3.zero, paint, bands);
            }

            public void ShellAt(Vector3 center, Paint paint, params Band[] bands)
            {
                const int segments = 12;
                var rings = new Vector3[bands.Length][];
                for (int row = 0; row < bands.Length; row++)
                {
                    Band band = bands[row];
                    rings[row] = new Vector3[segments];
                    for (int corner = 0; corner < segments; corner++)
                    {
                        float angle = Mathf.PI * 2f * (corner + 0.5f) / segments;
                        rings[row][corner] = new Vector3(Mathf.Cos(angle) * band.XRadius, band.Y,
                            Mathf.Sin(angle) * band.ZRadius + band.Z);
                    }
                }

                Hull(rings, center, Quaternion.identity, paint);
            }

            public void Round(Vector3 center, Vector3 size, Paint paint)
            {
                var bands = new Band[7];
                for (int i = 0; i < bands.Length; i++)
                {
                    float angle = Mathf.PI * Mathf.Lerp(-0.486f, 0.486f, i / (bands.Length - 1f));
                    float radius = Mathf.Cos(angle) * 0.5f;
                    bands[i] = new Band(Mathf.Sin(angle) * size.y * 0.5f, size.x * radius, size.z * radius);
                }

                ShellAt(center, paint, bands);
            }

            public void Plate(Vector3 center, Vector3 size, float bevel, Paint paint, Quaternion? rotation = null)
            {
                float corner = 0.5f - bevel * 0.46f;
                var profile = new[]
                {
                    new Vector2(-corner * size.x, -0.5f * size.z), new Vector2(corner * size.x, -0.5f * size.z),
                    new Vector2(0.5f * size.x, -corner * size.z), new Vector2(0.5f * size.x, corner * size.z),
                    new Vector2(corner * size.x, 0.5f * size.z), new Vector2(-corner * size.x, 0.5f * size.z),
                    new Vector2(-0.5f * size.x, corner * size.z), new Vector2(-0.5f * size.x, -corner * size.z),
                };

                Section(profile, center, size.y, 1f, 0f, 1f - bevel * 0.62f, rotation ?? Quaternion.identity, paint);
            }

            public void Beam(Vector3 start, Vector3 finish, float width, float depth, Paint paint)
            {
                Vector3 delta = finish - start;
                Plate((start + finish) * 0.5f, new Vector3(width, delta.magnitude, depth), 0.23f, paint,
                    Quaternion.FromToRotation(Vector3.up, delta.normalized));
            }

            public void Deck(Vector2[] profile, Vector3 center, float thickness, float scale, float curl, Paint paint)
            {
                Section(profile, center, thickness, scale, curl, 0.90f, Quaternion.identity, paint);
            }

            private void Section(Vector2[] profile, Vector3 center, float thickness, float scale, float curl,
                float bevelScale, Quaternion rotation, Paint paint)
            {
                var rings = new Vector3[4][];
                for (int level = 0; level < rings.Length; level++)
                {
                    rings[level] = new Vector3[profile.Length];
                    bool outer = level == 0 || level == rings.Length - 1;
                    float ringScale = scale * (outer ? bevelScale : 1f);
                    float y = (level < 2 ? -1f : 1f) * thickness * (outer ? 0.5f : 0.22f);
                    for (int i = 0; i < profile.Length; i++)
                    {
                        Vector2 point = profile[i] * ringScale;
                        float nose = Mathf.Max(0f, Mathf.Abs(point.y) - 0.33f) * curl;
                        rings[level][i] = new Vector3(point.x, y + nose, point.y);
                    }
                }

                Hull(rings, center, rotation, paint);
            }

            private void Hull(Vector3[][] rings, Vector3 center, Quaternion rotation, Paint paint)
            {
                int width = rings[0].Length;
                int start = vertices.Count;
                var swatch = new Vector2(((int)paint + 0.5f) / Palette.Length, 0.5f);
                foreach (Vector3[] ring in rings)
                {
                    foreach (Vector3 point in ring)
                        AddVertex(point, center, rotation, swatch);
                }

                for (int level = 0; level < rings.Length - 1; level++)
                {
                    int lower = start + level * width;
                    int upper = lower + width;
                    for (int i = 0; i < width; i++)
                    {
                        int next = (i + 1) % width;
                        Triangle(lower + i, upper + next, lower + next);
                        Triangle(lower + i, upper + i, upper + next);
                    }
                }

                Cap(rings[0], center, rotation, swatch, false);
                Cap(rings[rings.Length - 1], center, rotation, swatch, true);
            }

            private void Cap(Vector3[] ring, Vector3 center, Quaternion rotation, Vector2 swatch, bool top)
            {
                int start = vertices.Count;
                Vector3 average = Vector3.zero;
                float lowest = float.PositiveInfinity;
                foreach (Vector3 point in ring)
                {
                    AddVertex(point, center, rotation, swatch);
                    average += point;
                    lowest = Mathf.Min(lowest, point.y);
                }

                average /= ring.Length;
                average.y = lowest;
                int middle = vertices.Count;
                AddVertex(average, center, rotation, swatch);
                for (int i = 0; i < ring.Length; i++)
                {
                    int next = (i + 1) % ring.Length;
                    if (top)
                        Triangle(middle, start + next, start + i);
                    else
                        Triangle(middle, start + i, start + next);
                }
            }

            private void AddVertex(Vector3 point, Vector3 center, Quaternion rotation, Vector2 swatch)
            {
                vertices.Add(center + rotation * point);
                uv.Add(swatch);
            }

            private void Triangle(int a, int b, int c)
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
            }

            public Mesh Create(string label)
            {
                var mesh = new Mesh { name = label };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        private sealed class RibbonBatch
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int> indices = new List<int>();

            public void Add(Vector3 from, Vector3 forward, Vector3 across, Color32 paint)
            {
                const int segments = 7;
                int start = vertices.Count;
                for (int step = 0; step <= segments; step++)
                {
                    float axial = step / (float)segments;
                    float radius = 0.55f + 0.66f * Mathf.Sin(axial * Mathf.PI);
                    Vector3 middle = from + forward * axial;
                    vertices.Add(middle - across * radius);
                    vertices.Add(middle + across * radius);
                    colors.Add(paint);
                    colors.Add(paint);
                    uv.Add(new Vector2(0f, axial));
                    uv.Add(new Vector2(1f, axial));
                }

                for (int step = 0; step < segments; step++)
                {
                    int bottom = start + step * 2;
                    indices.Add(bottom);
                    indices.Add(bottom + 1);
                    indices.Add(bottom + 2);
                    indices.Add(bottom + 1);
                    indices.Add(bottom + 3);
                    indices.Add(bottom + 2);
                }
            }

            public Mesh Create(string label)
            {
                var mesh = new Mesh { name = label };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
