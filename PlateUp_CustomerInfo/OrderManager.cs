using Kitchen;
using KitchenData;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkripOrderUp
{
    internal class OrderManager : MonoBehaviour
    {
        public class OrderItem
        {
            public string DisplayName { get; set; }
            public string ExtrasText { get; set; }
            public string SideItem { get; set; }
            public Vector3 SeatPosition { get; set; }
            public Vector3 TablePosition { get; set; }
        }

        public class OrderGroup
        {
            public float StartTime { get; set; }
            public List<OrderItem> Items { get; set; } = new List<OrderItem>();
            public int OrderNumber { get; set; }
        }

        public readonly List<OrderGroup> orderGroups = new List<OrderGroup>();

        private readonly Dictionary<int, OrderGroup> _groupsByView = new Dictionary<int, OrderGroup>();
        private int _orderSeq;

        public static OrderManager Instance { get; private set; }
        public event Action<List<OrderGroup>> OnOrdersUpdated;

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ResetAll()
        {
            _groupsByView.Clear();
            orderGroups.Clear();
            _orderSeq = 0;
            NotifyUpdated();
        }

        public void UpdateFromView(int viewId, ItemCollectionView.ViewData viewData)
        {
            DisplayNameConfig.ReloadIfChanged();
            List<OrderItem> items = BuildItems(viewData.Items);
            if (items.Count == 0)
            {
                RemoveGroup(viewId);
                return;
            }

            OrderGroup group;
            if (!_groupsByView.TryGetValue(viewId, out group))
            {
                group = new OrderGroup
                {
                    StartTime = Time.time,
                    OrderNumber = ++_orderSeq
                };
                _groupsByView.Add(viewId, group);
            }

            group.Items.Clear();
            group.Items.AddRange(items);
            RebuildSnapshot();
        }

        public void RemoveGroup(int viewId)
        {
            if (!_groupsByView.Remove(viewId))
                return;

            RebuildSnapshot();
        }

        private void RebuildSnapshot()
        {
            orderGroups.Clear();
            orderGroups.AddRange(_groupsByView.Values);
            orderGroups.Sort((a, b) => a.OrderNumber.CompareTo(b.OrderNumber));
            NotifyUpdated();
        }

        private void NotifyUpdated()
        {
            var handler = OnOrdersUpdated;
            if (handler != null)
                handler(orderGroups);
        }

        private static List<OrderItem> BuildItems(List<ItemCollectionView.ItemData> source)
        {
            var result = new List<OrderItem>(source != null ? source.Count : 0);
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                ItemCollectionView.ItemData item = source[i];
                if (item.ItemID == 0 || item.IsComplete || item.IsSatisfiedBySharer)
                    continue;

                string itemName = ResolveItemName(item.ItemID);
                string displayName = itemName + ResolveChosenIngredientsText(item.Components, item.ItemID);
                string extraName = item.ShowExtra && item.ExtraID != 0
                    ? ResolveItemName(item.ExtraID)
                    : string.Empty;

                result.Add(new OrderItem
                {
                    DisplayName = displayName,
                    ExtrasText = string.IsNullOrEmpty(extraName) ? string.Empty : "w/ " + extraName,
                    SideItem = item.IsSide ? displayName : string.Empty,
                    SeatPosition = item.SeatPosition,
                    TablePosition = item.TablePosition
                });
            }

            return result;
        }

        private static string ResolveChosenIngredientsText(ItemList components, int primaryItemId)
        {
            if (components.Count <= 1)
                return string.Empty;

            string primaryName = ResolveItemName(primaryItemId);
            var names = new List<string>();
            var seen = new HashSet<int>();

            for (int i = 0; i < components.Count; i++)
            {
                int componentId = components[i];
                if (componentId == 0 || componentId == primaryItemId || !seen.Add(componentId))
                    continue;

                Item component;
                if (GameData.Main == null ||
                    !GameData.Main.TryGet<Item>(componentId, out component, true) ||
                    component == null ||
                    component.IsMergeableSide)
                {
                    continue;
                }

                string name = component.name != null
                    ? component.name
                    : component.Prefab != null ? component.Prefab.name : null;

                name = CleanDisplayName(name, true);
                if (string.IsNullOrEmpty(name) ||
                    string.Equals(name, primaryName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                names.Add(name);
            }

            if (names.Count == 0)
                return string.Empty;

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return " (" + string.Join(", ", names.ToArray()) + ")";
        }

        private static string ResolveItemName(int itemId)
        {
            Item item;
            if (itemId != 0 &&
                GameData.Main != null &&
                GameData.Main.TryGet<Item>(itemId, out item, true) &&
                item != null)
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
            return DisplayNameConfig.Clean(name, isIngredient);
        }
    }
}
