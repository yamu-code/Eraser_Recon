using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(Animator))]
    public class AnimationRandomizer : MonoBehaviour
    {
        [SerializeField] float m_MinRandomAnimStartTime = 1f;
        [SerializeField] float m_MaxRandomAnimStartTime = 3f;
        [SerializeField] bool m_Repeat = true;
        [SerializeField] bool m_AvoidRepetition = true;
        [SerializeField] float m_CrossfadeDuration = 0.25f;

        [SerializeField] string[] m_Animations;

        private Animator m_Animator;
        private int m_LastAnimIndex;

        private void Awake()
        {
            m_Animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            InvokeRepeating(nameof(RandomizeAnimation), 0f, Random.Range(m_MinRandomAnimStartTime, m_MaxRandomAnimStartTime));
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(RandomizeAnimation));
        }

        private void RandomizeAnimation()
        {
            int newAnimIndex = Random.Range(0, m_Animations.Length);
            if (m_AvoidRepetition && m_Animations.Length > 1 && newAnimIndex == m_LastAnimIndex)
            {
                int retries = 10;
                for(int i = 0; i < retries; i++)
                {
                    newAnimIndex = Random.Range(0, m_Animations.Length);
                    if (newAnimIndex != m_LastAnimIndex)
                        break;
                }
            }

            m_LastAnimIndex = newAnimIndex;
            m_Animator.CrossFade(m_Animations[m_LastAnimIndex], m_CrossfadeDuration);
            

            if (!m_Repeat)
            {
                CancelInvoke(nameof(RandomizeAnimation));
            }
        }

    }
}