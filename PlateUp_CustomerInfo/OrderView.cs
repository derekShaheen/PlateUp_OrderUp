using Kitchen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkripOrderUp
{
    internal class OrderView : MonoBehaviour
    {
        class MainGroup
        {
            public string Name;
            public string Extras;
            public int Count;
        }

        class SideGroup
        {
            public string Name;
            public int Count;
        }

        Canvas ordersCanvas;
        TextMeshProUGUI ordersText;
        TextMeshProUGUI headerText;

        public float currentFontSize = PreferencesManager.Get<float>("FontSize", 16);

        int lastDisplayedTime = -1;

        public Canvas OrdersCanvas { get => ordersCanvas; set => ordersCanvas = value; }
        TextMeshProUGUI OrdersText { get => ordersText; set => ordersText = value; }
        TextMeshProUGUI HeaderText { get => headerText; set => headerText = value; }

        // display option
        const bool ShowSidesInline = false;

        // Flair colors (hex RGB with alpha via TMP)
        const string ColorAccent = "#FFD98A";     // soft amber for headers
        const string ColorSubtle = "#AAAAAA";     // muted gray for timers/labels
        const string ColorSep = "#444444";     // separator line color
        const string ColorBullet = "#FFFFFF";     // main bullet color

        // Box-drawing separator
        const string Separator = "<color=" + ColorSep + ">───────────────</color>";

        public void Awake()
        {
            InitializeUI();

            OrdersCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            OrdersCanvas.overrideSorting = true;
            OrdersCanvas.sortingOrder = 10000;

            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOrdersUpdated += HandleOrdersUpdated;
            }
            else
            {
                Debug.LogError("[OrderUp] OrderManager instance not found. Ensure OrderManager is present in the scene.");
            }
        }

        void OnDestroy()
        {
            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOrdersUpdated -= HandleOrdersUpdated;
            }
        }

        void Update()
        {
            if((GameInfo.CurrentScene == SceneType.Kitchen
                && !GameInfo.IsPreparationTime)
               || GameInfo.CurrentScene == SceneType.Franchise)
            if (OrdersText != null && OrdersText.fontSize != currentFontSize)
            {
                if (currentFontSize == -1f)
                {
                    if (OrdersCanvas != null) OrdersCanvas.enabled = false;
                }
                else
                {
                    if (OrdersCanvas != null) OrdersCanvas.enabled = true;
                }
                OrdersText.fontSize = currentFontSize;
                HeaderText.fontSize = currentFontSize + 4f;
            }

            int currentSeconds = Mathf.FloorToInt(Time.time);
            if (currentSeconds != lastDisplayedTime)
            {
                lastDisplayedTime = currentSeconds;
                var inst = OrderManager.Instance;
                UpdateOrderText(inst != null ? inst.orderGroups : null);
            }
        }

        void HandleOrdersUpdated(List<OrderManager.OrderGroup> orderGroups)
        {
            UpdateOrderText(orderGroups);
        }

        void UpdateOrderText(List<OrderManager.OrderGroup> orderGroups)
        {
            if (OrdersText == null) return;

            if (orderGroups == null || orderGroups.Count == 0)
            {
                OrdersText.text = string.Empty;
                return;
            }

            var sb = new StringBuilder(1024);

            var orderedGroups = orderGroups
                .Where(g => g != null)
                .OrderBy(g => g.OrderNumber)
                .ToList();

            for (int gIdx = 0; gIdx < orderedGroups.Count; gIdx++)
            {
                var group = orderedGroups[gIdx];

                int secondsElapsed = (int)(Time.time - group.StartTime);
                string timeElapsedString = Helpers.FormatTime(secondsElapsed);

                var mains = group.Items.Where(i => i != null && string.IsNullOrEmpty(i.SideItem)).ToList();
                var sides = group.Items.Where(i => i != null && !string.IsNullOrEmpty(i.SideItem)).ToList();

                var mainGroups = mains
                    .GroupBy(i => new { i.DisplayName, Extras = i.ExtrasText ?? "" })
                    .Select(g => new MainGroup
                    {
                        Name = g.Key.DisplayName,
                        Extras = g.Key.Extras,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Name)
                    .ThenBy(x => x.Extras)
                    .ToList();

                var sideGroups = sides
                    .GroupBy(i => i.SideItem)
                    .Select(g => new SideGroup
                    {
                        Name = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Name)
                    .ToList();

                if (mainGroups.Count == 0 && sideGroups.Count == 0)
                    continue;

                // Header (no bold)
                sb.Append("<color=").Append(ColorAccent).Append(">Order #").Append(group.OrderNumber).Append("</color>");
                sb.Append("   <color=").Append(ColorSubtle).Append(">").Append(timeElapsedString).Append("</color>");
                sb.Append('\n');

                // Body
                BuildVerboseMains(sb, mainGroups);

                if (sideGroups.Count > 0)
                {
                    if (ShowSidesInline)
                    {
                        sb.Append("   ");
                        BuildInlineSides(sb, sideGroups);
                    }
                    else
                    {
                        BuildSideBlock(sb, sideGroups);
                    }
                }

                // Separator between orders
                if (gIdx < orderedGroups.Count - 1)
                {
                    sb.Append(Separator).Append('\n');
                }
            }

            OrdersText.text = sb.ToString();
        }

        static void BuildInlineSides(StringBuilder sb, List<SideGroup> sideGroups)
        {
            sb.Append("<color=").Append(ColorSubtle).Append(">sides:</color> ");
            for (int i = 0; i < sideGroups.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var entry = sideGroups[i];
                if (entry.Count > 1)
                {
                    sb.Append(entry.Count).Append("x ");
                }
                sb.Append(entry.Name);
            }
        }

        static void BuildVerboseMains(StringBuilder sb, List<MainGroup> mainGroups)
        {
            for (int i = 0; i < mainGroups.Count; i++)
            {
                var entry = mainGroups[i];
                sb.Append("   <color=").Append(ColorBullet).Append(">►</color> ");
                if (entry.Count > 1)
                {
                    sb.Append(entry.Count).Append("x ");
                }
                sb.Append(entry.Name);
                if (!string.IsNullOrEmpty(entry.Extras))
                {
                    sb.Append(" ").Append(entry.Extras);
                }
                sb.Append('\n');
            }
        }

        static void BuildSideBlock(StringBuilder sb, List<SideGroup> sideGroups)
        {
            sb.Append("   ").Append("<color=").Append(ColorSubtle).Append(">sides:</color>").Append('\n');
            for (int i = 0; i < sideGroups.Count; i++)
            {
                var entry = sideGroups[i];
                sb.Append("     • ");
                if (entry.Count > 1)
                {
                    sb.Append(entry.Count).Append("x ");
                }
                sb.Append(entry.Name).Append('\n');
            }
        }

        void InitializeUI()
        {
            if (GameObject.Find("OrderUpCanvas") != null)
            {
                OrdersCanvas = GameObject.Find("OrderUpCanvas").GetComponent<Canvas>();
                OrdersText = OrdersCanvas.GetComponentInChildren<TextMeshProUGUI>();
                return;
            }

            GameObject canvasGO = new GameObject("OrderUpCanvas");
            OrdersCanvas = canvasGO.AddComponent<Canvas>();
            OrdersCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            GameObject draggablePanelGO = new GameObject("DraggablePanel");
            draggablePanelGO.transform.SetParent(canvasGO.transform, false);

            RectTransform panelRect = draggablePanelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(0, -20);
            panelRect.sizeDelta = new Vector2(600, 120);

            if (PreferencesManager.HasKey("PosX") && PreferencesManager.HasKey("PosY"))
            {
                float posX = PreferencesManager.Get<float>("PosX", 0f);
                float posY = PreferencesManager.Get<float>("PosY", -20f);
                panelRect.anchoredPosition = new Vector2(posX, posY);
            }
            else
            {
                PreferencesManager.Set<float>("PosX", panelRect.anchoredPosition.x);
                PreferencesManager.Set<float>("PosY", panelRect.anchoredPosition.y);
            }

            EnsurePanelVisibility(panelRect, OrdersCanvas.GetComponent<RectTransform>());

            Image backgroundImage = draggablePanelGO.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.95f);

            draggablePanelGO.AddComponent<Draggable>();

            VerticalLayoutGroup layoutGroup = draggablePanelGO.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.spacing = 0;

            ContentSizeFitter contentSizeFitter = draggablePanelGO.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject headerGO = new GameObject("HeaderText");
            headerGO.transform.SetParent(draggablePanelGO.transform, false);

            HeaderText = headerGO.AddComponent<TextMeshProUGUI>();
            HeaderText.text = "Order Up!";
            HeaderText.alignment = TextAlignmentOptions.Center;
            HeaderText.fontSize = PreferencesManager.Get<float>("FontSize", 16) + 4f;
            HeaderText.color = Color.white;
            HeaderText.fontMaterial = new Material(HeaderText.fontMaterial);
            HeaderText.fontMaterial.SetColor("_UnderlayColor", new Color(1f, 0.9411765f, 0.9137255f, .3f));
            HeaderText.fontMaterial.SetFloat("_UnderlaySoftness", 1f);
            HeaderText.fontMaterial.SetFloat("_UnderlayDilate", -0.5f);

            RectTransform headerRect = HeaderText.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1);
            headerRect.anchorMax = new Vector2(0.5f, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 30);
            headerRect.anchoredPosition = new Vector2(0, 0);

            LayoutElement headerLayoutElement = HeaderText.gameObject.AddComponent<LayoutElement>();
            headerLayoutElement.preferredWidth = -1;
            headerLayoutElement.minWidth = 100;

            GameObject textGO = new GameObject("OrdersText");
            textGO.transform.SetParent(draggablePanelGO.transform, false);

            OrdersText = textGO.AddComponent<TextMeshProUGUI>();
            OrdersText.fontMaterial = new Material(OrdersText.fontMaterial);
            OrdersText.alignment = TextAlignmentOptions.TopLeft;
            OrdersText.fontSize = PreferencesManager.Get<float>("FontSize", 16);
            OrdersText.color = new Color(1f, 0.9411765f, 0.9137255f, 1f);
            OrdersText.fontMaterial.DisableKeyword("UNDERLAY_ON");
            OrdersText.outlineColor = new Color32(0, 0, 0, 200);
            OrdersText.outlineWidth = 0.1f;
            OrdersText.enableWordWrapping = false;
            OrdersText.margin = new Vector4(10, 10, 10, 10);

            RectTransform ordersRect = OrdersText.GetComponent<RectTransform>();
            ordersRect.anchorMin = new Vector2(0, 1);
            ordersRect.anchorMax = new Vector2(1, 1);
            ordersRect.pivot = new Vector2(0.5f, 1);
            ordersRect.sizeDelta = new Vector2(0, -15);
            ordersRect.anchoredPosition = new Vector2(0, -15);

            LayoutElement ordersLayoutElement = OrdersText.gameObject.AddComponent<LayoutElement>();
            ordersLayoutElement.preferredWidth = -1;
            ordersLayoutElement.minWidth = 180;

            LayoutElement panelLayoutElement = draggablePanelGO.AddComponent<LayoutElement>();
            panelLayoutElement.minWidth = 180;
            panelLayoutElement.minHeight = 120;

            if (currentFontSize == -1f && OrdersCanvas != null)
                OrdersCanvas.enabled = false;
        }

        void EnsurePanelVisibility(RectTransform panelRect = null, RectTransform canvasRect = null)
        {
            if (panelRect == null)
            {
                GameObject panelGO = GameObject.Find("OrderUpCanvas/DraggablePanel");
                if (panelGO != null) panelRect = panelGO.GetComponent<RectTransform>();
                else return;
            }

            if (canvasRect == null)
            {
                if (OrdersCanvas != null) canvasRect = OrdersCanvas.GetComponent<RectTransform>();
                else return;
            }

            Vector2 panelSize = panelRect.sizeDelta;
            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 panelPos = panelRect.anchoredPosition;

            float left = panelPos.x;
            float right = panelPos.x + panelSize.x;
            float top = panelPos.y;
            float bottom = panelPos.y - panelSize.y;

            bool isWithinX = left >= 0 && right <= canvasSize.x;
            bool isWithinY = top <= 0 && bottom >= -canvasSize.y;

            if (!isWithinX || !isWithinY)
            {
                float centerX = (canvasSize.x - panelSize.x) / 2f;
                float centerY = (-canvasSize.y + panelSize.y) / 2f;

                panelRect.anchoredPosition = new Vector2(centerX, centerY);
                PreferencesManager.Set<float>("PosX", centerX);
                PreferencesManager.Set<float>("PosY", centerY);
            }
        }
    }
}
