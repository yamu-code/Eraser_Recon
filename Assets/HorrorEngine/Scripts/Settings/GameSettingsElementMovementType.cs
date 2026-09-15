using System;
using UnityEngine;

namespace HorrorEngine
{
    public enum PlayerMovementType
    {
        Tank,
        Alternate
    }

    [Serializable]
    public class MovementTypeSettingsName
    {
        public PlayerMovementType Type;
        public LocalizableText Name;
    }

    [CreateAssetMenu(fileName = "GameSettingMovement", menuName = "Horror Engine/Settings/MovementType")]
    public class GameSettingsElementMovementType : SettingsElementComboContent
    {
        public MovementTypeSettingsName[] LocalizedNames;

        public override void Apply()
        {
            // Do Nothing - Player movement will read the setting value
        }

        public override int GetItemCount()
        {
            return Enum.GetNames(typeof(PlayerMovementType)).Length;
        }

        public override string GetItemName(int index)
        {
            PlayerMovementType type = (PlayerMovementType)index;
            foreach (var name in LocalizedNames)
            {
                if (name.Type == type)
                    return name.Name;
            }

            return Enum.GetNames(typeof(PlayerMovementType))[index];
        }
    }
}