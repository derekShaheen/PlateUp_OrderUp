using HarmonyLib;
using Kitchen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems; // Required for EventSystem
using UnityEngine.UI;
using UnityEngine.VFX;

namespace SkripOrderUp
{
    internal class MainMono : MonoBehaviour
    {
        // Define OrderItem class to encapsulate Colour Blind GameObject details
        private class OrderItem
        {
            public GameObject ColourBlindObject { get; set; }
            public string DisplayName { get; set; }
            public string ColourblindText { get; set; }
        }

        // Define OrderGroup class
        private class OrderGroup
        {
            public float StartTime { get; set; }
            public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        }

        // List to track OrderGroups
        private List<OrderGroup> orderGroups = new List<OrderGroup>();

        // HashSet to track active OrderItem instances based on their GameObjects
        private HashSet<GameObject> itemsInThought = new HashSet<GameObject>();

        // References to UI components
        private Canvas ordersCanvas;
        private TextMeshProUGUI ordersText;

        // Singleton instance
        public static MainMono Instance { get; private set; }

        // Shared time variables
        private float totalTime = 0f;
        private int lastDisplayedTime = -1; // Initialize to -1 to ensure the first update occurs

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[OrderUp] Multiple instances of MainMono detected. Destroying duplicate.");
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize Harmony
            Harmony harmonyInstance = new Harmony("Skrip.Plateup.OrderUp");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            Debug.Log("[OrderUp] Harmony patches applied.");
            InitializeUI();
        }

        public void Update()
        {
            // Update totalTime
            totalTime += Time.deltaTime;

            try
            {
                // Find all active GameObjects named "Colour Blind" in the scene
                List<GameObject> currentColourBlindObjects = FindObjectsOfType<Transform>()
                    .Where(t => t.name == "Colour Blind")
                    .Select(t => t.gameObject)
                    .ToList();

                List<OrderItem> validItems = new List<OrderItem>();

                foreach (GameObject colourBlindGO in currentColourBlindObjects)
                {
                    if (colourBlindGO == null || !colourBlindGO.activeInHierarchy)
                        continue;

                    Transform parentTransform = colourBlindGO.transform.parent;
                    if (parentTransform == null)
                    {
                        // Debug.Log($"[OrderUp] {colourBlindGO.name} has no parent transform.");
                        continue;
                    }

                    Transform grandParentTransform = parentTransform.parent;
                    if (grandParentTransform == null)
                    {
                        // Debug.Log($"[OrderUp] {colourBlindGO.name} has no grandparent transform.");
                        continue;
                    }

                    Transform greatGrandParentTransform = grandParentTransform.parent;
                    if (greatGrandParentTransform == null)
                    {
                        // Debug.Log($"[OrderUp] {colourBlindGO.name} has no grandparent transform.");
                        continue;
                    }

                    Transform managerTransform;
                    managerTransform = grandParentTransform.Find("Manager");
                    if (managerTransform == null)
                    {
                        managerTransform = greatGrandParentTransform.Find("Manager");
                        if (managerTransform == null)
                        {
                            // Debug.Log($"[OrderUp] {colourBlindGO.name} does not have a 'Manager' parent.");
                            continue;
                        }
                    }

                    Transform thoughtCloudTransform = managerTransform.Find("Thought Cloud");
                    if (thoughtCloudTransform == null)
                        continue;

                    VisualEffect visualEffect = thoughtCloudTransform.gameObject.GetComponent<VisualEffect>();
                    if (visualEffect != null && !visualEffect.enabled)
                        continue;

                    // Retrieve the TextMeshPro component from "Colour Blind" -> "Element"
                    Transform elementTransform = colourBlindGO.transform.Find("Title");
                    if (elementTransform == null)
                    {
                        Debug.Log($"[OrderUp] {colourBlindGO.name} does not have an 'Element' child.");
                        continue;
                    }

                    TextMeshPro textMeshPro = elementTransform.GetComponent<TextMeshPro>();
                    if (textMeshPro == null)
                    {
                        // Debug.Log($"[OrderUp] 'Element' under {colourBlindGO.name} does not have a TextMeshPro component.");
                        continue;
                    }

                    string colourblindText = textMeshPro.text;

                    // Clean the display name
                    string displayName = CleanDisplayName(colourBlindGO.transform.parent.name);

                    // Create an OrderItem
                    OrderItem orderItem = new OrderItem
                    {
                        ColourBlindObject = colourBlindGO,
                        DisplayName = displayName,
                        ColourblindText = ProcessColourblindText(colourblindText)
                    };

                    validItems.Add(orderItem);
                }

                // Extract GameObjects from validItems for tracking
                HashSet<GameObject> currentValidGameObjects = new HashSet<GameObject>(validItems.Select(oi => oi.ColourBlindObject));

                // Detect new Colour Blind GameObjects
                List<OrderItem> newItems = validItems
                    .Where(oi => !itemsInThought.Contains(oi.ColourBlindObject))
                    .ToList();

                if (newItems.Count > 0)
                {
                    // Create a new OrderGroup for new items
                    OrderGroup newOrderGroup = new OrderGroup
                    {
                        StartTime = totalTime,
                        Items = newItems
                    };
                    orderGroups.Add(newOrderGroup);
                    Debug.Log($"[OrderUp] Detected new order group with {newItems.Count} items.");
                }

                // Detect removed Colour Blind GameObjects
                List<GameObject> removedGameObjects = itemsInThought.Except(currentValidGameObjects).ToList();

                if (removedGameObjects.Count > 0)
                {
                    foreach (var removedGO in removedGameObjects)
                    {
                        // Find and remove the corresponding OrderItem from OrderGroups
                        foreach (var orderGroup in orderGroups.ToList())
                        {
                            OrderItem itemToRemove = orderGroup.Items.FirstOrDefault(oi => oi.ColourBlindObject == removedGO);
                            if (itemToRemove != null)
                            {
                                orderGroup.Items.Remove(itemToRemove);
                                Debug.Log($"[OrderUp] Removed Colour Blind object: {removedGO.name} from an order group.");

                                // If the orderGroup is now empty, remove it
                                if (orderGroup.Items.Count == 0)
                                {
                                    orderGroups.Remove(orderGroup);
                                    Debug.Log($"[OrderUp] Removed empty OrderGroup.");
                                }
                                break; // Assuming a Colour Blind object can only be in one OrderGroup
                            }
                        }
                    }
                }

                // Update the itemsInThought HashSet
                itemsInThought = currentValidGameObjects;

                // Clean up any null OrderItems in OrderGroups
                CleanUpOrderGroups();

                // Update the order text display when the integer part of totalTime changes
                int currentTimeInt = (int)totalTime;
                if (currentTimeInt != lastDisplayedTime)
                {
                    UpdateOrderText();
                    lastDisplayedTime = currentTimeInt;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OrderUp] Error processing Colour Blind objects: {ex.Message}");
            }
        }

