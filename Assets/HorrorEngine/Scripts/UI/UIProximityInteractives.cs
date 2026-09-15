using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{

    public class UIProximityInteractives : MonoBehaviour
    {
        [SerializeField] private GameObject m_UIProximityInteractiveHint;
        [SerializeField] private GameObject m_HintsParent;

        private Dictionary<Interactive, PooledGameObject> m_CreatedUI = new Dictionary<Interactive, PooledGameObject>();

        private void Awake()
        {
            MessageBuffer<GameUnpausedMessage>.Subscribe(OnGameUnpaused);
            MessageBuffer<GamePausedMessage>.Subscribe(OnGamePaused);
            MessageBuffer<ProximityInteractiveDetectedMessage>.Subscribe(StartTrackingInteractive);
            MessageBuffer<ProximityInteractiveUndetectedMessage>.Subscribe(StopTrackingInteractive);
            MessageBuffer<ProximityInteractiveClearedMessage>.Subscribe(ClearTrackingInteractive);
        }

        public void StartTrackingInteractive(ProximityInteractiveDetectedMessage msg)
        {
            PooledGameObject interactiveUI = GameObjectPool.Instance.GetFromPool(m_UIProximityInteractiveHint, m_HintsParent.transform);
            interactiveUI.gameObject.SetActive(true);
            m_CreatedUI.Add(msg.Interactive, interactiveUI);
        }

        public void StopTrackingInteractive(ProximityInteractiveUndetectedMessage msg)
        {
            if (msg.Interactive != null && m_CreatedUI.ContainsKey(msg.Interactive))
            {
                var obj = m_CreatedUI[msg.Interactive];
                obj.ReturnToPool();
                m_CreatedUI.Remove(msg.Interactive);
            }
        }

        public void ClearTrackingInteractive(ProximityInteractiveClearedMessage msg)
        {
            foreach (var entry in m_CreatedUI)
            {
                var obj = entry.Value;
                obj.ReturnToPool();
            }
            m_CreatedUI.Clear();
        }

        private void OnGamePaused(GamePausedMessage msg)
        {
            foreach (var entry in m_CreatedUI)
            {
                entry.Value.gameObject.SetActive(false);
            }
        }

        private void OnGameUnpaused(GameUnpausedMessage msg)
        {
            foreach (var entry in m_CreatedUI)
            {
                entry.Value.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            var cam = Camera.main;
            foreach (var entry in m_CreatedUI) //Key: Interactive, Value: GameObject
            {
                var interactive = entry.Key;
                var interactiveUI = entry.Value;
                interactiveUI.transform.position = cam.WorldToScreenPoint(interactive.transform.position);
            }
        }

        private void OnDestroy()
        {
            MessageBuffer<GameUnpausedMessage>.Unsubscribe(OnGameUnpaused);
            MessageBuffer<GamePausedMessage>.Unsubscribe(OnGamePaused);
            MessageBuffer<ProximityInteractiveDetectedMessage>.Unsubscribe(StartTrackingInteractive);
            MessageBuffer<ProximityInteractiveUndetectedMessage>.Unsubscribe(StopTrackingInteractive);
            MessageBuffer<ProximityInteractiveClearedMessage>.Unsubscribe(ClearTrackingInteractive);
        }
    }
}
