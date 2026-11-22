using System.Diagnostics;
using UnityEngine;

namespace ProjectB.Core
{
    /// <summary>
    /// Development-only diagnostic logger stripped automatically from release builds.
    /// </summary>
    public static class DevLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message, Object context)
        {
            UnityEngine.Debug.Log(message, context);
        }
    }
}
