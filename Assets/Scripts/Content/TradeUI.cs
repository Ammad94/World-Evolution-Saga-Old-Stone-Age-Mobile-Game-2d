using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrehistoricSurvival.Core;
using PrehistoricSurvival.UI;

namespace PrehistoricSurvival.Content
{
    /// <summary>One barter offer: give N of item A, receive M of item B.</summary>
    public class TradeOffer
    {
        public string giveId; public int giveAmount;
        public string receiveId; public int receiveAmount;
        public string Label;
    }

    /// <summary>
    /// Parchment trade panel with a rotating set of barter offers. Prices improve
    /// slightly with friendship. Opening near the elder completes the trade quest.
    /// </summary>
    public class TradeUI : MonoBehaviour
    {
        private static TradeUI _instance;
        public static TradeUI Instance => _instance;

        private GameObject _panel;
        private RectTransform _list;
        private TextMeshProUGUI _friendLabel;
        private readonly List<TradeOffer> _offers = new List<TradeOffer>();

        public static void Open() => Ensure().ShowPanel();
        public static void Close() { if (_instance != null) _instance.HidePanel(); }
        public static bool IsOpen => _instance != null && _instance._panel.activeSelf;

        public static TradeUI Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("TradeUI");
            _instance = go.AddComponent<TradeUI>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        private void BuildCatalog()
        {
            _offers.Add(new TradeOffer { giveId = "animal_hide", giveAmount = 3, receiveId = "bone_spear", receiveAmount = 1, Label = "3 hides → bone spear" });
            _offers.Add(new TradeOffer { giveId = "raw_meat", giveAmount = 4, receiveId = "fur_pelt", receiveAmount = 1, Label = "4 raw meat → fur pelt" });
            _offers.Add(new TradeOffer { giveId = "flint_shard", giveAmount = 3, receiveId = "obsidian", receiveAmount = 1, Label = "3 flint → obsidian" });
            _offers.Add(new TradeOffer { giveId = "fiber", giveAmount = 6, receiveId = "healing_salve", receiveAmount = 1, Label = "6 fiber → healing salve" });
            _offers.Add(new TradeOffer { giveId = "fur_pelt", giveAmount = 2, receiveId = "copper_ore", receiveAmount = 1, Label = "2 pelts → copper ore" });
            _offers.Add(new TradeOffer { giveId = "stone", giveAmount = 8, receiveId = "water_skin", receiveAmount = 1, Label = "8 stones → waterskin" });
            _offers.Add(new TradeOffer { giveId = "berries", giveAmount = 8, receiveId = "dried_meat", receiveAmount = 2, Label = "8 berries → 2 dried meat" });
            _offers.Add(new TradeOffer { giveId = "bone", giveAmount = 4, receiveId = "atlatl", receiveAmount = 1, Label = "4 bones → atlatl" });
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildCatalog();
            BuildPanel();
            _panel.SetActive(false);
        }

        private void BuildPanel()
        {
            var canvasGO = new GameObject("TradeCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 180;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            UIFactory.EnsureEventSystem();

            _panel = new GameObject("TradePanel");
            _panel.transform.SetParent(canvasGO.transform, false);
            var rt = _panel.AddComponent<RectTransform>();
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(780, 880));
            var bg = UIFactory.Panel(_panel.transform, "BG", new Color(0.94f, 0.87f, 0.7f, 0.97f));
            UIFactory.Stretch(bg);
            bg.sprite = UITheme.Parchment;
            bg.type = Image.Type.Sliced;

            var title = UIFactory.Text(_panel.transform, "Title", "TRIBE TRADING", 42, Vector2.zero, new Vector2(700, 60),
                new Color(0.32f, 0.22f, 0.12f));
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(700, 60));

            _friendLabel = UIFactory.Text(_panel.transform, "Friendship", "", 24, Vector2.zero, new Vector2(700, 36),
                new Color(0.45f, 0.33f, 0.2f));
            UIFactory.Anchor(_friendLabel.rectTransform, new Vector2(0.5f, 0.885f), new Vector2(700, 36));

