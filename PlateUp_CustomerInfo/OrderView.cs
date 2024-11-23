using System;
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
        // References to UI components
        private Canvas ordersCanvas;
        private TextMeshProUGUI ordersText;
        private TextMeshProUGUI headerText;
        public float currentFontSize = PreferencesManager.Get<float>("FontSize", 16);

        // Shared time variables
        private int lastDisplayedTime = -1; // Initialize to -1 to ensure the first update occurs

        public Canvas OrdersCanvas { get => ordersCanvas; set => ordersCanvas = value; }
        private TextMeshProUGUI OrdersText { get => ordersText; set => ordersText = value; }
        private TextMeshProUGUI HeaderText { get => headerText; set => headerText = value; }

        public void Awake()
        {
            // Initialize UI
            InitializeUI();

            // Subscribe to OrderManager events
            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOrdersUpdated += HandleOrdersUpdated;
            }
            else
            {
                Debug.LogError("[OrderUp] OrderManager instance not found. Ensure OrderManager is present in the scene.");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOrdersUpdated -= HandleOrdersUpdated;
            }
        }

        private void Update()
        {
            if (OrdersText != null && OrdersText.fontSize != currentFontSize)
            {
                if (currentFontSize == -1f)
                {
                    // Disable UI visibility
                    if (OrdersCanvas != null)
                    {
                        OrdersCanvas.enabled = false;
                    }
                }
                else
                {
                    // Enable UI visibility
                    if (OrdersCanvas != null)
                    {
                        OrdersCanvas.enabled = true;
                    }
                }
                OrdersText.fontSize = currentFontSize;
                HeaderText.fontSize = currentFontSize + 4f;
            }

            // Update the time elapsed every second
            int currentSeconds = Mathf.FloorToInt(Time.time);
            if (currentSeconds != lastDisplayedTime)
            {
                lastDisplayedTime = currentSeconds;
                UpdateOrderText(OrderManager.Instance.orderGroups);
            }
        }

        private void HandleOrdersUpdated(System.Collections.Generic.List<OrderManager.OrderGroup> orderGroups)
        {
            UpdateOrderText(orderGroups);
        }

        private void UpdateOrderText(System.Collections.Generic.List<OrderManager.OrderGroup> orderGroups)
        {
            if (orderGroups == null || orderGroups.Count == 0)
            {
                OrdersText.text = string.Empty;
                return;
            }

            StringBuilder allOrders = new StringBuilder();

            // Iterate over OrderGroups in the order they were added
            foreach (var orderGroup in orderGroups.Where(og => og != null).OrderBy(og => og.StartTime))
            {
                // Calculate time elapsed
                int secondsElapsed = (int)(Time.time - orderGroup.StartTime);
                string timeElapsedString = $"{Helpers.FormatTime(secondsElapsed)}";

                //string timeElapsedString = $"{secondsElapsed}";

                // Group items within each OrderGroup by DisplayName and ColourblindText
                var groupedItems = orderGroup.Items
                    .Where(oi => oi != null) // Ensure OrderItem is not null
                    .OrderBy(oi => oi.DisplayName)
                    .GroupBy(oi => new { oi.DisplayName, oi.ColourblindText, oi.SideItem })
                    .Select(g => new
                    {
                        DisplayName = g.Key.DisplayName,
                        ColourblindText = g.Key.ColourblindText,
                        SideItem = g.Key.SideItem,
                        Count = g.Count()
                    })
                    .ToList();

                // Skip this OrderGroup if there are no valid items
                if (groupedItems.Count == 0)
                    continue;

                // Build the order lines for this OrderGroup
                StringBuilder orderGroupLines = new StringBuilder();
                foreach (var item in groupedItems.OrderByDescending(comparer => comparer.Count)
                                                 .ThenBy(comparer => comparer.DisplayName)
                                                 .ThenBy(comparer => comparer.ColourblindText)
                                                 .ThenBy(comparer => comparer.SideItem))
                {
                    string orderLine = item.Count > 1
                        ? $"\t► {item.Count}x{item.DisplayName}{item.ColourblindText}"
                        : $"\t► {item.DisplayName}{item.ColourblindText}";

                    if (!string.IsNullOrEmpty(item.SideItem))
                    {
                        orderLine += $"\n\t\tw/{item.SideItem}";
                    }

                    orderGroupLines.Append(orderLine + "\n");
                }

                // Remove the trailing comma and space
                //if (orderGroupLines.Length > 2)
                //    orderGroupLines.Length -= 2;

                // Append the time elapsed to the order group
                allOrders.Append($"Order #{orderGroup.OrderNumber}\t{timeElapsedString}\n{orderGroupLines.ToString()}");
            }

            OrdersText.text = allOrders.ToString();
        }

        private void InitializeUI()
        {
            // Check if the Canvas already exists to prevent duplicates
            if (GameObject.Find("OrderUpCanvas") != null)
            {
                OrdersCanvas = GameObject.Find("OrderUpCanvas").GetComponent<Canvas>();
                OrdersText = OrdersCanvas.GetComponentInChildren<TextMeshProUGUI>();
                Debug.Log("[OrderUp] Existing OrderUpCanvas found and initialized.");
                return;
            }

            // Create a new Canvas GameObject
            GameObject canvasGO = new GameObject("OrderUpCanvas");
            OrdersCanvas = canvasGO.AddComponent<Canvas>();
            OrdersCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Add Canvas Scaler for UI scaling
            CanvasScaler canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;

            // Add Graphic Raycaster (optional, needed for UI interactions)
            canvasGO.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists in the scene
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            // Create a Draggable Panel
            GameObject draggablePanelGO = new GameObject("DraggablePanel");
            draggablePanelGO.transform.SetParent(canvasGO.transform, false);

            // Add RectTransform
            RectTransform panelRect = draggablePanelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(0, -20); // Initial position
            panelRect.sizeDelta = new Vector2(600, 120); // Initial size
            if (PreferencesManager.HasKey("PosX") && PreferencesManager.HasKey("PosY"))
            {
                float posX = PreferencesManager.Get<float>("PosX", 0f);
                float posY = PreferencesManager.Get<float>("PosY", -20f); // Default to initial position
                panelRect.anchoredPosition = new Vector2(posX, posY);
                //Debug.Log($"[OrderUp] Loaded panel position from preferences: ({posX}, {posY})");
            }
            else
            {
                // Set initial prefs based on the default anchoredPosition
                PreferencesManager.Set<float>("PosX", panelRect.anchoredPosition.x);
                PreferencesManager.Set<float>("PosY", panelRect.anchoredPosition.y);
                //Debug.Log($"[OrderUp] Saved initial panel position to preferences: ({panelRect.anchoredPosition.x}, {panelRect.anchoredPosition.y})");
            }

            // **Ensure the panel is visible upon initialization**
            EnsurePanelVisibility(panelRect, OrdersCanvas.GetComponent<RectTransform>());

            // Add Image component for the background
            Image backgroundImage = draggablePanelGO.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.95f); // Semi-transparent black

            // Add the Draggable component
            draggablePanelGO.AddComponent<Draggable>();

            // **Add Vertical Layout Group and Content Size Fitter to the panel**
            VerticalLayoutGroup layoutGroup = draggablePanelGO.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.spacing = 0; // Space between header and content

            ContentSizeFitter contentSizeFitter = draggablePanelGO.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // **Create Header Text**
            GameObject headerGO = new GameObject("HeaderText");
            headerGO.transform.SetParent(draggablePanelGO.transform, false);

            HeaderText = headerGO.AddComponent<TextMeshProUGUI>();
            HeaderText.text = "Order Up!";
            HeaderText.alignment = TextAlignmentOptions.Center;
            HeaderText.fontSize = PreferencesManager.Get<float>("FontSize", 16) + 4f; // Adjust font size as needed
            HeaderText.color = Color.white; //new Color(1f, 0.9411765f, 0.9137255f, 1f);
            HeaderText.fontMaterial = new Material(HeaderText.fontMaterial);
            HeaderText.fontMaterial.SetColor("_UnderlayColor", new Color(1f, 0.9411765f, 0.9137255f, .3f));
            HeaderText.fontMaterial.SetFloat("_UnderlaySoftness", 1f);
            HeaderText.fontMaterial.SetFloat("_UnderlayDilate", -0.5f);   // Range: -1 to 1

            // Configure RectTransform for headerText
            RectTransform headerRect = HeaderText.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1);
            headerRect.anchorMax = new Vector2(0.5f, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 30); // Height of header
            headerRect.anchoredPosition = new Vector2(0, 0);

            // **Add Layout Element to HeaderText**
            LayoutElement headerLayoutElement = HeaderText.gameObject.AddComponent<LayoutElement>();
            headerLayoutElement.preferredWidth = -1; // Allow flexible width
            headerLayoutElement.minWidth = 100;      // Set a minimum width if needed

            // **Create Orders Text**
            GameObject textGO = new GameObject("OrdersText");
            textGO.transform.SetParent(draggablePanelGO.transform, false);

            OrdersText = textGO.AddComponent<TextMeshProUGUI>();
            OrdersText.fontMaterial = new Material(OrdersText.fontMaterial);
            OrdersText.alignment = TextAlignmentOptions.TopLeft;
            OrdersText.fontSize = PreferencesManager.Get<float>("FontSize", 16);
            OrdersText.color = new Color(1f, 0.9411765f, 0.9137255f, 1f); // #FFF0E9
            OrdersText.fontMaterial.DisableKeyword("UNDERLAY_ON");
            OrdersText.outlineColor = new Color32(0, 0, 0, 200);
            OrdersText.outlineWidth = 0.1f;
            OrdersText.enableWordWrapping = false; // Disable word wrapping
            OrdersText.margin = new Vector4(10, 10, 10, 10); // Padding inside the panel

            // Configure RectTransform for ordersText
            RectTransform ordersRect = OrdersText.GetComponent<RectTransform>();
            ordersRect.anchorMin = new Vector2(0, 1);
            ordersRect.anchorMax = new Vector2(1, 1);
            ordersRect.pivot = new Vector2(0.5f, 1);
            ordersRect.sizeDelta = new Vector2(0, -15); // Adjust for padding
            ordersRect.anchoredPosition = new Vector2(0, -15); // Position below header

            // **Add Layout Element to OrdersText for flexible width and height**
            LayoutElement ordersLayoutElement = OrdersText.gameObject.AddComponent<LayoutElement>();
            ordersLayoutElement.preferredWidth = -1; // Allow flexible width
            ordersLayoutElement.minWidth = 180;      // Minimum width for the orders text
            //ordersLayoutElement.minHeight = 100;     // Minimum height for the orders text

            // **Set Minimum Size for Draggable Panel**
            LayoutElement panelLayoutElement = draggablePanelGO.AddComponent<LayoutElement>();
            panelLayoutElement.minWidth = 180;  // Minimum width of the panel
            panelLayoutElement.minHeight = 120; // Minimum height of the panel

            if (currentFontSize == -1f)
            {
                // Disable UI visibility
                if (OrdersCanvas != null)
                {
                    OrdersCanvas.enabled = false;
                }
            }

            // Log initialization
            //Debug.Log("[OrderUp] New OrderUpCanvas and DraggablePanel with OrdersText initialized.");
        }

        private void EnsurePanelVisibility(RectTransform panelRect = null, RectTransform canvasRect = null)
        {
            if (panelRect == null)
            {
                GameObject panelGO = GameObject.Find("OrderUpCanvas/DraggablePanel");
                if (panelGO != null)
                {
                    panelRect = panelGO.GetComponent<RectTransform>();
                }
                else
                {
                    //Debug.LogError("[OrderUp] DraggablePanel not found for visibility check.");
                    return;
                }
            }

            if (canvasRect == null)
            {
                if (OrdersCanvas != null)
                {
                    canvasRect = OrdersCanvas.GetComponent<RectTransform>();
                }
                else
                {
                    //Debug.LogError("[OrderUp] Canvas RectTransform not found for visibility check.");
                    return;
                }
            }

            // Get the panel's size
            Vector2 panelSize = panelRect.sizeDelta;

            // Get the canvas size
            Vector2 canvasSize = canvasRect.sizeDelta;

            // Get the panel's anchored position
            Vector2 panelPos = panelRect.anchoredPosition;

            // Calculate panel boundaries based on top-left anchoring
            float left = panelPos.x;
            float right = panelPos.x + panelSize.x;
            float top = panelPos.y;
            float bottom = panelPos.y - panelSize.y;

            // Determine if the panel is within the canvas bounds
            bool isWithinX = left >= 0 && right <= canvasSize.x;
            bool isWithinY = top <= 0 && bottom >= -canvasSize.y;

            if (!isWithinX || !isWithinY)
            {
                // Calculate center position relative to top-left anchoring
                float centerX = (canvasSize.x - panelSize.x) / 2f;
                float centerY = (-canvasSize.y + panelSize.y) / 2f;

                // Reset panel position to center
                panelRect.anchoredPosition = new Vector2(centerX, centerY);

                // Update preferences to save the new position
                PreferencesManager.Set<float>("PosX", centerX);
                PreferencesManager.Set<float>("PosY", centerY);
            }
            else
            {
                // Panel is within bounds
                // Debug.Log("[OrderUp] DraggablePanel is within screen bounds.");
            }
        }
    }
}
