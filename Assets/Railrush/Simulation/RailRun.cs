using System;
using System.Collections.Generic;

namespace Railrush.Simulation
{
    public sealed class TrackObject
    {
        public readonly string Kind;
        public readonly int Lane;
        public readonly float At;
        public readonly float Elevation;
        public bool Removed { get; internal set; }
        internal bool Passed;

        internal TrackObject(string kind, int lane, float at, float elevation = 0)
        {
            Kind = kind;
            Lane = lane;
            At = at;
            Elevation = elevation;
        }
    }

    public sealed class RailRun
    {
        public const float StepSeconds = 1f / 120f;
        public const float LaneWidth = 2.55f;
        public const float RoofHeight = 3.42f;
        public const float RampLength = 7.8f;
        public const float TrainLength = 10.2f;
        public const float MaximumSpeed = 31f;

        private static readonly string[] UpgradeCycle = { "magnet", "double", "jet", "board", "boots" };
        private readonly Random random;
        private readonly List<TrackObject> objects = new List<TrackObject>();
        private int nextRowIndex, previousSafeLane;
        private float nextRowAt, jumpBuffer, coyote, holdTime, landingSlide, coinChainTime;
        private float slideTime, jetTime, bootsTime, boardTime, magnetTime, doubleTime, safeTime;
        private int chain, distanceScore, coinScore;
        private bool jumpHeld, variableJump;
        private float scoreRemainder;

        public RailRun(int seed) { random = new Random(seed); }

        public event Action<string, int> Effect;
        public IReadOnlyList<TrackObject> Objects => objects;
        public float Distance { get; private set; }
        public float Speed { get; private set; } = 13;
        public float Clock { get; private set; }
        public float X { get; private set; }
        public float Height { get; private set; }
        public float Support { get; private set; }
        public float VelocityX { get; private set; }
        public float VelocityY { get; private set; }
        public float BodyHeight { get; private set; } = 1.75f;
        public int Lane { get; private set; } = 1;
        public int Coins { get; private set; }
        public int Score => distanceScore + coinScore;
        public int Combo => Math.Min(4, 1 + chain / 6);
        public int BoardStock { get; private set; } = 1;
        public bool Grounded { get; private set; } = true;
        public bool Crashed { get; private set; }
        public bool Sliding => slideTime > 0;
        public bool Boarding => boardTime > 0;
        public bool Flying => jetTime > 0;
        public bool Boots => bootsTime > 0;
        public bool Magnet => magnetTime > 0;
        public bool DoubleScore => doubleTime > 0;
        public float JetTime => jetTime;
        public float BootsTime => bootsTime;
        public float BoardTime => boardTime;
        public float MagnetTime => magnetTime;
        public float DoubleTime => doubleTime;
        public static float LaneX(int lane) => (lane - 1) * LaneWidth;

        public void Reset()
        {
            foreach (var item in objects) item.Removed = true;
            objects.Clear();
            Distance = Clock = X = Height = Support = VelocityX = VelocityY = 0;
            nextRowAt = 105;
            nextRowIndex = 3;
            previousSafeLane = Lane = BoardStock = 1;
            Coins = chain = distanceScore = coinScore = 0;
            jumpBuffer = coyote = holdTime = landingSlide = coinChainTime = scoreRemainder = 0;
            slideTime = jetTime = bootsTime = boardTime = magnetTime = doubleTime = safeTime = 0;
            BodyHeight = 1.75f;
            Speed = 13;
            Grounded = true;
            Crashed = jumpHeld = variableJump = false;
            for (int i = 0; i < 6; i++) Add("coin", 1, 14 + i * 3.2f, .86f);
            Add("hurdle", 1, 40);
            Trail(0, 40);
            Add("train", 0, 61);
            Trail(1, 61);
            Add("gate", 2, 83);
            Trail(1, 83);
            Add("boots", 1, 51, 1.05f);
            Add("board", 1, 73, 1.05f);
            Add("jet", 1, 94, 1.05f);
            Populate();
        }

        public void ChangeLane(int direction)
        {
            int next = Math.Max(0, Math.Min(2, Lane + Math.Sign(direction)));
            if (next == Lane || Crashed) return;
            Lane = next;
            Emit("switch");
        }

        public void QueueJump(bool held)
        {
            if (Crashed || Flying) return;
            jumpHeld = held;
            jumpBuffer = .135f;
        }

        public void ReleaseJump() { jumpHeld = false; }

        public void Slide()
        {
            if (Crashed || Flying) return;
            if (!Grounded)
            {
                VelocityY = Math.Min(VelocityY, -13.7f);
                variableJump = false;
                landingSlide = .48f;
                return;
            }
            if (slideTime < .1f)
            {
                slideTime = .72f;
                Emit("slide");
            }
        }

        public bool UseBoard()
        {
            if (BoardStock <= 0 || Boarding || Flying || Crashed) return false;
            BoardStock--;
            boardTime = 16;
            Emit("board");
            return true;
        }

