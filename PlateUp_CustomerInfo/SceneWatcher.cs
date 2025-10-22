using UnityEngine;
using Kitchen;

namespace SkripOrderUp
{
    internal class SceneWatcher : MonoBehaviour
    {
        private SceneType _lastScene;

        void Start()
        {
            _lastScene = GameInfo.CurrentScene;
        }

        void Update()
        {
            var current = GameInfo.CurrentScene;
            if (current == _lastScene)
                return;

            _lastScene = current;

            var mgr = OrderManager.Instance;
            if (mgr == null)
                return;

            if (current == SceneType.Franchise || current == SceneType.Kitchen || current == SceneType.Postgame)
            {
                mgr.ResetAll();
            }
        }
    }
}
