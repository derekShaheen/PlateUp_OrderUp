using System;
using System.Collections.Generic;
using Kitchen;
using KitchenData;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SkripOrderUp
{
    public struct COrderLogged : IComponentData { }

    public class OrderItemDto
    {
        public int ItemID;
        public string ItemName;
        public bool IsSide;
        public int ExtraID;
        public string ExtraName;
        public bool IsComplete;
        public Vector3 SeatPosition;
        public Vector3 TablePosition;
    }

    public sealed class OrderGroupDto
    {
        public Entity Group;
        public List<OrderItemDto> Items = new List<OrderItemDto>();
    }

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
                ComponentType.ReadOnly<CDisplayedItem>());

            var go = new GameObject("OrderUp");
            go.AddComponent<OrderManager>();
            go.AddComponent<OrderView>();
        }

        protected override void OnUpdate()
        {
            var inst = OrderManager.Instance;

            if (Orders.IsEmpty)
            {
                if (inst != null)
                    inst.UpdateFromEcs(new List<OrderGroupDto>()); // forces clear when no orders exist
                return;
            }

            var entities = Orders.ToEntityArray(Allocator.Temp);
            try
            {
                var dtoList = new List<OrderGroupDto>();

                for (int gi = 0; gi < entities.Length; gi++)
                {
                    var e = entities[gi];

                    if (!EntityManager.HasComponent<CDisplayedItem>(e))
                        continue;

                    var buf = EntityManager.GetBuffer<CDisplayedItem>(e);
                    if (buf.Length == 0)
                        continue;

                    var groupDto = new OrderGroupDto { Group = e };

                    for (int i = 0; i < buf.Length; i++)
                    {
                        var d = buf[i];

                        var name = ResolveItemName(d.ItemID);
                        var extraName = d.ExtraID != 0 ? ResolveItemName(d.ExtraID) : string.Empty;

                        if (name.Equals(extraName))
                            extraName = string.Empty;

                        groupDto.Items.Add(new OrderItemDto
                        {
                            ItemID = d.ItemID,
                            ItemName = name,
                            IsSide = d.IsSide,
                            ExtraID = d.ExtraID,
                            ExtraName = extraName,
                            IsComplete = d.IsComplete,
                            SeatPosition = d.SeatPosition,
                            TablePosition = d.TablePosition
                        });
                    }

                    dtoList.Add(groupDto);
                }

                if (inst != null)
                    inst.UpdateFromEcs(dtoList);
            }
            finally
            {
                entities.Dispose();
            }
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId == 0)
                return "Item#0";

            Item item;
            if (GameData.Main != null && GameData.Main.TryGet<Item>(itemId, out item, true) && item != null)
            {
                // For each list Item in item.NeedsIngredients, append their names
                var name = string.Empty;
                if(item.NeedsIngredients != null && item.NeedsIngredients.Count > 0)
                {
                    List<string> ingredientNames = new List<string>();
                    foreach (var ingredient in item.NeedsIngredients)
                    {
                        Item ingredientItem;
                        if (GameData.Main.TryGet<Item>(ingredient.ID, out ingredientItem, true) && ingredientItem != null)
                        {
                            ingredientNames.Add(CleanDisplayName(ingredientItem.name));
                        }
                    }
                    if (ingredientNames.Count > 0)
                    {
                        name = string.Join("$ ", ingredientNames);
                    }
                }
                if (item.name != null)
                    return CleanDisplayName(item.name + name);
                if (item.Prefab != null)
                    return CleanDisplayName(item.Prefab.name);
            }

            return "Item#" + itemId;
        }

        private static string CleanDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            string displayName = name.Replace("Plated", string.Empty)
                                     .Replace("(Clone)", string.Empty)
                                     .Replace("-", string.Empty)
                                     .Replace("Flavour Icon", "Cake")
                                     .Replace("Cooked", string.Empty)
                                     .Replace("Condiment", string.Empty)
                                     .Replace("Coffee Cup", string.Empty)
                                     .Trim();

            // If displayName contains "Pie", strip the text before Pie
            if (displayName.Contains("Pie"))
            {
                displayName = "Pie";
            }
            else if (displayName.Contains("Cake"))
            {
                displayName = "Cake";
            }

            return displayName;
        }
    }
}
