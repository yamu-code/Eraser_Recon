using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIInventoryContextMenu : MonoBehaviour
    {
        public Button UseButton;
        public Button EquipButton;
        public Button ExamineButton;
        public Button CombineButton;
        public Button DropButton;
        public Button ExchangeButton;
        public Vector3 Offset;

        public UnityEvent OnClose;

        private IUIInput m_Input;
        private Button m_DefaultButton;
        private List<Button> m_NavFixButtonList = new List<Button>(); // Only for not allocating
        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();

            UseButton?.onClick.AddListener(OnOptionSelected);
            EquipButton?.onClick.AddListener(OnOptionSelected);
            ExamineButton?.onClick.AddListener(OnOptionSelected);
            CombineButton?.onClick.AddListener(OnOptionSelected);
            DropButton?.onClick.AddListener(OnOptionSelected);
            ExchangeButton?.onClick.AddListener(OnOptionSelected);
        }

        // --------------------------------------------------------------------

        private void OnOptionSelected()
        {
            m_Input.Flush(); // Flush so an object is not immediately selected after this
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public bool Show(ItemData item, Transform positionAt = null)
        {
            Fill(item);
            if (!m_DefaultButton)
            {
                return false;
            }

            SelectDefault();
            gameObject.SetActive(true);

            if (positionAt)
            {
                transform.position = positionAt.position + Offset;
            }

            return true;
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            OnClose?.Invoke();
        }

        // --------------------------------------------------------------------

        private void Fill(ItemData item)
        {
            m_DefaultButton = null;

            UseButton?.gameObject.SetActive(false);
            EquipButton?.gameObject.SetActive(false);
            ExamineButton?.gameObject.SetActive(false);
            CombineButton?.gameObject.SetActive(false);
            DropButton?.gameObject.SetActive(false);

            if (UseButton && item.InventoryAction.HasFlag(InventoryMainAction.Use))
            {
                UseButton.gameObject.SetActive(true);
                m_DefaultButton = UseButton;
            }
            
            if(EquipButton && item.InventoryAction.HasFlag(InventoryMainAction.Equip))
            {
                var equipable = item as EquipableItemData;
                if (equipable.SlotTag != null)
                {
                    EquipButton.gameObject.SetActive(true);
                    if (!m_DefaultButton) m_DefaultButton = EquipButton;
                }
            }

            if (CombineButton && item.Flags.HasFlag(ItemFlags.Combinable))
            {
                CombineButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = CombineButton;
            }

            if (ExamineButton && item.Flags.HasFlag(ItemFlags.Examinable))
            {
                ExamineButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = ExamineButton;
            }

            if (DropButton && item.Flags.HasFlag(ItemFlags.Droppable))
            {
                DropButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = DropButton;
            }

            if (ExchangeButton)
            {
                ExchangeButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = ExchangeButton;
            }

            FixNavigation();
        }

        // --------------------------------------------------------------------

        
        private void FixNavigation() 
        {
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.Explicit;

            GetComponentsInChildren(m_NavFixButtonList);

            int lastIndex = m_NavFixButtonList.Count - 1;
            for (int i = 0; i < m_NavFixButtonList.Count; ++i)
            {
                navigation.selectOnUp = i == 0 ? m_NavFixButtonList[lastIndex] : m_NavFixButtonList[i - 1];
                navigation.selectOnDown = i == lastIndex ? m_NavFixButtonList[0] : m_NavFixButtonList[i + 1];
                m_NavFixButtonList[i].navigation = navigation;
            }
        }

        // --------------------------------------------------------------------

        private void SelectDefault()
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(m_DefaultButton.gameObject);
        }
    }
}