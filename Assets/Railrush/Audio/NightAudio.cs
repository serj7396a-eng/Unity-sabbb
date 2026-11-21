using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Railrush.Audio
{
    [DisallowMultipleComponent]
    public sealed class NightAudio : MonoBehaviour
    {
        private const int VoiceCount = 6;
        private const int CueRate = 20000;
        private const int MusicRate = 18000;
        private const int RainRate = 12000;
        private const int FramesPerBatch = 1800;
        private const float BeatSeconds = 60f / 122f;
        private const float MusicLength = 16f * BeatSeconds;
        private const float Tau = 2f * Mathf.PI;

        private static readonly float[] Roots = { 34.648f, 41.203f, 55f, 61.735f };
        private static readonly float[] Intervals = { 1f, 1.189207f, 1.498307f, 2f, 2.996614f, 4f };
        private static readonly int[] BassSteps = { 0, -1, 0, 2, 0, -1, 3, 2 };
        private static readonly int[] LeadSteps = { 4, -1, -1, 5, -1, 4, -1, 3, 4, -1, -1, 2, 3, -1, 5, -1 };
        private static readonly float[] CoinPitch = { 1f, 1.122462f, 1.189207f, 1.334840f, 1.498307f, 1.681793f };
        private static readonly float[] ComboNotes = { 554.365f, 659.255f, 830.609f, 1108.731f };
        private static readonly float[] DistrictRain = { 0.93f, 0.73f, 0.96f, 1.15f };
        private static readonly Dictionary<string, Cue> Cues = new Dictionary<string, Cue>(StringComparer.OrdinalIgnoreCase)
        {
            { "start", new Cue(0.25f, 150f, 540f, 0.29f, 0.19f) },
            { "switch", new Cue(0.095f, 470f, 125f, 0.19f, 0.65f) },
            { "jump", new Cue(0.17f, 228f, 810f, 0.22f, 0.16f) },
            { "slide", new Cue(0.27f, 425f, 80f, 0.29f, 0.81f) },
            { "board", new Cue(0.34f, 104f, 279f, 0.32f, 0.25f) },
            { "boost", new Cue(0.54f, 87f, 360f, 0.35f, 0.51f) },
            { "coin", new Cue(0.16f, 830.609f, 950f, 0.29f, 0.025f) },
            { "crash", new Cue(0.47f, 164f, 38f, 0.47f, 0.58f) },
            { "land", new Cue(0.13f, 130f, 45f, 0.31f, 0.42f) },
            { "combo", new Cue(0.38f, 0f, 0f, 0.23f, 0f) },
            { "near_miss", new Cue(0.24f, 640f, 165f, 0.21f, 0.77f) },
            { "passing", new Cue(0.40f, 298f, 87f, 0.27f, 0.47f) },
            { "pause", new Cue(0.12f, 494f, 330f, 0.15f, 0.09f) },
            { "shield", new Cue(0.27f, 106f, 436f, 0.30f, 0.30f) },
            { "power", new Cue(0.32f, 340f, 910f, 0.29f, 0.14f) },
            { "magnet", new Cue(0.25f, 740f, 208f, 0.26f, 0.12f) }
        };

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private AudioSource[] voices;
        private AudioSource music;
        private AudioSource ambience;
        private AudioSource impactVoice;
        private Coroutine warmup;
        private bool built;
        private bool warmed;
        private bool soundEnabled = true;
        private bool running;
        private bool started;
        private bool musicPaused;
        private bool ambiencePaused;
        private int nextVoice;
        private int district;
        private float districtMix = 1f;
        private float targetSpeed;
        private float motion;
        private float impact;
        private bool airborne;
        private bool sliding;

        private readonly struct Cue
        {
            public Cue(float length, float startHz, float endHz, float gain, float noise)
            {
                Length = length;
                StartHz = startHz;
                EndHz = endHz;
                Gain = gain;
                Noise = noise;
            }

            public readonly float Length;
            public readonly float StartHz;
            public readonly float EndHz;
            public readonly float Gain;
            public readonly float Noise;
        }

        public void Build()
        {
            if (built)
                return;

            built = true;
            music = CreateSource(true);
            ambience = CreateSource(true);
            impactVoice = CreateSource(false);
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = CreateSource(false);

            WarmWhenActive();
        }

        public void SetEnabled(bool enabled)
        {
            if (soundEnabled == enabled)
                return;

            soundEnabled = enabled;
            if (!built)
                return;

            if (soundEnabled && isActiveAndEnabled)
                RefreshLoops();
            else
                Silence();
        }

        public void SetRunning(bool value)
        {
            running = value;
            started |= value;
            if (built && soundEnabled && isActiveAndEnabled)
                RefreshLoops();
        }

        public void SetMotion(float speed, bool inAir = false, bool isSliding = false)
        {
            targetSpeed = Mathf.InverseLerp(13f, 33f, speed);
            airborne = inAir;
            sliding = isSliding;
        }

        public void SetDistrict(int index)
        {
            district = (index % DistrictRain.Length + DistrictRain.Length) % DistrictRain.Length;
        }

        public void Play(string key, int combo = 1)
        {
            if (!built || !soundEnabled || !isActiveAndEnabled || string.IsNullOrEmpty(key))
                return;

            AudioClip clip = GetCue(key);
            if (clip == null)
                return;

            string cue = clip.name;
            AudioSource source = cue == "crash" ? impactVoice : FindVoice();
            source.Stop();
            source.clip = clip;
            source.volume = cue == "coin" ? 0.72f : 0.88f;
            source.pitch = 1f;
            source.panStereo = 0f;
            switch (cue)
            {
                case "coin":
                    source.pitch = CoinPitch[Mathf.Clamp(combo, 1, CoinPitch.Length) - 1];
                    break;
                case "combo":
                    source.pitch = 1f + 0.035f * (Mathf.Clamp(combo, 1, 6) - 1);
                    break;
                case "switch":
                case "jump":
                case "slide":
                case "land":
                case "near_miss":
                    source.pitch = Mathf.Lerp(0.97f, 1.17f, motion);
                    break;
                case "passing":
                    source.panStereo = Mathf.Clamp(combo, -1, 1) * 0.66f;
                    source.pitch = Mathf.Lerp(0.95f, 1.1f, motion);
                    break;
                case "crash":
                    impact = 1f;
                    break;
            }

            source.Play();
        }

        private void OnEnable()
        {
            if (!built)
                return;

            WarmWhenActive();
            if (soundEnabled)
                RefreshLoops();
        }

        private void OnDisable()
        {
            if (!built)
                return;

            if (warmup != null)
            {
                StopCoroutine(warmup);
                warmup = null;
            }
            Silence();
        }

        private void Update()
        {
            if (!built)
                return;

            float time = Time.unscaledDeltaTime;
            motion = Mathf.MoveTowards(motion, running ? targetSpeed : 0f, time * 1.9f);
            impact = Mathf.MoveTowards(impact, 0f, time * 2.5f);
            if (!soundEnabled)
                return;

            districtMix = Mathf.MoveTowards(districtMix, DistrictRain[district], time * 0.45f);
            float musicLevel = running ? 0.32f * (1f - impact * 0.6f) : 0f;
            music.volume = Mathf.MoveTowards(music.volume, musicLevel, time * (running ? 0.75f : 1.3f));
            if (!running && music.volume <= 0.001f)
                PauseLoop(music, ref musicPaused);

            float rainLevel = running ? 0.14f + motion * 0.065f : 0.070f;
            float rainPitch = 0.95f + motion * 0.17f;
            if (running && airborne)
                rainLevel += 0.020f;
            if (running && sliding)
            {
                rainLevel -= 0.026f;
                rainPitch -= 0.06f;
            }

            ambience.volume = Mathf.MoveTowards(ambience.volume, rainLevel * districtMix * (1f - impact * 0.69f), time * 0.38f);
            ambience.pitch = Mathf.MoveTowards(ambience.pitch, rainPitch, time * 0.7f);
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = loop ? 0f : 1f;
            return source;
        }

        private AudioSource FindVoice()
        {
            for (int offset = 0; offset < voices.Length; offset++)
            {
                int index = (nextVoice + offset) % voices.Length;
                if (!voices[index].isPlaying)
                {
                    nextVoice = (index + 1) % voices.Length;
                    return voices[index];
                }
            }

            AudioSource oldest = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            return oldest;
        }

        private void Silence()
        {
            PauseLoop(music, ref musicPaused);
            PauseLoop(ambience, ref ambiencePaused);
            impactVoice.Stop();
            foreach (AudioSource source in voices)
                source.Stop();
        }

        private void RefreshLoops()
        {
            if (running)
                ResumeLoop(music, ref musicPaused);
            if (started)
                ResumeLoop(ambience, ref ambiencePaused);
        }

        private static void PauseLoop(AudioSource source, ref bool paused)
        {
            if (!source.isPlaying)
                return;

            source.Pause();
            paused = true;
        }

        private static void ResumeLoop(AudioSource source, ref bool paused)
        {
            if (source.clip == null)
                return;

            if (paused)
            {
                source.UnPause();
                paused = false;
            }
            else if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void WarmWhenActive()
        {
            if (warmup == null && !warmed && isActiveAndEnabled)
                warmup = StartCoroutine(Warm());
        }

        private IEnumerator Warm()
        {
            yield return null;
            foreach (string key in Cues.Keys)
            {
                GetCue(key);
                yield return null;
            }

            if (ambience.clip == null)
            {
                IEnumerator rain = BuildRain();
                while (rain.MoveNext())
                    yield return rain.Current;
            }

            if (music.clip == null)
            {
                IEnumerator soundtrack = BuildMusic();
                while (soundtrack.MoveNext())
                    yield return soundtrack.Current;
            }

            warmed = true;
            warmup = null;
        }

        private AudioClip GetCue(string key)
        {
            if (clips.TryGetValue(key, out AudioClip existing))
                return existing;
            if (!Cues.TryGetValue(key, out Cue cue))
                return null;

            string name = key.ToLowerInvariant();
            int frames = Mathf.RoundToInt(cue.Length * CueRate);
            var pcm = new float[frames];
            uint noise = 0x624e21b5;
            float phase = 0f;
            float grit = 0f;
            bool chord = name == "combo";
            bool passing = name == "passing";
            bool swell = passing || name == "boost";

            for (int i = 0; i < frames; i++)
            {
                if (chord)
                {
                    pcm[i] = ComboSample(i / (float)CueRate, cue.Gain);
                    continue;
                }

                float progress = i / (float)frames;
                float glide = passing ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.23f, 0.68f, progress)) : Mathf.Sqrt(progress);
                phase += Tau * Mathf.Lerp(cue.StartHz, cue.EndHz, glide) / CueRate;
                float white = Noise(ref noise);
                grit += (white - grit) * 0.24f;
                float tone = Mathf.Sin(phase) * 0.77f + Mathf.Sin(phase * 2.013f) * 0.15f + Mathf.Sin(phase * 0.501f) * 0.08f;
                float sample = Mathf.Lerp(tone, grit * 0.77f + white * 0.23f, cue.Noise);
                float tail = 1f - progress;
                float envelope = Mathf.Min(1f, progress * 46f) * tail * tail;
                if (swell)
                    envelope = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 1.48f);
                pcm[i] = sample * cue.Gain * envelope;
            }

            AudioClip clip = CreateClip(name, pcm, 1, CueRate);
            clips.Add(name, clip);
            return clip;
        }

        private static float ComboSample(float time, float gain)
        {
            float value = 0f;
            for (int i = 0; i < ComboNotes.Length; i++)
            {
                float noteTime = time - i * 0.051f;
                if (noteTime < 0f)
                    break;

                float phase = Tau * ComboNotes[i] * noteTime;
                float body = Mathf.Sin(phase) + Mathf.Sin(phase * 2.011f) * 0.25f;
                float envelope = Mathf.Min(noteTime * 249f, 1f) * Mathf.Exp(-noteTime * 27f);
                value += body * envelope;
            }

            float release = Mathf.Clamp01((0.38f - time) * 65f);
            return value * gain * release;
        }

        private IEnumerator BuildRain()
        {
            const int frames = RainRate * 4;
            const int blendFrames = 720;
            var pcm = new float[(frames + blendFrames) * 2];
            uint seed = 0xc452fd11;
            float rumble = 0f;
            float mist = 0f;
            float dripLeft = 0f;
            float dripRight = 0f;

            for (int begin = 0; begin < frames + blendFrames; begin += FramesPerBatch)
            {
                int end = Math.Min(begin + FramesPerBatch, frames + blendFrames);
                for (int i = begin; i < end; i++)
                {
                    float noise = Noise(ref seed);
                    float sides = Noise(ref seed);
                    rumble += (noise - rumble) * 0.039f;
                    mist += (noise - mist) * 0.21f;
                    dripLeft *= -0.86f;
                    dripRight *= -0.83f;
                    if (noise > 0.9905f)
                        dripLeft += sides * 0.16f;
                    if (sides > 0.988f)
                        dripRight += noise * 0.17f;

                    float time = i / (float)RainRate;
                    float wind = 0.76f + 0.24f * Mathf.Sin(Tau * time * 0.25f);
                    float body = (rumble * 0.51f + mist * 0.24f + noise * 0.092f) * wind;
                    float motor = Mathf.Sin(Tau * 74f * time + 0.22f * Mathf.Sin(Tau * 2.5f * time)) * 0.019f;
                    float width = sides * (0.032f + wind * 0.028f);
                    pcm[i * 2] = body + width + dripLeft + motor * 0.36f;
                    pcm[i * 2 + 1] = body - width + dripRight + motor;
                }
                yield return null;
            }

            for (int i = 0; i < blendFrames; i++)
            {
                float mix = i / (float)blendFrames;
                pcm[i * 2] = Mathf.Lerp(pcm[(frames + i) * 2], pcm[i * 2], mix);
                pcm[i * 2 + 1] = Mathf.Lerp(pcm[(frames + i) * 2 + 1], pcm[i * 2 + 1], mix);
            }

            Array.Resize(ref pcm, frames * 2);
            ambience.clip = CreateClip("Rain on the rails", pcm, 2, RainRate);
            if (soundEnabled && started)
                ResumeLoop(ambience, ref ambiencePaused);
        }

        private IEnumerator BuildMusic()
        {
            int frames = Mathf.RoundToInt(MusicLength * MusicRate);
            var pcm = new float[frames * 2];
            uint seed = 0x90e1b37;
            float previous = 0f;

            for (int begin = 0; begin < frames; begin += FramesPerBatch)
            {
                int end = Math.Min(begin + FramesPerBatch, frames);
                for (int i = begin; i < end; i++)
                {
                    float time = i / (float)MusicRate;
                    MusicSample(time, ref seed, ref previous, out float left, out float right);
                    float fade = Mathf.Clamp01(Mathf.Min(time, MusicLength - time) * 46f);
                    pcm[i * 2] = left * fade;
                    pcm[i * 2 + 1] = right * fade;
                }
                yield return null;
            }

            music.clip = CreateClip("Night line", pcm, 2, MusicRate);
            if (soundEnabled && running)
                ResumeLoop(music, ref musicPaused);
        }

        private static void MusicSample(float time, ref uint seed, ref float previous, out float left, out float right)
        {
            float beat = time / BeatSeconds;
            int whole = Mathf.Min(15, (int)beat);
            int half = (int)(beat * 2f);
            float beatTime = time - whole * BeatSeconds;
            float halfTime = time - half * BeatSeconds * 0.5f;
            int bar = whole / 4;
            float root = Roots[bar];
            float noise = Noise(ref seed);
            float washedNoise = noise - previous * 0.86f;
            previous = noise;

            float kickPhase = Tau * (44f * beatTime + 2.24f * (1f - Mathf.Exp(-beatTime * 48f)));
            float kickLevel = (whole & 1) == 0 ? 0.32f : 0.15f;
            float kick = Mathf.Sin(kickPhase) * Mathf.Exp(-beatTime * 16.5f) * Mathf.Min(beatTime * 665f, 1f) * kickLevel;
            float hat = washedNoise * Mathf.Exp(-halfTime * 64f) * ((half & 1) == 1 ? 0.053f : 0.025f);
            hat *= Mathf.Min(halfTime * 815f, 1f);
            float snare = 0f;
            if ((whole & 1) == 1)
            {
                float rattle = Mathf.Exp(-beatTime * 31f) + 0.3f * Mathf.Exp(-Mathf.Abs(beatTime - 0.016f) * 52f);
                snare = (noise * 0.15f + Mathf.Sin(Tau * 183f * beatTime) * 0.040f) * rattle * Mathf.Min(beatTime * 600f, 1f);
            }

            float bass = 0f;
            int bassStep = BassSteps[half % BassSteps.Length];
            if (bassStep >= 0)
            {
                float phase = Tau * root * Intervals[bassStep] * halfTime;
                float body = Mathf.Sin(phase) * 0.18f + Mathf.Sin(phase * 2f) * 0.079f + Mathf.Sin(phase * 3f) * 0.020f;
                float strike = 0.68f + 0.32f * Mathf.Exp(-halfTime * 7f);
                bass = body * strike * NoteEnvelope(halfTime, BeatSeconds * 0.5f, 89f);
            }

            float barTime = time - bar * 4f * BeatSeconds;
            float padPhase = Tau * root * 2f * barTime;
            float third = bar == 0 ? 1.189207f : 1.259921f;
            float padEnvelope = NoteEnvelope(barTime, 4f * BeatSeconds, 6.3f);
            float pad = (Mathf.Sin(padPhase) * 0.020f + Mathf.Sin(padPhase * third) * 0.027f +
                Mathf.Sin(padPhase * 1.498307f) * 0.021f) * padEnvelope;
            float spread = (Mathf.Sin(padPhase * third * 1.006f) - Mathf.Sin(padPhase * third * 0.994f)) * padEnvelope * 0.021f;

            float lead = 0f;
            int leadStep = LeadSteps[half % LeadSteps.Length];
            if (leadStep >= 0)
            {
                float phase = Tau * root * Intervals[leadStep] * halfTime * 2f;
                float body = Mathf.Sin(phase + Mathf.Sin(phase * 2f) * 0.40f);
                lead = body * 0.077f * Mathf.Exp(-halfTime * 11.5f) * NoteEnvelope(halfTime, BeatSeconds * 0.5f, 92f);
            }

            float common = kick + snare + bass + pad + lead;
            left = Mathf.Clamp(common + spread + hat * 0.82f, -1f, 1f);
            right = Mathf.Clamp(common - spread + hat * 1.06f, -1f, 1f);
        }

        private static float NoteEnvelope(float time, float duration, float attack)
        {
            return Mathf.Clamp01(Mathf.Min(time * attack, (duration - time) * attack));
        }

        private static float Noise(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state >> 8) * (1f / 8388607.5f) - 1f;
        }

        private static AudioClip CreateClip(string label, float[] pcm, int channels, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(label, pcm.Length / channels, channels, sampleRate, false);
            if (clip.SetData(pcm, 0))
                return clip;

            Destroy(clip);
            return null;
        }

        private void OnDestroy()
        {
            if (!built)
                return;

            if (music != null && music.clip != null)
                Destroy(music.clip);
            if (ambience != null && ambience.clip != null)
                Destroy(ambience.clip);
            foreach (AudioClip clip in clips.Values)
                if (clip != null)
                    Destroy(clip);
        }
    }
}