        private void CleanUpOrderGroups()
        {
            foreach (var orderGroup in orderGroups.ToList())
            {
                // Remove any OrderItems with null ColourBlindObject
                orderGroup.Items.RemoveAll(item => item.ColourBlindObject == null);

                // If the order group is empty, remove it
                if (orderGroup.Items.Count == 0)
                {
                    orderGroups.Remove(orderGroup);
                    Debug.Log("[OrderUp] Removed empty OrderGroup during cleanup.");
                }
            }
        }

        private void UpdateOrderText()
        {
            if (orderGroups == null || orderGroups.Count == 0)
            {
                ordersText.text = string.Empty;
                return;
            }

            StringBuilder allOrders = new StringBuilder();

            // Iterate over OrderGroups in the order they were added
            foreach (var orderGroup in orderGroups.OrderBy(og => og.StartTime))
            {
                // Calculate time elapsed
                int secondsElapsed = (int)(totalTime - orderGroup.StartTime);
                string timeElapsedString = $"{secondsElapsed}s";

                // Group items within each OrderGroup by DisplayName and ColourblindText
                var groupedItems = orderGroup.Items
                    .Where(oi => oi != null) // Ensure OrderItem is not null
                    .OrderBy(oi => oi.DisplayName)
                    .GroupBy(oi => new { oi.DisplayName, oi.ColourblindText })
                    .Select(g => new
                    {
                        DisplayName = g.Key.DisplayName,
                        ColourblindText = g.Key.ColourblindText,
                        Count = g.Count()
                    })
                    .ToList();

                // Skip this OrderGroup if there are no valid items
                if (groupedItems.Count == 0)
                    continue;

                // Build the order lines for this OrderGroup
                StringBuilder orderGroupLines = new StringBuilder();
                foreach (var item in groupedItems.OrderByDescending(comparer => comparer.Count)
                                                 .ThenByDescending(comparer => comparer.DisplayName))
                {
                    string orderLine = item.Count > 1
                        ? $"{item.Count}x{item.DisplayName}{item.ColourblindText}"
                        : $"{item.DisplayName}{item.ColourblindText}";

                    orderGroupLines.Append(orderLine + ", ");
                }

                // Remove the trailing comma and space
                if (orderGroupLines.Length > 2)
                    orderGroupLines.Length -= 2;

                // Append the time elapsed to the order group
                allOrders.AppendLine($"► {orderGroupLines.ToString()}  {timeElapsedString}");
            }

            ordersText.text = allOrders.ToString();
        }

