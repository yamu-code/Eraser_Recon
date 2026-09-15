using System;
using UnityEngine;

namespace HorrorEngine
{
    public class GCUtils : MonoBehaviour
    {
        public static void ForceGCCollect()
        {
#if UNITY_EDITOR
            var memUsed = RuntimeDebug.IsCategoryEnabled("GC") ? GC.GetTotalMemory(true) : 0f;
#endif

            // Collect all generations of memory.
            GC.Collect();

#if UNITY_EDITOR
            if (RuntimeDebug.IsCategoryEnabled("GC"))
                Debug.Log($"Memory GC collected:  {memUsed - GC.GetTotalMemory(true)}");
#endif
        }
    }
}