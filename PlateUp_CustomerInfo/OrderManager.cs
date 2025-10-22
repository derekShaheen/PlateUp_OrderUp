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
            public GameObject ColourBlindObject { get; set; }
            public string DisplayName { get; set; }
            public string ColourblindText { get; set; }
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

        public List<OrderGroup> orderGroups = new List<OrderGroup>();

        private readonly Dictionary<Entity, OrderGroup> _groupsByEntity = new Dictionary<Entity, OrderGroup>();

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

        public void ResetAll()
        {
            _groupsByEntity.Clear();
            orderGroups.Clear();
            _orderSeq = 0;
            var ev = OnOrdersUpdated;
            if (ev != null) ev(orderGroups);
        }

        public void UpdateFromEcs(List<OrderGroupDto> snapshot)
        {
            var seenActiveGroups = new HashSet<Entity>();

            for (int i = 0; i < snapshot.Count; i++)
            {
                var dto = snapshot[i];
                var builtItems = BuildItems(dto);
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

                og.Items.Clear();
                og.Items.AddRange(builtItems);
            }

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

            orderGroups.Clear();
            if (_groupsByEntity.Count > 0)
            {
                var tmp = new List<OrderGroup>(_groupsByEntity.Values);
                tmp.Sort((a, b) => a.OrderNumber.CompareTo(b.OrderNumber));
                orderGroups.AddRange(tmp);
            }

            var ev2 = OnOrdersUpdated;
            if (ev2 != null) ev2(orderGroups);
        }

        private static List<OrderItem> BuildItems(OrderGroupDto dto)
        {
            var list = new List<OrderItem>(dto.Items.Count);
            for (int j = 0; j < dto.Items.Count; j++)
            {
                var it = dto.Items[j];
                if (it.IsComplete)
                    continue;

                var extrasSuffix = string.IsNullOrEmpty(it.ExtraName) ? "" : (" w/ " + it.ExtraName);
                var sideName = it.IsSide ? it.ItemName : "";

                if (extrasSuffix == it.ItemName) extrasSuffix = "";
                if (sideName == it.ItemName) sideName = "";

                var displayWithIngredients = it.ItemName + (string.IsNullOrEmpty(it.IngredientsText) ? "" : it.IngredientsText);

                list.Add(new OrderItem
                {
                    ColourBlindObject = null,
                    DisplayName = displayWithIngredients,
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
