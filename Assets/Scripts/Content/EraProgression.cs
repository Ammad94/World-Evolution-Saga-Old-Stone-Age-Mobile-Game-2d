using System;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.UI;

namespace PrehistoricSurvival.Content
{
    /// <summary>The three playable eras of human technological development.</summary>
    public enum Era { Paleolithic = 0, AdvancedStone = 1, CopperAge = 2 }

    /// <summary>
    /// Era progression ("World Evolution" backbone): crafting and hunting grants
    /// Knowledge; reaching thresholds advances the tribe to the next era, unlocking
    /// recipe tiers, with an on-screen banner and fanfare. Persisted via SaveSystem.
    /// </summary>
    public class EraProgression : MonoBehaviour
    {
        public static EraProgression Instance { get; private set; }

        [Header("Knowledge thresholds per era advance")]
        public int era1Requirement = 40;
        public int era2Requirement = 120;

        public Era CurrentEra { get; private set; } = Era.Paleolithic;
        public float Knowledge { get; private set; }

        public event Action<Era> OnEraAdvanced;

        /// <summary>Knowledge needed for the next era (int.MaxValue when maxed).</summary>
        public float NextRequirement =>
            CurrentEra == Era.Paleolithic ? era1Requirement :
            CurrentEra == Era.AdvancedStone ? era2Requirement : float.MaxValue;

        private BannerUI _banner;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            EnsureBanner();
            EventManager.Subscribe(GameEvents.ItemCrafted, OnItemCrafted);
            EventManager.Subscribe(GameEvents.AnimalKilled, OnAnimalKilled);
            EventManager.Subscribe(GameEvents.TileDestroyed, OnWorked);
        }

        private void EnsureBanner()
        {
            if (_banner != null) return;
            var canvasGO = new GameObject("EraCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            var bannerGo = new GameObject("EraBanner");
            bannerGo.transform.SetParent(canvasGO.transform, false);
            _banner = bannerGo.AddComponent<BannerUI>();
            canvasGO.SetActive(false);
            _banner.ownerCanvas = canvasGO;
        }

        // ------------------------------------------------------------------
        public void Learn(float points)
        {
            Knowledge += points;
            if (CurrentEra == Era.Paleolithic && Knowledge >= era1Requirement) Advance();
            else if (CurrentEra == Era.AdvancedStone && Knowledge >= era2Requirement) Advance();
        }

        public void Advance()
        {
            if (CurrentEra >= Era.CopperAge) return;
            CurrentEra = (Era)((int)CurrentEra + 1);
            Debug.Log($"[EraProgression] Advanced to {CurrentEra} era!");
            StartCoroutine(BannerRoutine(CurrentEra));
            EventManager.TriggerEvent("EraAdvanced", CurrentEra);
            OnEraAdvanced?.Invoke(CurrentEra);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSound("era_up", 0.9f);
            if (Feedback.GameFeel.Instance != null) Feedback.GameFeel.Shake(0.4f);
        }

        /// <summary>Restore from a save without triggering the banner.</summary>
        public void Restore(int eraIndex, float knowledge)
        {
            Knowledge = knowledge;
            CurrentEra = (Era)Mathf.Clamp(eraIndex, 0, 2);
        }

        private IEnumerator BannerRoutine(Era era)
        {
            EnsureBanner();
            _banner.ownerCanvas.SetActive(true);
            yield return _banner.Play(era);
            _banner.ownerCanvas.SetActive(false);
        }

        // ------------------------------------------------------------------
        private void OnItemCrafted(object payload)
        {
            // Crafting anything teaches; tools/weapons teach more (matched by id).
            string id = payload as string;
            float points = 2f;
            if (id != null)
            {
                if (id.Contains("spear") || id.Contains("axe") || id.Contains("pickaxe") || id.Contains("knife") || id.Contains("atlatl")) points = 5f;
                if (id.Contains("copper") || id.Contains("amulet")) points = 8f;
            }
            Learn(points);
        }

        private void OnAnimalKilled(object payload)
        {
            var ai = payload as PrehistoricSurvival.AI.AnimalAI;
            float points = 3f;
            if (ai != null)
            {
                string n = ai.animalName.ToLowerInvariant();
                if (n.Contains("mammoth") || n.Contains("rhino")) points = 9f;
                else if (n.Contains("sabertooth") || n.Contains("bear") || n.Contains("lion")) points = 6f;
            }
            Learn(points);
        }

        private void OnWorked(object _) => Learn(0.6f);

        // ------------------------------------------------------------------
        /// <summary>Full-width era banner with drum pulse animation.</summary>
        private class BannerUI : MonoBehaviour
        {
            public GameObject ownerCanvas;
            private TextMeshProUGUI _title;
            private TextMeshProUGUI _sub;
            private Image _bg;

            private void Awake()
            {
                var rt = gameObject.AddComponent<RectTransform>();
                UIFactory.Anchor(rt, new Vector2(0.5f, 0.72f), new Vector2(1100, 190));
                _bg = UIFactory.Panel(transform, "BG", new Color(0f, 0f, 0f, 0.55f));
                UIFactory.Stretch(_bg);
                _bg.sprite = UITheme.Banner;
                _bg.type = Image.Type.Sliced;

                _title = UIFactory.Text(transform, "Title", "", 52, Vector2.zero, new Vector2(1000, 70),
                            new Color(0.95f, 0.87f, 0.62f));
                UIFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(1000, 70));
                _sub = UIFactory.Text(transform, "Sub", "", 28, Vector2.zero, new Vector2(1000, 50),
                            new Color(0.85f, 0.78f, 0.6f));
                UIFactory.Anchor(_sub.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(1000, 50));
            }

            public IEnumerator Play(Era era)
            {
                string title = era == Era.AdvancedStone ? "ADVANCED STONE AGE" : "COPPER AGE";
                _title.text = title;
                _sub.text = era == Era.AdvancedStone
                    ? "Your tribe masters knapped bone and obsidian"
                    : "Fire-hardened copper flows into new tools";
                transform.localScale = Vector3.one * 0.8f;
                float t = 0f;
                while (t < 0.4f)
                {
                    t += Time.unscaledDeltaTime;
                    transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, t / 0.4f);
                    yield return null;
                }
                transform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(3.2f);
            }
        }
    }
}
