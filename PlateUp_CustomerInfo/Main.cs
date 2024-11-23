using System;
using System.Collections.Generic;
using System.Reflection;
using Kitchen;
using KitchenData;
using KitchenMods;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace SkripOrderUp
{
    public struct COrderLogged : IComponentData { }

    public class Main : GenericSystemBase, IModSystem
    {
        public const string MOD_GUID = "Skrip.PlateUp.OrderUp";
        public const string MOD_NAME = "CustomerInfo";

        private EntityQuery Orders;



        protected override void Initialise()
        {
            base.Initialise();
            Orders = GetEntityQuery(
                ComponentType.ReadOnly<CInterfaceOf>(),
                ComponentType.ReadOnly<CDisplayedItem>(),
                ComponentType.Exclude<COrderLogged>() // Initially exclude processed orders
            );

            // Optionally, initialize Harmony patches if needed
            //Harmony harmony = new Harmony("Skrip.Plateup.OrderUp");
            //harmony.PatchAll(Assembly.GetExecutingAssembly());

            GameObject orderTrackerGO = new GameObject("OrderUp");
            orderTrackerGO.AddComponent<OrderManager>();
            orderTrackerGO.AddComponent<OrderView>();
        }

        protected override void OnUpdate()
        {
            //// Fetch new orders (entities without COrderLogged)
            //var newOrders = Orders.ToEntityArray(Allocator.TempJob);

            //foreach (var entity in newOrders)
            //{
            //    // Get the DynamicBuffer<CDisplayedItem> attached to the entity
            //    var displayedItems = EntityManager.GetBuffer<CDisplayedItem>(entity);

            //    // Initialize a string to accumulate order details
            //    string orderDetails = "New Order:\n";

            //    foreach (var item in displayedItems)
            //    {
            //        // Access item properties
            //        bool isComplete = item.IsComplete;
            //        Vector3 seatPosition = item.SeatPosition;
            //        Vector3 tablePosition = item.TablePosition;
            //        Entity itemEntity = item.Item;
            //        int itemId = item.ItemID;
            //        bool isSide = item.IsSide;
            //        bool showExtra = item.ShowExtra;
            //        int extraId = item.ExtraID;
            //        bool isSatisfiedBySharer = item.IsSatisfiedBySharer;

            //        // Fetch item data from GameData
            //        GameData.Main.TryGet<Item>(itemId, out Item gameItem, false);
            //        string itemName = gameItem != null ? gameItem.Prefab.name : $"ItemID {itemId}";

            //        // Access the ColourblindLabel via reflection
            //        TextMeshPro colourblindLabel = GetColourblindLabel(gameItem?.Prefab);
            //        string colourblindText = colourblindLabel != null ? colourblindLabel.text : "N/A";

            //        // Accumulate order information with ColourblindLabel
            //        orderDetails += $"> {itemName}\n";
            //        orderDetails += $"- Colourblind Label: {colourblindText}\n";
            //    }

            //    // Add the new order to the activeOrders dictionary
            //    activeOrders.Add(entity, orderDetails);

            //    // Update the TextMeshProUGUI text
            //    UpdateOrderText();

            //    // Mark the entity as processed by adding COrderLogged
            //    EntityManager.AddComponent<COrderLogged>(entity);
            //}

            //newOrders.Dispose();

            //// Now, check for completed orders
            //CheckForCompletedOrders();
        }

        //private void CheckForCompletedOrders()
        //{
        //    // Create a query for entities that have been processed (COrderLogged)
        //    var completedOrderQuery = GetEntityQuery(
        //        ComponentType.ReadOnly<CInterfaceOf>(),
        //        ComponentType.ReadOnly<CDisplayedItem>(),
        //        ComponentType.ReadOnly<COrderLogged>()
        //    );

        //    var completedEntities = completedOrderQuery.ToEntityArray(Allocator.TempJob);

        //    foreach (var entity in completedEntities)
        //    {
        //        var displayedItems = EntityManager.GetBuffer<CDisplayedItem>(entity);

        //        // Check if all items in the order are complete
        //        bool allComplete = true;
        //        foreach (var item in displayedItems)
        //        {
        //            if (!item.IsComplete)
        //            {
        //                allComplete = false;
        //                break;
        //            }
        //        }

        //        if (allComplete)
        //        {
        //            // Remove the order from activeOrders and update the UI
        //            if (activeOrders.ContainsKey(entity))
        //            {
        //                activeOrders.Remove(entity);
        //                UpdateOrderText();
        //            }

        //            // Remove the COrderLogged component to allow future processing
        //            EntityManager.RemoveComponent<COrderLogged>(entity);
        //        }
        //    }

        //    completedEntities.Dispose();
        //}

       
    }
}
