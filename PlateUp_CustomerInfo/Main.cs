using HarmonyLib;
using KitchenMods;
using System.Reflection;
using UnityEngine;

namespace SkripOrderUp
{
    public class Main : IModInitializer
    {
        public const string MOD_GUID = "Skrip.PlateUp.OrderUp";
        public const string MOD_NAME = "CustomerInfo";

        private static bool _initialised;

        public void PostActivate(Mod mod)
        {
        }

        public void PreInject()
        {
        }

        public void PostInject()
        {
            if (_initialised)
                return;

            if (OrderManager.Instance == null)
            {
                var go = new GameObject("OrderUp");
                go.AddComponent<OrderManager>();
                go.AddComponent<OrderView>();
                go.AddComponent<SceneWatcher>();
            }

            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _initialised = true;
        }
    }
}
