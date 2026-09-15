using UnityEngine;
using TMPro;

public class UISubtitles : MonoBehaviour
{
    private TMPro.TextMeshProUGUI m_SubtitleText;

    public void Start()
    {
        m_SubtitleText = GetComponentInChildren<TMPro.TextMeshProUGUI>();
    }

    public void SetSubtitles(string text) 
    {
        m_SubtitleText.text = text;
        gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}
