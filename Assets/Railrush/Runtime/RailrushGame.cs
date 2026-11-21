using System;
using System.Collections.Generic;
using Railrush.Audio;
using Railrush.Interface;
using Railrush.Presentation;
using Railrush.Props;
using Railrush.Simulation;
using Railrush.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railrush.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RailrushGame : MonoBehaviour
    {
        private static readonly string[] Districts = { "Лунный терминал", "Неоновый рынок", "Акведук", "Медный завод" };
        private static readonly Color[] FogTints =
        {
            new Color(.19f,.20f,.35f), new Color(.37f,.17f,.34f),
            new Color(.10f,.32f,.35f), new Color(.39f,.24f,.30f)
        };

        private readonly Dictionary<TrackObject, GameObject> views = new Dictionary<TrackObject, GameObject>();
        private readonly Dictionary<string, Stack<GameObject>> pool = new Dictionary<string, Stack<GameObject>>();
        private readonly List<TrackObject> released = new List<TrackObject>();
        private readonly List<string> effects = new List<string>(5);
        private RailRun run;
        private NightTrack track;
        private RunnerVisual hero;
        private PropLibrary props;
        private NightAudio audio;
        private RunnerHud hud;
        private NightWeather weather;
        private Transform propRoot;
        private Camera follow;
        private Light fill;
        private Material sky;
        private string screen = "menu";
        private int best, quality, lastDistrict = -1, finger = -1;
        private float accumulator, visualClock, cameraBank, verticalView, captionTime, hudClock, impact;
        private float lastTap = -2, touchStartedAt;
        private Vector3 cameraDamping;
        private Vector2 touchStart;
        private bool sound, consumedSwipe, swipeJumpHeld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (FindFirstObjectByType<RailrushGame>() == null)
                new GameObject("RAILRUSH Unity").AddComponent<RailrushGame>();
        }

        private void Awake()
        {
            best = PlayerPrefs.GetInt("railrush.best");
            sound = PlayerPrefs.GetInt("railrush.audio", 1) != 0;
            quality = Mathf.Clamp(PlayerPrefs.GetInt("railrush.quality", 2), 0, 2);
            Application.targetFrameRate = Application.isMobilePlatform ? 120 : 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            run = new RailRun(unchecked(Environment.TickCount * 17));
            run.Effect += OnEffect;
            run.Reset();

            MakeLighting();
            track = Add<NightTrack>("Ночное метро");
            track.Build();
            props = Add<PropLibrary>("Библиотека объектов");
            props.Build();
            propRoot = new GameObject("Пути и бонусы").transform;
            propRoot.SetParent(transform, false);
            hero = Add<RunnerVisual>("Курьер");
            hero.Build();
            follow = Add<Camera>("Камера забега");
            follow.gameObject.tag = "MainCamera";
            follow.clearFlags = CameraClearFlags.Skybox;
            follow.backgroundColor = new Color(.086f,.069f,.155f);
            follow.nearClipPlane = .10f;
            follow.fieldOfView = 58;
            follow.transform.position = new Vector3(-2.9f, 4.3f, -8.4f);
            follow.gameObject.AddComponent<AudioListener>();
            weather = Add<NightWeather>("Дождь и искры");
            weather.Build();
            audio = Add<NightAudio>("Музыка и звук");
            audio.Build();
            audio.SetEnabled(sound);
            hud = Add<RunnerHud>("Интерфейс");
            hud.Build();
            hud.ActionRequested += OnAction;
            ApplyQuality();
            Draw(0);
            RefreshHud();
        }

        private T Add<T>(string label) where T : Component
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }

        private void MakeLighting()
        {
            sky = new Material(Resources.Load<Shader>("RailrushSky"));
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .014f;
            RenderSettings.fogColor = FogTints[0];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.37f,.39f,.58f);
            RenderSettings.ambientEquatorColor = new Color(.21f,.19f,.32f);
            RenderSettings.ambientGroundColor = new Color(.073f,.065f,.126f);
            var key = Add<Light>("Лунный свет");
            key.type = LightType.Directional;
            key.color = new Color(.68f,.76f,1);
            key.intensity = 1.17f;
            key.transform.rotation = Quaternion.Euler(34, -137, 0);
            fill = Add<Light>("Отражённый неон");
            fill.type = LightType.Point;
            fill.color = new Color(.22f,.77f,1);
            fill.intensity = 2.6f;
            fill.range = 5.5f;
        }

        private void Update()
        {
            float delta = Mathf.Min(Time.unscaledDeltaTime, .25f);
            ReadControls();
            if (screen == "running")
            {
                accumulator += delta;
                while (accumulator >= RailRun.StepSeconds && !run.Crashed)
                {
                    run.Step();
                    accumulator -= RailRun.StepSeconds;
                }
                if (run.Crashed)
                {
                    if (run.Score > best)
                    {
                        best = run.Score;
                        PlayerPrefs.SetInt("railrush.best", best);
                        PlayerPrefs.Save();
                    }
                    SetScreen("crashed");
                }
            }

            if (screen != "paused")
            {
                visualClock += delta;
                Draw(delta);
                captionTime = Mathf.Max(0, captionTime - delta);
                impact = Mathf.MoveTowards(impact, 0, delta * 2.5f);
            }
            hudClock += delta;
            if (hudClock >= .08f)
            {
                hudClock = 0;
                RefreshHud();
            }
        }

        private void ReadControls()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (screen == "paused") SetScreen("running");
                else if (screen == "running") SetScreen("paused");
            }
            bool jumpPress = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            if (screen != "running")
            {
                if ((jumpPress || Input.GetKeyDown(KeyCode.Return)) && (screen == "menu" || screen == "crashed"))
                    StartRun();
                return;
            }
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) run.ChangeLane(-1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) run.ChangeLane(1);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) run.Slide();
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) run.UseBoard();
            if (jumpPress) run.QueueJump(true);
            bool keyboardJumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            ReadSwipe();
            if (!swipeJumpHeld && !keyboardJumpHeld) run.ReleaseJump();
        }

        private void ReadSwipe()
        {
            float now = Time.unscaledTime;
            float threshold = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * .028f, 12f, 30f);
            bool foundFinger = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && finger == -1 && !hud.IsPointerOverControls(touch.position))
                {
                    finger = touch.fingerId;
                    touchStart = touch.position;
                    touchStartedAt = now;
                    consumedSwipe = false;
                }
                if (touch.fingerId != finger) continue;
                foundFinger = true;
                Vector2 movement = touch.position - touchStart;
                if (touch.phase != TouchPhase.Canceled && movement.sqrMagnitude > threshold * threshold && !consumedSwipe)
                {
                    consumedSwipe = true;
                    if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y)) run.ChangeLane(movement.x < 0 ? -1 : 1);
                    else if (movement.y > 0)
                    {
                        swipeJumpHeld = true;
                        run.QueueJump(true);
                    }
                    else run.Slide();
                }
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) continue;
                finger = -1;
                swipeJumpHeld = false;
                if (touch.phase == TouchPhase.Ended && !consumedSwipe && now - touchStartedAt <= .25f)
                {
                    if (now - lastTap < .30f)
                    {
                        run.UseBoard();
                        lastTap = -2;
                    }
                    else lastTap = now;
                }
                else lastTap = -2;
            }
            if (foundFinger || finger == -1) return;
            finger = -1;
            swipeJumpHeld = false;
            lastTap = -2;
        }

        private void OnAction(string action)
        {
            switch (action)
            {
                case "start": case "retry": StartRun(); break;
                case "resume": if (screen == "paused") SetScreen("running"); break;
                case "pause": if (screen == "running") SetScreen("paused"); break;
                case "menu": run.Reset(); SetScreen("menu"); break;
                case "left": if (screen == "running") run.ChangeLane(-1); break;
                case "right": if (screen == "running") run.ChangeLane(1); break;
                case "jump": if (screen == "running") run.QueueJump(false); break;
                case "slide": if (screen == "running") run.Slide(); break;
                case "board": if (screen == "running") run.UseBoard(); break;
                case "audio":
                    sound = !sound;
                    audio.SetEnabled(sound);
                    PlayerPrefs.SetInt("railrush.audio", sound ? 1 : 0);
                    PlayerPrefs.Save();
                    RefreshHud();
                    break;
                case "quality":
                    quality = (quality + 1) % 3;
                    ApplyQuality();
                    PlayerPrefs.SetInt("railrush.quality", quality);
                    PlayerPrefs.Save();
                    break;
            }
        }

        private void StartRun()
        {
            run.Reset();
            visualClock = impact = verticalView = 0;
            cameraDamping = Vector3.zero;
            lastDistrict = -1;
            SetScreen("running");
            audio.Play("start");
        }

        private void SetScreen(string value)
        {
            screen = value;
            accumulator = 0;
            finger = -1;
            swipeJumpHeld = false;
            lastTap = -2;
            run.ReleaseJump();
            audio.SetRunning(screen == "running");
            weather.SetPaused(screen == "paused");
            RefreshHud();
        }

        private void ApplyQuality()
        {
            QualitySettings.antiAliasing = quality == 2 ? 2 : 0;
            follow.farClipPlane = quality == 0 ? 131 : 192;
            fill.enabled = quality > 0;
            weather.SetQuality(quality);
            RefreshHud();
        }

        private void Draw(float delta)
        {
            bool menu = screen == "menu";
            float speed = screen == "running" ? run.Speed : 0;
            bool tall = follow.aspect < 1.04f;
            float blend = 1 - Mathf.Exp(-5.5f * delta);
            float lift = Mathf.Max(0, run.Height - 1.1f) * .68f;
            verticalView = Mathf.Lerp(verticalView, lift, blend);
            float desiredX = menu ? (tall ? -1.15f : -3) : run.X * .31f - run.VelocityX * .085f;
            float cadence = run.Grounded && speed > 0 ? Mathf.Sin(run.Clock * (11 + speed * .31f)) * .043f : 0;
            float shake = Mathf.Sin(visualClock * 69) * impact * .13f;
            Vector3 desired = new Vector3(desiredX + shake, (tall ? 4.9f : 4.3f) + verticalView + cadence, tall ? -10.0f : -8.15f);
            follow.transform.position = Vector3.SmoothDamp(follow.transform.position, desired, ref cameraDamping, .15f, 40, delta);
            Vector3 target = new Vector3(menu ? desiredX * .62f : run.X * .17f, 1.65f + verticalView, 20);
            cameraBank = Mathf.Lerp(cameraBank, run.VelocityX * .22f, blend);
            follow.transform.rotation = Quaternion.LookRotation(target - follow.transform.position) * Quaternion.Euler(0, 0, cameraBank);
            float fov = (tall ? Mathf.Lerp(76, 62, Mathf.InverseLerp(.44f, 1, follow.aspect)) : 59) + (speed > 0 ? (speed - 13) * .22f : 0);
            follow.fieldOfView = Mathf.Lerp(follow.fieldOfView, fov, blend);

            hero.transform.position = new Vector3(run.X, run.Height, 0);
            hero.Pose(visualClock, speed, Mathf.Max(0, run.Height - run.Support), run.VelocityX,
                run.Sliding, run.Flying, run.Boarding, run.Boots, run.Crashed);
            fill.transform.position = new Vector3(run.X * .5f, run.Height + 2.0f, -.85f);
            fill.color = run.Flying ? new Color(.21f,.89f,1) : run.Boarding ? new Color(.85f,.28f,1) : new Color(.55f,.54f,1);
            int district = NightTrack.DistrictAt(run.Distance);
            Color tint = FogTints[district];
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, tint, 1 - Mathf.Exp(-1.7f * delta));
            sky.SetColor("_District", RenderSettings.fogColor * .78f);
            track.RenderDistance(run.Distance, visualClock, quality);
            weather.Move(run.X, run.Height, speed);
            if (lastDistrict != district)
            {
                lastDistrict = district;
                audio.SetDistrict(lastDistrict);
                captionTime = menu ? 0 : 2.05f;
            }
            audio.SetMotion(speed, !run.Grounded, run.Sliding);
            RenderProps();
        }

        private void RenderProps()
        {
            released.Clear();
            foreach (var entry in views)
                if (entry.Key.Removed) released.Add(entry.Key);
            foreach (var item in released)
            {
                GameObject view = views[item];
                views.Remove(item);
                view.SetActive(false);
                if (!pool.TryGetValue(item.Kind, out var reserve))
                    pool[item.Kind] = reserve = new Stack<GameObject>();
                reserve.Push(view);
            }
            foreach (var item in run.Objects)
            {
                if (item.Removed || item.At - run.Distance > 122) continue;
                if (!views.TryGetValue(item, out GameObject view))
                {
                    view = pool.TryGetValue(item.Kind, out var reserve) && reserve.Count > 0 ? reserve.Pop() : props.Create(item.Kind);
                    view.transform.SetParent(propRoot, false);
                    view.SetActive(true);
                    views.Add(item, view);
                }
                float z = item.At - run.Distance;
                float x = RailRun.LaneX(item.Lane);
                float y = item.Elevation;
                if (!RailRun.IsHazard(item.Kind))
                {
                    if (item.Kind == "coin" && run.Magnet && z < 7 && Mathf.Abs(run.Height + 1 - y) < 2.7f)
                    {
                        float pull = 1 - Mathf.Clamp01(z / 7);
                        x = Mathf.Lerp(x, run.X, pull * pull);
                        y = Mathf.Lerp(y, run.Height + 1, pull * pull);
                    }
                    float phase = visualClock * 3.4f + item.At * .19f;
                    y += .087f * Mathf.Sin(phase);
                    view.transform.localRotation = Quaternion.Euler(0, phase * Mathf.Rad2Deg, 0);
                }
                view.transform.localPosition = new Vector3(x, y, z);
            }
        }

        private void OnEffect(string effect, int amount)
        {
            audio.Play(effect, amount);
            Vector3 at = new Vector3(run.X, run.Height + .35f, 0);
            switch (effect)
            {
                case "crash": impact = 1; weather.Burst(at, new Color(1,.29f,.18f), 44); break;
                case "shield": impact = .55f; weather.Burst(at, new Color(.88f,.30f,1), 37); break;
                case "boost": weather.Burst(at + Vector3.up, new Color(.31f,.91f,1), 25); break;
                case "coin": case "combo": weather.Burst(at + Vector3.up, new Color(1,.76f,.18f), effect == "combo" ? 18 : 7); break;
                case "land": weather.Burst(at - Vector3.up * .27f, new Color(.32f,.65f,1), 9); break;
            }
        }

        private void RefreshHud()
        {
            if (hud == null) return;
            effects.Clear();
            Timer(run.JetTime, "РАНЕЦ");
            Timer(run.BootsTime, "БОТИНКИ");
            Timer(run.BoardTime, "ДОСКА");
            Timer(run.MagnetTime, "МАГНИТ");
            Timer(run.DoubleTime, "СЧЁТ ×2");
            hud.Present(screen, run.Score, run.Coins, best, run.BoardStock, string.Join("  ·  ", effects),
                run.Speed, captionTime > 0 && lastDistrict >= 0 ? Districts[lastDistrict] : "", sound, quality);
        }

        private void Timer(float time, string name)
        {
            if (time > 0) effects.Add(name + " " + Mathf.CeilToInt(time));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && screen == "running") SetScreen("paused");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && screen == "running") SetScreen("paused");
        }
    }
}
