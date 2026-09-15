using HorrorEngine;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SubtitleLine {
    public float TimeInSeconds;
    public float Duration;
    public LocalizableText Text;
}

// --------------------------------------------------------------------

public class CinematicSubtitles : MonoBehaviour
{
    [SerializeField] private CinematicPlayer m_CinematicPlayer;
    [SerializeField] private UICinematicPlayer m_UICinematicPlayer;
    [SerializeField] private UISubtitles m_UISubtitles;

    public List<SubtitleLine> Lines;

    private float m_Timer = 0f;
    private int m_CurrentLineIndex = 0;
    private bool m_WasPrinted = false;

    // --------------------------------------------------------------------

    public void Awake()
    {
        m_CinematicPlayer.OnCutsceneStart.AddListener(OnCutsceneStart);
        enabled = false;

        for (int i = 1; i < Lines.Count; i++)
        {
            if (Lines[i].TimeInSeconds <= Lines[i-1].TimeInSeconds + Lines[i-1].Duration)
            {
                Debug.LogError("Subtitle lines overlap in line: " + (i+1), gameObject);
            }
        }
    }


    // --------------------------------------------------------------------

    private void OnCutsceneStart()
    {
        m_UICinematicPlayer = UIManager.Get<UICinematicPlayer>();
        m_UISubtitles = UIManager.Get<UISubtitles>();
        m_UICinematicPlayer.OnPlaybackStart.AddListener(OnPlaybackStart);
        m_UICinematicPlayer.OnPlaybackEnd.AddListener(OnPlaybackEnd);
    }

    private void OnPlaybackStart()
    {
        m_Timer = 0f;
        m_CurrentLineIndex = 0;
        m_WasPrinted = false;
        enabled = true;
    }

    private void OnPlaybackEnd()
    {
        enabled = false;
        CleanSubtitle();
        m_UICinematicPlayer.OnPlaybackStart.RemoveListener(OnPlaybackStart);
        m_UICinematicPlayer.OnPlaybackEnd.RemoveListener(OnPlaybackEnd);
    }

    // --------------------------------------------------------------------

    private void Update()
    {
        m_Timer += Time.unscaledDeltaTime;
        var currentLine = Lines[m_CurrentLineIndex];

        float lineStartTime = currentLine.TimeInSeconds;
        float lineEndTime = currentLine.TimeInSeconds + currentLine.Duration;

        if (!m_WasPrinted && m_Timer > lineStartTime && m_Timer <= lineEndTime)
        {
            PrintSubtitle();
        }
        else if (m_Timer > lineEndTime)
        {
            CleanSubtitle();

            if(m_CurrentLineIndex < Lines.Count - 1)
            {
                m_CurrentLineIndex += 1;
                m_WasPrinted = false;
            }
        }
    }

    private void PrintSubtitle()
    {
        if (m_WasPrinted == false)
        {
            m_UISubtitles.SetSubtitles(Lines[m_CurrentLineIndex].Text.ToString());
            m_WasPrinted = true;
        }
    }

    private void CleanSubtitle() 
    { 
        m_UISubtitles.SetSubtitles(string.Empty);
    }

    // --------------------------------------------------------------------

    private void OnDestroy()
    {
        if (m_CinematicPlayer != null) 
        { 
            m_CinematicPlayer.OnCutsceneStart.RemoveListener(OnCutsceneStart); 
        }
        if (m_UICinematicPlayer != null)
        {
            m_UICinematicPlayer.OnPlaybackStart.RemoveListener(OnPlaybackStart);
            m_UICinematicPlayer.OnPlaybackEnd.RemoveListener(OnPlaybackEnd);
        }
    }
}