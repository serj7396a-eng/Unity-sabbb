using System;
using System.Globalization;
using UnityEngine;

namespace Railrush.Interface
{
    [DisallowMultipleComponent]
    public sealed class RunnerHud : MonoBehaviour
    {
        private const string Menu = "menu";
        private const string Running = "running";
        private const string Paused = "paused";
        private const string Crashed = "crashed";

        private static readonly Color Ink = new Color32(8, 11, 23, 255);
        private static readonly Color Panel = new Color32(17, 17, 36, 245);
        private static readonly Color Glass = new Color32(18, 18, 40, 218);
        private static readonly Color Line = new Color32(83, 72, 117, 225);
        private static readonly Color White = new Color32(247, 243, 255, 255);
        private static readonly Color Muted = new Color32(187, 177, 207, 255);
        private static readonly Color Lime = new Color32(214, 255, 65, 255);
        private static readonly Color Cyan = new Color32(105, 232, 255, 255);
        private static readonly Color Gold = new Color32(255, 216, 102, 255);
        private static readonly Color Pink = new Color32(251, 131, 240, 255);
        private static readonly Color LimeHover = new Color32(232, 255, 125, 255);
        private static readonly Color GlassHover = new Color32(45, 41, 73, 255);
        private static readonly string[] Qualities = { "ЭКО", "БАЛАНС", "УЛЬТРА" };

        public event Action<string> ActionRequested;

        private string screen = Menu;
        private int score;
        private int coins;
        private int best;
        private int stock = 1;
        private int quality = 2;
        private int kmh;
        private bool audio = true;
        private string powers = "";
        private string district = "";
        private string scoreText = "000000";
        private string coinsText = "0";
        private string bestText = "000000";
        private string speedText = "000";
        private string boardText = "◇ 1";
        private float screenTime;
        private float districtTime;
        private Texture2D menuShade;
        private Font bodyFont;
        private Font titleFont;
        private GUIStyle labelStyle;
        private GUIStyle controlStyle;
        private bool built;

        private struct Layout
        {
            public Rect Safe;
            public float Scale;
            public float Width;
            public float Height;
            public bool Touch;
            public bool Compact;
            public bool Short;
            public bool Stacked;
            public Rect Audio;
            public Rect Quality;
            public Rect Pause;
            public Rect Primary;
            public Rect Secondary;
            public Rect Board;
            public Rect MainCard;
        }

        public void Build()
        {
            if (built)
                return;

            built = true;
            screenTime = Time.unscaledTime;
            districtTime = -100f;
            bodyFont = Resources.Load<Font>("RailrushTypography/RailrushSans");
            titleFont = Resources.Load<Font>("RailrushTypography/BarlowCondensed-BlackItalic");
            if (bodyFont == null)
                bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleFont == null)
                titleFont = bodyFont;
            menuShade = MakeMenuShade();
        }

        public void Present(string screen, int score, int coins, int best, int stock, string powers,
            float speed, string district, bool audio, int quality)
        {
            if (this.screen != screen)
            {
                this.screen = screen;
                screenTime = Time.unscaledTime;
            }

            if (this.district != (district ?? ""))
            {
                this.district = district ?? "";
                districtTime = Time.unscaledTime;
            }

            if (this.score != score)
            {
                this.score = score;
                scoreText = score.ToString("D6", CultureInfo.InvariantCulture);
            }
            if (this.coins != coins)
            {
                this.coins = coins;
                coinsText = coins.ToString(CultureInfo.InvariantCulture);
            }
            if (this.best != best)
            {
                this.best = best;
                bestText = best.ToString("D6", CultureInfo.InvariantCulture);
            }
            if (this.stock != stock)
            {
                this.stock = stock;
                boardText = "◇ " + stock.ToString(CultureInfo.InvariantCulture);
            }

            int converted = Mathf.Max(0, Mathf.RoundToInt(speed * 3.6f));
            if (kmh != converted)
            {
                kmh = converted;
                speedText = converted.ToString("D3", CultureInfo.InvariantCulture);
            }

            this.powers = powers ?? "";
            this.audio = audio;
            this.quality = Mathf.Clamp(quality, 0, Qualities.Length - 1);
        }

