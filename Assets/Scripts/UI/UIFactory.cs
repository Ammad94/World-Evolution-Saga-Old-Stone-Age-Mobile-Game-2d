using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace PrehistoricSurvival.UI
{
    /// <summary>
    /// Helpers that build uGUI elements from code. Used by the runtime bootstrapper,
    /// the loading screen, the pause menu and the world map so that a fully working
    /// interface exists even in a completely empty scene.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Parchment = new Color(0.92f, 0.85f, 0.68f);
        public static readonly Color Bark = new Color(0.24f, 0.17f, 0.10f, 0.92f);
        public static readonly Color Ember = new Color(0.78f, 0.45f, 0.14f, 1f);

        /// <summary>Create a screen-space canvas scaled for mobile.</summary>
        public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Guarantee an EventSystem exists (without it, nothing is clickable).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            return FinishPanel(RawPanel(parent, name, color), UITheme.SkinPanel.Dark);
        }

        /// <summary>Untyled panel (background image only).</summary>
        public static Image RawPanel(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>Make a RectTransform fill its parent.</summary>
        public static void Stretch(Graphic graphic) => Stretch(graphic.rectTransform);

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content, int fontSize,
            Vector2 anchor, Vector2 size, Color color, Vector2 offset = default)
        {
            var rt = Rect(parent, name);
            Anchor(rt, anchor, size, offset);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>A stone-age styled button that is wired to an action.</summary>
        public static Button Button(Transform parent, string name, string label, Vector2 anchor,
            Vector2 size, UnityAction onClick, Color? tint = null, int fontSize = 34)
        {
            var rt = Rect(parent, name);
            Anchor(rt, anchor, size);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = tint ?? Bark;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.72f, 0.66f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = Text(rt, "Label", label, fontSize, new Vector2(0.5f, 0.5f), size, Parchment);
            Stretch(text.rectTransform);

            // Themed skin + tactile feel (added automatically when the skin exists).
            if (UITheme.Button != null)
            {
                var spriteSwap = button.spriteState;
                img.type = Image.Type.Sliced;
                img.sprite = UITheme.Button;
                if (UITheme.ButtonPressed != null) spriteSwap.pressedSprite = UITheme.ButtonPressed;
                button.spriteState = spriteSwap;
                img.color = tint ?? Color.white;
            }
            rt.gameObject.AddComponent<Feedback.UIButtonFX>();

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>Horizontal fill bar (health, hunger, loading progress…).</summary>
        public static Slider ProgressBar(Transform parent, string name, Vector2 anchor, Vector2 size, Color fill)
        {
            var rt = Rect(parent, name);
            Anchor(rt, anchor, size);

            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.09f, 0.08f, 0.85f);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var fillArea = Rect(rt, "Fill Area");
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(3, 3);
            fillArea.offsetMax = new Vector2(-3, -3);

            var fillRT = Rect(fillArea, "Fill");
            Stretch(fillRT);
            var fillImg = fillRT.gameObject.AddComponent<Image>();
            fillImg.color = fill;

            if (UITheme.BarFrame != null)
            {
                bg.sprite = UITheme.BarFrame;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            if (UITheme.BarFill != null)
            {
                fillImg.sprite = UITheme.BarFill;
                fillImg.type = Image.Type.Sliced;
                fillImg.color = fill;
            }

            slider.fillRect = fillRT;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        /// <summary>Apply the themed skin to a raw panel image.</summary>
        public static Image FinishPanel(Image img, UITheme.SkinPanel panel)
        {
            UITheme.ApplyPanel(img, panel);
            return img;
        }

        /// <summary>Round sprite generated at runtime (joystick ring, knob, map pins).</summary>
        public static Sprite CircleSprite(int size = 128, float thickness = 0f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            float inner = thickness > 0f ? r - thickness : 0f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    bool on = d <= r && d >= inner;
                    float edge = Mathf.Clamp01(r - d);
                    pixels[y * size + x] = on
                        ? new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(edge)))
                        : new Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
