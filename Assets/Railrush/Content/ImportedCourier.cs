using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Railrush.Content
{
    public sealed class ImportedCourier : MonoBehaviour
    {
        private readonly AnimationClipPlayable[] samples = new AnimationClipPlayable[5];
        private readonly float[] weights = new float[5];
        private readonly float[] lengths = new float[5];
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private Transform mount;
        private float lastTime = -1, stroll, stateTime;
        private int priorState = -1;

        public Transform Mount => mount;

        public bool Build()
        {
            string path = ImportedArt.Root + "Models/Courier";
            var assets = Resources.LoadAll<AnimationClip>(path);
            var idle = Find(assets, "Man_Idle");
            var run = Find(assets, "Man_Run");
            if (idle == null || run == null ||
                !ImportedArt.TryPlace(transform, "Courier", new Vector3(1.1f, 1.82f, .8f), Vector3.zero,
                    out mount, out var model, uniform: true))
                return false;

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AnimationClip[] clips = { idle, run, Find(assets, "Man_RunningJump") ?? Find(assets, "Man_Jump") ?? run,
                Find(assets, "Man_Sitting") ?? idle, Find(assets, "Man_Death") ?? idle };
            graph = PlayableGraph.Create("Railrush Courier");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            var output = AnimationPlayableOutput.Create(graph, "Courier", animator);
            output.SetSourcePlayable(mixer);
            for (int i = 0; i < clips.Length; i++)
            {
                var input = AnimationClipPlayable.Create(graph, clips[i]);
                samples[i] = input;
                lengths[i] = Mathf.Max(.01f, clips[i].length);
                input.SetApplyFootIK(false);
                input.SetSpeed(0);
                graph.Connect(input, 0, mixer, i);
            }
            graph.Play();
            Pose(0, 0, 0, 0, false, false, false, false);
            return true;
        }

        public void Pose(float time, float speed, float height, float lateral,
            bool sliding, bool flying, bool boarding, bool crashed)
        {
            if (!graph.IsValid() || mount == null) return;
            float delta = Mathf.Clamp(time - lastTime, 0, .075f);
            if (lastTime < 0 || time < lastTime)
            {
                stroll = 0;
                priorState = -1;
            }
            lastTime = time;
            int state = crashed ? 4 : flying || height > .09f ? 2 : sliding || boarding ? 3 : speed > .1f ? 1 : 0;
            bool newPose = priorState < 0;
            if (state != priorState)
            {
                priorState = state;
                stateTime = 0;
            }
            stateTime += delta;
            stroll += delta * Mathf.Lerp(1, 1.7f, Mathf.InverseLerp(13, 31, speed));
            float amount = newPose ? 1 : 1 - Mathf.Exp(-17f * delta);
            for (int i = 0; i < samples.Length; i++)
            {
                weights[i] = Mathf.Lerp(weights[i], state == i ? 1 : 0, amount);
                mixer.SetInputWeight(i, weights[i]);
                float sample = i < 2 ? Mathf.Repeat(i == 1 ? stroll : time, lengths[i])
                    : i == 3 ? Mathf.Min(lengths[i] * .17f, stateTime * 3)
                    : Mathf.Clamp(stateTime, 0, lengths[i] - .002f);
                samples[i].SetTime(sample);
            }
            mount.localRotation = Quaternion.Euler(sliding ? 24 : flying ? 11 : 0, 0, -lateral * 2.5f);
            mount.localPosition = new Vector3(0, sliding ? -.12f : boarding ? .13f : 0, 0);
            graph.Evaluate(0);
        }

        private static AnimationClip Find(AnimationClip[] clips, string name) =>
            Array.Find(clips, clip => clip != null && clip.name.EndsWith(name, StringComparison.OrdinalIgnoreCase));

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
