using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsQualityPreset", menuName = "Horror Engine/Settings/Quality Preset")]
    public class GraphicSettingsElementQualityPreset : SettingsElementComboContent
    {
        public override void Apply()
        {
            try
            {
                int index = Convert.ToInt16(GetAsString());
                if (index >= 0)
                    QualitySettings.SetQualityLevel(index);
            }
            catch { }
        }

        public override string GetItemName(int index)
        {
            return QualitySettings.names[index];
        }

        public override int GetItemCount()
        {
            return QualitySettings.names.Length;
        }
    }
}