        private string CleanDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            string displayName = name.Replace("Plated", string.Empty)
                                     .Replace("(Clone)", string.Empty)
                                     .Replace("-", string.Empty)
                                     .Replace("Flavour Icon", "Cake")
                                     .Replace("Cooked", string.Empty)
                                     .Trim();
            //<sprite name="cake"> Choc
            return displayName;
        }

        private string ProcessColourblindText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // If the whole string is uppercase, sort each character alphabetically
            if (text.All(char.IsUpper))
            {
                text = string.Concat(text.OrderBy(c => c));
            }

            text = text.Replace("\n", " ");
            text = text.Trim();
            text = $"-{text}";
            return text;
        }

        private void InitializeUI()
        {
            // Check if the Canvas already exists to prevent duplicates
            if (GameObject.Find("OrderUpCanvas") != null)
            {
                ordersCanvas = GameObject.Find("OrderUpCanvas").GetComponent<Canvas>();
                ordersText = ordersCanvas.GetComponentInChildren<TextMeshProUGUI>();
                Debug.Log("[OrderUp] Existing OrderUpCanvas found and initialized.");
                return;
            }

            // Create a new Canvas GameObject
            GameObject canvasGO = new GameObject("OrderUpCanvas");
            ordersCanvas = canvasGO.AddComponent<Canvas>();
            ordersCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

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
            panelRect.sizeDelta = new Vector2(600, 180); // Initial size

            // Log initial position
            Debug.Log($"[OrderUp] Initial panel position: {panelRect.anchoredPosition}");

            // Add Image component for the background
            Image backgroundImage = draggablePanelGO.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black

            // Add the Draggable component
            draggablePanelGO.AddComponent<Draggable>();

            // **Add Vertical Layout Group and Content Size Fitter to the panel**
            VerticalLayoutGroup layoutGroup = draggablePanelGO.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.spacing = 5; // Space between header and content

            ContentSizeFitter contentSizeFitter = draggablePanelGO.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // **Create Header Text**
            GameObject headerGO = new GameObject("HeaderText");
            headerGO.transform.SetParent(draggablePanelGO.transform, false);

            TextMeshProUGUI headerText = headerGO.AddComponent<TextMeshProUGUI>();
            headerText.text = "Order Up!";
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.fontSize = 16; // Adjust font size as needed
            headerText.color = new Color(1f, 1f, 1f, 0.7f); // Slightly faded white

            // Configure RectTransform for headerText
            RectTransform headerRect = headerText.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1);
            headerRect.anchorMax = new Vector2(0.5f, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 30); // Height of header
            headerRect.anchoredPosition = new Vector2(0, 0);

            // **Add Layout Element to HeaderText**
            LayoutElement headerLayoutElement = headerText.gameObject.AddComponent<LayoutElement>();
            headerLayoutElement.preferredWidth = -1; // Allow flexible width
            headerLayoutElement.minWidth = 100;      // Set a minimum width if needed

            // **Create Orders Text**
            GameObject textGO = new GameObject("OrdersText");
            textGO.transform.SetParent(draggablePanelGO.transform, false);

            ordersText = textGO.AddComponent<TextMeshProUGUI>();
            ordersText.alignment = TextAlignmentOptions.TopLeft;
            ordersText.fontSize = 14;
            ordersText.color = Color.white;
            ordersText.enableWordWrapping = false; // Disable word wrapping
            ordersText.margin = new Vector4(10, 10, 10, 10); // Padding inside the panel

            // Configure RectTransform for ordersText
            RectTransform ordersRect = ordersText.GetComponent<RectTransform>();
            ordersRect.anchorMin = new Vector2(0, 1);
            ordersRect.anchorMax = new Vector2(1, 1);
            ordersRect.pivot = new Vector2(0.5f, 1);
            ordersRect.sizeDelta = new Vector2(0, -60); // Adjust for padding
            ordersRect.anchoredPosition = new Vector2(0, -30); // Position below header

            // **Add Layout Element to OrdersText for flexible width and height**
            LayoutElement ordersLayoutElement = ordersText.gameObject.AddComponent<LayoutElement>();
            ordersLayoutElement.preferredWidth = -1; // Allow flexible width
            ordersLayoutElement.minWidth = 300;      // Minimum width for the orders text
            ordersLayoutElement.minHeight = 100;     // Minimum height for the orders text

            // **Set Minimum Size for Draggable Panel**
            LayoutElement panelLayoutElement = draggablePanelGO.AddComponent<LayoutElement>();
            panelLayoutElement.minWidth = 300;  // Minimum width of the panel
            panelLayoutElement.minHeight = 180; // Minimum height of the panel

            // Log initialization
            Debug.Log("[OrderUp] New OrderUpCanvas and DraggablePanel with OrdersText initialized.");
        }

        internal static string FormatTime(float totalSeconds)
        {
            if (totalSeconds > 59f)
            {
                int minutes = Mathf.FloorToInt(totalSeconds / 60f);
                int seconds = Mathf.FloorToInt(totalSeconds % 60f);
                return string.Format("{0}m {1}s", minutes, seconds);
            }
            else
            {
                int seconds = Mathf.CeilToInt(totalSeconds);
                return string.Format("{0}s", seconds);
            }
        }
    }
}
