using UnityEngine;
using System;
using System.Collections.Generic;

namespace HorrorEngine
{
    [Serializable]
    public class CursorControlSchemeBehaviour
    {
        public InputSchemeHandle SchemeHandle;
        public CursorState InUIState;
        public CursorState OutOfUIState;
    }

    [Serializable]
    public class CursorState
    {
        public CursorLockMode Mode;
        public bool Visible;
    }

    public class CursorController : SingletonBehaviour<CursorController>
    {
        [SerializeField] bool m_StartInUI;
        [SerializeField] CursorState m_InUIState;
        [SerializeField] CursorState m_OutOfUIState;
        [SerializeField] List<CursorControlSchemeBehaviour> m_SchemeCursorBehaviour = new List<CursorControlSchemeBehaviour>();
        [SerializeField] Texture2D m_CursorImage;
        [SerializeField] Vector2 m_CursorHotspot;

        private int m_InUICount;

        private MessageBuffer<InputSchemeChangedMessage>.MessageCallback m_OnInputSchemeChangedCallback;

        public bool IsInUI => m_InUICount > 0;

        protected override void Awake()
        {
            base.Awake();

            if (m_CursorImage)
                Cursor.SetCursor(m_CursorImage, m_CursorHotspot, CursorMode.Auto);

            m_OnInputSchemeChangedCallback = OnInputSchemeChanged;
            MessageBuffer<InputSchemeChangedMessage>.Subscribe(m_OnInputSchemeChangedCallback);

            if (!m_StartInUI)
                UpdateState(true);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<InputSchemeChangedMessage>.Unsubscribe(m_OnInputSchemeChangedCallback);
        }

        // --------------------------------------------------------------------

        private void OnInputSchemeChanged(InputSchemeChangedMessage msg)
        {
            UpdateState(IsInUI);
        }


        public void SetInUI(bool inUI)
        {
            if (!inUI)
            {
                --m_InUICount;
                Debug.Assert(m_InUICount >= 0, "Cursor InUI count went negative. This shouldn't happen. Something is calling SetInUI multiple times with the same value");
            }
            else
            {
                 ++m_InUICount;
            }

            UpdateState(IsInUI);
        }

        private void UpdateState(bool inUI)
        {
            var scheme = ControlSchemeDetector.CurrentControlScheme;
            CursorState state = ApplyControlSchemeBehaviour(scheme, IsInUI);
            Cursor.lockState = state.Mode;
            Cursor.visible = state.Visible;
        }


        private CursorState ApplyControlSchemeBehaviour(InputSchemeHandle scheme, bool inUI)
        {
            var state = inUI ? m_InUIState : m_OutOfUIState;
            
            foreach(var schemeEntry in m_SchemeCursorBehaviour)
            {
                if (schemeEntry.SchemeHandle == scheme)
                {
                    state = inUI ? schemeEntry.InUIState : schemeEntry.OutOfUIState;
                }
            }

            return state;
        }
    }
}
