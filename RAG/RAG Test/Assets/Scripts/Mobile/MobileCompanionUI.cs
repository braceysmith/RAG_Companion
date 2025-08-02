using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class MobileCompanionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform chatContainer;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button imageUploadButton;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private GameObject messagePrefab;
    
    [Header("Mobile UI Settings")]
    [SerializeField] private bool enableSwipeGestures = true;
    [SerializeField] private bool enableVoiceInput = true;
    [SerializeField] private bool enableHapticFeedback = true;
    [SerializeField] private float messageAnimationDuration = 0.3f;
    
    [Header("Voice Control UI")]
    [SerializeField] private Button voiceRecordButton;
    [SerializeField] private Image voiceButtonImage;
    [SerializeField] private TextMeshProUGUI voiceButtonText;
    [SerializeField] private GameObject recordingIndicator;
    [SerializeField] private GameObject processingIndicator;
    [SerializeField] private TextMeshProUGUI transcriptText;
    [SerializeField] private GameObject transcriptPanel;
    
    [Header("Audio Playback UI")]
    [SerializeField] private GameObject audioPlaybackIndicator;
    [SerializeField] private Image audioWaveform;
    [SerializeField] private TextMeshProUGUI audioStatusText;
    
    [Header("Status Indicators")]
    [SerializeField] private Image connectionStatusIcon;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject typingIndicator;
    [SerializeField] private GameObject offlineIndicator;
    
    [Header("Error Handling")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button closeErrorButton;
    
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
    
    // Voice state management
    private ConversationState currentState = ConversationState.Idle;
    private bool isRecording = false;
    private bool isProcessing = false;
    private bool isPlayingAudio = false;
    private bool pendingVoiceInputStart = false; // Track if user wants to start voice input
    private Coroutine connectionTimeoutCoroutine; // Track connection timeout
    
    // Component references
    private MobileRAGCompanionSystem companionSystem;
    private MobileAudioManager audioManager;
    private MobileRealtimeChat realtimeChat;
    
    // Voice button states
    private Color defaultButtonColor;
    private Color recordingButtonColor = Color.red;
    private Color processingButtonColor = Color.yellow;
    
    public enum ConversationState
    {
        Idle,
        Listening,
        Processing,
        Responding,
        PlayingAudio,
        Error
    }
    
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
        // Initialize component references
        companionSystem = FindFirstObjectByType<MobileRAGCompanionSystem>();
        audioManager = FindFirstObjectByType<MobileAudioManager>();
        realtimeChat = FindFirstObjectByType<MobileRealtimeChat>();
        
        // Configure for mobile
        ConfigureMobileLayout();
        
        // Store default button color
        if (voiceButtonImage != null)
        {
            defaultButtonColor = voiceButtonImage.color;
        }
        
        // Initialize voice UI states
        if (recordingIndicator != null)
            recordingIndicator.SetActive(false);
        if (processingIndicator != null)
            processingIndicator.SetActive(false);
        if (audioPlaybackIndicator != null)
            audioPlaybackIndicator.SetActive(false);
        if (transcriptPanel != null)
            transcriptPanel.SetActive(false);
        if (errorPanel != null)
            errorPanel.SetActive(false);
        
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
        
        // Setup voice button
        if (voiceRecordButton != null)
        {
            voiceRecordButton.onClick.AddListener(OnVoiceButtonClicked);
        }
        
        // Setup image upload button
        if (imageUploadButton != null)
        {
            imageUploadButton.onClick.AddListener(OnImageUploadButtonClicked);
        }
        
        // Setup error panel buttons
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryButtonClicked);
        }
        if (closeErrorButton != null)
        {
            closeErrorButton.onClick.AddListener(OnCloseErrorButtonClicked);
        }
        
        // Initialize status indicators
        UpdateConnectionStatus(false);
        UpdateOfflineStatus(false);
        UpdateStatusText("Ready - Tap to talk");
        UpdateVoiceButtonText("🎤 Talk");
        
        // Set initial state
        SetState(ConversationState.Idle);
        
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
        // Audio manager events
        if (audioManager != null)
        {
            audioManager.OnRecordingStarted += OnRecordingStarted;
            audioManager.OnRecordingStopped += OnRecordingStopped;
            audioManager.OnVoiceDetected += OnVoiceDetected;
            audioManager.OnVoiceEnded += OnVoiceEnded;
            audioManager.OnAudioPlaybackCompleted += OnAudioPlaybackCompleted;
            audioManager.OnError += OnAudioError;
        }
        
        // Companion system events
        if (companionSystem != null)
        {
            companionSystem.OnUserMessage += OnUserMessage;
            companionSystem.OnAssistantResponse += OnAssistantResponse;
            companionSystem.OnError += OnSystemError;
            companionSystem.OnSystemStatusChanged += OnSystemStatusChanged;
        }
        
        // Realtime chat events
        if (realtimeChat != null)
        {
            realtimeChat.OnConnectionEstablished += OnRealtimeConnected;
            realtimeChat.OnConnectionLost += OnRealtimeDisconnected;
            realtimeChat.OnTranscriptReceived += OnTranscriptReceived;
            realtimeChat.OnAIResponseReceived += OnAIResponseReceived;
            realtimeChat.OnError += OnRealtimeError;
        }
        
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
        
        LogMessage("Event handlers configured");
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
    
    public void AddImageMessage(Texture2D image, string caption, bool isUserMessage = false, bool animate = true)
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
            
            // Configure as image message
            ConfigureImageMessageObject(messageObj, image, caption, isUserMessage ? "user" : "assistant");
            
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
            
            LogMessage($"Image message added: {(isUserMessage ? "user" : "assistant")} - {caption}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to add image message: {ex.Message}");
        }
    }
    
    private void ConfigureImageMessageObject(GameObject messageObj, Texture2D image, string caption, string sender)
    {
        // Find or create image component
        Image imageComponent = messageObj.GetComponentInChildren<Image>();
        if (imageComponent == null)
        {
            // Create image GameObject as child
            GameObject imageChild = new GameObject("ImageDisplay");
            imageChild.transform.SetParent(messageObj.transform, false);
            imageComponent = imageChild.AddComponent<Image>();
            
            // Configure image layout
            RectTransform imageRect = imageChild.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0, 0.3f);
            imageRect.anchorMax = new Vector2(1, 1);
            imageRect.offsetMin = new Vector2(10, 0);
            imageRect.offsetMax = new Vector2(-10, -5);
        }
        
        // Set the image texture
        if (image != null)
        {
            Sprite imageSprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.5f));
            imageComponent.sprite = imageSprite;
            imageComponent.preserveAspect = true;
        }
        
        // Configure caption text
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = caption;
            
            // Move text to bottom of message
            RectTransform textRect = messageText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0.3f);
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, 0);
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
            
            // Make image messages taller
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, 200);
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
        
        // Send through realtime chat if connected, otherwise use traditional method
        if (realtimeChat != null && realtimeChat.IsConnected)
        {
            realtimeChat.SendTextMessage(message);
        }
        else
        {
            // Notify listeners (traditional method)
            OnUserMessageSubmitted?.Invoke(message);
        }
        
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
        LogError($"Showing error: {error}");
        
        if (errorMessageText != null)
        {
            errorMessageText.text = error;
        }
        
        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
        }
        
        AddMessage($"Error: {error}", "system");
        SetState(ConversationState.Error);
    }
    
    public void HideError()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
        
        SetState(ConversationState.Idle);
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
        LogMessage("OnDestroy called - cleaning up event handlers");
        
        try
        {
            // Cancel any pending connection timeout
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }
            
            // Remove event handlers
            if (audioManager != null)
            {
                audioManager.OnRecordingStarted -= OnRecordingStarted;
                audioManager.OnRecordingStopped -= OnRecordingStopped;
                audioManager.OnVoiceDetected -= OnVoiceDetected;
                audioManager.OnVoiceEnded -= OnVoiceEnded;
                audioManager.OnAudioPlaybackCompleted -= OnAudioPlaybackCompleted;
                audioManager.OnError -= OnAudioError;
            }
        
        if (companionSystem != null)
        {
            companionSystem.OnUserMessage -= OnUserMessage;
            companionSystem.OnAssistantResponse -= OnAssistantResponse;
            companionSystem.OnError -= OnSystemError;
            companionSystem.OnSystemStatusChanged -= OnSystemStatusChanged;
        }
        
        if (realtimeChat != null)
        {
            realtimeChat.OnConnectionEstablished -= OnRealtimeConnected;
            realtimeChat.OnConnectionLost -= OnRealtimeDisconnected;
            realtimeChat.OnTranscriptReceived -= OnTranscriptReceived;
            realtimeChat.OnAIResponseReceived -= OnAIResponseReceived;
            realtimeChat.OnError -= OnRealtimeError;
        }
        
        // Cleanup UI event handlers
        if (messageInput != null)
        {
            messageInput.onEndEdit.RemoveListener(OnMessageInputSubmitted);
        }
        
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendButtonClicked);
        }
        
        if (voiceRecordButton != null)
        {
            voiceRecordButton.onClick.RemoveListener(OnVoiceButtonClicked);
        }
        
            LogMessage("Mobile Companion UI destroyed successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during UI cleanup: {ex.Message}");
        }
    }
    
    // Voice Control Methods
    private void OnVoiceButtonClicked()
    {
        LogMessage($"Voice button clicked - Current state: {currentState}, pendingVoiceInputStart: {pendingVoiceInputStart}, realtimeChat.IsConnected: {(realtimeChat?.IsConnected ?? false)}");
        
        try
        {
            switch (currentState) 
            {
                case ConversationState.Idle:
                    StartVoiceRecording();
                    break;
                    
                case ConversationState.Listening:
                    StopVoiceRecording();
                    break;
                    
                case ConversationState.Processing:
                case ConversationState.Responding:
                case ConversationState.PlayingAudio:
                    // Allow interruption of AI during any response phase
                    InterruptAIAndStartListening();
                    break;
                    
                default:
                    LogMessage($"Voice button disabled in state: {currentState}");
                    break;
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error in voice button click handler: {ex.Message}\nStackTrace: {ex.StackTrace}");
            ShowError($"Voice button error: {ex.Message}");
            SetState(ConversationState.Idle);
        }
    }
    
    private void StartVoiceRecording()
    {
        LogMessage("StartVoiceRecording called");
        
        // Prefer WebRTC realtime chat for voice input
        if (realtimeChat != null && realtimeChat.IsConnected)
        {
            LogMessage("Realtime chat connected - starting voice input immediately");
            realtimeChat.StartVoiceInput();
            SetState(ConversationState.Listening);
            pendingVoiceInputStart = false;
        }
        else if (realtimeChat != null && !realtimeChat.IsConnected)
        {
            LogMessage("Realtime chat not connected - starting session first");
            // Mark that user wants to start voice input after connection
            pendingVoiceInputStart = true;
            realtimeChat.StartRealtimeSession();
            SetState(ConversationState.Processing);
            UpdateStatusText("Connecting to voice chat...");
            
            // Start connection timeout
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
            }
            connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine());
        }
        else if (companionSystem != null)
        {
            LogMessage("Using companion system fallback");
            // Fallback to companion system
            companionSystem.StartVoiceRecording();
            SetState(ConversationState.Listening);
            pendingVoiceInputStart = false;
        }
        else
        {
            ShowError("Voice system not available");
        }
    }
    
    private void StopVoiceRecording()
    {
        LogMessage("StopVoiceRecording called");
        
        try
        {
            // Prefer WebRTC realtime chat
            if (realtimeChat != null && realtimeChat.IsTalking)
            {
                LogMessage("Stopping realtime chat voice input");
                realtimeChat.StopVoiceInput();
                SetState(ConversationState.Processing);
            }
            else if (companionSystem != null)
            {
                LogMessage("Using companion system fallback to stop recording");
                // Fallback to companion system
                companionSystem.StopVoiceRecording();
                SetState(ConversationState.Processing);
            }
            else
            {
                LogMessage("No voice system available to stop");
                SetState(ConversationState.Idle);
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error stopping voice recording: {ex.Message}");
            ShowError($"Error stopping voice recording: {ex.Message}");
            SetState(ConversationState.Idle);
        }
    }
    
    private void InterruptAIAndStartListening()
    {
        LogMessage($"=== INTERRUPTION STARTED === Current state: {currentState}");
        
        // Set interruption flag to prevent unwanted state changes
        // (interruption tracking removed as it wasn't being used)
        
        try
        {
            // Stop any ongoing AI activity
            if (realtimeChat != null)
            {
                LogMessage($"Sending interruption to realtime chat - IsConnected: {realtimeChat.IsConnected}, IsAIResponding: {realtimeChat.IsAIResponding}");
                realtimeChat.InterruptAIResponse();
            }
            else
            {
                LogError("RealtimeChat is null - cannot interrupt");
            }
            
            // Stop audio playback if playing
            if (audioManager != null)
            {
                LogMessage("Stopping audio manager playback");
                audioManager.StopPlayback();
            }
            
            // Reset UI indicators immediately
            LogMessage("Resetting UI indicators");
            ShowProcessingIndicator(false);
            ShowAudioPlaybackIndicator(false);
            
            // Start listening immediately - this should NOT go to Processing state
            LogMessage($"Starting voice input after interruption - realtimeChat != null: {realtimeChat != null}, IsConnected: {realtimeChat?.IsConnected ?? false}");
            if (realtimeChat != null && realtimeChat.IsConnected)
            {
                LogMessage("Calling StartVoiceInput() directly for interruption");
                realtimeChat.StartVoiceInput();
                LogMessage("Setting state to Listening immediately");
                SetState(ConversationState.Listening);
                LogMessage("=== INTERRUPTION COMPLETE - NOW LISTENING ===");
            }
            else
            {
                LogError($"Cannot interrupt - voice chat not ready. realtimeChat: {realtimeChat != null}, IsConnected: {realtimeChat?.IsConnected ?? false}");
                ShowError("Cannot interrupt - voice chat not connected");
                SetState(ConversationState.Idle);
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error interrupting AI: {ex.Message}\nStackTrace: {ex.StackTrace}");
            ShowError($"Error interrupting AI: {ex.Message}");
            SetState(ConversationState.Idle);
        }
        finally
        {
            // Clear interruption flag
            // (interruption tracking removed as it wasn't being used)
        }
    }
    
    private void StopAudioPlayback()
    {
        if (audioManager != null)
        {
            audioManager.StopPlayback();
            SetState(ConversationState.Idle);
        }
    }
    
    // State Management
    private void SetState(ConversationState newState)
    {
        ConversationState previousState = currentState;
        currentState = newState;
        
        // Get stack trace to see what's calling this
        var stackTrace = System.Environment.StackTrace;
        var callerInfo = stackTrace.Split('\n')[1].Trim(); // Get the immediate caller
        
        LogMessage($"State changed: {previousState} → {newState} | Called by: {callerInfo}");
        
        UpdateUIForState(newState);
    }
    
    private void UpdateUIForState(ConversationState state)
    {
        // Reset all indicators
        ShowRecordingIndicator(false);
        ShowProcessingIndicator(false);
        ShowAudioPlaybackIndicator(false);
        
        switch (state)
        {
            case ConversationState.Idle:
                UpdateStatusText("Ready - Tap to talk");
                UpdateVoiceButtonText("MIC Talk");
                SetVoiceButtonColor(defaultButtonColor);
                SetVoiceButtonEnabled(true);
                break;
                
            case ConversationState.Listening:
                UpdateStatusText("Listening... Speak now");
                UpdateVoiceButtonText("LISTENING");
                SetVoiceButtonColor(recordingButtonColor);
                SetVoiceButtonEnabled(true);
                ShowRecordingIndicator(true);
                break;
                
            case ConversationState.Processing:
                UpdateStatusText("Processing voice... Tap to interrupt");
                UpdateVoiceButtonText("INTERRUPT");
                SetVoiceButtonColor(processingButtonColor);
                SetVoiceButtonEnabled(true); // Allow interruption
                ShowProcessingIndicator(true);
                break;
                
            case ConversationState.Responding:
                UpdateStatusText("AI is thinking... Tap to interrupt");
                UpdateVoiceButtonText("INTERRUPT");
                SetVoiceButtonColor(processingButtonColor);
                SetVoiceButtonEnabled(true); // Allow interruption
                ShowProcessingIndicator(true);
                break;
                
            case ConversationState.PlayingAudio:
                UpdateStatusText("AI is speaking... Tap to interrupt");
                UpdateVoiceButtonText("INTERRUPT");
                SetVoiceButtonColor(Color.red); // Red for interruption
                SetVoiceButtonEnabled(true);
                ShowAudioPlaybackIndicator(true);
                break;
                
            case ConversationState.Error:
                UpdateStatusText("Error occurred - Check details");
                UpdateVoiceButtonText("ERROR");
                SetVoiceButtonColor(Color.red);
                SetVoiceButtonEnabled(true);
                break;
        }
    }
    
    // UI Update Methods
    public void UpdateStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }
    
    private void UpdateVoiceButtonText(string text)
    {
        if (voiceButtonText != null)
        {
            voiceButtonText.text = text;
        }
    }
    
    private void SetVoiceButtonColor(Color color)
    {
        if (voiceButtonImage != null)
        {
            voiceButtonImage.color = color;
        }
    }
    
    private void SetVoiceButtonEnabled(bool enabled)
    {
        if (voiceRecordButton != null)
        {
            voiceRecordButton.interactable = enabled;
        }
    }
    
    // Voice Indicator Methods
    public void ShowRecordingIndicator(bool show = true)
    {
        if (recordingIndicator != null)
        {
            recordingIndicator.SetActive(show);
            isRecording = show;
        }
    }
    
    public void ShowProcessingIndicator(bool show = true)
    {
        if (processingIndicator != null)
        {
            processingIndicator.SetActive(show);
            isProcessing = show;
        }
    }
    
    public void ShowAudioPlaybackIndicator(bool show = true)
    {
        if (audioPlaybackIndicator != null)
        {
            audioPlaybackIndicator.SetActive(show);
            isPlayingAudio = show;
        }
        
        if (audioStatusText != null)
        {
            audioStatusText.text = show ? "AI Speaking..." : "";
        }
    }
    
    // Transcript Display
    public void ShowTranscript(string transcript)
    {
        if (transcriptText != null && !string.IsNullOrEmpty(transcript))
        {
            transcriptText.text = $"You said: \"{transcript}\"";
            
            if (transcriptPanel != null)
            {
                transcriptPanel.SetActive(true);
                StartCoroutine(HideTranscriptAfterDelay(3f));
            }
        }
        
        LogMessage($"Transcript displayed: {transcript}");
    }
    
    private IEnumerator HideTranscriptAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (transcriptPanel != null)
        {
            transcriptPanel.SetActive(false);
        }
    }
    
    // Error Handling
    private void OnRetryButtonClicked()
    {
        HideError();
        LogMessage("Retry button clicked");
    }
    
    private void OnCloseErrorButtonClicked()
    {
        HideError();
        LogMessage("Close error button clicked");
    }
    
    // Image Upload Handling
    private void OnImageUploadButtonClicked()
    {
        LogMessage("🖼️ Image upload button clicked");
        
        try
        {
            LogMessage($"🔍 Image upload requested - Current state: {currentState}");
            
            // For now, allow image upload from any state for testing
            LogMessage("⚠️ Bypassing state check for image upload testing");
            
            StartCoroutine(PickAndUploadImage());
        }
        catch (System.Exception ex)
        {
            LogError($"Error in image upload button handler: {ex.Message}");
            ShowError($"Image upload error: {ex.Message}");
        }
    }
    
    private IEnumerator PickAndUploadImage()
    {
        LogMessage("Starting image picker...");
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        // Android image picker
        yield return StartCoroutine(PickImageAndroid());
        #elif UNITY_IOS && !UNITY_EDITOR
        // iOS image picker
        yield return StartCoroutine(PickImageIOS());
        #else
        // Editor/Desktop fallback - use demo image for testing
        LogMessage("Image picker not available in editor - using demo image for testing");
        yield return StartCoroutine(LoadDemoImageForTesting());
        #endif
        
        yield return null;
    }
    
    #if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator PickImageAndroid()
    {
        // Android implementation using Intent (requires native plugin in production)
        // For demonstration, show how this would work
        LogMessage("Android image picker would launch here");
        
        // In production, you would:
        // 1. Install Unity Native Gallery package from Asset Store
        // 2. Or create custom Android plugin with Java code like:
        // Intent intent = new Intent(Intent.ACTION_PICK, MediaStore.Images.Media.EXTERNAL_CONTENT_URI);
        // UnityPlayer.currentActivity.startActivityForResult(intent, PICK_IMAGE_REQUEST);
        
        ShowError("Image picking ready for Android deployment. Install Unity Native Gallery package for full functionality.");
        yield return null;
    }
    #endif
    
    #if UNITY_IOS && !UNITY_EDITOR
    private IEnumerator PickImageIOS()
    {
        // iOS implementation using UIImagePickerController (requires native plugin in production)
        // For demonstration, show how this would work
        LogMessage("iOS image picker would launch here");
        
        // In production, you would:
        // 1. Install Unity Native Gallery package from Asset Store
        // 2. Or create custom iOS plugin with Objective-C code like:
        // UIImagePickerController *picker = [[UIImagePickerController alloc] init];
        // picker.sourceType = UIImagePickerControllerSourceTypePhotoLibrary;
        
        ShowError("Image picking ready for iOS deployment. Install Unity Native Gallery package for full functionality.");
        yield return null;
    }
    #endif
    
    private IEnumerator LoadDemoImageForTesting()
    {
        LogMessage("Creating test pattern for AI analysis...");
        
        // Create a simple recognizable checkerboard pattern
        Texture2D demoTexture = new Texture2D(128, 128);
        Color[] colors = new Color[128 * 128];
        
        // Create checkerboard pattern with clear description
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                bool isBlack = ((x / 16) + (y / 16)) % 2 == 0;
                colors[y * 128 + x] = isBlack ? Color.black : Color.white;
            }
        }
        
        demoTexture.SetPixels(colors);
        demoTexture.Apply();
        
        // Wait a frame to ensure texture is ready
        yield return null;
        
        // Convert to base64 for analysis
        byte[] imageBytes = null;
        try
        {
            imageBytes = demoTexture.EncodeToPNG();
        }
        catch (System.Exception ex)
        {
            LogError($"Failed to encode texture to PNG: {ex.Message}");
            yield break;
        }
        
        string base64Image = System.Convert.ToBase64String(imageBytes);
        
        LogMessage($"✅ Checkerboard pattern created: {imageBytes.Length} bytes, base64 length: {base64Image.Length}");
        
        // Add to chat first
        AddImageMessage(demoTexture, "Black and white checkerboard test pattern", true);
        
        // Wait another frame before sending for analysis
        yield return null;
        
        // Send image to RAG server for analysis (not directly to realtime chat)
        if (realtimeChat != null)
        {
            StartCoroutine(SendImageToRAGServer(base64Image));
        }
        else
        {
            LogError("RAG server not available for image analysis");
        }
        
        LogMessage("🚀 Checkerboard pattern sent for AI analysis");
    }
    
    private IEnumerator SendImageToRAGServer(string base64Image)
    {
        LogMessage("📡 Sending image to RAG server for analysis...");
        
        // Get RAG API URL from the realtime chat component using reflection
        var ragApiUrlField = realtimeChat.GetType().GetField("ragApiUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var userIdField = realtimeChat.GetType().GetField("userId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        string ragApiUrl = ragApiUrlField?.GetValue(realtimeChat)?.ToString() ?? "";
        string userId = userIdField?.GetValue(realtimeChat)?.ToString() ?? "mobile-user";
        
        if (string.IsNullOrEmpty(ragApiUrl))
        {
            LogError("RAG API URL not configured");
            yield break;
        }
        
        var requestBody = new 
        {
            image_data = base64Image,
            question = "Please describe what you see in this image in detail.",
            user_id = userId
        };
        
        string jsonBody = JsonUtility.ToJson(requestBody);
        
        using (UnityWebRequest request = new UnityWebRequest($"{ragApiUrl}/analyze_image", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                LogMessage("✅ RAG server processed image successfully");
                
                var response = JsonUtility.FromJson<ImageAnalysisResponse>(request.downloadHandler.text);
                
                if (!string.IsNullOrEmpty(response.description))
                {
                    // Send the analysis result through realtime chat for audio response
                    string analysisPrompt = $"The user just uploaded an image. Here's what I can see in it: {response.description}. Please respond naturally as if you're looking at the image they shared.";
                    
                    if (realtimeChat != null)
                    {
                        realtimeChat.SendTextMessage(analysisPrompt);
                    }
                }
                else
                {
                    LogError("No description received from RAG server");
                }
            }
            else
            {
                LogError($"RAG server image analysis failed: {request.error}");
                ShowError($"Image analysis failed: {request.error}");
            }
        }
    }
    
    [System.Serializable]
    private class ImageAnalysisResponse
    {
        public bool success;
        public string description;
        public string error;
    }
    
    private IEnumerator ProcessSelectedImage(string imagePath)
    {
        LogMessage($"Processing selected image: {imagePath}");
        
        try
        {
            // Load the image as texture
            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (texture.LoadImage(imageBytes))
            {
                // Convert to base64 for API
                string base64Image = System.Convert.ToBase64String(imageBytes);
                
                // Add image to chat UI
                AddImageMessage(texture, "Image uploaded for analysis", true); // true = user message
                
                // Send to realtime chat for analysis
                if (realtimeChat != null)
                {
                    realtimeChat.AnalyzeUploadedImage(base64Image);
                }
                else
                {
                    ShowError("Realtime chat not available for image analysis");
                }
                
                LogMessage("Image uploaded and sent for analysis");
            }
            else
            {
                LogError("Failed to load image texture");
                ShowError("Failed to load the selected image");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error processing image: {ex.Message}");
            ShowError($"Error processing image: {ex.Message}");
        }
        
        yield return null;
    }
    
    // Audio Manager Event Handlers
    private void OnRecordingStarted()
    {
        LogMessage("Recording started event received");
        SetState(ConversationState.Listening);
    }
    
    private void OnRecordingStopped()
    {
        LogMessage("Recording stopped event received");
        SetState(ConversationState.Processing);
    }
    
    private void OnVoiceDetected()
    {
        LogMessage("Voice detected event received");
        UpdateStatusText("Voice detected - Keep talking...");
        
        // Add visual feedback for voice detection
        if (recordingIndicator != null)
        {
            StartCoroutine(PulseRecordingIndicator());
        }
    }
    
    private void OnVoiceEnded()
    {
        LogMessage("Voice ended event received");
        UpdateStatusText("Voice ended - Processing...");
    }
    
    private void OnAudioPlaybackCompleted()
    {
        LogMessage("Audio playback completed event received");
        SetState(ConversationState.Idle);
    }
    
    private void OnAudioError(string error)
    {
        ShowError($"Audio error: {error}");
    }
    
    // Companion System Event Handlers
    private void OnUserMessage(string message)
    {
        LogMessage($"User message received: {message}");
        if (message != "[Voice Input]")
        {
            AddMessage(message, "user");
        }
    }
    
    private void OnAssistantResponse(string response)
    {
        LogMessage($"Assistant response received: {response.Substring(0, Math.Min(50, response.Length))}...");
        AddMessage(response, "assistant");
        SetState(ConversationState.PlayingAudio);
    }
    
    private void OnSystemError(string error)
    {
        ShowError($"System error: {error}");
    }
    
    private void OnSystemStatusChanged(bool isReady)
    {
        LogMessage($"System status changed: {(isReady ? "Ready" : "Not Ready")}");
        
        if (!isReady)
        {
            UpdateStatusText("System initializing...");
            SetVoiceButtonEnabled(false);
        }
        else if (currentState == ConversationState.Idle)
        {
            UpdateStatusText("Ready - Tap to talk");
            SetVoiceButtonEnabled(true);
        }
    }
    
    // Visual Effects
    private IEnumerator PulseRecordingIndicator()
    {
        if (recordingIndicator == null) yield break;
        
        Image indicatorImage = recordingIndicator.GetComponent<Image>();
        if (indicatorImage == null) yield break;
        
        Color originalColor = indicatorImage.color;
        Color pulseColor = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        
        float duration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < duration && isRecording)
        {
            float progress = elapsed / duration;
            float alpha = Mathf.Lerp(0.5f, 1f, Mathf.Sin(progress * Mathf.PI));
            
            indicatorImage.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, alpha);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (indicatorImage != null)
        {
            indicatorImage.color = originalColor;
        }
    }
    
    // Realtime Chat Event Handlers
    private void OnRealtimeConnected()
    {
        LogMessage($"Realtime chat connected - pendingVoiceInputStart: {pendingVoiceInputStart}, currentState: {currentState}");
        
        // Cancel connection timeout
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }
        
        // If user was waiting for connection to start voice input, do it now
        if (pendingVoiceInputStart && currentState == ConversationState.Processing)
        {
            LogMessage("Connection established - starting pending voice input after delay");
            pendingVoiceInputStart = false;
            
            // Wait a moment for session configuration to complete, then start voice input
            StartCoroutine(StartVoiceInputAfterDelay());
        }
        else
        {
            // Normal connection without pending voice input
            UpdateStatusText("Voice chat ready - Tap to talk");
            SetVoiceButtonEnabled(true);
            
            if (currentState == ConversationState.Processing)
            {
                SetState(ConversationState.Idle);
            }
        }
    }
    
    private void OnRealtimeDisconnected()
    {
        LogMessage("Realtime chat disconnected");
        UpdateStatusText("Voice chat disconnected");
        
        if (currentState == ConversationState.Listening || currentState == ConversationState.PlayingAudio)
        {
            SetState(ConversationState.Error);
        }
    }
    
    private void OnTranscriptReceived(string transcript)
    {
        LogMessage($"Transcript received: {transcript}");
        ShowTranscript(transcript);
        
        // The transcript is already added to chat by the realtime chat component
        // We just need to update the UI state
        if (currentState == ConversationState.Processing)
        {
            SetState(ConversationState.Responding);
        }
    }
    
    private void OnAIResponseReceived(string response)
    {
        LogMessage($"AI response received: {response.Substring(0, Math.Min(50, response.Length))}...");
        
        // The response is already added to chat by the realtime chat component
        // We just need to update the UI state
        if (currentState == ConversationState.Responding)
        {
            SetState(ConversationState.PlayingAudio);
        }
    }
    
    private void OnRealtimeError(string error)
    {
        LogError($"Realtime chat error: {error}");
        ShowError($"Voice chat error: {error}");
        
        // Reset pending voice input on error
        if (pendingVoiceInputStart)
        {
            pendingVoiceInputStart = false;
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }
        }
    }
    
    // Connection timeout handling
    private IEnumerator ConnectionTimeoutCoroutine()
    {
        yield return new WaitForSeconds(10f); // 10 second timeout
        
        if (pendingVoiceInputStart && currentState == ConversationState.Processing)
        {
            LogError("Connection timeout - resetting to idle state");
            pendingVoiceInputStart = false;
            connectionTimeoutCoroutine = null;
            
            ShowError("Connection timeout. Please try again.");
            SetState(ConversationState.Idle);
        }
    }
    
    private IEnumerator StartVoiceInputAfterDelay()
    {
        LogMessage("Waiting for session configuration to complete...");
        yield return new WaitForSeconds(0.5f); // Wait 500ms for session config
        
        if (realtimeChat != null && realtimeChat.IsConnected)
        {
            LogMessage("Starting voice input after connection delay");
            realtimeChat.StartVoiceInput();
            SetState(ConversationState.Listening);
        }
        else
        {
            LogError("Realtime chat not ready after delay");
            ShowError("Voice chat not ready. Please try again.");
            SetState(ConversationState.Idle);
        }
    }
    
    // Public methods for external state management
    public void ResetToIdleState()
    {
        LogMessage("Resetting UI to idle state for next conversation turn");
        SetState(ConversationState.Idle);
    }
    
    public void SetToPlayingAudioState()
    {
        LogMessage("Setting UI to PlayingAudio state - INTERRUPT button should be visible");
        SetState(ConversationState.PlayingAudio);
    }
    
    public void SetToRespondingState()
    {
        LogMessage("Setting UI to Responding state - INTERRUPT button should be visible");
        SetState(ConversationState.Responding);
    }
    
    // Public getters
    public bool IsTyping => isTyping;
    public int MessageCount => messageObjects.Count;
    public bool IsVoiceInputEnabled => enableVoiceInput;
    public bool IsHapticFeedbackEnabled => enableHapticFeedback;
    public bool IsRecording => isRecording;
    public bool IsProcessing => isProcessing;
    public bool IsPlayingAudio => isPlayingAudio;
    public ConversationState CurrentState => currentState;
}