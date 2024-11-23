using HarmonyLib;
using Kitchen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.VFX;

namespace SkripOrderUp
{
    internal class OrderManager : MonoBehaviour
    {
        // Define OrderItem class to encapsulate Colour Blind GameObject details
        public class OrderItem
        {
            public GameObject ColourBlindObject { get; set; }
            public string DisplayName { get; set; }
            public string ColourblindText { get; set; }
            public string SideItem { get; set; }
        }

        // Define OrderGroup class
        public class OrderGroup
        {
            public float StartTime { get; set; }
            public List<OrderItem> Items { get; set; } = new List<OrderItem>();
            public int OrderNumber { get; set; }
        }

        // List to track OrderGroups
        public List<OrderGroup> orderGroups = new List<OrderGroup>();

        // HashSet to track active OrderItem instances based on their GameObjects
        private HashSet<GameObject> itemsInThought = new HashSet<GameObject>();

        // Singleton instance
        public static OrderManager Instance { get; private set; }

        // Event to notify listeners when orders are updated
        public event Action<List<OrderGroup>> OnOrdersUpdated;

        private int orderNumber = 0;

        // Timer variables
        private float updateInterval = 0.25f; // Interval in seconds
        private float timer = 0f;

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize Harmony
            Harmony harmonyInstance = new Harmony("Skrip.Plateup.OrderUp");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            //Debug.Log("[OrderUp] Harmony patches applied.");
        }

