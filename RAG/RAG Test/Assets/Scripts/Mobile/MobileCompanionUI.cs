using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MobileCompanionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform chatContainer;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private GameObject messagePrefab;
    
    [Header("Mobile UI Settings")]
    [SerializeField] private bool enableSwipeGestures = true;
    [SerializeField] private bool enableVoiceInput = true;
    [SerializeField] private bool enableHapticFeedback = true;
    [SerializeField] private float messageAnimationDuration = 0.3f;
    
    [Header("Status Indicators")]
    [SerializeField] private Image connectionStatusIcon;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject typingIndicator;
    [SerializeField] private GameObject offlineIndicator;
    
    [Header("Colors")]
    [SerializeField] private Color userMessageColor = Color.blue;
    [SerializeField] private Color assistantMessageColor = Color.gray;
    [SerializeField] private Color systemMessageColor = Color.yellow;
    [SerializeField] private Color onlineColor = Color.green;
    [SerializeField] private Color offlineColor = Color.red;
    
    // Message management
    private List<GameObject> messageObjects = new List<GameObject>();
    private int maxVisibleMessages = 50;
    private bool isTyping = false;
    
    // Events
    public event Action<string> OnUserMessageSubmitted;
    public event Action<Dictionary<string, object>> OnSettingsChanged;
    public event Action OnVoiceInputRequested;
    public event Action OnUIHidden;
    public event Action OnUIShown;
    
    private void Start()
    {
        InitializeMobileUI();
        SetupEventHandlers();
    }
    
    private void InitializeMobileUI()
    {
        // Configure for mobile
        ConfigureMobileLayout();
        
        // Setup message input
        if (messageInput != null)
        {
            messageInput.onEndEdit.AddListener(OnMessageInputSubmitted);
            messageInput.placeholder.GetComponent<TextMeshProUGUI>().text = "Type your message...";
        }
        
        // Setup send button
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }
        
        // Initialize status indicators
        UpdateConnectionStatus(false);
        UpdateOfflineStatus(false);
        
        LogMessage("Mobile UI initialized");
    }
    
    private void ConfigureMobileLayout()
    {
        // Configure for mobile screen sizes
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }
        
        // Configure safe area
        ConfigureSafeArea();
    }
    
    private void ConfigureSafeArea()
    {
        // Handle mobile safe areas (notches, etc.)
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
            
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
    
    private void SetupEventHandlers()
    {
        // Touch and gesture handling
        if (enableSwipeGestures)
        {
            SetupSwipeGestures();
        }
        
        // Voice input handling
        if (enableVoiceInput)
        {
            SetupVoiceInput();
        }
        
        // Haptic feedback
        if (enableHapticFeedback)
        {
            SetupHapticFeedback();
        }
    }
    
    private void SetupSwipeGestures()
    {
        // Add swipe gesture detection
        // This would integrate with Unity's Input System for touch
        LogMessage("Swipe gestures enabled");
    }
    
    private void SetupVoiceInput()
    {
        // Setup voice input button or gesture
        LogMessage("Voice input enabled");
    }
    
    private void SetupHapticFeedback()
    {
        // Configure haptic feedback for mobile
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android haptic feedback
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS haptic feedback
#endif
        LogMessage("Haptic feedback enabled");
    }
    
    public void AddMessage(string message, string sender = "user", bool animate = true)
    {
        if (chatContainer == null || messagePrefab == null)
        {
            LogError("UI components not configured");
            return;
        }
        
        try
        {
            // Create message object
            GameObject messageObj = Instantiate(messagePrefab, chatContainer);
            
            // Configure message
            ConfigureMessageObject(messageObj, message, sender);
            
            // Add to list
            messageObjects.Add(messageObj);
            
            // Animate if requested
            if (animate)
            {
                AnimateMessageAppearance(messageObj);
            }
            
            // Cleanup old messages
            CleanupOldMessages();
            
            // Auto-scroll to bottom
            ScrollToBottom();
            
            // Haptic feedback
            if (enableHapticFeedback)
            {
                TriggerHapticFeedback();
            }
            
            LogMessage($"Message added: {sender} - {message.Substring(0, Math.Min(50, message.Length))}...");
        }
        catch (Exception ex)
        {
            LogError($"Failed to add message: {ex.Message}");
        }
    }
    
    private void ConfigureMessageObject(GameObject messageObj, string message, string sender)
    {
        // Configure message bubble
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message;
        }
        
        // Configure message appearance based on sender
        Image backgroundImage = messageObj.GetComponent<Image>();
        if (backgroundImage != null)
        {
            switch (sender.ToLower())
            {
                case "user":
                    backgroundImage.color = userMessageColor;
                    break;
                case "assistant":
                    backgroundImage.color = assistantMessageColor;
                    break;
                case "system":
                    backgroundImage.color = systemMessageColor;
                    break;
                default:
                    backgroundImage.color = assistantMessageColor;
                    break;
            }
        }
        
        // Configure message alignment
        RectTransform rectTransform = messageObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            if (sender.ToLower() == "user")
            {
                // Align user messages to the right
                rectTransform.anchorMin = new Vector2(0.3f, 0);
                rectTransform.anchorMax = new Vector2(1f, 1);
            }
            else
            {
                // Align assistant messages to the left
                rectTransform.anchorMin = new Vector2(0f, 0);
                rectTransform.anchorMax = new Vector2(0.7f, 1);
            }
        }
    }
    
    private void AnimateMessageAppearance(GameObject messageObj)
    {
        // Simple scale animation using Unity's built-in animation
        if (messageObj != null)
        {
            StartCoroutine(AnimateScale(messageObj));
        }
    }
    
    private System.Collections.IEnumerator AnimateScale(GameObject messageObj)
    {
        if (messageObj == null) yield break;
        
        Transform transform = messageObj.transform;
        transform.localScale = Vector3.zero;
        
        float elapsed = 0f;
        while (elapsed < messageAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / messageAnimationDuration;
            
            // Ease out back animation
            float scale = EaseOutBack(progress);
            transform.localScale = Vector3.one * scale;
            
            yield return null;
        }
        
        transform.localScale = Vector3.one;
    }
    
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    
    private void CleanupOldMessages()
    {
        while (messageObjects.Count > maxVisibleMessages)
        {
            GameObject oldMessage = messageObjects[0];
            messageObjects.RemoveAt(0);
            
            if (oldMessage != null)
            {
                Destroy(oldMessage);
            }
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
    
    private void TriggerHapticFeedback()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android haptic feedback
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        
        if (vibrator != null)
        {
            vibrator.Call("vibrate", 50); // Short vibration
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS haptic feedback
        Handheld.Vibrate();
#endif
    }
    
    private void OnMessageInputSubmitted(string message)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessage();
        }
    }
    
    private void OnSendButtonClicked()
    {
        SendMessage();
    }
    
    private void SendMessage()
    {
        if (messageInput == null)
            return;
        
        string message = messageInput.text.Trim();
        if (string.IsNullOrEmpty(message))
            return;
        
        // Clear input
        messageInput.text = "";
        
        // Add user message to chat
        AddMessage(message, "user");
        
        // Show typing indicator
        ShowTypingIndicator(true);
        
        // Notify listeners
        OnUserMessageSubmitted?.Invoke(message);
        
        // Haptic feedback
        if (enableHapticFeedback)
        {
            TriggerHapticFeedback();
        }
        
        LogMessage($"User message sent: {message}");
    }
    
    public void ShowTypingIndicator(bool show)
    {
        if (typingIndicator != null)
        {
            typingIndicator.SetActive(show);
            isTyping = show;
        }
        
        if (show)
        {
            ScrollToBottom();
        }
    }
    
    public void UpdateConnectionStatus(bool isOnline)
    {
        if (connectionStatusIcon != null)
        {
            connectionStatusIcon.color = isOnline ? onlineColor : offlineColor;
        }
        
        if (statusText != null)
        {
            statusText.text = isOnline ? "Online" : "Offline";
            statusText.color = isOnline ? onlineColor : offlineColor;
        }
    }
    
    public void UpdateOfflineStatus(bool isOffline)
    {
        if (offlineIndicator != null)
        {
            offlineIndicator.SetActive(isOffline);
        }
    }
    
    public void ShowSystemMessage(string message)
    {
        AddMessage(message, "system");
    }
    
    public void ShowError(string error)
    {
        AddMessage($"Error: {error}", "system");
    }
    
    public void ClearChat()
    {
        foreach (GameObject messageObj in messageObjects)
        {
            if (messageObj != null)
            {
                Destroy(messageObj);
            }
        }
        
        messageObjects.Clear();
        LogMessage("Chat cleared");
    }
    
    public void ShowSettings()
    {
        // Show mobile settings panel
        var settings = new Dictionary<string, object>
        {
            ["enableVoiceInput"] = enableVoiceInput,
            ["enableHapticFeedback"] = enableHapticFeedback,
            ["enableSwipeGestures"] = enableSwipeGestures
        };
        
        OnSettingsChanged?.Invoke(settings);
    }
    
    public void ToggleVoiceInput()
    {
        enableVoiceInput = !enableVoiceInput;
        OnVoiceInputRequested?.Invoke();
    }
    
    public void HideUI()
    {
        gameObject.SetActive(false);
        OnUIHidden?.Invoke();
    }
    
    public void ShowUI()
    {
        gameObject.SetActive(true);
        OnUIShown?.Invoke();
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileCompanionUI] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileCompanionUI] {message}");
    }
    
    private void OnDestroy()
    {
        // Cleanup
        if (messageInput != null)
        {
            messageInput.onEndEdit.RemoveListener(OnMessageInputSubmitted);
        }
        
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendButtonClicked);
        }
    }
    
    // Public getters
    public bool IsTyping => isTyping;
    public int MessageCount => messageObjects.Count;
    public bool IsVoiceInputEnabled => enableVoiceInput;
    public bool IsHapticFeedbackEnabled => enableHapticFeedback;
}