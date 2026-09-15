using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace HorrorEngine
{
   
    [CreateAssetMenu(fileName = "GameSettingLanguage", menuName = "Horror Engine/Settings/Language")]
    public class GameSettingsElementLanguage : SettingsElementComboContent
    {
        public Locale[] Languages;

        public override void Apply()
        {
            SettingsManager.Instance.Get(this, out string outVal);
            try
            {
                int localeIndex = Convert.ToInt16(outVal);
                string localeName = GetItemName(localeIndex);
                foreach (var locale in Languages)
                {
                    if (locale.LocaleName == localeName)
                    {
                        LocalizationSettings.SelectedLocale = locale;
                        return;
                    }
                }
                }
            catch { }
        }

        public override int GetItemCount()
        {
            return Languages.Length;
        }

        public override string GetItemName(int index)
        {
            return Languages[index].LocaleName;
        }
    }
}