        public void Step()
        {
            if (Crashed) return;
            const float dt = StepSeconds;
            float before = Distance;
            float beforeX = X;
            float beforeHeight = Height;
            Clock += dt;
            Tick(ref jetTime); Tick(ref bootsTime); Tick(ref boardTime); Tick(ref magnetTime); Tick(ref doubleTime);
            Tick(ref safeTime); Tick(ref landingSlide); Tick(ref jumpBuffer); Tick(ref slideTime);
            Tick(ref coinChainTime);
            if (coinChainTime <= 0) chain = 0;

            float baseSpeed = Math.Min(MaximumSpeed, 13f + Distance * .012f);
            float pace = baseSpeed + (Flying ? 1.2f : Boarding ? .75f : 0);
            if (Sliding) pace *= .96f + .093f * (slideTime / .72f);
            Speed = Approach(Speed, pace, (Grounded ? 5.4f : 3.3f) * dt);
            Distance += Speed * dt;
            UpdateLateral(dt);
            UpdateAltitude(dt);
            BodyHeight = Approach(BodyHeight, Sliding ? .58f : 1.75f, (Sliding ? 19 : 11) * dt);

            scoreRemainder += (Distance - before) * (DoubleScore ? 14 : 7);
            int whole = (int)scoreRemainder;
            distanceScore += whole;
            scoreRemainder -= whole;
            Populate();
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                var item = objects[i];
                if (Distance - item.At > (IsTrain(item.Kind) ? 16 : 8))
                {
                    item.Removed = true;
                    objects.RemoveAt(i);
                    continue;
                }
                if (item.Removed) continue;
                if (IsHazard(item.Kind))
                {
                    CheckHazard(item, before, beforeX, beforeHeight);
                    if (Crashed) return;
                }
                else
                {
                    TryPickup(item, before);
                }
            }
        }

        private void UpdateLateral(float dt)
        {
            float target = LaneX(Lane);
            float error = target - X;
            float desired = Clamp(9.25f * error, -9.1f, 9.1f);
            float accel = Flying ? 48 : Grounded ? Sliding ? 40 : 67 : 34;
            VelocityX = Approach(VelocityX, desired, accel * dt);
            float next = X + VelocityX * dt;
            if (Math.Abs(error) < .13f && (target - next) * error <= 0)
            {
                X = target;
                VelocityX = 0;
            }
            else X = Clamp(next, -LaneWidth - .12f, LaneWidth + .12f);
        }

        private void UpdateAltitude(float dt)
        {
            if (Flying)
            {
                Height = Approach(Height, 6.9f, (11f - Math.Max(0, Height - 4.7f)) * dt);
                Support = 0;
                safeTime = 1.15f;
                Grounded = false;
                VelocityY = 0;
                jumpBuffer = coyote = 0;
                return;
            }
            float oldSupport = Support;
            Support = Surface(Distance);
            if (Support > Height + .25f) Support = 0;
            if (Grounded && Support < oldSupport - .06f)
                Grounded = false;
            if (Grounded)
            {
                Height = Support;
                VelocityY = 0;
                coyote = .092f;
            }
            else Tick(ref coyote);

            if (jumpBuffer > 0 && (Grounded || coyote > 0) && !Sliding)
            {
                jumpBuffer = coyote = 0;
                holdTime = 0;
                VelocityY = Boots ? 14.4f : 9.35f;
                variableJump = true;
                Grounded = false;
                Emit("jump");
            }

            if (Grounded) return;
            holdTime += dt;
            float gravity = VelocityY > 0 ? (variableJump && jumpHeld && holdTime < .19f ? 22 : 43) : 29;
            float oldHeight = Height;
            VelocityY -= gravity * dt;
            Height += VelocityY * dt;
            float floor = oldHeight + .035f >= Support ? Support : 0;
            if (Height > floor || VelocityY > 0) return;
            Height = floor;
            Grounded = true;
            variableJump = false;
            VelocityY = 0;
            if (landingSlide > 0)
            {
                slideTime = .69f;
                landingSlide = 0;
                Emit("slide");
            }
            else Emit("land");
        }

        private float Surface(float distance)
        {
            foreach (var item in objects)
            {
                if (!IsTrain(item.Kind) || Math.Abs(X - LaneX(item.Lane)) > .92f) continue;
                float z = distance - item.At;
                if (z >= -.12f && z <= TrainLength - .15f) return RoofHeight;
                if (item.Kind == "ramp_train" && z >= -RampLength && z < 0)
                    return RoofHeight * Clamp((z + RampLength) / (RampLength - .58f), 0, 1);
            }
            return 0;
        }

