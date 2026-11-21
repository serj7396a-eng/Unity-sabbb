using System;
using System.Collections.Generic;
using System.Linq;
using Railrush.Simulation;

internal static class Program
{
    private static int failures;
    private static int passes;

    private static RailRun NewRun(int seed = 2026)
    {
        var result = new RailRun(seed);
        result.Reset();
        return result;
    }

    private static void Verify(bool ok, string label)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + label);
        if (ok) passes++; else failures++;
    }

    private static void To(RailRun run, float d)
    {
        for (int step = 0; step < 200000 && run.Distance < d && !run.Crashed; step++) run.Step();
    }

    private static void Steps(RailRun run, int n)
    {
        for (int i = 0; i < n; i++) run.Step();
    }

    private static int Main()
    {
        var run = NewRun();
        Verify(run.Lane == 1 && run.Distance == 0 && run.Coins == 0 && run.Score == 0 && run.Grounded && run.BoardStock == 1 && run.Objects.Count >= 20, "ready state and tutorial seed");
        To(run, 31);
        Verify(run.Coins == 6 && run.Combo == 2 && run.Score > 340, "six opening coins counted once with combo bonus");
        To(run, 42);
        Verify(run.Crashed && run.Distance >= 39.74f && run.Distance < 40, "swept grounded hurdle collision");
        float frozen = run.Distance;
        Steps(run, 24);
        Verify(run.Distance == frozen && run.Crashed, "post-crash simulation frozen");
        var priorObjects = run.Objects.ToArray();
        run.Reset();
        Verify(run.Distance == 0 && run.Coins == 0 && run.Score == 0 && run.BoardStock == 1 && priorObjects.All(x => x.Removed), "restart clears state and retires stale render handles");

        run.ChangeLane(-1);
        run.ChangeLane(-9);
        Steps(run, 85);
        Steps(run, 55);
        Verify(run.Lane == 0 && Math.Abs(run.X - RailRun.LaneX(0)) < .005f, "left clamp and stable settled position");
        run.ChangeLane(1);
        run.ChangeLane(1);
        run.ChangeLane(8);
        Steps(run, 140);
        Verify(run.Lane == 2 && Math.Abs(run.X - RailRun.LaneX(2)) < .005f, "right clamp and stable settled position");
        run = NewRun();
        To(run, 36.5f);
        run.QueueJump(true);
        To(run, 41);
        run.ReleaseJump();
        Verify(!run.Crashed && run.Height > .68f, "held jump clears tutorial hurdle");
        To(run, 73);
        run.ChangeLane(1);
        To(run, 79.8f);
        run.Slide();
        To(run, 84);
        Verify(!run.Crashed && run.Sliding && run.BodyHeight <= .59, "crouch clears right-hand gate after interpolated lane switch");

        run = NewRun();
        run.ChangeLane(-1);
        To(run, 48);
        run.ChangeLane(1);
        To(run, 75);
        run.ChangeLane(1);
        To(run, 84);
        Verify(run.Crashed && run.Distance >= 82.74f && run.Distance < 83, "upright gate impacts at its near edge");

        run = NewRun();
        run.ChangeLane(-1);
        To(run, 63);
        Verify(run.Crashed && run.Distance >= 60.46f && run.Distance < 61, "train impacts at its nose");
        run = NewRun();
        run.ChangeLane(-1);
        To(run, 48);
        run.ChangeLane(1);
        To(run, 65);
        run.ChangeLane(-1);
        To(run, 70);
        Verify(run.Crashed && run.Distance > 65 && run.Distance < 70, "cross-lane entry into the train flank still impacts");

        run = NewRun();
        int shatters = 0;
        run.Effect += (key, amount) => { if (key == "shield") shatters++; };
        Verify(run.UseBoard() && !run.UseBoard() && run.BoardStock == 0, "board consumes exactly one stock item");
        To(run, 43);
        Verify(!run.Crashed && !run.Boarding && shatters == 1, "board absorbs exactly one hazard impact");

        run = NewRun();
        run.Add("ramp_train", 1, 27);
        To(run, 23.5f);
        Verify(!run.Crashed && run.Grounded && run.Support > 1 && run.Height == run.Support, "continuous ramp support engages while entering");
        To(run, 29);
        Verify(!run.Crashed && run.Grounded && Math.Abs(run.Height - RailRun.RoofHeight) <= .001f, "ramp transitions onto train roof without impact");
        To(run, 38);
        Verify(!run.Crashed && !run.Grounded && run.Height > 3, "leaving roof opens airborne state");
        run.QueueJump(true);
        Steps(run, 12);
        Verify(run.VelocityY > 0 && run.Height > 3.6f, "roof-edge coyote jump accepts buffered input");

        var released = NewRun();
        var held = NewRun();
        released.QueueJump(false);
        held.QueueJump(true);
        Steps(released, 31);
        Steps(held, 31);
        Verify(held.Height > released.Height + .35f, "held and tap jumps have separate trajectories");
        held.Slide();
        Steps(held, 28);
        Verify(held.Grounded && held.Sliding, "air slide dives to floor and starts buffered landing slide");

        run = NewRun();
        run.Activate("jet");
        Steps(run, 110);
        Verify(run.Flying && !run.Grounded && Math.Abs(run.Height - 6.9f) < .03f, "jet raises player to fixed aerial track");
        Verify(run.Objects.Count(o => o.Kind == "coin" && o.Elevation > 7) >= 24, "jet spawns bounded aerial trail");
        To(run, 44f);
        Verify(!run.Crashed && run.Flying, "jet crosses tutorial hazards");
        run = NewRun();
        run.Activate("magnet");
        run.Add("coin", 0, 5, .86f);
        run.Add("coin", 2, 5, .86f);
        To(run, 6);
        Verify(run.Magnet && run.Coins == 2, "magnet pulls ordinary coins across lanes");
        var single = NewRun();
        var doubled = NewRun();
        doubled.Activate("double");
        To(single, 25);
        To(doubled, 25);
        Verify(doubled.Score >= single.Score * 2 - 1 && doubled.Coins == single.Coins, "double boosts distance and coins without duplicate pickups");

        for (int seed = 0; seed < 6; seed++)
        {
            run = NewRun(seed);
            To(run, 30);
            run.ChangeLane(-1);
            To(run, 47);
            run.ChangeLane(1);
            To(run, 96);
            int priorSafe = 1;
            float next = 105;
            int maxObjects = 0;
            int completed = 0;
            while (completed < 200 && !run.Crashed)
            {
                var upcoming = run.Objects.Where(o => RailRun.IsHazard(o.Kind) && o.At >= next).OrderBy(o => o.At).FirstOrDefault();
                if (upcoming == null) break;
                float at = upcoming.At;
                var marker = run.Objects.FirstOrDefault(o => o.Kind == "coin" && o.Elevation < 1 && Math.Abs(o.At - (at + .4f)) < .014f);
                if (marker == null || Math.Abs(marker.Lane - priorSafe) > 1 || run.Objects.Any(o => RailRun.IsHazard(o.Kind) && Math.Abs(o.At - at) < .014f && o.Lane == marker.Lane))
                    break;
                To(run, at - 8);
                run.ChangeLane(marker.Lane - run.Lane);
                To(run, at + 1);
                priorSafe = marker.Lane;
                next = at + .1f;
                maxObjects = Math.Max(maxObjects, run.Objects.Count);
                completed++;
            }
            Verify(completed == 200 && !run.Crashed && maxObjects < 90, $"seed {seed}: 200 reachable generated rows, max active {maxObjects}, distance {run.Distance:F1}");
        }

        var result = new List<(float distance, int score, int steps)>();
        foreach (int fps in new[]{30, 60, 165})
        {
            run = NewRun();
            double accumulated = 0;
            int count = 0;
            for (int frame = 0; frame < fps * 2; frame++)
            {
                accumulated += 1.0 / fps;
                while (accumulated + 1e-9 >= 1.0 / 120)
                {
                    accumulated -= 1.0 / 120;
                    run.Step();
                    count++;
                }
            }
            result.Add((run.Distance, run.Score, count));
        }
        Verify(result.All(entry => entry == result[0]) && result[0].steps == 240, "identical fixed-tick result at render cadences 30, 60, 165");
        Console.WriteLine("SUMMARY " + passes + " passed, " + failures + " failed");
        return failures;
    }
}
