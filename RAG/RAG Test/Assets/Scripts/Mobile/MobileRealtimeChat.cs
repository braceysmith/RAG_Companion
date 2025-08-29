using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Unity.WebRTC;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MobileRealtimeChat : MonoBehaviour
{
    [Header("OpenAI Realtime Settings")]
    [SerializeField] private string realtimeModel = "gpt-4o-realtime-preview-2024-10-01";
    
    [Header("ICE Servers")]
    [SerializeField] private string iceServers = "stun:stun.l.google.com:19302,stun:stun1.l.google.com:19302";
    
    [Header("RAG Integration")]
    [SerializeField] private string ragApiUrl = "https://ragcompanion-production-bf25.up.railway.app";
    [SerializeField] private string userId = "mobile-user";
    [SerializeField] private bool enableRAGContext = true; // Now enabled for conversation memory
    [SerializeField] private int maxRAGResults = 3;
    
    [Header("Auto-Connection")]
    [SerializeField] private bool autoConnectOnStart = true; // Automatically establish WebRTC connection on app start
    [SerializeField] private float autoConnectDelay = 2f; // Delay before starting auto-connection (seconds)
    
    [Header("Auto-Greeting")]
    [SerializeField] private bool enableAutoGreeting = true; // Automatically greet user when connection is established
    [SerializeField] private float greetingDelay = 1.5f; // Delay before greeting (seconds)
    
    [Header("Reminder Integration")]
    [SerializeField] private bool enableReminders = true;
    [SerializeField] private ReminderManager reminderManager;
    
    [Header("Audio Configuration")]
    [SerializeField] private AudioSource remoteAudioSource;
    
    [Header("BAi Component Integration")]
    [SerializeField] private BAiComponentManagerV0002 baiComponentManager;
    
    [Header("Chat UI Integration")]
    [SerializeField] private Transform coachChatRoot;        // Chat container
    [SerializeField] private GameObject chatPostPrefab;      // ChatPostPrefab prefab
    [SerializeField] private Sprite aiBackgroundSprite;      // AI message background
    [SerializeField] private ScrollRect chatScrollRect;      // For scrolling
    
    // WebRTC Components
    private RTCPeerConnection peerConnection;
    private RTCDataChannel dataChannel;
    private AudioStreamTrack localMicTrack;
    
    // Connection state
    private string ephemeralKey;
    private bool isConnectionActive = false;
    private bool isTalking = false;
    private bool isAIResponding = false;
    
    // Mobile components integration
    private MobileCompanionUI companionUI;
    private MobileRAGClient ragClient;
    private RAGConfiguration ragConfig;
    
    // Connection health
    private float lastResponseTime = 0f;
    private int reconnectAttempts = 0;
    private const int MAX_RECONNECT_ATTEMPTS = 3;
    private const float CONNECTION_TIMEOUT = 30f;
    
    // Audio transcript tracking
    private StringBuilder currentTranscript = new StringBuilder();
    private StringBuilder currentAITranscript;
    
    // Audio streaming
    private Coroutine audioStreamingCoroutine;
    private AudioClip microphoneClip;
    private int lastMicrophonePosition = 0;
    
    // Events
    public event Action OnConnectionEstablished;
    public event Action OnConnectionLost;
    public event Action<string> OnTranscriptReceived;
    public event Action<string> OnAIResponseReceived;
    public event Action<string> OnError;
    
    // Debug helper methods
    private bool ShouldLog(DebugCategory category)
    {
        return enableDebugLogging && (debugCategories.HasFlag(category) || debugCategories.HasFlag(DebugCategory.All));
    }
    
    private void LogDebug(DebugCategory category, string message)
    {
        if (ShouldLog(category))
        {
            Debug.Log($"[{category}] {message}");
        }
    }
    
    // Public methods to control debug logging
    public void SetDebugCategory(DebugCategory category, bool enabled)
    {
        if (enabled)
            debugCategories |= category;
        else
            debugCategories &= ~category;
        
        Debug.Log($"[Debug] {category} logging {(enabled ? "enabled" : "disabled")}");
    }
    
    public void EnableOnlyDebugCategory(DebugCategory category)
    {
        debugCategories = category;
        Debug.Log($"[Debug] Only {category} logging enabled");
    }
    
    public void DisableAllDebugLogging()
    {
        debugCategories = DebugCategory.None;
        Debug.Log("[Debug] All debug logging disabled");
    }
    
    public void EnableAllDebugLogging()
    {
        debugCategories = DebugCategory.All;
        Debug.Log("[Debug] All debug logging enabled");
    }

    [Header("Animation")]
    public UI_no_weapon animationController; // Reference to character animation controller
    
    [Header("Debug Logging")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private DebugCategory debugCategories = DebugCategory.SystemPrompt | DebugCategory.ImageGeneration;
    
    [System.Flags]
    public enum DebugCategory
    {
        None = 0,
        SystemPrompt = 1 << 0,      // System prompt messages
        ImageGeneration = 1 << 1,    // Image generation process
        WebRTC = 1 << 2,            // WebRTC connection details
        RAG = 1 << 3,               // RAG API calls
        Audio = 1 << 4,             // Audio processing
        All = ~0                     // All categories
    }
    
    // Flag to track when we're loading conversation history
    private bool isLoadingHistory = false;
    
    // Enhanced conversation state tracking
    private string currentOpenAIResponseId = null;
    private bool isWaitingForOpenAIResponse = false;
    private float lastResponseStartTime = 0f;
    private const float RESPONSE_TIMEOUT_SECONDS = 120f; // Increased from 30s to 120s for complex responses
    
    // Startup protection to prevent initial conversation conflicts
    private bool isInitializing = true;
    private float startupCompleteTime = 0f;
    private const float STARTUP_PROTECTION_SECONDS = 15f; // Increased from 5 to 15 seconds to allow for network operations
    
    // Startup timeout protection
    private Coroutine startupTimeoutCoroutine;
    
    // Conversation state validation
    public bool CanStartNewConversation => !isAIResponding && !isTalking && !isWaitingForOpenAIResponse && !isInitializing;
    
    public string GetConversationStatus()
    {
        if (isInitializing) return "Initializing - please wait";
        if (isAIResponding) return "AI is responding";
        if (isTalking) return "Recording voice input";
        if (isWaitingForOpenAIResponse) return "Waiting for OpenAI response";
        if (isConnectionActive) return "Ready for input";
        return "Not connected";
    }
    
    // Check if we can start a new conversation and provide detailed status
    public (bool canStart, string reason) CanStartNewConversationWithReason()
    {
        if (isInitializing)
            return (false, "System is still initializing - please wait");
        
        if (!isConnectionActive)
            return (false, "Not connected to realtime session");
        
        if (isTalking)
            return (false, "Already recording voice input");
        
        if (isAIResponding)
            return (false, "AI is currently responding");
        
        if (isWaitingForOpenAIResponse)
            return (false, "Waiting for OpenAI response to complete");
        
        return (true, "Ready for new conversation");
    }
    
    // Check if it's safe to proceed with greeting (after startup and history loading)
    public bool IsReadyForGreeting()
    {
        if (isInitializing)
            return false;
            
        var companionUI = FindObjectOfType<MobileCompanionUI>();
        if (companionUI != null && companionUI.IsLoadingHistory)
            return false;
            
        return isConnectionActive;
    }
    
    // Debug method to check connection status
    public void LogConnectionStatus()
    {
        LogMessage($"🔍 CONNECTION STATUS CHECK:");
        LogMessage($"   - isConnectionActive: {isConnectionActive}");
        LogMessage($"   - dataChannel != null: {dataChannel != null}");
        LogMessage($"   - dataChannel.ReadyState: {(dataChannel != null ? dataChannel.ReadyState.ToString() : "NULL")}");
        LogMessage($"   - isInitializing: {isInitializing}");
        LogMessage($"   - isAIResponding: {isAIResponding}");
        LogMessage($"   - isWaitingForOpenAIResponse: {isWaitingForOpenAIResponse}");
        LogMessage($"   - lastResponseTime: {lastResponseTime}");
        LogMessage($"   - Time since last response: {Time.time - lastResponseTime:F1}s");
    }
    
    // Force reset conversation state for emergency recovery
    public void ForceResetConversationState()
    {
        LogWarning("🔄 Force resetting conversation state for emergency recovery");
        
        // Reset all conversation flags
        isAIResponding = false;
        isTalking = false;
        CancelOpenAIResponse();
        
        // Stop any ongoing coroutines
        if (audioStreamingCoroutine != null)
        {
            StopCoroutine(audioStreamingCoroutine);
            audioStreamingCoroutine = null;
        }
        
        // Reset UI state
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(false);
            companionUI.ShowRecordingIndicator(false);
            companionUI.ResetToIdleState();
            companionUI.UpdateStatusText("Ready - Tap to talk");
        }
        
        LogMessage("✅ Conversation state force reset complete");
    }
    
    private void Start()
    {
        // Reset greeting flags on each app start to prevent duplicate welcomes
        greetingTriggered = false;
        
        // Initialize Unity audio settings for better WebRTC compatibility
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        audioConfig.sampleRate = 24000; // Common rate for voice
        audioConfig.numRealVoices = 32;
        AudioSettings.Reset(audioConfig);
        
        InitializeMobileRealtime();
        
        // Start startup timeout protection
        startupTimeoutCoroutine = StartCoroutine(StartupTimeoutProtection());
        
        // Auto-connect to WebRTC if enabled
        if (autoConnectOnStart)
        {
            LogMessage($"🔗 Auto-connect enabled - will establish WebRTC connection in {autoConnectDelay} seconds");
            StartCoroutine(DelayedAutoConnect());
        }
        else
        {
            LogMessage("🔗 Auto-connect disabled - WebRTC connection requires manual activation");
        }
        
        // Initialize reminder integration
        if (enableReminders && reminderManager == null)
        {
            reminderManager = FindFirstObjectByType<ReminderManager>();
            if (reminderManager == null)
            {
                GameObject reminderObj = new GameObject("ReminderManager");
                reminderManager = reminderObj.AddComponent<ReminderManager>();
                
                // Sync settings
                var reminderManagerType = typeof(ReminderManager);
                var ragApiUrlField = reminderManagerType.GetField("ragApiUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var userIdField = reminderManagerType.GetField("userId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (ragApiUrlField != null) ragApiUrlField.SetValue(reminderManager, ragApiUrl);
                if (userIdField != null) userIdField.SetValue(reminderManager, userId);
            }
        }
    }
    
    private void InitializeMobileRealtime()
    {
        // Get component references
        companionUI = GetComponent<MobileCompanionUI>() ?? FindFirstObjectByType<MobileCompanionUI>();
        ragClient = GetComponent<MobileRAGClient>() ?? FindFirstObjectByType<MobileRAGClient>();
        
        // Initialize RAG configuration with validation
        try
        {
            RAGConfiguration.ValidateAndFixConfiguration();
            ragConfig = RAGConfiguration.Instance;
            
            if (ragConfig != null && ragConfig.Settings != null)
            {
                LogMessage("✅ RAG Configuration loaded successfully");
                LogMessage($"Companion Name: {ragConfig.Settings.companionName}");
                LogMessage($"Model: {ragConfig.Settings.modelName}");
                LogMessage($"RAG Enabled: {ragConfig.Settings.enableRAG}");
            }
            else
            {
                LogError("❌ RAG Configuration failed to load properly");
                ragConfig = null;
            }
        }
        catch (System.Exception ex)
        {
            LogError($"❌ Error loading RAG Configuration: {ex.Message}");
            ragConfig = null;
        }
        
        // Debug RAG configuration
        LogMessage($"RAG Client initialized: {ragClient != null}");
        LogMessage($"RAG Config initialized: {ragConfig != null}");
        LogMessage($"RAG Context enabled: {enableRAGContext}");
        LogMessage($"RAG API URL: {ragApiUrl}");
        LogMessage($"User ID: {userId}");
        
        if (enableRAGContext && ragClient == null)
        {
            LogError("RAG context is enabled but no MobileRAGClient found! Memory will not work.");
        }
        
        // Check and sync RAG API URL configuration
        if (enableRAGContext && ragClient != null)
        {
            // First, try to sync URLs if realtime URL is configured
            if (!IsPlaceholderUrl(ragApiUrl))
            {
                LogMessage($"Configuring RAG Client with Railway URL: {ragApiUrl}");
                ragClient.SetCloudApiUrl(ragApiUrl);
            }
            
            // Verify configuration
            var ragClientApiUrl = ragClient.GetType().GetField("cloudApiUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ragClientApiUrl != null)
            {
                string currentUrl = ragClientApiUrl.GetValue(ragClient) as string;
                LogMessage($"RAG Client API URL: {currentUrl}");
                
                if (IsPlaceholderUrl(currentUrl))
                {
                    LogError("🚨 CONFIGURATION REQUIRED: Set your Railway server URL in MobileRealtimeChat.ragApiUrl to enable RAG memory!");
                    LogError("Example: https://your-app-name.up.railway.app");
                    LogError("RAG memory storage and recall will NOT work until this is configured.");
                }
                else
                {
                    LogMessage("✅ RAG Client configured with Railway server URL - memory should work!");
                }
            }
        }
        
        // Setup audio source for remote audio
        if (remoteAudioSource == null)
        {
            remoteAudioSource = gameObject.GetComponent<AudioSource>();
            if (remoteAudioSource == null)
            {
                remoteAudioSource = gameObject.AddComponent<AudioSource>();
                LogMessage("✅ Auto-created AudioSource component for remote audio");
            }
            else
            {
                LogMessage("✅ Found existing AudioSource component on GameObject");
            }
        }
        else
        {
            LogMessage("✅ Using AudioSource assigned via Inspector");
        }
        
        // Configure audio source for WebRTC remote audio
        remoteAudioSource.playOnAwake = false;
        remoteAudioSource.loop = false;
        remoteAudioSource.volume = 1.0f;
        //remoteAudioSource.spatialBlend = 0f; // 2D audio
        remoteAudioSource.priority = 128;
        
        LogMessage("Remote audio source configured for WebRTC output");
        
        // Log AudioSource configuration for debugging
        LogAudioSourceConfiguration();
        
        // Subscribe to UI events
        if (companionUI != null)
        {
            // The UI will call our public methods directly
        }
        
        LogMessage($"Mobile Realtime Chat initialized - RAG ready: {enableRAGContext && ragClient != null}");
    }
    
    // Public methods for UI integration
    public void StartRealtimeSession()
    {
        if (isConnectionActive)
        {
            LogMessage("Already connected to realtime session");
            return;
        }
        
        // Check if Railway server URL is configured
        if (string.IsNullOrEmpty(ragApiUrl))
        {
            OnError?.Invoke("Railway server URL not configured");
            return;
        }
        
        LogMessage("Starting realtime session via Railway server...");
        
        // Debug RAG status before starting session
        LogMessage($"=== RAG STATUS CHECK ===");
        LogMessage($"RAG Context Enabled: {enableRAGContext}");
        LogMessage($"RAG Client Available: {ragClient != null}");
        LogMessage($"RAG API URL: {ragApiUrl}");
        LogMessage($"User ID: {userId}");
        LogMessage($"=== END RAG STATUS ===");
        
        // Connection will be initiated by auto-connect or manual trigger
    }
    
    private IEnumerator DelayedAutoConnect()
    {
        LogMessage($"⏳ Waiting {autoConnectDelay} seconds before auto-connecting...");
        yield return new WaitForSeconds(autoConnectDelay);
        
        if (isConnectionActive)
        {
            LogMessage("✅ WebRTC connection already active - skipping auto-connect");
            yield break;
        }
        
        LogMessage("🔗 Starting automatic WebRTC connection...");
        
        // Request microphone permissions first (important for mobile)
        if (Application.isMobilePlatform)
        {
            LogMessage("📱 Requesting microphone permissions for mobile...");
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
                
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    LogError("❌ Microphone permission denied - cannot auto-connect");
                    if (companionUI != null)
                    {
                        companionUI.UpdateStatusText("Microphone permission required for voice features");
                    }
                    yield break;
                }
            }
        }
        
        // Start the connection
        StartCoroutine(CreateSessionAndConnect());
        
        // Wait a bit and check if connection succeeded
        yield return new WaitForSeconds(5f);
        
        if (isConnectionActive)
        {
            LogMessage("✅ Auto-connect successful - WebRTC connection established");
            if (companionUI != null)
            {
                companionUI.UpdateStatusText("Voice connection ready");
            }
        }
        else
        {
            LogMessage("⚠️ Auto-connect failed - connection not established within timeout");
            if (companionUI != null)
            {
                companionUI.UpdateStatusText("Voice connection failed - tap MIC Talk to retry");
            }
        }
    }
    
    private IEnumerator DelayedAutoGreetingWithStartupProtection()
    {
        LogMessage("🔄 Waiting for startup protection to expire before greeting...");
        
        // Wait for startup protection to expire
        while (isInitializing)
        {
            yield return new WaitForSeconds(0.5f); // Check every 500ms
        }
        
        LogMessage($"✅ Startup protection expired, now waiting for conversation history to load...");
        
        // Wait for conversation history to be loaded
        var companionUI = FindObjectOfType<MobileCompanionUI>();
        if (companionUI != null)
        {
            // Temporarily disabled conversation history loading for testing
            // while (companionUI.IsLoadingHistory)
            // {
            //     LogMessage("⏳ Waiting for conversation history to finish loading...");
            //     yield return new WaitForSeconds(0.5f); // Check every 500ms
            // }
            // LogMessage("✅ Conversation history loaded, proceeding with greeting...");
            LogMessage("⏳ Conversation history loading temporarily disabled for testing");
        }
        
        LogMessage($"⏳ Now waiting {greetingDelay} seconds before greeting...");
        yield return new WaitForSeconds(greetingDelay);
        
        if (!isConnectionActive)
        {
            LogMessage("⚠️ Connection lost before greeting could be delivered");
            yield break;
        }
        
        LogMessage("🤝 Generating personalized greeting...");
        
        // Get user's name from RAG system using coroutine
        yield return StartCoroutine(GetUserNameCoroutine((userName) => {
            // Generate appropriate greeting based on whether we know the name
            string greetingMessage = GenerateGreetingMessage(userName);
            
            LogMessage($"📢 Delivering greeting: '{greetingMessage}'");
            LogMessage($"📏 Greeting message length: {greetingMessage.Length} characters");
            LogMessage($"🔤 Greeting message words: {greetingMessage.Split(' ').Length} words");
            
            // Use simplified welcome method to avoid word-by-word processing
            DeliverSimpleWelcome(greetingMessage);
        }));
    }
    
    private IEnumerator GetUserNameCoroutine(System.Action<string> callback)
    {
        string userName = "";
        
        if (!enableRAGContext)
        {
            LogMessage("RAG not available - cannot retrieve user name");
            callback?.Invoke("");
            yield break;
        }
        
        // Try to get user profile from server
        using (UnityWebRequest request = UnityWebRequest.Get($"{ragApiUrl}/user/{userId}/profile"))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                    
                    // Check for simple profile structure: {"profile": {"name": "Bracey"}, "has_name": true}
                    if (response.ContainsKey("profile") && response["profile"] is Newtonsoft.Json.Linq.JObject profile)
                    {
                        string name = profile["name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            LogMessage($"✅ Found user name: {name}");
                            userName = name.Split(' ')[0]; // Use first name only
                        }
                    }
                    // Also check for has_name flag to confirm name validity
                    else if (response.ContainsKey("has_name") && response["has_name"].ToString().ToLower() == "false")
                    {
                        LogMessage("Profile indicates user has no stored name");
                    }
                }
                catch (System.Exception parseEx)
                {
                    LogMessage($"Error parsing user profile response: {parseEx.Message}");
                }
            }
            else
            {
                LogMessage($"Failed to get user profile: {request.error}");
            }
        }
        
        if (string.IsNullOrEmpty(userName))
        {
            LogMessage("No user name found in profile");
        }
        
        callback?.Invoke(userName);
    }
    
    private string GenerateGreetingMessage(string userName)
    {
        if (!string.IsNullOrEmpty(userName))
        {
            // Personalized greeting with known name
            string[] personalizedGreetings = {
                $"Hello {userName}! Great to see you again. How can I help you today?",
                $"Hi {userName}! Welcome back. What would you like to talk about?",
                $"Hey {userName}! I'm here and ready to chat. What's on your mind?",
                $"Good to see you, {userName}! How are you doing today?",
                $"Hello {userName}! I'm here to help. What can I do for you?"
            };
            
            return personalizedGreetings[UnityEngine.Random.Range(0, personalizedGreetings.Length)];
        }
        else
        {
            // Generic greeting that asks for name
            string[] introductoryGreetings = {
                "Hello! I'm your AI companion. I'm here to help with conversations, reminders, and more. What's your name?",
                "Hi there! Great to meet you. I'm your personal AI assistant. Could you tell me your name so I can address you properly?",
                "Welcome! I'm excited to be your AI companion. I can help with conversations and reminders. What should I call you?",
                "Hello! I'm here to chat, help with reminders, and assist you. What's your name so we can get better acquainted?",
                "Hi! I'm your AI companion, ready to help with whatever you need. Could you share your name with me?"
            };
            
            return introductoryGreetings[UnityEngine.Random.Range(0, introductoryGreetings.Length)];
        }
    }
    
    /// <summary>
    /// Manually establish WebRTC connection - can be called from UI button
    /// </summary>
    public void ConnectToRealtime()
    {
        if (isConnectionActive)
        {
            LogMessage("✅ WebRTC connection already active");
            return;
        }
        
        LogMessage("🔗 Manual WebRTC connection requested...");
        StartCoroutine(CreateSessionAndConnect());
    }
    
    /// <summary>
    /// Check if WebRTC connection is active - useful for UI state
    /// </summary>
    public bool IsWebRTCConnected()
    {
        return isConnectionActive;
    }
    
    /// <summary>
    /// Manually trigger a greeting - useful for testing or re-greeting
    /// </summary>
    public void TriggerGreeting()
    {
        if (!isConnectionActive)
        {
            LogError("Cannot trigger greeting - WebRTC connection not active");
            return;
        }
        
        LogMessage("🤝 Manual greeting triggered");
        StartCoroutine(DelayedAutoGreetingWithStartupProtection());
    }
    
    public void StartVoiceInput()
    {
        LogMessage($"StartVoiceInput called - isConnectionActive: {isConnectionActive}, isTalking: {isTalking}, isAIResponding: {isAIResponding}, isWaitingForOpenAIResponse: {isWaitingForOpenAIResponse}, isInitializing: {isInitializing}, localMicTrack != null: {localMicTrack != null}");
        
        // Log detailed connection status for debugging
        LogConnectionStatus();
        
        if (isInitializing)
        {
            LogMessage("Cannot start voice input - system is still initializing");
            OnError?.Invoke("Please wait for the system to finish initializing");
            return;
        }
        
        if (!isConnectionActive)
        {
            LogError("Cannot start voice input - not connected to realtime session");
            OnError?.Invoke("Not connected to realtime session");
            return;
        }
        
        if (isTalking)
        {
            LogMessage("Already recording voice input");
            return;
        }
        
        if (isAIResponding || isWaitingForOpenAIResponse)
        {
            LogMessage("Cannot start voice input - AI is currently responding or waiting for response");
            OnError?.Invoke("Please wait for the AI to finish responding");
            return;
        }
        
        // Enable microphone for continuous listening
        if (localMicTrack != null && microphoneClip != null)
        {
            
            LogMessage($"Starting audio streaming to OpenAI");
            isTalking = true;

            //ai is waiting
            if(animationController!=null)
                animationController.ListenNeutralOnClick();

            currentTranscript.Clear();
            lastMicrophonePosition = Microphone.GetPosition(null);
            
            // Start streaming audio data to OpenAI
            if (audioStreamingCoroutine != null)
            {
                StopCoroutine(audioStreamingCoroutine);
            }
            audioStreamingCoroutine = StartCoroutine(StreamAudioToOpenAI());
            
            LogMessage($"Voice input started successfully - Audio streaming started");
            
            // Clear any previous audio buffer (just in case)
            SendClearAudioBuffer();
            
            // Update UI state
            if (companionUI != null)
            {
                companionUI.ShowRecordingIndicator(true);
                companionUI.UpdateStatusText("Listening... Speak now");
            }
        }
        else
        {
            LogError("Cannot start voice input - microphone not initialized");
            OnError?.Invoke("Microphone not initialized");
        }
    }
    
    public void StopVoiceInput()
    {
        LogMessage($"StopVoiceInput called - isTalking: {isTalking}");
        
        if (!isTalking)
        {
            LogMessage("Voice input was not active, nothing to stop");
            return;
        }
        
        try
        {
            // Stop audio streaming
            if (audioStreamingCoroutine != null)
            {
                LogMessage($"Stopping audio streaming coroutine");
                StopCoroutine(audioStreamingCoroutine);
                audioStreamingCoroutine = null;
            }
            
            isTalking = false;
            LogMessage($"Voice input stopped successfully - waiting for server VAD to detect end");
            
            // Update UI state
            if (companionUI != null)
            {
                companionUI.ShowRecordingIndicator(false);
                companionUI.ShowProcessingIndicator(true);
                companionUI.UpdateStatusText("Processing speech...");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error stopping voice input: {ex.Message}");
            isTalking = false;
            OnError?.Invoke($"Error stopping voice input: {ex.Message}");
        }
    }
    
    public void SendTextMessage(string message)
    {
        if (isInitializing)
        {
            LogMessage("Cannot send text message - system is still initializing");
            OnError?.Invoke("Please wait for the system to finish initializing");
            return;
        }
        
        if (!isConnectionActive)
        {
            OnError?.Invoke("Not connected to realtime session");
            return;
        }
        
        if (isAIResponding || isWaitingForOpenAIResponse)
        {
            LogMessage("Cannot send text message - AI is currently responding or waiting for response");
            OnError?.Invoke("Please wait for the AI to finish responding");
            return;
        }
        
        StartCoroutine(SendTextMessageWithRAGContext(message));
    }
    
    public void InterruptAIResponse()
    {
        LogMessage($"=== INTERRUPTING AI RESPONSE === isConnectionActive: {isConnectionActive}, isAIResponding: {isAIResponding}");
        
        if (!isConnectionActive)
        {
            LogError("Cannot interrupt - not connected to realtime session");
            return;
        }
        
        try
        {
            // Cancel any ongoing response
            if (isAIResponding)
            {
                LogMessage("Sending response cancel to OpenAI");
                SendResponseCancel();
                isAIResponding = false;
                CancelOpenAIResponse(); // Mark OpenAI response as cancelled
                LogMessage("AI response canceled successfully");
            }
            else
            {
                LogMessage("No active AI response to cancel");
            }
            
            // Stop remote audio if playing
            if (remoteAudioSource != null && remoteAudioSource.isPlaying)
            {
                LogMessage("Stopping remote audio playback");
                remoteAudioSource.Stop();
                LogMessage("Remote audio playback stopped");
            }
            else
            {
                LogMessage($"Remote audio not playing - isPlaying: {remoteAudioSource?.isPlaying ?? false}");
            }
            
            // Clear any pending audio buffer
            LogMessage("Clearing audio buffer for interruption");
            SendClearAudioBuffer();
            
            LogMessage("=== AI RESPONSE INTERRUPTION COMPLETE ===");
            
        }
        catch (System.Exception ex)
        {
            LogError($"Error interrupting AI response: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }
    
    public void StopRealtimeSession()
    {
        if (!isConnectionActive)
        {
            return;
        }
        
        LogMessage("Stopping realtime session...");
        CleanupConnection();
    }
    
    // Core WebRTC implementation
    private IEnumerator CreateSessionAndConnect()
    {
        // Step 1: Get ephemeral key from Railway server
        yield return StartCoroutine(CreateRealtimeSession(""));
        
        if (string.IsNullOrEmpty(ephemeralKey))
        {
            OnError?.Invoke("Failed to get ephemeral key from server");
            yield break;
        }
        
        // Step 2: Setup WebRTC connection
        yield return StartCoroutine(SetupWebRTCConnection());
    }
    
    private IEnumerator CreateRealtimeSession(string apiKey)
    {
        // Get ephemeral key from Railway server instead of calling OpenAI directly
        var url = $"{ragApiUrl}/realtime/session";
        
        var payload = new JObject
        {
            ["user_id"] = userId,
            ["model"] = realtimeModel,
            ["instructions"] = GetDefaultInstructions()
        };
        
        var request = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString())),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 15
        };
        
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            var response = JObject.Parse(request.downloadHandler.text);
            ephemeralKey = response["ephemeral_key"]?.ToString();
            
            if (!string.IsNullOrEmpty(ephemeralKey))
            {
                LogMessage($"Got ephemeral key from Railway server");
            }
            else
            {
                LogError("No ephemeral key in server response");
            }
        }
        else
        {
            LogError($"Railway session creation failed: {request.error}");
            LogError($"Response: {request.downloadHandler.text}");
        }
        
        request.Dispose();
    }
    
    private IEnumerator SetupWebRTCConnection()
    {
        // Setup ICE servers
        var servers = new List<RTCIceServer>();
        foreach (var url in iceServers.Split(','))
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                servers.Add(new RTCIceServer { urls = new[] { url.Trim() } });
            }
        }
        
        var config = new RTCConfiguration
        {
            iceServers = servers.ToArray(),
            iceTransportPolicy = RTCIceTransportPolicy.All
        };
        
        peerConnection = new RTCPeerConnection(ref config);
        
        // Setup event handlers
        SetupWebRTCEventHandlers();
        
        // Setup data channel for text communication
        dataChannel = peerConnection.CreateDataChannel("oai-events");
        SetupDataChannelHandlers();
        
        // Initialize microphone
        yield return StartCoroutine(InitializeMicrophone());
        
        // Create and send offer
        yield return StartCoroutine(CreateAndSendOffer());
    }
    
    private void SetupWebRTCEventHandlers()
    {
        peerConnection.OnTrack = (RTCTrackEvent e) =>
        {
            LogMessage($"Track received: {e.Track.Kind}");
            
            if (e.Track is AudioStreamTrack audioTrack)
            {
                LogMessage("Setting up remote audio track");
                
                // Set the audio track to the remote audio source
                remoteAudioSource.SetTrack(audioTrack);
                remoteAudioSource.Play();
                
                LogMessage($"🔊 Remote audio playing on: {remoteAudioSource.name} (GameObject: {remoteAudioSource.gameObject.name})");
                LogMessage("Remote audio track connected and playing");
                
                // Update UI to show audio is playing
                if (companionUI != null)
                {
                    companionUI.ShowAudioPlaybackIndicator(true);
                }
            }
        };
        
        peerConnection.OnIceConnectionChange = (RTCIceConnectionState state) =>
        {
            LogMessage($"ICE connection state: {state}");
            
            switch (state)
            {
                case RTCIceConnectionState.Connected:
                case RTCIceConnectionState.Completed:
                    // ICE connection established, but wait for data channel to open
                    lastResponseTime = Time.time;
                    reconnectAttempts = 0;
                    LogMessage("ICE connection established - waiting for data channel");
                    break;
                    
                case RTCIceConnectionState.Failed:
                case RTCIceConnectionState.Disconnected:
                    isConnectionActive = false;
                    OnConnectionLost?.Invoke();
                    StartCoroutine(AttemptReconnection());
                    break;
            }
        };
        
        peerConnection.OnConnectionStateChange = (RTCPeerConnectionState state) =>
        {
            LogMessage($"Peer connection state: {state}");
        };
    }
    
    private void SetupDataChannelHandlers()
    {
        dataChannel.OnOpen = () =>
        {
            LogMessage("Data channel opened - WebRTC connection fully established");
            isConnectionActive = true;
            
            // Activate BAi component when connection opens
            if (baiComponentManager != null)
            {
                LogMessage("🤖 Activating BAi component - connection established");
                baiComponentManager.ActivateBAi();
            }
            else
            {
                LogMessage("⚠️ BAi component manager not assigned - BAi will not be activated");
            }
            
            // Send session configuration
            SendSessionConfiguration();
            
            LogMessage("Invoking OnConnectionEstablished event");
            
            // Trigger auto-greeting if enabled
            if (enableAutoGreeting)
            {
                LogMessage("🤝 Auto-greeting enabled - will be triggered after initialization sequence completes");
                // Note: Auto-greeting is now controlled by MobileRAGCompanionSystem after initial sync
                // StartCoroutine(DelayedAutoGreetingWithStartupProtection());
            }
            // Notify connection established
            OnConnectionEstablished?.Invoke();
        };
        
        dataChannel.OnMessage = (byte[] data) =>
        {
            lastResponseTime = Time.time;
            LogMessage($"📥 Received data channel message: {data.Length} bytes");
            ProcessDataChannelMessage(data);
        };
        
        dataChannel.OnClose = () =>
        {
            LogMessage("Data channel closed");
            isConnectionActive = false;
            
            // Deactivate BAi component when connection closes
            if (baiComponentManager != null)
            {
                LogMessage("🤖 Deactivating BAi component - connection lost");
                baiComponentManager.DeactivateBAI();
            }
            
            OnConnectionLost?.Invoke();
        };
        
        dataChannel.OnError = (RTCError error) =>
        {
            LogError($"Data channel error: {error}");
            OnError?.Invoke($"Data channel error: {error}");
        };
    }
    
    private IEnumerator InitializeMicrophone()
    {
        // Check microphone permission on mobile
        #if UNITY_ANDROID || UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                OnError?.Invoke("Microphone permission required for voice chat");
                yield break;
            }
        }
        #endif
        
        // Check if microphone is available
        if (Microphone.devices.Length == 0)
        {
            OnError?.Invoke("No microphone device found");
            yield break;
        }
        
        // Start microphone with standard voice sample rate
        int sampleRate = 24000; // Standard rate for voice (24kHz)
        microphoneClip = Microphone.Start(null, true, 10, sampleRate);
        
        LogMessage($"Started microphone - Sample rate: {sampleRate}, Device: {(Microphone.devices.Length > 0 ? Microphone.devices[0] : "default")}");
        
        // Wait for microphone to start
        while (Microphone.GetPosition(null) <= 0)
        {
            yield return null;
        }
        
        // Create local audio source for microphone (separate from remote audio source)
        AudioSource localAudioSource = gameObject.GetComponent<AudioSource>();
        if (localAudioSource == null)
        {
            localAudioSource = gameObject.AddComponent<AudioSource>();
            LogMessage("✅ Created local AudioSource for microphone input");
        }
        else if (localAudioSource == remoteAudioSource)
        {
            // If the same AudioSource is used for both local and remote, create a separate one for local
            localAudioSource = gameObject.AddComponent<AudioSource>();
            LogMessage("✅ Created separate local AudioSource to avoid conflict with remote audio");
        }
        else
        {
            LogMessage("✅ Using existing local AudioSource for microphone input");
        }
        
        localAudioSource.clip = microphoneClip;
        localAudioSource.loop = true;
        localAudioSource.mute = true; // Mute local playback
        localAudioSource.Play();
        
        // Create WebRTC audio track (enabled initially for testing)
        localMicTrack = new AudioStreamTrack(localAudioSource);
        localMicTrack.Enabled = false; // Start disabled
        
        // Add track to peer connection
        var sender = peerConnection.AddTrack(localMicTrack);
        LogMessage($"Added microphone track to peer connection - Track enabled: {localMicTrack.Enabled}");
        
        LogMessage("Microphone initialized successfully");
    }
    
    private IEnumerator CreateAndSendOffer()
    {
        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;
        
        if (offerOp.IsError)
        {
            LogError($"Create offer failed: {offerOp.Error}");
            yield break;
        }
        
        var localDesc = offerOp.Desc;
        var setLocalOp = peerConnection.SetLocalDescription(ref localDesc);
        yield return setLocalOp;
        
        if (setLocalOp.IsError)
        {
            LogError($"Set local description failed: {setLocalOp.Error}");
            yield break;
        }
        
        // Send offer to OpenAI
        yield return StartCoroutine(SendOfferToOpenAI(localDesc.sdp));
    }
    
    private IEnumerator SendOfferToOpenAI(string sdp)
    {
        var url = $"https://api.openai.com/v1/realtime?model={realtimeModel}";
        var request = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sdp)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        
        request.SetRequestHeader("Content-Type", "application/sdp");
        request.SetRequestHeader("Authorization", $"Bearer {ephemeralKey}");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            var answer = request.downloadHandler.text;
            var answerDesc = new RTCSessionDescription 
            { 
                type = RTCSdpType.Answer, 
                sdp = answer 
            };
            
            var setRemoteOp = peerConnection.SetRemoteDescription(ref answerDesc);
            yield return setRemoteOp;
            
            if (setRemoteOp.IsError)
            {
                LogError($"Set remote description failed: {setRemoteOp.Error}");
            }
            else
            {
                LogMessage("WebRTC connection established successfully");
            }
        }
        else
        {
            LogError($"Send offer failed: {request.error}");
        }
        
        request.Dispose();
    }
    
    // Message processing
    private void ProcessDataChannelMessage(byte[] data)
    {
        try
        {
            string messageText = System.Text.Encoding.UTF8.GetString(data);
            LogMessage($"🔍 Processing message: {messageText.Substring(0, Math.Min(200, messageText.Length))}...");
            
            var jo = JObject.Parse(messageText);
            var messageType = jo["type"]?.ToString();
            
            LogMessage($"📋 Message type: {messageType}");
            
            // Basic logging for all messages to debug
            LogMessage($"🔍 BASIC DEBUG - Message type: {messageType}, Length: {messageText.Length}");
            
            // Log the full message structure for debugging
            if (messageType != null && (messageType.Contains("response") || messageType.Contains("conversation")))
            {
                LogMessage($"🔍 Full message structure: {jo.ToString(Formatting.None)}");
            }
            
            // Log all response-related messages for debugging
            if (messageType != null && messageType.Contains("response"))
            {
                LogMessage($"🔍 RESPONSE MESSAGE DEBUG - Type: {messageType}");
                LogMessage($"🔍 Full response message: {jo.ToString(Formatting.Indented)}");
                
                // Log all available fields in response messages
                var allFields = jo.Properties().Select(p => p.Name).ToArray();
                LogMessage($"🔍 Available fields in response: [{string.Join(", ", allFields)}]");
                
                // Log the value of each field for debugging
                foreach (var field in allFields)
                {
                    var value = jo[field];
                    if (value != null)
                    {
                        LogMessage($"🔍 Field '{field}': {value.ToString().Substring(0, Math.Min(100, value.ToString().Length))}...");
                    }
                }
            }
            
            switch (messageType)
            {
                case "response.output_item.added":
                case "response.content_part.added":
                    LogMessage("📝 Processing text response...");
                    HandleTextResponse(jo);
                    break;
                    
                case "response.audio.delta":
                    LogMessage("🔊 Processing audio delta...");
                    // Handle audio response deltas
                    HandleAudioResponseDelta(jo);
                    break;
                    
                case "response.audio_transcript.delta":
                    LogMessage("🎤 Processing audio transcript delta...");
                    // Handle audio transcript deltas (this is what was actually spoken)
                    HandleAudioTranscriptDelta(jo);
                    break;
                    
                case "response.audio_transcript.done":
                    LogMessage("✅ Processing audio transcript done...");
                    // Handle completed audio transcript
                    HandleAudioTranscriptDone(jo);
                    break;
                    
                case "response.output_item.done":
                case "response.content_part.done":
                    LogMessage("✅ Content part completed");
                    // Content part completed
                    break;
                    
                case "response.created":
                    LogMessage("🚀 Response created - starting AI response");
                    HandleResponseStart();
                    break;
                    
                case "response.create":
                    LogMessage("🔊 Processing response creation request");
                    HandleResponseCreate(jo);
                    break;
                    
                case "response.done":
                    LogMessage("✅ Response done - completing AI response");
                    HandleResponseComplete();
                    break;
                    
                case "response.cancelled":
                    LogMessage("❌ Response cancelled");
                    HandleResponseCancelled(jo);
                    break;
                    
                case "response.function_call_arguments.delta":
                    LogMessage("🔧 Processing function call arguments delta...");
                    HandleFunctionCallArgumentsDelta(jo);
                    break;
                    
                case "response.function_call_arguments.done":
                    LogMessage("✅ Function call arguments done...");
                    HandleFunctionCallArgumentsDone(jo);
                    break;
                    
                case "session.created":
                case "session.updated":
                    LogMessage("📋 Session event received");
                    break;
                    
                case "input_audio_buffer.speech_started":
                    LogMessage("🎤 Speech started...");
                    HandleSpeechStarted(jo);
                    break;
                    
                case "input_audio_buffer.speech_stopped":
                    LogMessage("🔇 Speech stopped...");
                    HandleSpeechStopped(jo);
                    break;
                    
                case "conversation.item.created":
                    LogMessage("💬 Conversation item created");
                    break;
                    
                case "conversation.item.create":
                    LogMessage("💬 Processing conversation item creation");
                    HandleConversationItemCreate(jo);
                    break;
                    
                case "output_audio_buffer.started":
                    LogMessage("🔊 Output audio buffer started...");
                    HandleOutputAudioBufferStarted(jo);
                    break;
                    
                case "output_audio_buffer.stopped":
                    LogMessage("🔇 Output audio buffer stopped...");
                    HandleOutputAudioBufferStopped(jo);
                    break;
                    
                case "rate_limits.updated":
                    LogMessage("⏱️ Rate limits updated");
                    HandleRateLimitsUpdated(jo);
                    break;
                    
                case "error":
                    LogMessage("❌ Error message received");
                    HandleErrorMessage(jo);
                    break;
                    
                default:
                    LogMessage($"❓ Unhandled message type: {messageType}");
                    // Don't process unknown message types to avoid duplicate processing
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing message: {ex.Message}");
        }
    }
    
    private void HandleAudioTranscription(JObject message)
    {
        var transcript = message["transcript"]?.ToString();
        if (!string.IsNullOrEmpty(transcript))
        {
            LogMessage($"Audio transcript completed: {transcript}");
            currentTranscript.Clear();
            currentTranscript.Append(transcript);
            
            OnTranscriptReceived?.Invoke(transcript);

            //user is speaking
            if (animationController != null)
                animationController.ListenFriendlyOnClick();

            // Store user input in RAG memory
            StoreUserMessage(transcript);

            // Update UI - transcript received, now waiting for AI response
            if (companionUI != null)
            {
                companionUI.ShowTranscript(transcript);
                companionUI.ShowProcessingIndicator(false);

                // Show that we're now waiting for AI response
                companionUI.UpdateStatusText("AI is thinking...");

                //ai is thinking
                if (animationController != null)
                    animationController.Idle01OnClick();
            }

            // Create chat post using ChatPostPrefab
            CreateChatPost(ChatPostPrefab.ChatPostType.text, transcript, true);
        }
    }
    
    private void HandleAudioTranscriptionDelta(JObject message)
    {
        var delta = message["delta"]?.ToString();
        if (!string.IsNullOrEmpty(delta))
        {
            currentTranscript.Append(delta);
            LogMessage($"Transcript delta: {delta}");
        }
    }
    
    private void HandleResponseStart()
    {
        LogMessage("🚀 Response started - AI is beginning to respond");
        isAIResponding = true;
        
        // Update UI
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(true);
            companionUI.UpdateStatusText("AI is responding...");
        }
    }
    
    private void HandleResponseComplete()
    {
        LogMessage("✅ Response completed - AI finished responding");
        isAIResponding = false;
        
        // Complete the OpenAI response tracking
        CompleteOpenAIResponse();
        
        // Update UI
        if (companionUI != null)
        {
            companionUI.ShowAudioPlaybackIndicator(false);
            companionUI.UpdateStatusText("Ready - Tap to talk");
            companionUI.ResetToIdleState();
        }
    }
    
    private void HandleAudioResponseDelta(JObject jo)
    {
        try
        {
            var audioData = jo["delta"]?["audio"]?.ToString();
            if (!string.IsNullOrEmpty(audioData))
            {
                LogMessage($"🔊 Received audio delta: {audioData.Length} bytes");
                
                // Reset timeout since we're receiving audio content
                if (isWaitingForOpenAIResponse)
                {
                    float elapsed = Time.time - lastResponseStartTime;
                    LogMessage($"🔄 Audio content received after {elapsed:F1}s - Resetting timeout");
                    lastResponseStartTime = Time.time; // Reset the timer
                }
                
                // Handle audio response chunks
                LogMessage("Audio response delta received - AI is speaking");
                
                // Update UI to show audio is being received
                if (companionUI != null && !isAIResponding)
                {
                    companionUI.ShowProcessingIndicator(false);
                    companionUI.ShowAudioPlaybackIndicator(true);
                    
                    // Transition to PlayingAudio state for interrupt capability
                    companionUI.ShowAudioPlaybackIndicator(true);
                    companionUI.UpdateStatusText("AI is speaking...");
                    isAIResponding = true;
                }
                
                // Ensure remote audio source is playing
                if (remoteAudioSource != null && !remoteAudioSource.isPlaying)
                {
                    remoteAudioSource.Play();
                    LogMessage("Started remote audio playback");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error handling audio response delta: {ex.Message}");
        }
    }
    
    private void HandleErrorMessage(JObject message)
    {
        var errorObj = message["error"];
        var errorCode = errorObj?["code"]?.ToString();
        var errorMessage = errorObj?["message"]?.ToString() ?? "Unknown error";
        var fullError = errorObj?.ToString() ?? "Unknown error";
        
        // Handle specific error types
        if (errorCode == "input_audio_buffer_commit_empty")
        {
            LogMessage("Ignoring empty audio buffer error - this can happen with server VAD");
            
            // Reset UI state quietly
            if (companionUI != null)
            {
                companionUI.ShowProcessingIndicator(false);
                if (isConnectionActive)
                {
                    companionUI.UpdateStatusText("Ready - Tap to talk");
                }
            }
            return;
        }
        
        // Handle conversation_already_has_active_response error specifically
        if (errorCode == "conversation_already_has_active_response")
        {
            LogWarning($"⚠️ OpenAI conversation conflict detected: {errorMessage}");
            LogMessage("Resetting OpenAI response state to resolve conflict");
            
            // Force reset the OpenAI response state
            CancelOpenAIResponse();
            isAIResponding = false;
            
            // Reset UI state
            if (companionUI != null)
            {
                companionUI.ShowProcessingIndicator(false);
                companionUI.ShowAudioPlaybackIndicator(false);
                companionUI.ResetToIdleState();
                companionUI.UpdateStatusText("Ready - Tap to talk");
            }
            
            LogMessage("✅ OpenAI response state reset - ready for new conversation");
            return;
        }
        
        LogError($"OpenAI error: {fullError}");
        OnError?.Invoke($"OpenAI error: {errorMessage}");
        
        // Reset UI state for other errors
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(false);
            
            // Reset the conversation state to Idle so the MIC Talk button works again
            companionUI.ResetToIdleState();
        }
    }
    
    private void HandleTextResponse(JObject jo)
    {
        try
        {
            // Log the raw message structure for debugging
            LogMessage($"🔍 Raw text response message: {jo.ToString(Formatting.None)}");
            
            // Log all top-level keys to see what fields exist
            LogMessage($"🔑 Top-level keys: [{string.Join(", ", jo.Properties().Select(p => p.Name))}]");
            
            var content = jo["content"]?.ToString();
            if (!string.IsNullOrEmpty(content))
            {
                LogMessage($"📝 Received text response content: {content.Substring(0, Math.Min(100, content.Length))}...");
                
                // Reset timeout since we're receiving content
                if (isWaitingForOpenAIResponse)
                {
                    float elapsed = Time.time - lastResponseStartTime;
                    LogMessage($"🔄 Text content received after {elapsed:F1}s - Resetting timeout");
                    lastResponseStartTime = Time.time; // Reset the timer
                }
                
                // Process the text response
                if (companionUI != null)
                {
                    companionUI.OnAssistantResponse(content);
                }
            }
            else
            {
                LogWarning("⚠️ Text response received but content is empty or null");
                
                // Try alternative field names with detailed logging
                LogMessage("🔍 Searching for content in alternative fields...");
                
                var alternativeContent = jo["text"]?.ToString();
                if (!string.IsNullOrEmpty(alternativeContent))
                {
                    LogMessage($"✅ Found content in 'text' field: {alternativeContent.Substring(0, Math.Min(100, alternativeContent.Length))}...");
                    ProcessAlternativeContent(alternativeContent);
                    return;
                }
                
                alternativeContent = jo["message"]?.ToString();
                if (!string.IsNullOrEmpty(alternativeContent))
                {
                    LogMessage($"✅ Found content in 'message' field: {alternativeContent.Substring(0, Math.Min(100, alternativeContent.Length))}...");
                    ProcessAlternativeContent(alternativeContent);
                    return;
                }
                
                alternativeContent = jo["response"]?.ToString();
                if (!string.IsNullOrEmpty(alternativeContent))
                {
                    LogMessage($"✅ Found content in 'response' field: {alternativeContent.Substring(0, Math.Min(100, alternativeContent.Length))}...");
                    ProcessAlternativeContent(alternativeContent);
                    return;
                }
                
                // Check nested fields
                var part = jo["part"];
                if (part != null)
                {
                    LogMessage($"🔍 Found 'part' object: {part.ToString(Formatting.None)}");
                    var partText = part["text"]?.ToString();
                    if (!string.IsNullOrEmpty(partText))
                    {
                        LogMessage($"✅ Found content in 'part.text' field: {partText.Substring(0, Math.Min(100, partText.Length))}...");
                        ProcessAlternativeContent(partText);
                        return;
                    }
                }
                
                var delta = jo["delta"];
                if (delta != null)
                {
                    LogMessage($"🔍 Found 'delta' object: {delta.ToString(Formatting.None)}");
                    var deltaText = delta["text"]?.ToString();
                    if (!string.IsNullOrEmpty(deltaText))
                    {
                        LogMessage($"✅ Found content in 'delta.text' field: {deltaText.Substring(0, Math.Min(100, deltaText.Length))}...");
                        ProcessAlternativeContent(deltaText);
                        return;
                    }
                }
                
                LogError("❌ No content found in any expected field - response may be malformed");
                LogMessage($"🔍 Complete message structure for debugging: {jo.ToString(Formatting.Indented)}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error handling text response: {ex.Message}");
        }
    }
    
    private void ProcessAlternativeContent(string content)
    {
        LogMessage($"🔄 Processing alternative content: {content.Substring(0, Math.Min(100, content.Length))}...");
        
        // Reset timeout since we're receiving content
        if (isWaitingForOpenAIResponse)
        {
            float elapsed = Time.time - lastResponseStartTime;
            LogMessage($"🔄 Content received after {elapsed:F1}s - Resetting timeout");
            lastResponseStartTime = Time.time; // Reset the timer
        }
        
        // Process the alternative content
        if (companionUI != null)
        {
            companionUI.OnAssistantResponse(content);
        }
    }
    
    // RAG Integration (simplified for coroutines)
    private void GetRAGEnhancedInstructions(System.Action<string> callback)
    {
        LogMessage($"GetRAGEnhancedInstructions called - enableRAGContext: {enableRAGContext}, ragClient != null: {ragClient != null}");
        
        if (!enableRAGContext)
        {
            LogMessage("RAG context disabled, using default instructions");
            callback?.Invoke(GetDefaultInstructions());
            return;
        }
        
        if (ragClient == null)
        {
            LogError("RAG client is null, using default instructions");
            callback?.Invoke(GetDefaultInstructions());
            return;
        }
        
        LogMessage("Starting RAG instructions coroutine");
        StartCoroutine(GetRAGInstructionsCoroutine(callback));
    }
    
    private IEnumerator GetRAGInstructionsCoroutine(System.Action<string> callback)
    {
        if (!enableRAGContext || ragClient == null)
        {
            callback?.Invoke(GetDefaultInstructions());
            yield break;
        }
        
        LogMessage("Querying RAG for conversation context...");
        
        // Query for recent conversation context with the user's current input if available
        string currentContext = currentTranscript.Length > 0 ? currentTranscript.ToString() : "conversation memory and personal information";
        
        List<RAGResult> ragResults = new List<RAGResult>();
        bool queryCompleted = false;
        
        // Start the async query using a coroutine wrapper to stay on main thread
        StartCoroutine(QueryRAGAsyncWrapper(currentContext, userId, maxRAGResults, (results) =>
        {
            ragResults = results ?? new List<RAGResult>();
            queryCompleted = true;
            LogMessage($"RAG query for '{currentContext}' returned {ragResults.Count} results");
        }));
        
        // Wait for query completion
        while (!queryCompleted)
        {
            yield return null;
        }
        
        // Build enhanced instructions with RAG context
        string enhancedInstructions = BuildEnhancedInstructions(ragResults);
        callback?.Invoke(enhancedInstructions);
    }
    
    private IEnumerator QueryRAGAsyncWrapper(string query, string userId, int maxResults, System.Action<List<RAGResult>> callback)
    {
        // Convert async call to coroutine to avoid threading issues
        var queryTask = ragClient.QueryAsync(query, userId, maxResults);
        
        // Wait for task completion without using Task.Run (stays on main thread)
        while (!queryTask.IsCompleted)
        {
            yield return null;
        }
        
        try
        {
            var results = queryTask.Result;
            callback?.Invoke(results);
        }
        catch (System.Exception ex)
        {
            LogError($"Failed to query RAG context: {ex.Message}");
            callback?.Invoke(new List<RAGResult>());
        }
    }
    
    private string BuildEnhancedInstructions(List<RAGResult> ragResults)
    {
        var baseInstructions = GetDefaultInstructions();
        
        if (ragResults == null || ragResults.Count == 0)
        {
            LogMessage("No RAG context found, using default instructions");
            return baseInstructions;
        }
        
        LogMessage($"Found {ragResults.Count} relevant conversation memories");
        
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine(baseInstructions);
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("## Recent Conversation Context:");
        contextBuilder.AppendLine("You have access to the following recent conversation history and personal information about the user:");
        contextBuilder.AppendLine();
        
        foreach (var result in ragResults)
        {
            if (!string.IsNullOrEmpty(result.text))
            {
                contextBuilder.AppendLine($"- {result.text}");
                LogMessage($"Including RAG context: {result.text.Substring(0, Math.Min(100, result.text.Length))}...");
            }
        }
        
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("## Important Instructions:");
        contextBuilder.AppendLine("- Use ONLY the information provided in the context above");
        contextBuilder.AppendLine("- If asked about names, people, or specific details NOT mentioned in the context, say \"I don't have that information from our previous conversations\"");
        contextBuilder.AppendLine("- DO NOT make up or invent names, facts, or details that aren't in the provided context");
        contextBuilder.AppendLine("- Reference previous conversations naturally when the context supports it");
        contextBuilder.AppendLine("- If the context is empty or irrelevant, respond based on the current conversation only");
        
        var enhancedInstructions = contextBuilder.ToString();
        LogMessage($"Enhanced instructions built with {ragResults.Count} context items");
        
        return enhancedInstructions;
    }
    
    private bool ContainsNameIndicators(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        
        string lowerText = text.ToLower();
        
        // Check for common name-related phrases
        string[] nameIndicators = {
            "my name is", "i'm called", "call me", "i am", "named", 
            "his name", "her name", "their name", "name is",
            "what's my name", "who am i", "do you remember my name"
        };
        
        return nameIndicators.Any(indicator => lowerText.Contains(indicator));
    }
    
    // Public method for testing RAG functionality
    public void TestRAGConnection()
    {
        if (!enableRAGContext || ragClient == null)
        {
            LogError("Cannot test RAG - context disabled or client missing");
            return;
        }
        
        LogMessage("Testing RAG connection by storing a test message...");
        
        var testMessage = $"RAG test message - {System.DateTime.Now}";
        StartCoroutine(TestRAGCoroutine(testMessage));
    }
    
    private IEnumerator TestRAGCoroutine(string testMessage)
    {
        // Test storage
        LogMessage($"Testing RAG storage with: {testMessage}");
        
        var metadata = new Dictionary<string, object>
        {
            ["test"] = true,
            ["timestamp"] = System.DateTime.UtcNow.ToString()
        };
        
        var storeTask = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                bool result = await ragClient.StoreMemoryAsync(userId, "test", testMessage, metadata);
                LogMessage($"RAG store test result: {result}");
                return result;
            }
            catch (System.Exception ex)
            {
                LogError($"RAG store test failed: {ex.Message}");
                return false;
            }
        });
        
        while (!storeTask.IsCompleted)
        {
            yield return null;
        }
        
        if (storeTask.Result)
        {
            LogMessage("RAG storage test successful!");
            
            // Test retrieval
            yield return new WaitForSeconds(1f); // Wait a moment
            
            var queryTask = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var results = await ragClient.QueryAsync("RAG test", userId, 1);
                    LogMessage($"RAG query test returned {results?.Count ?? 0} results");
                    return results;
                }
                catch (System.Exception ex)
                {
                    LogError($"RAG query test failed: {ex.Message}");
                    return new List<RAGResult>();
                }
            });
            
            while (!queryTask.IsCompleted)
            {
                yield return null;
            }
            
            if (queryTask.Result?.Count > 0)
            {
                LogMessage("✅ RAG connection test PASSED - storage and retrieval working!");
            }
            else
            {
                LogMessage("❌ RAG retrieval test failed - storage worked but query returned no results");
            }
        }
        else
        {
            LogMessage("❌ RAG connection test FAILED - storage not working");
        }
    }
    
    private IEnumerator SendTextMessageWithRAGContext(string message)
    {
        if (!enableRAGContext || ragClient == null)
        {
            SendDirectTextMessage(message);
            yield break;
        }
        
        // For now, send message directly without RAG context to avoid async issues
        // TODO: Implement proper coroutine-based RAG query
        SendDirectTextMessage(message);
        yield break;
    }
    
    private void SendDirectTextMessage(string message)
    {
        if (dataChannel?.ReadyState != RTCDataChannelState.Open)
        {
            OnError?.Invoke("Data channel not ready");
            return;
        }
        
        var evt = new JObject
        {
            ["type"] = "conversation.item.create",
            ["item"] = new JObject
            {
                ["type"] = "message",
                ["role"] = "user",
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "input_text",
                        ["text"] = message
                    }
                }
            }
        };
        
        // Send message
        dataChannel.Send(Encoding.UTF8.GetBytes(evt.ToString(Formatting.None)));
        
        // For text messages, create response immediately since no audio buffer is involved
        var responseEvt = new JObject 
        { 
            ["type"] = "response.create",
            ["response"] = new JObject
            {
                ["modalities"] = new JArray { "text", "audio" }
            }
        };
        
        LogMessage($"🔍 DEBUG: Sending response.create with modalities: {responseEvt["response"]["modalities"]}");
        dataChannel.Send(Encoding.UTF8.GetBytes(responseEvt.ToString(Formatting.None)));
        
        LogMessage($"Sent text message and created response: {message}");
    }
    
    private void SendClearAudioBuffer()
    {
        if (dataChannel?.ReadyState != RTCDataChannelState.Open)
        {
            LogError("Cannot clear audio buffer - data channel not ready");
            return;
        }
        
        // Clear any existing audio buffer to start fresh
        var clearEvt = new JObject { ["type"] = "input_audio_buffer.clear" };
        dataChannel.Send(Encoding.UTF8.GetBytes(clearEvt.ToString(Formatting.None)));
        
        LogMessage("Cleared input audio buffer for fresh start");
    }
    
    private void SendResponseCancel()
    {
        if (dataChannel?.ReadyState != RTCDataChannelState.Open)
        {
            LogError("Cannot send response cancel - data channel not ready");
            return;
        }
        
        // Send response cancel event to interrupt AI
        var cancelEvt = new JObject { ["type"] = "response.cancel" };
        dataChannel.Send(Encoding.UTF8.GetBytes(cancelEvt.ToString(Formatting.None)));
        
        LogMessage("Sent response cancel to OpenAI");
    }
    
    private IEnumerator StreamAudioToOpenAI()
    {
        LogMessage("Starting audio streaming coroutine");
        
        while (isTalking && microphoneClip != null)
        {
            // Process audio data with error handling (no yield in try-catch)
            ProcessAudioChunk();
            
            yield return new WaitForSeconds(0.1f); // Stream in 100ms chunks
        }
        
        LogMessage("Audio streaming coroutine ended");
    }
    
    private void ProcessAudioChunk()
    {
        try
        {
            int currentPosition = Microphone.GetPosition(null);
            
            if (currentPosition != lastMicrophonePosition)
            {
                // Calculate how much new audio data we have
                int sampleLength = currentPosition - lastMicrophonePosition;
                if (sampleLength < 0)
                {
                    // Handle wrap-around (circular buffer)
                    sampleLength = microphoneClip.samples - lastMicrophonePosition + currentPosition;
                }
                
                if (sampleLength > 0 && sampleLength < microphoneClip.samples)
                {
                    // Get the new audio samples (with bounds checking)
                    float[] audioData = new float[sampleLength];
                    microphoneClip.GetData(audioData, lastMicrophonePosition);
                    
                    // Convert to PCM16 and send to OpenAI
                    SendAudioDataToOpenAI(audioData);
                    
                    lastMicrophonePosition = currentPosition;
                }
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error processing audio chunk: {ex.Message}");
        }
    }
    
    private void SendAudioDataToOpenAI(float[] audioData)
    {
        if (dataChannel?.ReadyState != RTCDataChannelState.Open)
        {
            return;
        }
        
        try
        {
            // Convert float audio to PCM16 bytes
            byte[] pcm16Data = new byte[audioData.Length * 2];
            for (int i = 0; i < audioData.Length; i++)
            {
                short sample = (short)(audioData[i] * 32767f);
                pcm16Data[i * 2] = (byte)(sample & 0xFF);
                pcm16Data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            
            // Encode to base64
            string base64Audio = System.Convert.ToBase64String(pcm16Data);
            
            // Send audio append event
            var audioEvent = new JObject
            {
                ["type"] = "input_audio_buffer.append",
                ["audio"] = base64Audio
            };
            
            dataChannel.Send(Encoding.UTF8.GetBytes(audioEvent.ToString(Formatting.None)));
            
            LogMessage($"Sent {audioData.Length} audio samples to OpenAI");
        }
        catch (System.Exception ex)
        {
            LogError($"Error sending audio data: {ex.Message}");
        }
    }
    
    private void HandleSpeechStarted(JObject message)
    {
        LogMessage("Server VAD detected speech started");
        
        // Update UI to show speech is being detected
        if (companionUI != null)
        {
            companionUI.UpdateStatusText("Speech detected - keep talking...");
        }
        
        // Update session instructions with fresh RAG context when user starts speaking
        if (enableRAGContext)
        {
            StartCoroutine(UpdateSessionInstructionsWithRAG());
        }
    }
    
    private IEnumerator UpdateSessionInstructionsWithRAG()
    {
        LogMessage("Updating session instructions with fresh RAG context...");
        
        // Update the current transcript for better RAG targeting
        string instructions = GetDefaultInstructions();
        bool instructionsReceived = false;
        string enhancedInstructions = "";
        
        GetRAGEnhancedInstructions((result) => {
            enhancedInstructions = result;
            instructionsReceived = true;
        });
        
        // Wait for RAG instructions
        while (!instructionsReceived)
        {
            yield return null;
        }
        
        // Send updated session configuration
        if (dataChannel?.ReadyState == RTCDataChannelState.Open)
        {
            var updateEvt = new JObject
            {
                ["type"] = "session.update",
                ["session"] = new JObject
                {
                    ["instructions"] = enhancedInstructions
                }
            };
            
            dataChannel.Send(Encoding.UTF8.GetBytes(updateEvt.ToString(Formatting.None)));
            LogMessage("Updated session instructions with fresh RAG context");
        }
    }
    
    private void HandleSpeechStopped(JObject message)
    {
        LogMessage("Server VAD detected speech stopped - processing audio");
        
        // Update UI to show we're processing
        if (companionUI != null)
        {
            companionUI.ShowRecordingIndicator(false);
            companionUI.ShowProcessingIndicator(true);
            companionUI.UpdateStatusText("Processing speech...");
        }
        
        // Disable microphone since speech ended
        if (localMicTrack != null)
        {
            localMicTrack.Enabled = false;
            isTalking = false;
        }
    }
    
    private void HandleOutputAudioBufferStarted(JObject message)
    {
        LogMessage("AI audio output started - transitioning UI to PlayingAudio state");
        
        // Update UI to show AI is speaking with interrupt capability
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(true);
            
            // Transition to PlayingAudio state which shows INTERRUPT button by default
            companionUI.ShowAudioPlaybackIndicator(true);
            companionUI.UpdateStatusText("AI is speaking...");
        }
    }
    
    private void HandleOutputAudioBufferStopped(JObject message)
    {
        var responseId = message["response_id"]?.ToString();
        LogMessage($"🎵 AI audio buffer chunk stopped (response_id: {responseId}) - continuing to wait for response.done");
        LogMessage($"🎵 isAIResponding: {isAIResponding} - Audio should continue playing until response.done");
        
        // IMPORTANT: Don't reset UI state here! This just means one audio chunk ended.
        // The AI might still be generating more audio chunks.
        // Only response.done should reset the UI state.
        
        // Keep the audio playback indicator active until response.done
        // This prevents premature audio cutoff
        
        LogMessage("🎵 Audio buffer stopped but UI state remains active for continued playback");
    }
    
    private void HandleResponseCancelled(JObject jo)
    {
        LogMessage("❌ Response cancelled - AI response was interrupted");
        isAIResponding = false;
        
        // Cancel the OpenAI response tracking
        CancelOpenAIResponse();
        
        // Update UI
        if (companionUI != null)
        {
            companionUI.ShowAudioPlaybackIndicator(false);
            companionUI.UpdateStatusText("Response cancelled - Tap to talk");
            companionUI.ResetToIdleState();
        }
    }
    
    private void HandleAudioTranscriptDelta(JObject message)
    {
        var transcript = message["delta"]?.ToString();
        if (!string.IsNullOrEmpty(transcript))
        {
            LogMessage($"Audio transcript delta: {transcript}");
            // Accumulate the actual spoken text
            if (currentAITranscript == null)
            {
                currentAITranscript = new StringBuilder();
            }
            currentAITranscript.Append(transcript);
        }
    }
    
    private void HandleAudioTranscriptDone(JObject message)
    {
        var transcript = message["transcript"]?.ToString();
        if (!string.IsNullOrEmpty(transcript))
        {
            LogMessage($"Complete audio transcript (what AI actually said): {transcript}");
            
            // Store the actual spoken response instead of the text response
            StoreAIResponse(transcript);
            
            // Update UI with what was actually spoken
            if (companionUI != null)
            {
                // Clear any previous text response and show the actual spoken version
                // (UI updates handled by companionUI)
            }
            
            // Create chat post using ChatPostPrefab
            CreateChatPost(ChatPostPrefab.ChatPostType.text, transcript, false);
            
            currentAITranscript?.Clear();
        }
    }
    
    private void HandleFunctionCallArgumentsDelta(JObject message)
    {
        // Handle incremental function call arguments
        LogMessage("Function call arguments delta received");
    }
    
    // Track processed function calls to prevent duplicates
    private HashSet<string> processedFunctionCalls = new HashSet<string>();
    
    private void HandleFunctionCallArgumentsDone(JObject message)
    {
        LogMessage("Function call arguments complete");
        LogMessage($"🔍 Raw message: {message.ToString(Formatting.None)}");
        
        // Extract function call details
        var call_id = message["call_id"]?.ToString();
        var name = message["name"]?.ToString();
        var arguments = message["arguments"]?.ToString();
        
        LogMessage($"🔍 Extracted - call_id: {call_id}, name: {name}, args length: {arguments?.Length ?? 0}");
        
        if (string.IsNullOrEmpty(call_id) || string.IsNullOrEmpty(name))
        {
            LogError("❌ Invalid function call - missing call_id or name");
            return;
        }
        
        // Check if we've already processed this function call
        if (processedFunctionCalls.Contains(call_id))
        {
            LogMessage($"⚠️ Function call {call_id} already processed, skipping duplicate");
            return;
        }
        
        // Mark this function call as processed
        processedFunctionCalls.Add(call_id);
        LogMessage($"🔒 Function call {call_id} marked as processed");
        
        // Clean up old function call IDs to prevent memory growth
        if (processedFunctionCalls.Count > 100)
        {
            processedFunctionCalls.Clear();
            LogMessage("🧹 Cleaned up processed function calls cache");
        }
        
        LogMessage($"Function call: {name} with args: {arguments}");
        
        // Execute the function call
        StartCoroutine(ExecuteFunctionCall(call_id, name, arguments));
    }
    
    private IEnumerator ExecuteFunctionCall(string callId, string functionName, string argumentsJson)
    {
        switch (functionName)
        {
            case "generate_image":
                yield return StartCoroutine(HandleImageGeneration(callId, argumentsJson));
                break;
                
            case "analyze_image":
                yield return StartCoroutine(HandleImageAnalysis(callId, argumentsJson));
                break;
                
            default:
                LogError($"Unknown function: {functionName}");
                SendFunctionCallResult(callId, $"Error: Unknown function '{functionName}'");
                break;
        }
    }
    
    private IEnumerator HandleImageGeneration(string callId, string argumentsJson)
    {
        LogMessage($"Generating image with args: {argumentsJson}");
        
        // Parse arguments
        var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(argumentsJson);
        var prompt = args.ContainsKey("prompt") ? args["prompt"].ToString() : "";
        var size = args.ContainsKey("size") ? args["size"].ToString() : "1024x1024";
        
        if (string.IsNullOrEmpty(prompt))
        {
            SendFunctionCallResult(callId, "Error: No prompt provided");
            yield break;
        }
        
        if (!enableRAGContext || string.IsNullOrEmpty(ragApiUrl))
        {
            SendFunctionCallResult(callId, "Error: RAG server not configured for image generation");
            yield break;
        }
        
        // Call RAG server's image generation endpoint
        var requestBody = new JObject
        {
            ["prompt"] = prompt,
            ["size"] = size,
            ["user_id"] = userId,
            ["session_id"] = ephemeralKey ?? "default-session",
            ["turn_id"] = $"img_{DateTime.Now.Ticks}"
        };
        
        string currentRagApiUrl = GetCurrentCloudRAGUrl();
        
        // Send immediate system prompt to let user know image generation is starting
        string systemMessage = GetImageGenerationSystemPrompt(prompt, size);
        LogDebug(DebugCategory.SystemPrompt, $"🎨 About to send system prompt: {systemMessage}");
        LogDebug(DebugCategory.SystemPrompt, $"🔌 Data channel state: {dataChannel?.ReadyState}");
        SendSystemPrompt(systemMessage);
        LogDebug(DebugCategory.SystemPrompt, $"✅ System prompt sent, proceeding with image generation...");
        
        using (UnityWebRequest request = new UnityWebRequest($"{currentRagApiUrl}/generate_image", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody.ToString());
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                
                if (response.ContainsKey("image_url"))
                {
                    // Use immediate image access for instant display
                    var imageUrl = response["image_url"].ToString();
                    var contentId = response.ContainsKey("content_id") ? response["content_id"].ToString() : "";
                    var storageType = response.ContainsKey("immediate_access") ? response["immediate_access"].ToString() : "unknown";
                    
                    LogMessage($"Image generated with {storageType} access: {imageUrl}");
                    
                    // Display the image immediately using the provided URL
                    yield return StartCoroutine(DownloadAndDisplayImage(imageUrl, prompt));
                    
                    // If we have a content ID, also store it for future reference
                    if (!string.IsNullOrEmpty(contentId))
                    {
                        LogMessage($"Image stored with ID: {contentId}");
                    }
                    
                    SendFunctionCallResult(callId, $"Image generated successfully: {prompt}");
                }
                else if (response.ContainsKey("content_id"))
                {
                    // Fallback: try to display stored image
                    var contentId = response["content_id"].ToString();
                    LogMessage($"Image generated and stored (fallback): {contentId}");
                    
                    yield return StartCoroutine(DisplayStoredImage(contentId, prompt));
                    
                    SendFunctionCallResult(callId, $"Image generated successfully: {prompt}");
                }
                else
                {
                    var error = response.ContainsKey("error") ? response["error"].ToString() : "Unknown error";
                    LogError($"Image generation failed: {error}");
                    SendFunctionCallResult(callId, $"Error generating image: {error}");
                }
            }
            else
            {
                LogError($"Image generation request failed: {request.error}");
                SendFunctionCallResult(callId, $"Error generating image: {request.error}");
            }
        }
    }
    
    private void SendSystemPrompt(string message)
    {
        LogDebug(DebugCategory.SystemPrompt, $"🔍 SendSystemPrompt called with message: {message}");
        LogDebug(DebugCategory.SystemPrompt, $"🔌 Data channel state: {dataChannel?.ReadyState}");
        LogDebug(DebugCategory.SystemPrompt, $"🔌 Data channel null: {dataChannel == null}");
        
        if (dataChannel?.ReadyState != RTCDataChannelState.Open)
        {
            LogError("❌ Data channel not available for system prompt");
            return;
        }
        
        // Create a system message conversation item
        var conversationItem = new JObject
        {
            ["type"] = "conversation.item.create",
            ["item"] = new JObject
            {
                ["type"] = "message",
                ["role"] = "assistant",
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = message
                    }
                }
            }
        };
        
        string jsonMessage = conversationItem.ToString(Formatting.None);
        LogDebug(DebugCategory.SystemPrompt, $"📤 Sending conversation item: {jsonMessage}");
        
        // Send the system prompt
        dataChannel.Send(Encoding.UTF8.GetBytes(jsonMessage));
        LogDebug(DebugCategory.SystemPrompt, $"💬 Sent system prompt: {message}");
        
        // Trigger response generation to show the message
        var responseCreate = new JObject
        {
            ["type"] = "response.create"
        };
        
        string responseJson = responseCreate.ToString(Formatting.None);
        LogDebug(DebugCategory.SystemPrompt, $"📤 Sending response.create: {responseJson}");
        
        dataChannel.Send(Encoding.UTF8.GetBytes(responseJson));
        LogDebug(DebugCategory.SystemPrompt, "🚀 Requested AI response for system prompt");
    }
    
    private string GetImageGenerationSystemPrompt(string prompt, string size)
    {
        // Create a friendly, dynamic system prompt based on the request
        var prompts = new List<string>
        {
            $"I'll get right on that! Creating your image of \"{prompt}\" - it will be a moment while I work my magic. ✨",
            $"On it! Generating your {size} image of \"{prompt}\" - this should only take a few moments. 🎨",
            $"Perfect! I'm crafting your image of \"{prompt}\" right now. Please wait while I bring your vision to life. 🌟",
            $"Got it! Working on your \"{prompt}\" image. This will be worth the wait! 🚀",
            $"Excellent choice! I'm generating your {size} image of \"{prompt}\" - just a moment please. ⏳"
        };
        
        // Pick a random prompt for variety
        int randomIndex = UnityEngine.Random.Range(0, prompts.Count);
        return prompts[randomIndex];
    }
    
    private IEnumerator HandleImageAnalysis(string callId, string argumentsJson)
    {
        LogMessage($"Analyzing image with args: {argumentsJson}");
        
        // Parse arguments
        var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(argumentsJson);
        var imageData = args.ContainsKey("image_data") ? args["image_data"].ToString() : "";
        var question = args.ContainsKey("question") ? args["question"].ToString() : "Describe what you see in this image";
        
        if (string.IsNullOrEmpty(imageData))
        {
            SendFunctionCallResult(callId, "Error: No image data provided");
            yield break;
        }
        
        if (!enableRAGContext || string.IsNullOrEmpty(ragApiUrl))
        {
            SendFunctionCallResult(callId, "Error: RAG server not configured for image analysis");
            yield break;
        }
        
        // Call RAG server's image analysis endpoint
        var requestBody = new JObject
        {
            ["image_data"] = imageData,
            ["question"] = question,
            ["user_id"] = userId
        };
        
        string currentRagApiUrl = GetCurrentCloudRAGUrl();
        
        using (UnityWebRequest request = new UnityWebRequest($"{currentRagApiUrl}/analyze_image", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody.ToString());
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                
                if (response.ContainsKey("description"))
                {
                    var description = response["description"].ToString();
                    LogMessage($"Image analysis result: {description}");
                    SendFunctionCallResult(callId, description);
                }
                else
                {
                    var error = response.ContainsKey("error") ? response["error"].ToString() : "Unknown error";
                    LogError($"Image analysis failed: {error}");
                    SendFunctionCallResult(callId, $"Error analyzing image: {error}");
                }
            }
            else
            {
                LogError($"Image analysis request failed: {request.error}");
                SendFunctionCallResult(callId, $"Error analyzing image: {request.error}");
            }
        }
    }
    
    private IEnumerator DisplayStoredImage(string contentId, string prompt)
    {
        // Load image from RAG storage
        string imageUrl = $"{GetCurrentCloudRAGUrl()}/image/{contentId}";
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success) 
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                
                // Display image in UI using ChatPostPrefab
                CreateChatPost(ChatPostPrefab.ChatPostType.image, prompt, false, texture);
                
                LogMessage($"Stored image displayed in chat: {prompt} (ID: {contentId})");
            }
            else
            {
                LogError($"Failed to load stored image: {request.error}");
            }
        }
    }
    
    private IEnumerator DownloadAndDisplayImage(string imageUrl, string prompt)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success) 
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                
                // Display image in UI using ChatPostPrefab
                CreateChatPost(ChatPostPrefab.ChatPostType.image, prompt, false, texture);
                
                LogMessage($"Image displayed in chat: {prompt}");
            }
            else
            {
                LogError($"Failed to download image: {request.error}");
            }
        }
    }
    
    private void SendFunctionCallResult(string callId, string result)
    {
        var functionOutput = new JObject
        {
            ["type"] = "conversation.item.create",
            ["item"] = new JObject
            {
                ["type"] = "function_call_output",
                ["call_id"] = callId,
                ["output"] = result
            }
        };
        
        if (dataChannel?.ReadyState == RTCDataChannelState.Open)
        {
            dataChannel.Send(Encoding.UTF8.GetBytes(functionOutput.ToString(Formatting.None)));
            LogMessage($"✅ Sent function call result for {callId}: {result.Substring(0, Math.Min(100, result.Length))}...");
        }
        
        // Trigger response generation
        var responseCreate = new JObject
        {
            ["type"] = "response.create"
        };
        
        LogMessage($"🔍 DEBUG: Sending response.create (no modalities specified) for function call result");
        
        if (dataChannel?.ReadyState == RTCDataChannelState.Open)
        {
            dataChannel.Send(Encoding.UTF8.GetBytes(responseCreate.ToString(Formatting.None)));
            LogMessage($"🚀 Requested AI response after tool result for {callId}");
        }
    }
    
    // RAG Memory Storage Methods
    private void StoreUserMessage(string message)
    {
        LogMessage($"StoreUserMessage called - enableRAGContext: {enableRAGContext}, ragClient != null: {ragClient != null}, message length: {message?.Length ?? 0}");
        
        if (!enableRAGContext)
        {
            LogMessage("RAG context disabled, not storing user message");
            return;
        }
        
        if (ragClient == null)
        {
            LogError("RAG client is null, cannot store user message");
            return;
        }
        
        if (string.IsNullOrEmpty(message))
        {
            LogMessage("Message is empty, not storing");
            return;
        }
        
        LogMessage($"Starting to store user message: {message}");
        StartCoroutine(StoreUserMessageCoroutine(message));
    }
    
    private void StoreAIResponse(string response)
    {
        LogMessage($"StoreAIResponse called - enableRAGContext: {enableRAGContext}, ragClient != null: {ragClient != null}, response length: {response?.Length ?? 0}");
        
        if (!enableRAGContext)
        {
            LogMessage("RAG context disabled, not storing AI response");
            return;
        }
        
        if (ragClient == null)
        {
            LogError("RAG client is null, cannot store AI response");
            return;
        }
        
        if (string.IsNullOrEmpty(response))
        {
            LogMessage("Response is empty, not storing");
            return;
        }
        
        LogMessage($"Starting to store AI response: {response}");
        StartCoroutine(StoreAIResponseCoroutine(response));
    }
    
    private IEnumerator StoreUserMessageCoroutine(string message)
    {
        LogMessage($"Storing user message in RAG: {message.Substring(0, Math.Min(50, message.Length))}...");
        
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["message_type"] = "user_input",
            ["source"] = "voice",
            ["conversation_turn"] = System.DateTime.UtcNow.Ticks,
            ["contains_names"] = ContainsNameIndicators(message),
            ["is_question"] = message.TrimEnd().EndsWith("?"),
            ["session_id"] = System.Guid.NewGuid().ToString()
        };
        
        // Call async method directly on main thread
        var storeTask = ragClient.StoreMemoryAsync(userId, "conversation", message, metadata);
        
        // Wait for task completion without Task.Run (stays on main thread)
        while (!storeTask.IsCompleted)
        {
            yield return null;
        }
        
        try
        {
            if (storeTask.Result)
            {
                LogMessage("User message stored successfully in RAG");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Failed to store user message: {ex.Message}");
        }
    }
    
    private IEnumerator StoreAIResponseCoroutine(string response)
    {
        LogMessage($"Storing AI response in RAG: {response.Substring(0, Math.Min(50, response.Length))}...");
        
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["message_type"] = "ai_response",
            ["source"] = "realtime_api",
            ["model"] = realtimeModel,
            ["conversation_turn"] = System.DateTime.UtcNow.Ticks,
            ["contains_names"] = ContainsNameIndicators(response),
            ["session_id"] = System.Guid.NewGuid().ToString()
        };
        
        // Call async method directly on main thread
        var storeTask = ragClient.StoreMemoryAsync(userId, "conversation", response, metadata);
        
        // Wait for task completion without Task.Run (stays on main thread)
        while (!storeTask.IsCompleted)
        {
            yield return null;
        }
        
        try
        {
            if (storeTask.Result)
            {
                LogMessage("AI response stored successfully in RAG");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Failed to store AI response: {ex.Message}");
        }
    }
    
    private void HandleRateLimitsUpdated(JObject message)
    {
        // Just log rate limit info, no action needed
        LogMessage("Rate limits updated by OpenAI");
    }
    
    private void SendSessionConfiguration()
    {
        if (dataChannel?.ReadyState != RTCDataChannelState.Open)
        {
            LogError("Cannot send session config - data channel not ready");
            return;
        }
        
        LogMessage("Preparing session configuration with RAG context...");
        StartCoroutine(SendSessionConfigurationWithRAG());
    }
    
    private IEnumerator SendSessionConfigurationWithRAG()
    {
        string instructions = GetDefaultInstructions();
        
        // Get enhanced instructions with RAG context
        if (enableRAGContext)
        {
            bool instructionsReceived = false;
            string enhancedInstructions = "";
            
            GetRAGEnhancedInstructions((result) => {
                enhancedInstructions = result;
                instructionsReceived = true;
            });
            
            // Wait for RAG instructions
            while (!instructionsReceived)
            {
                yield return null;
            }
            
            instructions = enhancedInstructions;
        }
        
        // Configure session for voice input with server VAD
        var configEvt = new JObject
        {
            ["type"] = "session.update",
            ["session"] = new JObject
            {
                ["modalities"] = new JArray { "text", "audio" },
                ["instructions"] = instructions,
                ["voice"] = "alloy",
                ["input_audio_format"] = "pcm16",
                ["output_audio_format"] = "pcm16",
                ["input_audio_transcription"] = new JObject
                {
                    ["model"] = "whisper-1"
                },
                ["turn_detection"] = new JObject
                {
                    ["type"] = "server_vad",
                    ["threshold"] = 0.5,
                    ["prefix_padding_ms"] = 1000, // Increased from 300ms to 1000ms for better audio continuity
                    ["silence_duration_ms"] = 5000 // Increased from 2000ms to 5000ms to prevent premature cutoffs
                },
                ["tool_choice"] = "auto",
                ["tools"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "function",
                        ["name"] = "generate_image",
                        ["description"] = "Generate an image using DALL-E 3 based on a text prompt",
                        ["parameters"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                ["prompt"] = new JObject
                                {
                                    ["type"] = "string",
                                    ["description"] = "The text prompt describing the image to generate"
                                },
                                ["size"] = new JObject
                                {
                                    ["type"] = "string",
                                    ["description"] = "The size of the image",
                                    ["enum"] = new JArray { "1024x1024", "1792x1024", "1024x1792" },
                                    ["default"] = "1024x1024"
                                }
                            },
                            ["required"] = new JArray { "prompt" }
                        }
                    },
                    new JObject
                    {
                        ["type"] = "function", 
                        ["name"] = "analyze_image",
                        ["description"] = "Analyze and describe an image using GPT-4 Vision",
                        ["parameters"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                ["image_data"] = new JObject
                                {
                                    ["type"] = "string",
                                    ["description"] = "Base64 encoded image data"
                                },
                                ["question"] = new JObject
                                {
                                    ["type"] = "string", 
                                    ["description"] = "Optional specific question about the image",
                                    ["default"] = "Describe what you see in this image"
                                }
                            },
                            ["required"] = new JArray { "image_data" }
                        }
                    }
                },
                ["temperature"] = 0.8,
                ["max_response_output_tokens"] = 4096
            }
        };
        
        if (dataChannel?.ReadyState == RTCDataChannelState.Open)
        {
            dataChannel.Send(Encoding.UTF8.GetBytes(configEvt.ToString(Formatting.None)));
            LogMessage("Sent session configuration with RAG context");
        }
        else
        {
            LogError("Data channel closed while preparing session config");
        }
    }
    
    // Connection management
    private IEnumerator AttemptReconnection()
    {
        if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
        {
            OnError?.Invoke("Maximum reconnection attempts reached");
            yield break;
        }
        
        reconnectAttempts++;
        LogMessage($"Attempting reconnection ({reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})");
        
        // Clean up current connection
        CleanupConnection();
        
        // Wait before reconnecting
        yield return new WaitForSeconds(2f);
        
        // Attempt new connection
        StartCoroutine(CreateSessionAndConnect());
    }
    
    private void CleanupConnection()
    {
        isConnectionActive = false;
        isTalking = false;
        isAIResponding = false;
        
        if (localMicTrack != null)
        {
            localMicTrack.Enabled = false;
            localMicTrack.Dispose();
            localMicTrack = null;
        }
        
        if (dataChannel != null)
        {
            dataChannel.OnMessage = null;
            dataChannel.OnOpen = null;
            dataChannel.OnClose = null;
            dataChannel.OnError = null;
            dataChannel = null;
        }
        
        if (peerConnection != null)
        {
            peerConnection.OnTrack = null;
            peerConnection.OnIceConnectionChange = null;
            peerConnection.OnConnectionStateChange = null;
            peerConnection.Close();
            peerConnection = null;
        }
        
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }
        
        ephemeralKey = null;
        
        LogMessage("Connection cleaned up");
    }
    
    // Utility methods
    private string GetAPIKey()
    {
        // Try different sources for API key
        if (ragConfig?.Settings != null && !string.IsNullOrEmpty(ragConfig.Settings.openAIAPIKey))
        {
            return ragConfig.Settings.openAIAPIKey;
        }
        
        return PlayerPrefs.GetString("OpenAIApiKey", "");
    }
    
    private string GetDefaultInstructions()
    {
        string baseInstructions = "You are a helpful AI assistant with memory capabilities. " +
               "You can remember information from previous conversations with this user. " +
               "Respond naturally and conversationally, incorporating relevant personal details when available. " +
               "Keep responses concise but informative. " +
               "REMINDER CAPABILITY: You CAN set reminders for users! When they ask you to 'remind me' of something, " +
               "acknowledge that you'll set the reminder for them. The system will automatically detect and process reminder requests. " +
               "IMPORTANT: Only reference information you have been explicitly provided in your context. " +
               "If you don't know something specific (like names, dates, or details), " +
               "clearly state that you don't have that information rather than guessing or making something up.";
        
        LogMessage($"Generated default instructions: {baseInstructions.Substring(0, Math.Min(100, baseInstructions.Length))}...");
        return baseInstructions;
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileRealtimeChat] {message}");
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MobileRealtimeChat] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileRealtimeChat] {message}");
    }
    
    private bool IsPlaceholderUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        
        return url == "https://your-rag-api.com" || 
               url == "https://your-rag-api.up.railway.app" ||
               url.Contains("your-rag-api") ||
               url.Contains("your-app-name");
    }
    
    // Audio testing and debugging
    public void TestAudioOutput()
    {
        if (remoteAudioSource == null)
        {
            LogError("Remote audio source not initialized");
            return;
        }
        
        LogMessage($"Audio Source Status:");
        LogMessage($"- Volume: {remoteAudioSource.volume}");
        LogMessage($"- Is Playing: {remoteAudioSource.isPlaying}");
        LogMessage($"- Mute: {remoteAudioSource.mute}");
        LogMessage($"- Audio Settings Output Sample Rate: {AudioSettings.outputSampleRate}");
        LogMessage($"- Audio Settings Speaker Mode: {AudioSettings.speakerMode}");
        
        // Check if we have a valid audio track
        LogMessage($"- Has WebRTC Track: {remoteAudioSource.clip != null}");
    }
    
    // Test method for UI button - triggers a simple AI response to verify the system works
    public void TestAIResponse()
    {
        string testMessage = "Hello! This is a test response to verify the AI audio system is working correctly. If you can hear this message, then proactive AI delivery is functioning properly.";
        
        LogMessage($"🧪 TEST: Triggering test AI response");
        
        if (!isConnectionActive)
        {
            LogError("Cannot test AI response - no active connection. Please tap 'MIC Talk' first to establish connection.");
            
            if (companionUI != null)
            {
                companionUI.AddMessage("⚠️ No Connection: Please tap 'MIC Talk' first to establish WebRTC connection, then try the test button.", "system", false);
                companionUI.UpdateStatusText("Connection required for audio test");
            }
            return;
        }
        
        // Use direct message delivery for test (bypasses full conversation system)
        TriggerDirectAIMessage(testMessage);
        
        LogMessage($"✅ Test AI response triggered - should hear audio if system is working");
    }
    
    // Trigger AI to speak a message directly without full conversation processing
    public void TriggerDirectAIMessage(string message)
    {
        LogMessage($"🗣️ Triggering direct AI message: {message}");
        
        if (!isConnectionActive)
        {
            LogError("Cannot deliver direct message - no active connection");
            
            // Fall back to UI display only using ChatPostPrefab for consistency
            DisplayMessageInChat(message, false);
            if (companionUI != null)
            {
                companionUI.UpdateStatusText("Message delivered (no audio connection)");
            }
            return;
        }
        
        try
        {
            // Create a conversation item event to make AI speak the message directly
            // Using conversation.item.create instead of response.create to prevent word-by-word processing
            var messageEvent = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "assistant",
                    content = new[]
                    {
                        new { type = "text", text = message }
                    }
                }
            };
            
            string messageJson = JsonConvert.SerializeObject(messageEvent);
            
            // Send through data channel
            if (dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageJson);
                dataChannel.Send(messageBytes);
                
                LogMessage($"✅ Sent direct AI message through data channel as conversation item");
                
                // Display message in chat using ChatPostPrefab for consistency
                DisplayMessageInChat(message, false);
                
                // Update UI to show AI is speaking
                if (companionUI != null)
                {
                    companionUI.ShowAudioPlaybackIndicator(true);
                    companionUI.UpdateStatusText("AI is speaking...");
                }
            }
            else
            {
                LogError("Data channel not available for direct message delivery");
                
                // Fall back to UI display using ChatPostPrefab
                DisplayMessageInChat(message, false);
                if (companionUI != null)
                {
                    companionUI.UpdateStatusText("Message delivered (audio unavailable)");
                }
            }
        }
        catch (System.Exception e)
        {
            LogError($"Failed to trigger direct AI message: {e.Message}");
            
            // Fall back to UI display using ChatPostPrefab
            DisplayMessageInChat(message, false);
            if (companionUI != null)
            {
                companionUI.UpdateStatusText("Message delivered (fallback mode)");
            }
        }
    }
    
    // Display message in chat using ChatPostPrefab for consistency with history
    private void DisplayMessageInChat(string message, bool isUserMessage)
    {
        var companionUI = FindObjectOfType<MobileCompanionUI>();
        if (companionUI != null)
        {
            // Use the ChatPostPrefab system if available, otherwise fall back to AddMessage
            if (companionUI.HasChatPostPrefab())
            {
                companionUI.CreateChatPostForMessage(message, isUserMessage);
            }
            else
            {
                companionUI.AddMessage(message, isUserMessage ? "user" : "assistant", true);
            }
        }
    }
    
    // Trigger AI to deliver reminder messages proactively
    public void TriggerAIReminderDelivery(string aiMessage)
    {
        LogMessage($"🔔 Triggering proactive AI reminder delivery: {aiMessage}");
        
        if (!isConnectionActive)
        {
            LogError("Cannot trigger AI reminder - no active connection");
            
            // Fall back to UI display only using ChatPostPrefab for consistency
            DisplayMessageInChat(aiMessage, false);
            if (companionUI != null)
            {
                companionUI.UpdateStatusText("Reminder delivered (no audio connection)");
            }
            return;
        }
        
        try
        {
            // Create a text message event to send the AI reminder
            var messageEvent = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "assistant",
                    content = new[]
                    {
                        new { type = "text", text = aiMessage }
                    }
                }
            };
            
            string messageJson = JsonConvert.SerializeObject(messageEvent);
            
            // Send through data channel
            if (dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageJson);
                dataChannel.Send(messageBytes);
                
                LogMessage($"✅ Sent AI reminder through data channel");
                
                // Display message in chat using ChatPostPrefab for consistency
                DisplayMessageInChat(aiMessage, false);
                
                // Update UI to show AI is speaking
                if (companionUI != null)
                {
                    companionUI.ShowAudioPlaybackIndicator(true);
                    companionUI.UpdateStatusText("Delivering your reminder...");
                }
            }
            else
            {
                LogError("Data channel not available for AI reminder delivery");
                
                // Fall back to UI display using ChatPostPrefab
                DisplayMessageInChat(aiMessage, false);
                if (companionUI != null)
                {
                    companionUI.UpdateStatusText("Reminder delivered (audio unavailable)");
                }
            }
        }
        catch (System.Exception e)
        {
            LogError($"Failed to trigger AI reminder delivery: {e.Message}");
            
            // Fall back to UI display using ChatPostPrefab
            DisplayMessageInChat(aiMessage, false);
            if (companionUI != null)
            {
                companionUI.UpdateStatusText("Reminder delivered (fallback mode)");
            }
        }
    }
    
    // Public properties
    public bool IsConnected => isConnectionActive;
    public bool IsTalking => isTalking;
    public bool IsAIResponding => isAIResponding;
    
    public AudioSource RemoteAudioSource => remoteAudioSource;
    
    public void SetRemoteAudioSource(AudioSource audioSource)
    {
        remoteAudioSource = audioSource;
        if (audioSource != null)
        {
            LogMessage($"✅ Remote AudioSource set to: {audioSource.name}");
            LogAudioSourceConfiguration();
        }
        else
        {
            LogMessage("⚠️ Remote AudioSource cleared");
        }
    }
    
    public void SetBAiComponentManager(BAiComponentManagerV0002 manager)
    {
        baiComponentManager = manager;
        if (manager != null)
        {
            LogMessage($"✅ BAi Component Manager set to: {manager.name}");
        }
        else
        {
            LogMessage("⚠️ BAi Component Manager cleared");
        }
    }
    
    // Chat Post Creation Methods
    private void CreateChatPost(ChatPostPrefab.ChatPostType postType, string text, bool isHuman, Texture2D image = null)
    {
        if (coachChatRoot == null || chatPostPrefab == null) 
        {
            LogWarning("⚠️ Chat UI components not assigned - cannot create chat post");
            return;
        }
        
        // Check if we're currently loading conversation history to prevent duplicates
        var companionUI = FindObjectOfType<MobileCompanionUI>();
        if (companionUI != null && companionUI.IsLoadingHistory)
        {
            LogMessage("Skipping chat post creation - currently loading conversation history");
            return;
        }
        
        LogMessage($"🔍 Creating chat post: type={postType}, text='{text}', isHuman={isHuman}, image={(image != null ? $"valid ({image.width}x{image.height})" : "null")}");
        
        // Try to use shared chat container for consistency
        Transform targetContainer = GetSharedChatContainer();
        
        GameObject postObj = Instantiate(chatPostPrefab, targetContainer);
        ChatPostPrefab postComponent = postObj.GetComponent<ChatPostPrefab>();
        
        if (postComponent != null)
        {
            // Set AI background sprite if available
            if (aiBackgroundSprite != null && !isHuman)
            {
                postComponent.aiBackgroundSprite = aiBackgroundSprite;
            }
            
            // Initialize the post - now much simpler with RawImage support!
            if (image != null)
            {
                LogMessage($"🖼️ Initializing post with Texture2D image");
                postComponent.InitializeWithTexture(postType, text, isHuman, image);
            }
            else
            {
                LogMessage($"📝 Initializing text-only post");
                postComponent.Initialize(postType, text, isHuman);
            }
            
            LogMessage($"✅ Created chat post: {postType} - {(isHuman ? "User" : "AI")}");
        }
        else
        {
            LogError("❌ Failed to get ChatPostPrefab component from instantiated object");
        }
        
        // Scroll to bottom
        ScrollToBottom();
    }
    
    // Get shared chat container for consistency with MobileCompanionUI
    private Transform GetSharedChatContainer()
    {
        // Try to get the shared container from MobileCompanionUI
        var companionUI = FindObjectOfType<MobileCompanionUI>();
        if (companionUI != null)
        {
            var sharedContainer = companionUI.GetSharedChatContainer();
            if (sharedContainer != null && sharedContainer != coachChatRoot)
            {
                LogMessage("Using shared chat container from MobileCompanionUI");
                return sharedContainer;
            }
        }
        
        // Fallback to our own container
        return coachChatRoot;
    }
    
    private void ScrollToBottom()
    {
        if (chatScrollRect != null)
        {
            StartCoroutine(ScrollToBottomNextFrame());
        }
    }
    
    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        chatScrollRect.normalizedPosition = Vector2.zero;
    }
    
    private void LogAudioSourceConfiguration()
    {
        if (remoteAudioSource != null)
        {
            LogMessage($"🔊 AudioSource Configuration:");
            LogMessage($"   - Name: {remoteAudioSource.name}");
            LogMessage($"   - GameObject: {remoteAudioSource.gameObject.name}");
            LogMessage($"   - Volume: {remoteAudioSource.volume}");
            LogMessage($"   - Mute: {remoteAudioSource.mute}");
            LogMessage($"   - Play On Awake: {remoteAudioSource.playOnAwake}");
            LogMessage($"   - Loop: {remoteAudioSource.loop}");
            LogMessage($"   - Spatial Blend: {remoteAudioSource.spatialBlend}");
        }
        else
        {
            LogMessage("⚠️ No AudioSource configured for remote audio");
        }
    }
    
    // Image Analysis Methods
    public void AnalyzeUploadedImage(string base64ImageData)
    {
        LogMessage("Analyzing uploaded image...");
        
        if (!isConnectionActive)
        {
            LogError("Cannot analyze image - not connected to realtime service");
            return;
        }
        
        if (string.IsNullOrEmpty(base64ImageData))
        {
            LogError("Cannot analyze image - no image data provided");
            return;
        }
        
        // Create conversation item with image analysis request
        var conversationItem = new JObject
        {
            ["type"] = "conversation.item.create",
            ["item"] = new JObject
            {
                ["type"] = "message",
                ["role"] = "user",
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "input_text",
                        ["text"] = "Please describe what you see in this image in detail."
                    },
                    new JObject
                    {
                        ["type"] = "input_image", 
                        ["image"] = $"data:image/png;base64,{base64ImageData}"
                    }
                }
            }
        };
        
        LogMessage($"🖼️ Sending image analysis request: {conversationItem.ToString(Formatting.None).Substring(0, Math.Min(300, conversationItem.ToString(Formatting.None).Length))}...");
        
        // Send the conversation item
        if (dataChannel?.ReadyState == RTCDataChannelState.Open)
        {
            dataChannel.Send(Encoding.UTF8.GetBytes(conversationItem.ToString(Formatting.None)));
            LogMessage("✅ Sent image conversation item to OpenAI");
            
            // Trigger response generation
            var responseCreate = new JObject
            {
                ["type"] = "response.create"
            };
            
            LogMessage($"🔍 DEBUG: Sending response.create (no modalities specified) for image analysis");
            
            dataChannel.Send(Encoding.UTF8.GetBytes(responseCreate.ToString(Formatting.None)));
            LogMessage("🚀 Requested AI response for image analysis - waiting for response...");
        }
        else
        {
            LogError("❌ Data channel not available for image analysis");
        }
    }
    
    private void OnDestroy()
    {
        CleanupConnection();
    }
    
    // Session response classes
    [Serializable]
    private class SessionResponse
    {
        [JsonProperty("client_secret")]
        public ClientSecret client_secret;
    }
    
    [Serializable]
    private class ClientSecret
    {
        [JsonProperty("value")]
        public string value;
    }
    
    // Clear chat container - can be called by MobileCompanionUI for synchronization
    public void ClearChatContainer()
    {
        if (coachChatRoot != null)
        {
            int childCount = coachChatRoot.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(coachChatRoot.GetChild(i).gameObject);
            }
            LogMessage("Realtime chat container cleared");
        }
    }
    
    // Track OpenAI response state
    private void StartOpenAIResponse(string responseId)
    {
        currentOpenAIResponseId = responseId;
        isWaitingForOpenAIResponse = true;
        lastResponseStartTime = Time.time;
        LogMessage($"🚀 OpenAI response started - ID: {responseId}, Time: {Time.time:F1}s");
        
        // Start timeout protection
        StartCoroutine(ResponseTimeoutCoroutine());
    }
    
    private void CompleteOpenAIResponse()
    {
        if (isWaitingForOpenAIResponse)
        {
            float responseDuration = Time.time - lastResponseStartTime;
            LogMessage($"✅ OpenAI response completed - Duration: {responseDuration:F1}s");
            isWaitingForOpenAIResponse = false;
            currentOpenAIResponseId = null;
        }
    }
    
    private void CancelOpenAIResponse()
    {
        if (isWaitingForOpenAIResponse)
        {
            float responseDuration = Time.time - lastResponseStartTime;
            LogMessage($"❌ OpenAI response cancelled - Duration: {responseDuration:F1}s");
            isWaitingForOpenAIResponse = false;
            currentOpenAIResponseId = null;
        }
    }
    
    private IEnumerator ResponseTimeoutCoroutine()
    {
        float startTime = Time.time;
        float checkInterval = 5f; // Check every 5 seconds
        
        while (isWaitingForOpenAIResponse && Time.time - startTime < RESPONSE_TIMEOUT_SECONDS)
        {
            yield return new WaitForSeconds(checkInterval);
            
            float elapsed = Time.time - startTime;
            float remaining = RESPONSE_TIMEOUT_SECONDS - elapsed;
            
            if (elapsed % 15f < checkInterval) // Log every 15 seconds
            {
                LogMessage($"⏱️ OpenAI response in progress - Elapsed: {elapsed:F1}s, Remaining: {remaining:F1}s");
            }
        }
        
        if (isWaitingForOpenAIResponse)
        {
            float totalDuration = Time.time - startTime;
            LogWarning($"⚠ OpenAI response timeout after {totalDuration:F1} seconds - Response may have been lost");
            
            // Force reset the state
            CancelOpenAIResponse();
            
            // Reset UI state
            if (companionUI != null)
            {
                companionUI.ResetToIdleState();
            }
        }
    }
    
    // Mark startup as complete - call this after initial setup is done
    public void MarkStartupComplete()
    {
        if (isInitializing)
        {
            isInitializing = false;
            startupCompleteTime = Time.time;
            LogMessage($"✅ Startup protection disabled after {Time.time - startupCompleteTime:F1} seconds");
        }
    }
    
    // Startup timeout protection - automatically disables startup protection after timeout
    private IEnumerator StartupTimeoutProtection()
    {
        LogMessage($"⏰ Startup protection active for {STARTUP_PROTECTION_SECONDS} seconds");
        yield return new WaitForSeconds(STARTUP_PROTECTION_SECONDS);
        
        if (isInitializing)
        {
            LogWarning("⚠️ Startup protection timeout reached - automatically disabling protection");
            MarkStartupComplete();
            
            // If greeting hasn't been triggered yet, trigger it now
            if (!greetingTriggered)
            {
                LogMessage("🚀 Startup timeout reached - triggering greeting sequence now");
                greetingTriggered = true;
                StartCoroutine(DelayedAutoGreetingWithStartupProtection());
            }
        }
    }
    
    // Trigger greeting when system is ready (called after initial sync)
    public void TriggerGreetingWhenReady()
    {
        if (greetingTriggered)
        {
            LogMessage("⚠️ Greeting already triggered, skipping duplicate");
            return;
        }
        
        if (IsReadyForGreeting())
        {
            LogMessage("🚀 System ready - triggering greeting sequence");
            greetingTriggered = true;
            StartCoroutine(DelayedAutoGreetingWithStartupProtection());
        }
        else
        {
            LogMessage("⏳ System not ready for greeting yet - will wait for readiness");
            StartCoroutine(WaitForReadinessAndGreet());
        }
    }
    
    // Wait for system to be ready then greet
    private IEnumerator WaitForReadinessAndGreet()
    {
        LogMessage("⏳ Waiting for system to be ready for greeting...");
        
        while (!IsReadyForGreeting())
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        LogMessage("✅ System now ready - proceeding with greeting");
        greetingTriggered = true;
        StartCoroutine(DelayedAutoGreetingWithStartupProtection());
    }
    
    // Track if greeting has been triggered to prevent duplicates
    private bool greetingTriggered = false;
    
    // Simplified welcome method that bypasses complex realtime processing
    private void DeliverSimpleWelcome(string message)
    {
        LogMessage($"🎯 Delivering simple welcome: '{message}'");
        
        // Display message in chat immediately
        DisplayMessageInChat(message, false);
        
        // Update UI status
        if (companionUI != null)
        {
            companionUI.UpdateStatusText("Welcome message delivered");
        }
        
        // Try to trigger audio through the correct OpenAI API
        if (isConnectionActive && dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
        {
            // First create the conversation item
            var messageEvent = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "assistant",
                    content = new[]
                    {
                        new { type = "text", text = message }
                    }
                }
            };
            
            try
            {
                // Send the message
                string messageJson = JsonConvert.SerializeObject(messageEvent);
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageJson);
                dataChannel.Send(messageBytes);
                
                LogMessage("✅ Sent welcome message as conversation item");
                
                // Then create a response to generate audio
                var responseEvent = new
                {
                    type = "response.create",
                    response = new
                    {
                        modalities = new[] { "audio", "text" },
                        instructions = "Speak this message naturally and warmly"
                    }
                };
                
                LogMessage($"🔍 DEBUG: Sending welcome response.create with modalities: [{string.Join(", ", responseEvent.response.modalities)}]");
                
                string responseJson = JsonConvert.SerializeObject(responseEvent);
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                dataChannel.Send(responseBytes);
                
                LogMessage("✅ Sent response.create request for audio generation");
                
                if (companionUI != null)
                {
                    companionUI.ShowAudioPlaybackIndicator(true);
                    companionUI.UpdateStatusText("AI is speaking welcome message...");
                }
            }
            catch (System.Exception e)
            {
                LogError($"Failed to send welcome message: {e.Message}");
            }
        }
        else
        {
            LogMessage("⚠️ No audio connection available for welcome message");
        }
    }
    
    // Get the current cloud RAG URL dynamically
    private string GetCurrentCloudRAGUrl()
    {
        // Try to get the URL from the companion system first
        var companionSystem = FindFirstObjectByType<MobileRAGCompanionSystem>();
        if (companionSystem != null)
        {
            string dynamicUrl = companionSystem.GetCloudRAGUrl();
            if (!string.IsNullOrEmpty(dynamicUrl) && !IsPlaceholderUrl(dynamicUrl))
            {
                LogMessage($"Using dynamic cloud RAG URL: {dynamicUrl}");
                return dynamicUrl;
            }
        }
        
        // Fall back to the configured URL
        LogMessage($"Using configured RAG API URL: {ragApiUrl}");
        return ragApiUrl;
    }
    
    private void HandleConversationItemCreate(JObject jo)
    {
        try
        {
            LogMessage($"🔍 Processing conversation item create: {jo.ToString(Formatting.None)}");
            
            // Extract the message content
            var item = jo["item"];
            if (item != null)
            {
                var content = item["content"];
                if (content != null && content.HasValues)
                {
                    foreach (var contentItem in content)
                    {
                        if (contentItem["type"]?.ToString() == "text")
                        {
                            var text = contentItem["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                LogMessage($"💬 Conversation item text: {text.Substring(0, Math.Min(100, text.Length))}...");
                                
                                // Display the message in chat
                                DisplayMessageInChat(text, false);
                                
                                // Update UI
                                if (companionUI != null)
                                {
                                    companionUI.UpdateStatusText("Message displayed in chat");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error handling conversation item create: {ex.Message}");
        }
    }
    
    private void HandleResponseCreate(JObject jo)
    {
        try
        {
            LogMessage($"🔍 Processing response create: {jo.ToString(Formatting.None)}");
            
            // Extract response details
            var response = jo["response"];
            if (response != null)
            {
                var modalities = response["modalities"];
                if (modalities != null)
                {
                    LogMessage($"🔊 Response modalities: {modalities.ToString()}");
                    
                    // Check if audio is requested
                    bool hasAudio = false;
                    foreach (var modality in modalities)
                    {
                        if (modality.ToString() == "audio")
                        {
                            hasAudio = true;
                            break;
                        }
                    }
                    
                    if (hasAudio)
                    {
                        LogMessage("🎵 Audio generation requested - waiting for audio content");
                        
                        // Update UI to show audio is being generated
                        if (companionUI != null)
                        {
                            companionUI.ShowAudioPlaybackIndicator(true);
                            companionUI.UpdateStatusText("Generating audio...");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error handling response create: {ex.Message}");
        }
    }
}