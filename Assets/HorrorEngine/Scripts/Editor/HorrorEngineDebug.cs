using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    public class HorrorEngineDebug : MonoBehaviour
    {
        [MenuItem("Horror Engine/Debug/Kill Player")]
        public static void KillPlayer()
        {
            if (Application.isPlaying)
                GameManager.Instance.Player.GetComponent<Health>().Kill();
        }

        [MenuItem("Horror Engine/Debug/Heal Player")]
        public static void HealPlayer()
        {
            if (Application.isPlaying)
                GameManager.Instance.Player.GetComponent<Health>().RegenerateAll();
        }

        [MenuItem("Horror Engine/Debug/Set Player Invulnerable")]
        public static void SetPlayerInvulnerable()
        {
            if (Application.isPlaying)
                GameManager.Instance.Player.GetComponent<Health>().Infinite = true;
        }
    }
}