        private void CheckHazard(TrackObject item, float before, float beforeX, float beforeHeight)
        {
            if (Flying || safeTime > 0 || item.Passed) return;
            float start = item.At - (IsTrain(item.Kind) ? .54f : .26f);
            float end = item.At + (IsTrain(item.Kind) ? TrainLength + .35f : .30f);
            if (Distance < start || before > end) return;
            float crossing = Clamp((start - before) / (Distance - before), 0, 1);
            float x = beforeX + crossing * (X - beforeX);
            if (Math.Min(Math.Abs(X - LaneX(item.Lane)), Math.Abs(x - LaneX(item.Lane))) > 1.04f) return;
            float y = beforeHeight + crossing * (Height - beforeHeight);
            if (IsTrain(item.Kind) && y >= RoofHeight - .045f) return;
            if (item.Kind == "hurdle" && y >= .68f) return;
            if (item.Kind == "gate" && (y >= 2.55f || y + BodyHeight <= 1.06f)) return;
            if (Boarding)
            {
                boardTime = 0;
                safeTime = 1.12f;
                item.Passed = true;
                Emit("shield");
                return;
            }
            Crashed = true;
            Emit("crash");
        }

        private void TryPickup(TrackObject item, float before)
        {
            bool coin = item.Kind == "coin";
            bool attracted = coin && Magnet;
            if (item.At < before - .46f || item.At > Distance + (attracted ? 1.5f : .64f)) return;
            if (Math.Abs(X - LaneX(item.Lane)) > (attracted ? 5.4f : .86f)) return;
            if (Math.Abs(Height + (Sliding ? .62f : 1.02f) - item.Elevation) > (attracted ? 2.7f : .82f)) return;
            item.Removed = true;
            if (coin)
            {
                Coins++;
                int beforeCombo = Combo;
                chain++;
                coinChainTime = 3;
                coinScore += 25 * Combo * (DoubleScore ? 2 : 1);
                Emit(Combo > beforeCombo ? "combo" : "coin", Combo);
                return;
            }
            Activate(item.Kind);
        }

        internal void Activate(string kind)
        {
            switch (kind)
            {
                case "board": BoardStock = Math.Min(4, BoardStock + 1); break;
                case "boots": bootsTime = 13; break;
                case "magnet": magnetTime = 13; break;
                case "double": doubleTime = 13; break;
                case "jet":
                    jetTime = 7.5f;
                    slideTime = 0;
                    Grounded = false;
                    for (int i = 0; i < 25; i++)
                    {
                        int lane = Math.Max(0, Math.Min(2, Lane + (int)Math.Round(Math.Sin(i * .42) * 1.25)));
                        Add("coin", lane, Distance + 17 + i * Math.Min(6.9f, Speed * .24f), 7.92f);
                    }
                    break;
            }
            Emit("boost");
        }

        internal TrackObject Add(string kind, int lane, float distance, float elevation = 0)
        {
            var item = new TrackObject(kind, lane, distance, elevation);
            objects.Add(item);
            return item;
        }

        private void Populate()
        {
            while (nextRowAt < Distance + 126)
            {
                int safe = Math.Max(0, Math.Min(2, previousSafeLane + random.Next(-1, 2)));
                int other = random.Next(0, 2);
                int chosen = (safe + 1 + other) % 3;
                AddHazard(chosen);
                if (nextRowIndex >= 5 && random.NextDouble() < .72) AddHazard((safe + 2 - other) % 3);
                Trail(safe, nextRowAt);
                if (nextRowIndex >= 5 && nextRowIndex % 4 == 1)
                    Add(UpgradeCycle[(nextRowIndex / 4 - 1) % UpgradeCycle.Length], safe, nextRowAt + 7.3f, 1.05f);
                previousSafeLane = safe;
                nextRowIndex++;
                nextRowAt += Math.Max(22, Math.Min(MaximumSpeed, 13 + nextRowAt * .012f) * (1.28f + .21f * (float)random.NextDouble()));
            }
        }

        private void AddHazard(int lane)
        {
            int roll = random.Next(3);
            string kind = roll == 0 ? "hurdle" : roll == 1 ? "gate" : nextRowIndex % 3 == 0 ? "ramp_train" : "train";
            Add(kind, lane, nextRowAt);
            if (kind == "ramp_train")
                for (int i = 0; i < 3; i++) Add("coin", lane, nextRowAt + 1.5f + i * 3, RoofHeight + 1.02f);
        }

        private void Trail(int lane, float at)
        {
            for (int i = 0; i < 5; i++) Add("coin", lane, at - 7.4f + i * 2.6f, .86f);
        }

        public static bool IsTrain(string kind) => kind == "train" || kind == "ramp_train";
        public static bool IsHazard(string kind) => IsTrain(kind) || kind == "hurdle" || kind == "gate";
        private void Emit(string name, int amount = 1) { Effect?.Invoke(name, amount); }
        private static void Tick(ref float timer) { timer = Math.Max(0, timer - StepSeconds); }
        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
        private static float Approach(float value, float target, float amount) =>
            value < target ? Math.Min(value + amount, target) : Math.Max(value - amount, target);
    }
}
