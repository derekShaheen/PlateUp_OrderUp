using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenMods;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        public string IngredientsText;
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
            go.AddComponent<SceneWatcher>();

            Harmony harmonyInstance = new Harmony("Skrip.Plateup.OrderUp");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }

        protected override void OnUpdate()
        {
            var inst = OrderManager.Instance;

            if (Orders.IsEmpty)
            {
                if (inst != null)
                    inst.UpdateFromEcs(new List<OrderGroupDto>());
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
                            TablePosition = d.TablePosition,
                            IngredientsText = ResolveChosenIngredientsText(d.Item, d.ItemID)
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

        private string ResolveChosenIngredientsText(Entity itemEntity, int primaryItemId)
        {
            if (itemEntity == default(Entity))
                return string.Empty;
            if (!EntityManager.Exists(itemEntity))
                return string.Empty;
            if (!EntityManager.HasComponent<CItem>(itemEntity))
                return string.Empty;

            var citem = EntityManager.GetComponentData<CItem>(itemEntity);
            var comps = citem.Items;
            if (comps.Count <= 1)
                return string.Empty;

            var primaryName = ResolveItemName(primaryItemId);

            var names = new List<string>();
            var seen = new HashSet<int>();

            for (int idx = 0; idx < comps.Count; idx++)
            {
                int compId = comps[idx];
                if (compId == 0 || compId == primaryItemId || seen.Contains(compId))
                    continue;

                Item comp;
                if (!GameData.Main.TryGet<Item>(compId, out comp, true) || comp == null)
                    continue;

                if (comp.IsMergeableSide)
                    continue;

                string n = comp.name != null ? comp.name :
                           (comp.Prefab != null ? comp.Prefab.name : null);

                n = CleanDisplayName(n, true);
                if (string.IsNullOrEmpty(n))
                    continue;

                if (string.Equals(n, primaryName, StringComparison.OrdinalIgnoreCase))
                    continue;

                names.Add(n);
                seen.Add(compId);
            }

            if (names.Count == 0)
                return string.Empty;

            names.Sort(StringComparer.OrdinalIgnoreCase);

            return " (" + string.Join(", ", names.ToArray()) + ")";
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId == 0)
                return "Item#0";

            Item item;
            if (GameData.Main != null && GameData.Main.TryGet<Item>(itemId, out item, true) && item != null)
            {
                if (item.name != null)
                    return CleanDisplayName(item.name);
                if (item.Prefab != null)
                    return CleanDisplayName(item.Prefab.name);
            }

            return "Item#" + itemId;
        }

        private static string CleanDisplayName(string name, bool isIngredient = false)
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
                                     .Replace("Container", string.Empty)
                                     .Replace("Chopped", string.Empty)
                                     .Replace("Plate", string.Empty)
                                     .Replace("Rice", string.Empty)
                                     .Replace("Grated", string.Empty)
                                     .Replace("Tortilla", string.Empty)
                                     .Replace("Sauce", string.Empty)
                                     .Replace("Slice", string.Empty)
                                     .Replace("Turkey Gravy", "Gravy")
                                     .Replace("Bun", string.Empty)
                                     .Replace("Individual", string.Empty)
                                     .Replace("Bread", string.Empty)
                                     .Replace("Pot", string.Empty)
                                     .Replace("Serving", string.Empty)
                                     .Replace("Ingredient", string.Empty)
                                     .Replace("Stand", string.Empty)
                                     .Replace("Flavour", "Cake")
                                     .Replace("Mince", "Meat")
                                     .Trim();

            if(isIngredient)
            {
                displayName = displayName.Replace("Steak", string.Empty)
                                         .Replace("Serving", string.Empty)
                                         .Replace("Board", string.Empty)
                                         .Replace("Ice Cream", string.Empty)
                                         .Replace("Apple s", "Apples")
                                         .Trim();
            }

            if (displayName.Contains("Pie"))
            {
                displayName = "Pie";
            }
            //else if (displayName.Contains("Cake"))
            //{
            //    displayName = "Cake";
            //}

            // Remove any double spaces that may have been introduced
            while (displayName.Contains("  "))
            {
                displayName = displayName.Replace("  ", " ");
            }

            return displayName;
        }
    }
}
