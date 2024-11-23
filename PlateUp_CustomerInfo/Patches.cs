using HarmonyLib;
using Kitchen;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SkripOrderUp.Patches
{
    [HarmonyPatch(typeof(OptionsMenu<MenuAction>), "Setup")]
    public static class OptionsMenuSetupPatch
    {
        // Decorate the method with HarmonyPrefix and set the highest priority
        [HarmonyPrefix]
        private static void Prefix(OptionsMenu<MenuAction> __instance, int player_id)
        {
            try
            {
                // Define the font size options
                List<float> fontSizes = new List<float> { -1f, 14f, 18f, 22f, 26f, 30f };
                List<string> fontSizeNames = new List<string>
                {
                    "Disable UI",
                    "Small",
                    "Medium",
                    "Large",
                    "Extra Large",
                    "XXL"
                };

                // Get the Menu<T> type from the instance
                Type menuType = __instance.GetType().BaseType;

                if (menuType == null)
                {
                    Debug.LogError("[OrderUp] Could not determine the base Menu<T> type.");
                    return;
                }

                // Get MethodInfo for AddLabel and AddSelect
                MethodInfo addLabelMethod = AccessTools.Method(menuType, "AddLabel", new Type[] { typeof(string) });
                MethodInfo addSelectMethod = AccessTools.Method(menuType, "AddSelect", new Type[] { typeof(List<string>), typeof(Action<int>), typeof(int) });

                if (addLabelMethod == null || addSelectMethod == null)
                {
                    Debug.LogError("[OrderUp] Could not find AddLabel or AddSelect methods via reflection.");
                    return;
                }

                // Create a label for the font size selector
                string labelText = "Order Up! UI Size";
                addLabelMethod.Invoke(__instance, new object[] { labelText });

                // Retrieve current font size from preferences
                float currentFontSize = PreferencesManager.Get<float>("FontSize", 18f);

                // Determine the selected index based on current font size
                int selectedIndex = fontSizes.IndexOf(currentFontSize);
                if (selectedIndex == -1)
                {
                    // If current font size isn't in the list, default to 18
                    selectedIndex = 0;
                    PreferencesManager.Set<float>("FontSize", 18f);
                    currentFontSize = 18f; // Update the current font size
                }

                // Define the callback action when selection changes
                Action<int> onFontSizeChanged = (index) =>
                {
                    float selectedSize = fontSizes[index];
                    PreferencesManager.Set<float>("FontSize", selectedSize);
                    ApplyFontSize(selectedSize);
                };

                // Invoke AddSelect<float> to add the font size selector
                addSelectMethod.Invoke(__instance, new object[] { fontSizeNames, onFontSizeChanged, selectedIndex });

                // Apply the initial font size based on the current preference
                //ApplyFontSize(currentFontSize);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OrderUp] Exception in OptionsMenuSetupPatch.Prefix: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Applies the selected font size or disables the UI based on the selection.
        /// </summary>
        /// <param name="size">Selected font size, or -1 to disable UI.</param>
        private static void ApplyFontSize(float size)
        {
            var orderView = OrderManager.Instance?.GetComponent<OrderView>();
            if (orderView == null)
            {
                Debug.LogWarning("[OrderUp] OrderView component not found on OrderManager.");
                return;
            }
            else
            {
                // Enable UI visibility
                if (orderView.OrdersCanvas != null)
                {
                    orderView.OrdersCanvas.enabled = true;
                }
                else
                {
                    Debug.LogWarning("[OrderUp] ordersCanvas not found in OrderView.");
                }

                // Set the font size
                orderView.currentFontSize = size;
            }
        }
    }
}
