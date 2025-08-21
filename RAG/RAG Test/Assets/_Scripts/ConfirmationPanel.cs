using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConfirmationPanel : MonoBehaviour
{
    public static ConfirmationPanel instance = null;

    public enum ConfirmationType { Question, Message }

    public GameObject m_VisibleSection;
    public Button m_YesButton;
    public Button m_NoButton;
    public TextMeshProUGUI m_YesButtonText;
    public TextMeshProUGUI m_ConfirmationTextMessage;
    public UnityEvent m_YesEvent = new UnityEvent();
    public UnityEvent m_NoEvent = new UnityEvent();
    

    private void Awake()
    {
        if (instance == null) instance = this;

        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        m_YesButton.onClick.AddListener(OnYesButtonClicked);
        m_NoButton.onClick.AddListener(OnNoButtonClicked);
    }

    public void ActivateConfirmationPanel(string ConfirmationMessage, ConfirmationType messageType, Action YesAction, Action NoAction)
    {
        m_VisibleSection.gameObject.SetActive(true);
        m_ConfirmationTextMessage.text = ConfirmationMessage;
        m_YesEvent.RemoveAllListeners();
        m_NoEvent.RemoveAllListeners();
        if(messageType == ConfirmationType.Question)
        {
            m_YesButton.gameObject.SetActive(true);
            m_NoButton.gameObject.SetActive(true);
            if (m_YesButtonText != null)
                m_YesButtonText.text = "YES";
        }
        else
        {
            m_YesButton.gameObject.SetActive(true);
            m_NoButton.gameObject.SetActive(false);
            if (m_YesButtonText != null)
                m_YesButtonText.text = "ALRIGHTY THEN";
        }
        if(YesAction != null)
        {
            m_YesEvent.AddListener(YesAction.Invoke);
        }
        if(NoAction != null)
        {
            m_NoEvent.AddListener(NoAction.Invoke);
        }
    }
    private void OnYesButtonClicked()
    {
        m_VisibleSection.gameObject.SetActive(false);
        m_YesEvent.Invoke();

    }

    private void OnNoButtonClicked()
    {
        m_VisibleSection.gameObject.SetActive(false);
        m_NoEvent.Invoke();

    }
}