            var listGo = new GameObject("Offers");
            listGo.transform.SetParent(_panel.transform, false);
            _list = listGo.AddComponent<RectTransform>();
            UIFactory.Anchor(_list, new Vector2(0.5f, 0.5f), new Vector2(710, 620));

            var close = UIFactory.Button(_panel.transform, "Close", "LEAVE", new Vector2(0.5f, 0.07f),
                new Vector2(320, 74), HidePanel, new Color(0.45f, 0.28f, 0.16f));
        }

        private void ShowPanel()
        {
            if (TribeCampSystem.Instance != null)
                _friendLabel.text = $"Tribe friendship: {TribeCampSystem.Instance.Friendship:0}  (better prices at higher friendship)";
            else
                _friendLabel.text = "";
            RefreshOffers();
            _panel.SetActive(true);
            Feedback.UITween.Show(_panel);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSound("ui_page", 0.7f);
        }

        private void HidePanel()
        {
            Feedback.UITween.Hide(_panel);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBack();
        }

        private void RefreshOffers()
        {
            foreach (Transform child in _list) Destroy(child.gameObject);

            float discount = TribeCampSystem.Instance != null
                ? Mathf.Clamp01(TribeCampSystem.Instance.Friendship / 60f) * 0.25f : 0f;

            var inv = InventorySystem.Instance;
            for (int i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                int give = Mathf.Max(1, Mathf.RoundToInt(offer.giveAmount * (1f - discount)));

                var row = new GameObject("Offer_" + offer.receiveId);
                row.transform.SetParent(_list, false);
                var rrt = row.AddComponent<RectTransform>();
                UIFactory.Anchor(rrt, new Vector2(0.5f, 1f), new Vector2(700, 66), new Vector2(0, -12f - i * 74f));
                var rbg = UIFactory.Panel(row.transform, "BG", new Color(0.28f, 0.19f, 0.12f, 0.92f));
                UIFactory.Stretch(rbg);
                rbg.sprite = UITheme.PanelDark;
                rbg.type = Image.Type.Sliced;

                string labelText = $"{give}x {Title(offer.giveId)}  ->  {offer.receiveAmount}x {Title(offer.receiveId)}   (you have {CountOf(inv, offer.giveId)})";
                var label = UIFactory.Text(row.transform, "L", labelText, 22, new Vector2(0.5f, 0.5f), new Vector2(470, 62),
                    new Color(0.94f, 0.89f, 0.76f));
                label.alignment = TextAlignmentOptions.Left;
                label.rectTransform.anchoredPosition = new Vector2(-100f, 0f);

                bool can = inv != null && CountOf(inv, offer.giveId) >= give;
                var btn = UIFactory.Button(row.transform, "Trade", "TRADE", new Vector2(0.86f, 0.5f),
                    new Vector2(140, 48), null,
                    can ? new Color(0.55f, 0.36f, 0.18f) : new Color(0.32f, 0.27f, 0.23f), 22);
                var captured = offer;
                int capturedGive = give;
                btn.onClick.AddListener(() => TryTrade(captured, capturedGive));
            }
        }

        private static string Title(string itemId)
        {
            var so = Resources.Load<ItemDataSO>("Items/" + itemId);
            return so != null ? so.data.displayName : itemId;
        }

        private static int CountOf(InventorySystem inv, string itemId)
        {
            if (inv == null) return 0;
            int n = 0;
            foreach (var slot in inv.Slots)
                if (!slot.IsEmpty && slot.item.itemId == itemId) n += slot.quantity;
            return n;
        }

        private void TryTrade(TradeOffer offer, int giveAmount)
        {
            var inv = InventorySystem.Instance;
            if (inv == null) return;

            if (inv.RemoveItemById(offer.giveId, giveAmount) <= 0)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayError();
                return;
            }
            var so = Resources.Load<ItemDataSO>("Items/" + offer.receiveId);
            if (so != null) inv.AddItem(so.data, offer.receiveAmount);

            if (TribeCampSystem.Instance != null) TribeCampSystem.Instance.AddFriendship(2f);
            EventManager.TriggerEvent("TradeCompleted");
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSound("ui_trade", 0.9f);
            Feedback.UITween.Pop(_panel.GetComponent<RectTransform>());
            ShowPanel();
        }
    }
}
