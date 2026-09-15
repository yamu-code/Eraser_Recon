using System;
using UnityEngine;
using UnityEngine.Localization;

namespace HorrorEngine
{
    [Serializable]
    public class LocalizableText
    {
        public bool IsLocalized;
        public string Unlocalized;
        public LocalizedString Localized;

        public override string ToString()
        {
            if (!this.IsLocalized || this.Localized == null || this.Localized.IsEmpty)
                return this.Unlocalized ?? "";

            return this.Localized.GetLocalizedString() ?? "";
        }

        public static implicit operator string(LocalizableText localStr)
        {
            return localStr?.ToString() ?? "";
        }
    }
}