        public void Update()
        {
            try
            {
                // Increment the timer by the time elapsed since the last frame
                timer += Time.deltaTime;

                // Check if the timer has exceeded the update interval
                if (timer >= updateInterval)
                {
                    // Reset the timer, subtracting the interval to handle any excess time
                    timer -= updateInterval;

                    // Execute the expensive update logic
                    ExecuteUpdateLogic();
                }

                // Optional: Reset orderNumber if in Franchise scene
                if (GameInfo.CurrentScene == SceneType.Franchise && orderNumber > 0)
                {
                    orderNumber = 0;
                }
            }
            catch (Exception)
            {
                CleanUpOrderGroups();
                //Debug.LogError($"[OrderUp] Exception in Update: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ExecuteUpdateLogic()
        {
            try
            {
                if (GameInfo.CurrentScene == SceneType.Franchise && orderNumber > 0)
                {
                    orderNumber = 0;
                }

                // Find all active GameObjects named "Colour Blind" in the scene
                List<GameObject> currentColourBlindObjects = FindObjectsOfType<Transform>()
                    .Where(t => t.name == "Colour Blind" && t.gameObject.activeInHierarchy)
                    .Select(t => t.gameObject)
                    .ToList();

                List<OrderItem> validItems = new List<OrderItem>();

                foreach (GameObject colourBlindGO in currentColourBlindObjects)
                {
                    if (colourBlindGO == null || !colourBlindGO.activeInHierarchy)
                        continue;

                    Transform parentTransform = colourBlindGO.transform.parent;
                    if (parentTransform == null)
                        continue;

                    Transform grandParentTransform = parentTransform.parent;
                    if (grandParentTransform == null)
                        continue;

                    Transform greatGrandParentTransform = grandParentTransform.parent;

                    Transform managerTransform = grandParentTransform.Find("Manager");
                    if (managerTransform == null)
                    {
                        // Attempt to find "Manager" in great-grandparent if not found in grandparent
                        if (greatGrandParentTransform == null)
                            continue;

                        managerTransform = greatGrandParentTransform.Find("Manager");
                        if (managerTransform == null)
                            continue;
                    }

                    Transform thoughtCloudTransform = managerTransform.Find("Thought Cloud");
                    if (thoughtCloudTransform == null)
                        continue;

                    VisualEffect visualEffect = thoughtCloudTransform.gameObject.GetComponent<VisualEffect>();
                    if (visualEffect != null && !visualEffect.enabled)
                        continue;

                    // Retrieve the TextMeshPro component from "Colour Blind" -> "Element" or "Title"
                    Transform elementTransform = colourBlindGO.transform.Find("Element") ?? colourBlindGO.transform.Find("Title");
                    if (elementTransform == null)
                    {
                        Debug.LogWarning($"[OrderUp] {colourBlindGO.name} does not have an 'Element' or 'Title' child.");
                        continue;
                    }

                    TextMeshPro textMeshPro = elementTransform.GetComponent<TextMeshPro>();
                    if (textMeshPro == null)
                    {
                        Debug.LogWarning($"[OrderUp] 'Element' or 'Title' under {colourBlindGO.name} does not have a TextMeshPro component.");
                        continue;
                    }

                    // Search within the children of the greatgrantparent for a gameobject name "Side Highlight Disc" that is also enabled.
                    Transform sideTransform = null;
                    Transform sideHighlightDiscTransform = null;
                    try
                    {
                        sideHighlightDiscTransform = greatGrandParentTransform.parent.GetChild(greatGrandParentTransform.GetSiblingIndex() + 1)?.GetChild(1)?.GetChild(0);//.GetChild(2);
                        if (sideHighlightDiscTransform != null)
                        {
                            if (sideHighlightDiscTransform.gameObject.activeInHierarchy)
                            {
                                sideTransform = sideHighlightDiscTransform.parent.GetChild(2);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //
                    }

                    string colourblindText = textMeshPro.text;

                    // Clean the display name (using parent name as per user's latest code)
                    string displayName = CleanDisplayName(colourBlindGO.transform.parent.name);
                    string sideName = sideTransform != null ? CleanDisplayName(sideTransform.name) : string.Empty;
                    string toppingText = ProcessColourblindText(colourblindText, displayName);

                    // Create an OrderItem
                    OrderItem orderItem = new OrderItem
                    {
                        ColourBlindObject = colourBlindGO,
                        DisplayName = displayName,
                        ColourblindText = toppingText,
                        SideItem = sideName
                    };

                    validItems.Add(orderItem);
                }

                //if(validItems.Count > 0)
                //{
                //    Debug.Log($"========================================================");

                //    foreach (var item in validItems)
                //    {
                //        Debug.Log($"Item: {item.DisplayName}, Side: {item.SideItem}, Toppings: {item.ColourblindText}");
                //    }
                //}

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
                        StartTime = Time.time,
                        Items = newItems,
                        OrderNumber = ++orderNumber
                    };
                    orderGroups.Add(newOrderGroup);
                    //Debug.Log($"[OrderUp] Detected new order group with {newItems.Count} items.");

                    // Notify listeners about the update
                    OnOrdersUpdated?.Invoke(orderGroups);
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
                                //Debug.Log($"[OrderUp] Removed Colour Blind object: {removedGO.name} from an order group.");

                                // If the orderGroup is now empty, remove it
                                if (orderGroup.Items.Count == 0)
                                {
                                    orderGroups.Remove(orderGroup);
                                    //Debug.Log($"[OrderUp] Removed empty OrderGroup.");
                                }

                                // Notify listeners about the update
                                OnOrdersUpdated?.Invoke(orderGroups);

                                break; // Assuming a Colour Blind object can only be in one OrderGroup
                            }
                        }
                    }
                }

                // Update the itemsInThought HashSet
                itemsInThought = currentValidGameObjects;

                // Clean up any null OrderItems in OrderGroups
                CleanUpOrderGroups();

                // Optionally, notify listeners if needed based on other criteria
            }
            catch (Exception ex)
            {
                CleanUpOrderGroups();
                Debug.LogError($"[OrderUp] Exception in ExecuteUpdateLogic: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CleanUpOrderGroups()
        {
            bool cleaned = false;
            foreach (var orderGroup in orderGroups.ToList())
            {
                // Remove any OrderItems with null ColourBlindObject
                int initialCount = orderGroup.Items.Count;
                orderGroup.Items.RemoveAll(item => item.ColourBlindObject == null);
                if (orderGroup.Items.Count != initialCount)
                {
                    cleaned = true;
                    //Debug.Log("[OrderUp] Cleaned up null OrderItems from an OrderGroup.");
                }

                // If the order group is empty, remove it
                if (orderGroup.Items.Count == 0)
                {
                    orderGroups.Remove(orderGroup);
                    cleaned = true;
                    //Debug.Log("[OrderUp] Removed empty OrderGroup during cleanup.");
                }
            }

            if (cleaned)
            {
                // Notify listeners about the update
                OnOrdersUpdated?.Invoke(orderGroups);
            }
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

            // If displayName contains "Pie", strip the text before Pie
            if (displayName.Contains("Pie"))
            {
                displayName = "Pie";
            } else if (displayName.Contains("Cake"))
            {
                displayName = "Cake";
            }

            return displayName;
        }

        private string ProcessColourblindText(string toppings, string main)
        {
            if (string.IsNullOrEmpty(toppings))
                return string.Empty;

            // If the whole string is uppercase, sort each character alphabetically
            if (toppings.All(char.IsUpper))
            {
                toppings = string.Concat(toppings.OrderBy(c => c));
            }

            if (!string.IsNullOrEmpty(main))
            {
                if(main.Equals("Steak"))
                {
                    toppings = toppings.Replace("St-", string.Empty);
                }
            }

            toppings = toppings.Replace("\n", " ");
            toppings = toppings.Trim();
            if (!string.IsNullOrEmpty(main))
            {
                if (main.Equals("Cake"))
                {
                    toppings = $" -{toppings}";
                } else
                {
                    toppings = $" - {toppings}";
                }
            }
            
            return toppings;
        }
    }
}