        public bool IsPointerOverControls(Vector2 screenPosition)
        {
            Layout layout = Measure();
            if (!layout.Safe.Contains(screenPosition))
                return false;

            Vector2 pointer = new Vector2(screenPosition.x - layout.Safe.xMin,
                layout.Safe.yMax - screenPosition.y) / layout.Scale;
            if (layout.Audio.Contains(pointer) || layout.Quality.Contains(pointer))
                return true;

            if (screen != Running)
                return layout.Primary.Contains(pointer) || layout.Secondary.Contains(pointer);

            return layout.Pause.Contains(pointer) || (layout.Touch && layout.Board.Contains(pointer));
        }

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (menuShade != null)
                Destroy(menuShade);
        }

        private void OnGUI()
        {
            Build();
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                controlStyle = new GUIStyle(GUI.skin.button);
                controlStyle.padding = new RectOffset(3, 3, 1, 1);
                controlStyle.margin = new RectOffset(0, 0, 0, 0);
                controlStyle.alignment = TextAnchor.MiddleCenter;
                StyleButtons();
            }

            Layout layout = Measure();
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -50;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(layout.Safe.xMin, Screen.height - layout.Safe.yMax, 0f),
                Quaternion.identity, new Vector3(layout.Scale, layout.Scale, 1f));

            if (screen == Running)
                DrawRunning(layout);
            else if (screen == Paused || screen == Crashed)
                DrawResults(layout);
            else
                DrawMenu(layout);

            DrawTopControls(layout);
            GUI.depth = previousDepth;
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private static Texture2D MakeMenuShade()
        {
            var texture = new Texture2D(112, 2, TextureFormat.RGBA32, false);
            texture.name = "Railrush menu gradient";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[224];
            for (int x = 0; x < 112; x++)
            {
                float t = x / 111f;
                var color = new Color(0.025f, 0.029f, 0.072f,
                    0.94f * (1f - Mathf.SmoothStep(0f, 1f, t)));
                pixels[x] = color;
                pixels[112 + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private Layout Measure()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0)
                safe = new Rect(0, 0, Mathf.Max(Screen.width, 1), Mathf.Max(Screen.height, 1));

            bool touch = Application.isMobilePlatform || Input.touchSupported || safe.height > safe.width;
            float preferred = touch ? Mathf.Min(safe.width, safe.height) / 425f :
                Mathf.Min(safe.width / 1280f, safe.height / 730f);
            float scale = Mathf.Clamp(preferred, 0.45f, 6f);
            float width = safe.width / scale;
            float height = safe.height / scale;
            bool compact = width < 650f;
            bool shortView = height < 490f;
            float margin = compact ? 13f : 23f;
            float audioWidth = compact ? 74f : 91f;
            float qualityWidth = compact ? 77f : 92f;
            float topHeight = 40f;

            var layout = new Layout
            {
                Safe = safe,
                Scale = scale,
                Width = width,
                Height = height,
                Touch = touch,
                Compact = compact,
                Short = shortView,
                Stacked = width < 1000f,
                Audio = new Rect(width - margin - topHeight - 7f - audioWidth, margin, audioWidth, topHeight),
                Quality = new Rect(width - margin - topHeight - 14f - audioWidth - qualityWidth, margin, qualityWidth, topHeight),
                Pause = new Rect(width - margin - topHeight, margin, topHeight, topHeight)
            };

            if (screen != Running)
            {
                layout.Audio.x += topHeight + 7f;
                layout.Quality.x += topHeight + 7f;
            }

            if (screen == Menu)
            {
                float x = compact ? Mathf.Max(24f, width * 0.075f) : width * 0.087f;
                float primaryY = shortView ? height - 90f : height * (compact ? 0.77f : 0.72f);
                float buttonWidth = Mathf.Min(300f, width - 2f * x);
                layout.Primary = new Rect(x, primaryY, buttonWidth, shortView ? 48f : 57f);
            }
            else if (screen != Running)
            {
                float cardWidth = Mathf.Min(width - 32f, 455f);
                float cardHeight = screen == Crashed ? 290f : 244f;
                float cardTop = Mathf.Max(57f, (height - cardHeight) * 0.5f);
                float cardLeft = (width - cardWidth) * 0.5f;
                layout.MainCard = new Rect(cardLeft, cardTop, cardWidth, cardHeight);
                layout.Primary = new Rect(cardLeft + 24f, cardTop + cardHeight - 112f,
                    cardWidth - 48f, 48f);
                layout.Secondary = new Rect(cardLeft + 24f, cardTop + cardHeight - 53f,
                    cardWidth - 48f, 35f);
            }

            float buttonSize = compact ? 53f : 55f;
            float touchBottom = height - margin - buttonSize;
            float boardWidth = compact ? 62f : 72f;
            layout.Board = new Rect((width - boardWidth) * 0.5f, touchBottom, boardWidth, buttonSize);
            return layout;
        }

        private void DrawTopControls(Layout layout)
        {
            Button(layout.Quality, Qualities[quality], "quality", quality == 2 ? Lime : White, false, 11);
            Button(layout.Audio, audio ? "ЗВУК ВКЛ" : "ЗВУК ВЫКЛ", "audio", audio ? Cyan : Muted, false, 10);
            if (screen == Running)
                Button(layout.Pause, "Ⅱ", "pause", White, false, 21);

            if (!layout.Compact)
            {
                var brand = new Rect(23, 18, 158, 27);
                Label(brand, "RAILRUSH", 32, White, TextAnchor.MiddleLeft, FontStyle.Normal, true);
                Label(new Rect(24, 46, 168, 19), "РЕКОРД  " + bestText, 10, Muted, TextAnchor.MiddleLeft);
            }
            else if (screen != Running)
            {
                Label(new Rect(14, 16, 130, 30), "RAILRUSH", 27, White, TextAnchor.MiddleLeft, FontStyle.Normal, true);
            }
        }

        private void DrawMenu(Layout layout)
        {
            Rect button = layout.Primary;
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0f, 0f, layout.Width * (layout.Compact ? 1f : 0.78f), layout.Height), menuShade);

            float x = button.x;
            float titleHeight = layout.Short ? 87f : layout.Compact ? 111f : Mathf.Min(174f, layout.Height * 0.21f);
            float titleY = button.y - titleHeight * 1.55f - (layout.Short ? 24f : 57f);
            float titleWidth = Mathf.Min(layout.Width - x - 8f, 490f);
            int titleSize = Mathf.RoundToInt(titleHeight * 1.06f);
            if (layout.Short || titleY < 76f)
            {
                titleY = Mathf.Max(72f, button.y - titleHeight - 47f);
                titleSize = Mathf.Min(Mathf.RoundToInt(titleHeight * 0.95f), 105);
                Rect logo = new Rect(x, titleY, titleWidth, titleHeight);
                Label(logo, "RAILRUSH", titleSize, White, TextAnchor.MiddleLeft, FontStyle.Normal, true);
            }
            else
            {
                Rect first = new Rect(x - 3f, titleY, titleWidth, titleHeight);
                Rect second = new Rect(x - 3f, titleY + titleHeight * 0.71f, titleWidth, titleHeight);
                Label(first, "RAIL", titleSize, White, TextAnchor.MiddleLeft, FontStyle.Normal, true);
                Label(second, "RUSH.", titleSize, Lime, TextAnchor.MiddleLeft, FontStyle.Normal, true);
            }

            float subtitleY = Mathf.Max(56f, titleY - 25f);
            Fill(new Rect(x, subtitleY + 10f, 21f, 2f), Cyan);
            Label(new Rect(x + 29f, subtitleY, titleWidth - 29f, 21f), "НОЧНАЯ СМЕНА", 12,
                White, TextAnchor.MiddleLeft, FontStyle.Bold);

            if (!layout.Short)
            {
                Label(new Rect(x, button.y - 43f, titleWidth, 34f), "Три пути через ночной город.", 17,
                    White, TextAnchor.MiddleLeft);
            }

            Button(button, "НАЧАТЬ ЗАБЕГ", "start", Ink, true, 15);
            if (!layout.Compact || layout.Touch)
            {
                string inputHint = layout.Touch ? "Свайпы · Двойное касание — доска" : "ENTER — старт   •   СТРЕЛКИ — движение   •   B — доска";
                Label(new Rect(x, button.yMax + 8f, Mathf.Min(550f, layout.Width - x), 25f), inputHint,
                    11, Muted, TextAnchor.MiddleLeft);
            }
        }

        private void DrawRunning(Layout layout)
        {
            float y = layout.Stacked ? 61f : 22f;
            float first = layout.Compact ? 13f : (layout.Width - 338f) * 0.5f;
            Tile(new Rect(first, y, 124, 52), "СЧЁТ", scoreText, White);
            Tile(new Rect(first + 132f, y, 78, 52), "МОНЕТЫ", coinsText, Gold);
            Tile(new Rect(first + 218f, y, 79, 52), "КМ/Ч", speedText, Cyan);

            if (layout.Compact)
                Label(new Rect(first, y + 53f, 124f, 14f), "РЕКОРД  " + bestText, 10, Muted,
                    TextAnchor.MiddleLeft, FontStyle.Bold);

            if (!string.IsNullOrEmpty(powers))
            {
                var powerBar = new Rect(Mathf.Max(13f, (layout.Width - 500f) * 0.5f),
                    layout.Compact ? 143f : layout.Stacked ? 123f : 84f, Mathf.Min(layout.Width - 26f, 500f), 34f);
                Plate(powerBar, Glass, new Color(0.34f, 0.66f, 0.77f, 0.65f));
                Label(new Rect(powerBar.x + 5f, powerBar.y + 1f, powerBar.width - 10f, powerBar.height - 2f),
                    powers, layout.Compact ? 11 : 12, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, false, true);
            }

            DrawDistrict(layout);

            if (layout.Touch)
            {
                bool armed = stock > 0 && !powers.Contains("ДОСКА");
                Button(layout.Board, boardText, "board", armed ? Pink : Muted, false, 17, armed);
            }
            else
            {
                string keys = "←  →  путь     ↑  прыжок     ↓  подкат     B  доска";
                Label(new Rect(24, layout.Height - 46f, Mathf.Min(485f, layout.Width - 48f), 24f), keys,
                    11, Muted, TextAnchor.MiddleLeft);
                Tile(new Rect(layout.Width - 105f, layout.Height - 65f, 80f, 45f),
                    "ДОСКА", boardText, Pink);
            }
        }

        private void DrawDistrict(Layout layout)
        {
            float elapsed = Time.unscaledTime - districtTime;
            if (string.IsNullOrWhiteSpace(district) || elapsed > 3.7f)
                return;

            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.27f));
            float fade = Mathf.Clamp01((3.7f - elapsed) / 0.65f);
            float opacity = appear * fade;
            float cardWidth = Mathf.Min(layout.Width - 28f, 430f);
            float y = (layout.Compact ? 194f : layout.Stacked ? 174f : 136f) + (1f - appear) * 11f;
            Rect changeCard = new Rect((layout.Width - cardWidth) * 0.5f, y, cardWidth, 53f);
            Fill(changeCard, WithAlpha(Ink, opacity * 0.81f));
            Fill(new Rect(changeCard.x, changeCard.y, changeCard.width, 1.5f), WithAlpha(Cyan, opacity));
            Label(new Rect(changeCard.x + 8f, changeCard.y + 1f, cardWidth - 16f, 19f), "РАЙОН",
                10, WithAlpha(Cyan, opacity), TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(new Rect(changeCard.x + 8f, changeCard.y + 19f, cardWidth - 16f, 31f),
                district, layout.Compact ? 20 : 24, WithAlpha(White, opacity), TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void DrawResults(Layout layout)
        {
            float opacity = Mathf.SmoothStep(0.52f, 0.72f,
                Mathf.Clamp01((Time.unscaledTime - screenTime) / 0.25f));
            Fill(new Rect(0, 0, layout.Width, layout.Height), WithAlpha(Ink, opacity));
            bool crashed = screen == Crashed;
            Rect card = layout.MainCard;
            Plate(card, Panel, Line);
            Fill(new Rect(card.x + 24f, card.y + 19f, 44f, 3f), crashed ? Pink : Cyan);
            Label(new Rect(card.x + 24f, card.y + 28f, card.width - 48f, 20f), "НОЧНАЯ СМЕНА",
                11, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(new Rect(card.x + 24f, card.y + 48f, card.width - 48f, 47f),
                crashed ? "ЗАБЕГ ОКОНЧЕН" : "ПАУЗА", crashed ? 27 : 37, White,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            if (crashed)
            {
                float tileWidth = (card.width - 60f) / 3f;
                float x = card.x + 24f;
                float y = card.y + 107f;
                Tile(new Rect(x, y, tileWidth, 51), "СЧЁТ", scoreText, White);
                Tile(new Rect(x + tileWidth + 6f, y, tileWidth, 51), "МОНЕТЫ", coinsText, Gold);
                Tile(new Rect(x + 2f * (tileWidth + 6f), y, tileWidth, 51), "РЕКОРД", bestText, Lime);
            }
            else
            {
                Label(new Rect(card.x + 24f, card.y + 94f, card.width - 48f, 24f), "СЧЁТ " + scoreText,
                    13, Muted, TextAnchor.MiddleLeft);
            }

            Button(layout.Primary, crashed ? "ЕЩЁ ОДИН ЗАБЕГ" : "ПРОДОЛЖИТЬ", crashed ? "retry" : "resume",
                Ink, true, 14);
            Button(layout.Secondary, "ГЛАВНОЕ МЕНЮ", "menu", White, false, 10);
        }

        private void Tile(Rect rect, string title, string value, Color color)
        {
            Plate(rect, Glass, Line);
            Label(new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, 15f), title,
                10, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(new Rect(rect.x + 3f, rect.y + 17f, rect.width - 6f, rect.height - 20f), value,
                rect.width < 85f ? 20 : 23, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void Button(Rect rect, string text, string action, Color color, bool primary, int size, bool enabled = true)
        {
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            Color background = primary
                ? (hover ? LimeHover : Lime)
                : (hover ? GlassHover : Glass);
            Plate(rect, enabled ? background : WithAlpha(background, 0.57f), primary ? background : Line);
            if (primary)
                Fill(new Rect(rect.x + 5f, rect.y + 4f, 2f, rect.height - 8f), WithAlpha(Ink, 0.4f));
            controlStyle.font = bodyFont;
            controlStyle.fontStyle = FontStyle.Bold;
            controlStyle.fontSize = size;
            controlStyle.normal.textColor = color;
            controlStyle.hover.textColor = color;
            controlStyle.active.textColor = color;
            controlStyle.focused.textColor = color;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled && previousEnabled;
            bool pressed = GUI.Button(rect, text, controlStyle);
            GUI.enabled = previousEnabled;
            if (pressed)
                ActionRequested?.Invoke(action);
        }

        private void StyleButtons()
        {
            foreach (GUIStyleState state in new[] {
                controlStyle.normal, controlStyle.hover, controlStyle.active, controlStyle.focused,
                controlStyle.onNormal, controlStyle.onHover, controlStyle.onActive, controlStyle.onFocused })
            {
                state.background = null;
                state.textColor = White;
            }
        }

        private void Label(Rect rect, string text, int size, Color color, TextAnchor anchor,
            FontStyle style = FontStyle.Normal, bool display = false, bool wrap = false)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            labelStyle.font = display ? titleFont : bodyFont;
            labelStyle.fontSize = size;
            labelStyle.fontStyle = style;
            labelStyle.normal.textColor = color;
            labelStyle.alignment = anchor;
            labelStyle.wordWrap = wrap;
            labelStyle.clipping = TextClipping.Clip;
            GUI.Label(rect, text, labelStyle);
        }

        private static void Plate(Rect rect, Color fill, Color line)
        {
            Fill(rect, line);
            Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Color WithAlpha(Color color, float opacity) =>
            new Color(color.r, color.g, color.b, opacity);
    }
}
