using Kitchen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SkripOrderUp
{
    internal class Helpers
    {
        internal static bool IsPlaying()
        {
            if (GameInfo.CurrentScene == SceneType.Kitchen
                || GameInfo.CurrentScene == SceneType.Franchise
                || GameInfo.CurrentScene == SceneType.FranchiseBuilder)
            {
                return true;
            }
            return false;
        }

        // Helper method to format seconds into minutes and seconds
        internal static string FormatTime(float totalSeconds)
        {
            if (totalSeconds > 59f)
            {
                int minutes = Mathf.FloorToInt(totalSeconds / 60f);
                int seconds = Mathf.FloorToInt(totalSeconds % 60f);
                return string.Format("{0}m {1}s", minutes, seconds);
            }
            else
            {
                int seconds = Mathf.CeilToInt(totalSeconds);
                return string.Format("{0}s", seconds);
            }
        }
    }
}
