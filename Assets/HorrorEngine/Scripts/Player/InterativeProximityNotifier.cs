using UnityEngine;

namespace HorrorEngine
{
    public class ProximityInteractiveDetectedMessage : BaseMessage 
    { 
        public Interactive Interactive;
    }
    public class ProximityInteractiveUndetectedMessage : BaseMessage 
    {
        public Interactive Interactive;
    }
    public class ProximityInteractiveClearedMessage : BaseMessage { }

    public class InterativeProximityNotifier : MonoBehaviour
    {
        private InteractionColliderDetector m_InteractionColliderDetector;
        private ProximityInteractiveDetectedMessage m_ProximityInteractiveDetectedMessage = new ProximityInteractiveDetectedMessage();
        private ProximityInteractiveUndetectedMessage m_ProximityInteractiveUndetectedMessage = new ProximityInteractiveUndetectedMessage();    

        private void Awake()
        {
            m_InteractionColliderDetector = GetComponent<InteractionColliderDetector>();
            m_InteractionColliderDetector.OnInteractionDetected.AddListener(OnInteractiveDetected);
            m_InteractionColliderDetector.OnInteractionUndetected.AddListener(OnInteractiveUndetected);
            m_InteractionColliderDetector.OnInteractivesCleared.AddListener(OnInteractivesCleared);
        }
        private void OnInteractiveDetected(Interactive interactive)
        {
            m_ProximityInteractiveDetectedMessage.Interactive = interactive;
            MessageBuffer<ProximityInteractiveDetectedMessage>.Dispatch(m_ProximityInteractiveDetectedMessage);
        }


        private void OnInteractiveUndetected(Interactive interactive)
        {
            m_ProximityInteractiveUndetectedMessage.Interactive = interactive;  
            MessageBuffer<ProximityInteractiveUndetectedMessage>.Dispatch(m_ProximityInteractiveUndetectedMessage);
        }

        private void OnInteractivesCleared()
        {
            MessageBuffer<ProximityInteractiveClearedMessage>.Dispatch();
        }
        private void OnDestroy()
        {
            m_InteractionColliderDetector.OnInteractionDetected.RemoveListener(OnInteractiveDetected);
            m_InteractionColliderDetector.OnInteractionUndetected.RemoveListener(OnInteractiveUndetected);
            m_InteractionColliderDetector.OnInteractivesCleared.RemoveListener(OnInteractivesCleared);
        }
        
    }
}
