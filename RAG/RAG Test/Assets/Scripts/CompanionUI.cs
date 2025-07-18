using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private Transform chatContent;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Toggle ragToggle;
    [SerializeField] private TMP_InputField apiKeyInput;
    [SerializeField] private GameObject processingIndicator;
    [SerializeField] private Transform documentsPanel;
    
    [Header("Message Prefabs")]
    [SerializeField] private GameObject userMessagePrefab;
    [SerializeField] private GameObject companionMessagePrefab;
    [SerializeField] private GameObject errorMessagePrefab;
    [SerializeField] private GameObject documentItemPrefab;
    
    [Header("Settings")]
    [SerializeField] private int maxMessagesDisplayed = 50;
    // [SerializeField] private float autoScrollDelay = 0.1f; // Unused - commented out
    
    private List<GameObject> messageObjects = new List<GameObject>();
    private bool isProcessing = false;
    
    public event Action<string> OnUserMessageSubmitted;
    
    private void Start()
    {
        SetupUI();
        SetupEventHandlers();
    }
    
    private void SetupUI()
    {
        if (processingIndicator != null)
            processingIndicator.SetActive(false);
        
        if (chatPanel != null)
            chatPanel.SetActive(true);
        
        if (messageInput != null)
            messageInput.text = "";
        
        if (ragToggle != null)
            ragToggle.isOn = true;
    }
    
    private void SetupEventHandlers()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);
        
        if (clearButton != null)
            clearButton.onClick.AddListener(OnClearButtonClicked);
        
        if (messageInput != null)
            messageInput.onEndEdit.AddListener(OnMessageInputEndEdit);
        
        if (apiKeyInput != null)
            apiKeyInput.onEndEdit.AddListener(OnAPIKeyInputEndEdit);
        
        if (ragToggle != null)
            ragToggle.onValueChanged.AddListener(OnRAGToggleChanged);
    }
    
    private void OnSendButtonClicked()
    {
        SendCurrentMessage();
    }
    
    private void OnClearButtonClicked()
    {
        ClearConversation();
    }
    
    private void OnMessageInputEndEdit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendCurrentMessage();
        }
    }
    
    private void OnAPIKeyInputEndEdit(string apiKey)
    {
        var companion = FindFirstObjectByType<RAGCompanionController>();
        if (companion != null)
        {
            companion.SetAPIKey(apiKey);
        }
    }
    
    private void OnRAGToggleChanged(bool isEnabled)
    {
        var companion = FindFirstObjectByType<RAGCompanionController>();
        if (companion != null)
        {
            companion.ToggleRAG(isEnabled);
        }
    }
    
    private void SendCurrentMessage()
    {
        if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text) || isProcessing)
            return;
        
        string userMessage = messageInput.text.Trim();
        
        DisplayUserMessage(userMessage);
        
        OnUserMessageSubmitted?.Invoke(userMessage);
        
        messageInput.text = "";
        messageInput.Select();
        messageInput.ActivateInputField();
    }
    
    public void DisplayUserMessage(string message)
    {
        if (userMessagePrefab == null || chatContent == null)
            return;
        
        GameObject messageObj = Instantiate(userMessagePrefab, chatContent);
        SetupMessageObject(messageObj, message, "You");
        
        messageObjects.Add(messageObj);
        LimitMessageHistory();
        ScrollToBottom();
    }
    
    public void DisplayCompanionMessage(string message)
    {
        if (companionMessagePrefab == null || chatContent == null)
            return;
        
        GameObject messageObj = Instantiate(companionMessagePrefab, chatContent);
        SetupMessageObject(messageObj, message, "Assistant");
        
        messageObjects.Add(messageObj);
        LimitMessageHistory();
        ScrollToBottom();
    }
    
    public void DisplayErrorMessage(string message)
    {
        if (errorMessagePrefab == null || chatContent == null)
            return;
        
        GameObject messageObj = Instantiate(errorMessagePrefab, chatContent);
        SetupMessageObject(messageObj, message, "Error");
        
        messageObjects.Add(messageObj);
        LimitMessageHistory();
        ScrollToBottom();
    }
    
    private void SetupMessageObject(GameObject messageObj, string message, string sender)
    {
        var messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = $"{sender}: {message}";
        }
        
        var timeText = messageObj.transform.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
        if (timeText != null)
        {
            timeText.text = DateTime.Now.ToString("HH:mm");
        }
    }
    
    public void SetProcessingStatus(bool processing)
    {
        isProcessing = processing;
        
        if (processingIndicator != null)
            processingIndicator.SetActive(processing);
        
        if (sendButton != null)
            sendButton.interactable = !processing;
        
        if (messageInput != null)
            messageInput.interactable = !processing;
    }
    
    public void DisplayRetrievedDocuments(List<Document> documents)
    {
        if (documentsPanel == null || documentItemPrefab == null)
            return;
        
        ClearDocumentsPanel();
        
        foreach (var document in documents)
        {
            GameObject docObj = Instantiate(documentItemPrefab, documentsPanel);
            
            var titleText = docObj.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
                titleText.text = document.title;
            
            var contentText = docObj.transform.Find("Content")?.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                string truncatedContent = document.content.Length > 200 
                    ? document.content.Substring(0, 200) + "..."
                    : document.content;
                contentText.text = truncatedContent;
            }
            
            var scoreText = docObj.transform.Find("Score")?.GetComponent<TextMeshProUGUI>();
            if (scoreText != null)
                scoreText.text = $"Score: {document.relevanceScore:F2}";
        }
    }
    
    private void ClearDocumentsPanel()
    {
        if (documentsPanel == null)
            return;
        
        foreach (Transform child in documentsPanel)
        {
            Destroy(child.gameObject);
        }
    }
    
    public void ClearConversation()
    {
        foreach (var messageObj in messageObjects)
        {
            if (messageObj != null)
                Destroy(messageObj);
        }
        
        messageObjects.Clear();
        ClearDocumentsPanel();
        
        var companion = FindFirstObjectByType<RAGCompanionController>();
        if (companion != null)
        {
            companion.ClearConversation();
        }
    }
    
    private void LimitMessageHistory()
    {
        while (messageObjects.Count > maxMessagesDisplayed)
        {
            if (messageObjects[0] != null)
                Destroy(messageObjects[0]);
            messageObjects.RemoveAt(0);
        }
    }
    
    private void ScrollToBottom()
    {
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) && !isProcessing)
        {
            if (messageInput != null && messageInput.isFocused)
            {
                SendCurrentMessage();
            }
        }
    }
}