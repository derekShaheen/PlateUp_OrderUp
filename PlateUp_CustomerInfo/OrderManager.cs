using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace SkripOrderUp
{
    internal class OrderManager : MonoBehaviour
    {
        public class OrderItem
        {
            public GameObject ColourBlindObject { get; set; } // not used; kept for compatibility
            public string DisplayName { get; set; }
            public string ColourblindText { get; set; }       // extras suffix like " - Ketchup"
            public string SideItem { get; set; }
            public bool IsComplete { get; set; }
            public Vector3 SeatPosition { get; set; }
            public Vector3 TablePosition { get; set; }
        }

        public class OrderGroup
        {
            public float StartTime { get; set; }
            public List<OrderItem> Items { get; set; } = new List<OrderItem>();
            public int OrderNumber { get; set; }
        }

        // Public list for any existing UI consumers
        public List<OrderGroup> orderGroups = new List<OrderGroup>();

        // Stable state keyed by ECS group entity
        private readonly Dictionary<Entity, OrderGroup> _groupsByEntity = new Dictionary<Entity, OrderGroup>();

        // Strictly increasing for the whole play session (won’t reset per snapshot)
        private int _orderSeq = 0;

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
            if (Instance == this) Instance = null;
        }

        // Call this if you explicitly want to reset numbering (e.g., on franchise scene/load)
        public void ResetAll()
        {
            _groupsByEntity.Clear();
            orderGroups.Clear();
            _orderSeq = 0;
            var ev = OnOrdersUpdated;
            if (ev != null) ev(orderGroups);
        }

        // Receives ECS snapshots from Main; reconciles into persistent state.
        public void UpdateFromEcs(List<OrderGroupDto> snapshot)
        {
            // Track groups we saw this tick that still have at least one incomplete item
            var seenActiveGroups = new HashSet<Entity>();

            for (int i = 0; i < snapshot.Count; i++)
            {
                var dto = snapshot[i];

                // Build items from DTO, ignoring those already complete
                var builtItems = BuildItems(dto);

                // If no incomplete items remain, we consider the group cleared
                if (builtItems.Count == 0)
                    continue;

                seenActiveGroups.Add(dto.Group);

                OrderGroup og;
                if (!_groupsByEntity.TryGetValue(dto.Group, out og))
                {
                    og = new OrderGroup
                    {
                        StartTime = Time.time,
                        OrderNumber = ++_orderSeq
                    };
                    _groupsByEntity[dto.Group] = og;
                }

                // Replace the item list with the current snapshot
                og.Items.Clear();
                og.Items.AddRange(builtItems);
            }

            // Remove any groups not present anymore
            if (_groupsByEntity.Count > 0)
            {
                var toRemove = new List<Entity>();
                foreach (var kvp in _groupsByEntity)
                {
                    if (!seenActiveGroups.Contains(kvp.Key))
                        toRemove.Add(kvp.Key);
                }
                for (int r = 0; r < toRemove.Count; r++)
                    _groupsByEntity.Remove(toRemove[r]);
            }

            // Rebuild the public list in a stable order (by OrderNumber)
            orderGroups.Clear();
            if (_groupsByEntity.Count > 0)
            {
                // Copy values, then sort by OrderNumber to keep a consistent UI order
                var tmp = new List<OrderGroup>(_groupsByEntity.Values);
                tmp.Sort((a, b) => a.OrderNumber.CompareTo(b.OrderNumber));
                orderGroups.AddRange(tmp);
            }

            var ev = OnOrdersUpdated;
            if (ev != null) ev(orderGroups);
        }

        private static List<OrderItem> BuildItems(OrderGroupDto dto)
        {
            var list = new List<OrderItem>(dto.Items.Count);
            for (int j = 0; j < dto.Items.Count; j++)
            {
                var it = dto.Items[j];

                if (it.IsComplete)
                    continue;

                var extrasSuffix = string.IsNullOrEmpty(it.ExtraName) ? string.Empty : (" w/ " + it.ExtraName);
                var sideName = it.IsSide ? it.ItemName : string.Empty;

                // make sure extras doesn't match item name
                if (extrasSuffix == it.ItemName)
                {
                    extrasSuffix = string.Empty;
                }

                if (sideName == it.ItemName)
                {
                    sideName = string.Empty;
                }

                list.Add(new OrderItem
                {
                    ColourBlindObject = null,
                    DisplayName = it.ItemName,
                    ColourblindText = extrasSuffix,
                    SideItem = sideName,
                    IsComplete = it.IsComplete,
                    SeatPosition = it.SeatPosition,
                    TablePosition = it.TablePosition
                });
            }
            return list;
        }
    }
}
