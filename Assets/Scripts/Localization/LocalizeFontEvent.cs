using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Vampire
{
    [Serializable]
    public class LocalizedTMPFontAsset : LocalizedAsset<TMP_FontAsset>
    {
    }

    [Serializable]
    public class UnityEventTMPFont : UnityEvent<TMP_FontAsset>
    {
    }

    [AddComponentMenu("Localization/Asset/Localize Font Event")]
    public class LocalizeFontEvent : LocalizedAssetEvent<TMP_FontAsset, LocalizedTMPFontAsset, UnityEventTMPFont>
    {
        [NonSerialized] public TMP_FontAsset ThemeFontOverride;
        public TMP_FontAsset LocalizedFont { get; private set; }
        [Header("TextMeshPro UI Texts")]
        [Tooltip("폰트를 적용할 TextMeshProUGUI 목록입니다. 비어 있거나 삭제된 항목은 자동으로 건너뜁니다.")]
        [SerializeField] private TextMeshProUGUI[] _tmpUITexts;

        [Header("TextMeshPro World Texts")]
        [Tooltip("폰트를 적용할 TextMeshPro 목록입니다. 비어 있거나 삭제된 항목은 자동으로 건너뜁니다.")]
        [SerializeField] private TextMeshPro[] _tmpTexts;

        protected override void UpdateAsset(TMP_FontAsset font)
        {
            LocalizedFont = font;
            if (ThemeFontOverride != null) font = ThemeFontOverride;
            base.UpdateAsset(font);

            if (font == null)
            {
                Debug.LogWarning("[LocalizeFontEvent] 적용할 TMP_FontAsset이 비어 있습니다.");
                return;
            }

            if (_tmpUITexts != null)
            {
                foreach (TextMeshProUGUI tmp in _tmpUITexts)
                {
                    if (tmp == null)
                    {
                        continue;
                    }

                    tmp.font = font;
                }
            }

            if (_tmpTexts != null)
            {
                foreach (TextMeshPro tmp in _tmpTexts)
                {
                    if (tmp == null)
                    {
                        continue;
                    }

                    tmp.font = font;
                }
            }
        }
    }
}
