using UnityEngine;

namespace Railrush.Runtime
{
    public sealed class NightWeather : MonoBehaviour
    {
        private ParticleSystem rain, sparks;
        private int quality = 2;

        public void Build()
        {
            rain = Create("Ливень", 850, 1.65f, true);
            var drop = rain.main;
            drop.startColor = new Color(.31f,.67f,.94f,.44f);
            drop.startSpeed = 0;
            drop.startSize = new ParticleSystem.MinMaxCurve(.016f,.032f);
            var box = rain.shape;
            box.enabled = true;
            box.shapeType = ParticleSystemShapeType.Box;
            box.scale = new Vector3(23, 11, 40);
            var drift = rain.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.Local;
            drift.x = new ParticleSystem.MinMaxCurve(-1.7f, 1f);
            drift.y = new ParticleSystem.MinMaxCurve(-18f, -10f);
            drift.z = new ParticleSystem.MinMaxCurve(-6f, -1f);
            sparks = Create("Брызги и искры", 180, .6f, false);
            var flash = sparks.main;
            flash.simulationSpace = ParticleSystemSimulationSpace.World;
            flash.gravityModifier = .85f;
            var emitter = sparks.emission;
            emitter.enabled = false;
            rain.Play();
            sparks.Play();
        }

        private ParticleSystem Create(string label, int count, float lifetime, bool falling)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var properties = system.main;
            properties.maxParticles = count;
            properties.startLifetime = lifetime;
            properties.playOnAwake = false;
            properties.simulationSpace = ParticleSystemSimulationSpace.Local;
            properties.loop = true;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = falling ? 5 : 2.9f;
            renderer.velocityScale = falling ? .22f : .06f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = new Material(Resources.Load<Shader>("RailrushRain"));
            return system;
        }

        public void SetQuality(int value)
        {
            quality = value;
            var emission = rain.emission;
            emission.rateOverTime = value == 2 ? 390 : value == 1 ? 225 : 96;
        }

        public void Move(float x, float y, float speed)
        {
            rain.transform.localPosition = new Vector3(x * .6f, 9f + y * .52f, 16);
            var drift = rain.velocityOverLifetime;
            drift.z = new ParticleSystem.MinMaxCurve(-4.5f - speed * .19f, -1.1f - speed * .12f);
        }

        public void SetPaused(bool value)
        {
            if (value) { rain.Pause(); sparks.Pause(); }
            else { rain.Play(); sparks.Play(); }
        }

        public void Burst(Vector3 at, Color tint, int count)
        {
            count = Mathf.Max(2, count * (quality + 2) / 4);
            for (int i = 0; i < count; i++)
            {
                var spark = new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * .15f,
                    velocity = Random.insideUnitSphere * 3.8f + Vector3.up * 2.4f - Vector3.forward * .45f,
                    startColor = tint,
                    startSize = Random.Range(.031f,.079f),
                    startLifetime = Random.Range(.19f,.57f)
                };
                sparks.Emit(spark, 1);
            }
        }
    }
}
