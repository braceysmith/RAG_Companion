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
    [SerializeField] private Button cameraButton;
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
    
    [Header("Image Preview UI")]
    [SerializeField] private GameObject imagePreviewPanel;
    [SerializeField] private Image previewImage;
    [SerializeField] private TMP_InputField imageDescriptionInput;
    [SerializeField] private Button sendImageButton;
    [SerializeField] private Button cancelImageButton;
    
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
    
    // Image preview state
    private string currentImageBase64;
    private Texture2D currentImageTexture;
    
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
        
        // Setup image upload button (gallery) - clear existing listeners first
        if (imageUploadButton != null)
        {
            imageUploadButton.onClick.RemoveAllListeners();
            imageUploadButton.onClick.AddListener(OnImageUploadButtonClicked);
            LogMessage($"✅ Gallery button listener assigned to: {imageUploadButton.name}");
        }
        else
        {
            LogError("❌ imageUploadButton is NULL in Unity Inspector!");
        }
        
        // Setup camera button - clear existing listeners first
        if (cameraButton != null)
        {
            cameraButton.onClick.RemoveAllListeners();
            cameraButton.onClick.AddListener(OnCameraButtonClicked);
            LogMessage($"✅ Camera button listener assigned to: {cameraButton.name}");
        }
        else
        {
            LogError("❌ cameraButton is NULL in Unity Inspector!");
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
        
        // Setup image preview buttons
        if (sendImageButton != null)
        { 
            sendImageButton.onClick.AddListener(OnSendImageButtonClicked);
            LogMessage($"✅ Send image button listener assigned to: {sendImageButton.name}");
        }
        else
        {
            LogMessage("⚠️  sendImageButton is NULL in Unity Inspector - image preview won't work");
        }
        
        if (cancelImageButton != null)
        {
            cancelImageButton.onClick.AddListener(OnCancelImageButtonClicked);
            LogMessage($"✅ Cancel image button listener assigned to: {cancelImageButton.name}");
        }
        else
        {
            LogMessage("⚠️  cancelImageButton is NULL in Unity Inspector - image preview won't work");
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
        LogMessage("🖼️ GALLERY BUTTON CLICKED - Starting gallery functionality");
        LogMessage($"🖼️ Gallery button handler called: OnImageUploadButtonClicked()");
        
        try
        {
            LogMessage($"🔍 Image upload requested - Current state: {currentState}");
            LogMessage("🖼️ About to start PickAndUploadImage coroutine");
            
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
    
    private void OnCameraButtonClicked()
    {
        LogMessage("📸 CAMERA BUTTON CLICKED - Starting camera functionality");
        LogMessage($"📸 Camera button handler called: OnCameraButtonClicked()");
        
        try
        {
            LogMessage($"🔍 Camera capture requested - Current state: {currentState}");
            LogMessage("📸 About to start CapturePhotoWithCamera coroutine");
            
            // For now, allow camera capture from any state for testing
            LogMessage("⚠️ Bypassing state check for camera capture testing");
            
            StartCoroutine(CapturePhotoWithCamera());
        }
        catch (System.Exception ex)
        {
            LogError($"Error in camera button handler: {ex.Message}");
            ShowError($"Camera capture error: {ex.Message}");
        }
    }
    
    private IEnumerator CapturePhotoWithCamera()
    {
        LogMessage("📸 CapturePhotoWithCamera coroutine started");
        LogMessage("📸 Starting camera photo capture...");
        
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        LogMessage("📸 Mobile platform detected - calling TakePhotoWithCamera");
        yield return StartCoroutine(TakePhotoWithCamera());
        #else
        // Editor/Desktop fallback - use demo image for testing
        LogMessage("📸 Editor/Desktop detected - Camera not available in editor, using demo image for testing");
        yield return StartCoroutine(LoadDemoImageForTesting());
        #endif
        
        LogMessage("📸 CapturePhotoWithCamera coroutine completed");
        yield return null;
    }
    
    private IEnumerator PickAndUploadImage()
    {
        LogMessage("🖼️ PickAndUploadImage coroutine started");
        LogMessage("🖼️ Starting gallery image picker...");
        
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        LogMessage("🖼️ Mobile platform detected - calling SelectImageFromGallery");
        yield return StartCoroutine(SelectImageFromGallery());
        #else
        // Editor/Desktop fallback - use demo image for testing
        LogMessage("🖼️ Editor/Desktop detected - Gallery picker not available in editor, using demo image for testing");
        yield return StartCoroutine(LoadDemoImageForTesting());
        #endif
        
        LogMessage("🖼️ PickAndUploadImage coroutine completed");
        yield return null;
    }
    
    private IEnumerator ShowImageSourceDialog()
    {
        LogMessage("📱 Showing image source selection...");
        
        // Use Unity Native Gallery to show source selection
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        
        // Show native dialog asking user to choose camera or gallery
        // For now, we'll use gallery as default - in future versions we can add
        // a custom UI dialog with Camera and Gallery buttons
        
        // Default to gallery for this implementation
        // Future enhancement: Add custom dialog with "Camera" and "Gallery" buttons
        yield return StartCoroutine(SelectImageFromGallery());
        
        #else
        LogMessage("Image picker not available in editor - using demo image");
        yield return StartCoroutine(LoadDemoImageForTesting());
        #endif
        
        yield return null;
    }
    
    private IEnumerator SelectImageFromGallery()
    {
        LogMessage("🖼️ SelectImageFromGallery coroutine started");
        LogMessage("📷 Opening gallery for image selection...");
        
        // Check if media picker is busy
        if (!NativeGallery.IsMediaPickerBusy())
        {
            LogMessage("🖼️ Media picker is available, proceeding with permission check");
            // Check permission first
            bool hasPermission = NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
            
            if (!hasPermission)
            {
                // Request permission asynchronously
                bool permissionRequested = false;
                NativeGallery.Permission requestResult = NativeGallery.Permission.Denied;
                
                NativeGallery.RequestPermissionAsync((result) =>
                {
                    requestResult = result;
                    permissionRequested = true;
                }, NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
                
                // Wait for permission request result
                while (!permissionRequested)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                
                hasPermission = (requestResult == NativeGallery.Permission.Granted);
            }
            
            if (hasPermission)
            {
                // Pick image from gallery
                bool imageSelected = false;
                string imagePath = null;
                
                LogMessage("🖼️ Calling NativeGallery.GetImageFromGallery...");
                NativeGallery.GetImageFromGallery((path) =>
                {
                    LogMessage($"🖼️ Gallery callback triggered with path: {path}");
                    imagePath = path;
                    imageSelected = true;
                    if (path != null)
                    {
                        LogMessage($"✅ Image selected successfully: {path}");
                    }
                    else
                    {
                        LogMessage("❌ Image selection cancelled or failed (path is null)");
                    }
                }, "Select Image", "image/*");
                
                LogMessage("🖼️ Waiting for user to finish selecting image...");
                
                // Wait for user selection
                while (!imageSelected && !NativeGallery.IsMediaPickerBusy())
                {
                    yield return new WaitForSeconds(0.1f);
                }
                
                LogMessage($"🖼️ Selection completed. imageSelected: {imageSelected}, imagePath: '{imagePath}'");
                
                // Process selected image
                if (!string.IsNullOrEmpty(imagePath))
                {
                    LogMessage($"🖼️ About to call ProcessSelectedImage with path: {imagePath}");
                    yield return StartCoroutine(ProcessSelectedImage(imagePath));
                    LogMessage("🖼️ ProcessSelectedImage coroutine completed");
                }
                else
                {
                    LogMessage("❌ No image selected - imagePath is null or empty");
                }
            }
            else
            {
                LogError("Gallery permission denied");
                ShowError("Gallery access permission is required to select images.");
            }
        }
        else
        {
            LogError("Media picker is busy");
            ShowError("Another media selection is in progress.");
        }
        
        yield return null;
    }
    
    private IEnumerator ProcessSelectedImage(string imagePath)
    {
        LogMessage($"🖼️ ProcessSelectedImage coroutine started with path: {imagePath}");
        LogMessage($"🖼️ Processing selected image: {imagePath}");
        
        // Load texture from file path
        if (System.IO.File.Exists(imagePath))
        {
            byte[] imageData = System.IO.File.ReadAllBytes(imagePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (texture.LoadImage(imageData))
            {
                LogMessage($"✅ Image loaded: {texture.width}x{texture.height}");
                
                // Store image data for preview
                currentImageTexture = texture;
                currentImageBase64 = System.Convert.ToBase64String(imageData);
                
                // Show image preview instead of immediately sending
                string fileName = System.IO.Path.GetFileName(imagePath);
                ShowImagePreviewPanel(texture, fileName);
                
                LogMessage("📱 Image preview shown - waiting for user input");
            }
            else
            {
                LogError("Failed to load image from file");
                ShowError("Failed to process selected image.");
            }
        }
        else
        {
            LogError($"Image file not found: {imagePath}");
            ShowError("Selected image file not found.");
        }
        
        yield return null;
    }
    
    private IEnumerator TakePhotoWithCamera()
    {
        LogMessage("📸 Opening camera for photo capture...");
        
        // Check if device has camera
        if (!NativeCamera.DeviceHasCamera())
        {
            LogError("Device does not have a camera");
            ShowError("Your device does not have a camera available.");
            yield break;
        }
        
        // Check if camera is busy
        if (NativeCamera.IsCameraBusy())
        {
            LogError("Camera is currently busy");
            ShowError("Camera is currently in use by another application.");
            yield break;
        }
        
        // Take picture with Unity Native Camera
        bool photoTaken = false;
        string photoPath = null;
        
        NativeCamera.TakePicture((path) =>
        {
            photoPath = path;
            photoTaken = true;
            
            if (path != null)
            {
                LogMessage($"✅ Photo captured successfully: {path}");
            }
            else
            {
                LogMessage("❌ Photo capture was cancelled or failed");
            }
        }, 1024); // Max size 1024px
        
        // Wait for photo capture to complete
        while (!photoTaken)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Process captured photo
        if (!string.IsNullOrEmpty(photoPath))
        {
            yield return StartCoroutine(ProcessCapturedPhoto(photoPath));
        }
        else
        {
            LogMessage("No photo was captured");
            ShowError("Photo capture was cancelled or failed.");
        }
        
        yield return null;
    }
    
    private IEnumerator ProcessCapturedPhoto(string photoPath)
    {
        LogMessage($"📷 Processing captured photo: {photoPath}");
        
        try
        {
            // Load the captured image using Unity Native Camera's helper method
            Texture2D texture = NativeCamera.LoadImageAtPath(photoPath, 1024, false);
            
            if (texture != null)
            {
                LogMessage($"✅ Photo loaded successfully: {texture.width}x{texture.height}");
                
                // Store image data for preview  
                currentImageTexture = texture;
                byte[] imageBytes = texture.EncodeToPNG();
                currentImageBase64 = System.Convert.ToBase64String(imageBytes);
                
                // Show image preview instead of immediately sending
                ShowImagePreviewPanel(texture, "Captured Photo");
                
                LogMessage("📱 Captured photo preview shown - waiting for user input");
            }
            else
            {
                LogError("Failed to load captured photo");
                ShowError("Failed to process the captured photo.");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error processing captured photo: {ex.Message}");
            ShowError($"Error processing photo: {ex.Message}");
        }
        
        yield return null;
    }
    
    #if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator TryNativeGalleryAndroid()
    {
        LogMessage("🤖 Attempting Android gallery access...");
        
        try 
        {
            // Try using Android Intent directly for gallery access
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.PICK");
                intent.Call<AndroidJavaObject>("setType", "image/*");
                
                currentActivity.Call("startActivity", intent);
                LogMessage("✅ Android gallery launched");
                
                // Note: In production, you'd need to handle the result callback
                ShowError("Gallery opened! In production, implement result callback to get selected image.");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Android gallery error: {ex.Message}");
            ShowError("Install Unity Native Gallery package for full Android support.");
        }
        
        yield return null;
    }
    
    private IEnumerator TryNativeCameraAndroid()
    {
        LogMessage("📷 Attempting Android camera access...");
        
        try
        {
            // Try using Android Intent for camera
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.provider.MediaStore.ACTION_IMAGE_CAPTURE");
                
                currentActivity.Call("startActivity", intent);
                LogMessage("✅ Android camera launched");
                
                ShowError("Camera opened! In production, implement result callback to get captured image.");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Android camera error: {ex.Message}");
            ShowError("Install Unity Native Gallery package for full Android camera support.");
        }
        
        yield return null;
    }
    #endif
    
    #if UNITY_IOS && !UNITY_EDITOR
    private IEnumerator TryNativeGalleryIOS()
    {
        LogMessage("🍎 Attempting iOS photo gallery access...");
        
        // iOS requires native plugin for gallery access
        // The Unity Native Gallery package handles this automatically
        LogMessage("iOS gallery access requires Unity Native Gallery package");
        ShowError("Install Unity Native Gallery package from Asset Store for iOS photo selection.");
        
        yield return null;
    }
    
    private IEnumerator TryNativeCameraIOS()
    {
        LogMessage("📷 Attempting iOS camera access...");
        
        // iOS requires native plugin for camera access
        LogMessage("iOS camera access requires Unity Native Gallery package");
        ShowError("Install Unity Native Gallery package from Asset Store for iOS camera access.");
        
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
        LogMessage("📷 Adding image to chat UI...");
        AddImageMessage(demoTexture, "Black and white checkerboard test pattern", true);
        LogMessage("✅ Image added to chat UI");
        
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
        
        var requestBody = new ImageAnalysisRequest
        {
            image_data = base64Image,
            question = "Please describe what you see in this image in detail.",
            user_id = userId
        };
        
        string jsonBody = JsonUtility.ToJson(requestBody);
        LogMessage($"📦 Request JSON: {jsonBody.Substring(0, Math.Min(200, jsonBody.Length))}...");
        
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
                LogMessage($"🔍 RAG server response: {request.downloadHandler.text}");
                
                try
                {
                    var response = JsonUtility.FromJson<ImageAnalysisResponse>(request.downloadHandler.text);
                    
                    if (response != null && !string.IsNullOrEmpty(response.description))
                    {
                        LogMessage($"📝 Got description from RAG: {response.description.Substring(0, Math.Min(100, response.description.Length))}...");
                        
                        // Send the analysis result through realtime chat for audio response
                        // Tell the AI to speak as if it can see the image
                        string analysisPrompt = $"Speak as if you can see the image you are describing: {response.description}";
                        
                        if (realtimeChat != null)
                        {
                            realtimeChat.SendTextMessage(analysisPrompt);
                        }
                    }
                    else
                    {
                        LogError($"No description received from RAG server. Response object: {response}, Description: '{response?.description}'");
                    }
                }
                catch (System.Exception ex)
                {
                    LogError($"Failed to parse RAG response: {ex.Message}");
                    LogMessage($"Raw response was: {request.downloadHandler.text}");
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
    private class ImageAnalysisRequest
    {
        public string image_data;
        public string question;
        public string user_id;
    }
    
    [System.Serializable]
    private class ImageAnalysisResponse
    {
        public bool success;
        public string description;
        public string error;
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
    
    // Image Preview Methods
    private void ShowImagePreviewPanel(Texture2D imageTexture, string imageName)
    {
        LogMessage($"📱 Showing image preview panel for: {imageName}");
        
        if (imagePreviewPanel != null)
        {
            // Set the preview image
            if (previewImage != null)
            {
                // Create sprite from texture for UI Image component
                Sprite imageSprite = Sprite.Create(imageTexture, 
                    new Rect(0, 0, imageTexture.width, imageTexture.height), 
                    new Vector2(0.5f, 0.5f));
                previewImage.sprite = imageSprite;
            }
            
            // Set placeholder text in input field
            if (imageDescriptionInput != null)
            {
                imageDescriptionInput.text = "";
                imageDescriptionInput.placeholder.GetComponent<TextMeshProUGUI>().text = 
                    "Describe what you want to know about this image...";
            }
            
            // Show the preview panel
            imagePreviewPanel.SetActive(true);
            LogMessage("✅ Image preview panel displayed");
        }
        else
        {
            LogError("❌ imagePreviewPanel is NULL - cannot show preview");
            // Fallback: send immediately without preview
            AddImageMessage(imageTexture, imageName, true);
            StartCoroutine(SendImageToRAGServer(currentImageBase64));
        }
    }
    
    private void OnSendImageButtonClicked()
    {
        LogMessage("✅ Send image button clicked");
        
        if (string.IsNullOrEmpty(currentImageBase64))
        {
            LogError("No image data available to send");
            return;
        }
        
        // Get user's description/question
        string userDescription = "";
        if (imageDescriptionInput != null)
        {
            userDescription = imageDescriptionInput.text.Trim();
        }
        
        // Add image to chat
        if (currentImageTexture != null)
        {
            string displayText = string.IsNullOrEmpty(userDescription) ? 
                "Image for analysis" : 
                $"Image: {userDescription}";
            AddImageMessage(currentImageTexture, displayText, true);
        }
        
        // Hide preview panel
        HideImagePreviewPanel();
        
        // Send to RAG server with user's description
        StartCoroutine(SendImageToRAGServerWithDescription(currentImageBase64, userDescription));
        
        LogMessage($"🚀 Image sent for analysis with description: '{userDescription}'");
    }
    
    private void OnCancelImageButtonClicked()
    {
        LogMessage("❌ Cancel image button clicked");
        
        // Hide preview panel
        HideImagePreviewPanel();
        
        // Clear stored image data
        ClearImagePreviewData();
        
        LogMessage("📱 Image preview cancelled");
    }
    
    private void HideImagePreviewPanel()
    {
        if (imagePreviewPanel != null)
        {
            imagePreviewPanel.SetActive(false);
            LogMessage("📱 Image preview panel hidden");
        }
    }
    
    private void ClearImagePreviewData()
    {
        currentImageBase64 = null;
        
        if (currentImageTexture != null)
        {
            DestroyImmediate(currentImageTexture);
            currentImageTexture = null;
        }
        
        if (imageDescriptionInput != null)
        {
            imageDescriptionInput.text = "";
        }
        
        LogMessage("🗑️ Image preview data cleared");
    }
    
    private IEnumerator SendImageToRAGServerWithDescription(string base64Image, string userDescription)
    {
        LogMessage($"📤 Sending image to RAG server with description: '{userDescription}'");
        
        // Determine the question to send
        string question = string.IsNullOrEmpty(userDescription) ? 
            "Please describe what you see in this image in detail." : 
            userDescription;
        
        // Get RAG API URL and userId from the realtime chat component using reflection
        if (realtimeChat == null)
        {
            LogError("Realtime chat component not available");
            ShowError("Unable to connect to analysis service.");
            yield break;
        }
        
        var ragApiUrlField = realtimeChat.GetType().GetField("ragApiUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var userIdField = realtimeChat.GetType().GetField("userId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        string ragApiUrl = ragApiUrlField?.GetValue(realtimeChat)?.ToString() ?? "";
        string userId = userIdField?.GetValue(realtimeChat)?.ToString() ?? "mobile-user";
        
        if (string.IsNullOrEmpty(ragApiUrl))
        {
            LogError("RAG API URL not available");
            ShowError("Unable to connect to image analysis service.");
            yield break;
        }
        
        // Use the existing SendImageToRAGServer logic with the custom question
        var requestBody = new ImageAnalysisRequest
        {
            image_data = base64Image,
            question = question,
            user_id = userId
        };
        
        string jsonData = JsonUtility.ToJson(requestBody);
        
        using (UnityWebRequest request = new UnityWebRequest($"{ragApiUrl}/analyze_image", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;
            
            LogMessage("📡 Sending image analysis request to RAG server...");
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<ImageAnalysisResponse>(request.downloadHandler.text);
                    
                    if (response != null && response.success && !string.IsNullOrEmpty(response.description))
                    {
                        LogMessage($"✅ Image analysis successful: {response.description.Substring(0, Math.Min(100, response.description.Length))}...");
                        
                        // Send the description to realtime chat for audio response
                        string analysisPrompt = $"Speak as if you can see the image you are describing: {response.description}";
                        if (realtimeChat != null)
                        {
                            realtimeChat.SendTextMessage(analysisPrompt);
                        }
                    }
                    else
                    {
                        LogError("Invalid response from RAG server");
                        ShowError("Failed to analyze image. Please try again.");
                    }
                }
                catch (System.Exception ex)
                {
                    LogError($"Error parsing image analysis response: {ex.Message}");
                    ShowError("Error processing image analysis response.");
                }
            }
            else
            {
                LogError($"Image analysis request failed: {request.error}");
                ShowError($"Image analysis failed: {request.error}");
            }
        }
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