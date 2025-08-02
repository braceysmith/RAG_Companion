using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
    [SerializeField] private string ragApiUrl = "https://your-rag-api.up.railway.app";
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
    
    // WebRTC Components
    private RTCPeerConnection peerConnection;
    private RTCDataChannel dataChannel;
    private AudioStreamTrack localMicTrack;
    private AudioSource remoteAudioSource;
    
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
    
    private void Start()
    {
        // Initialize Unity audio settings for better WebRTC compatibility
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        audioConfig.sampleRate = 24000; // Common rate for voice
        audioConfig.numRealVoices = 32;
        AudioSettings.Reset(audioConfig);
        
        InitializeMobileRealtime();
        
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
        ragConfig = RAGConfiguration.Instance;
        
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
        remoteAudioSource = gameObject.GetComponent<AudioSource>();
        if (remoteAudioSource == null)
        {
            remoteAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure audio source for WebRTC remote audio
        remoteAudioSource.playOnAwake = false;
        remoteAudioSource.loop = false;
        remoteAudioSource.volume = 1.0f;
        remoteAudioSource.spatialBlend = 0f; // 2D audio
        remoteAudioSource.priority = 128;
        
        LogMessage("Remote audio source configured for WebRTC output");
        
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
    
    private IEnumerator DelayedAutoGreeting()
    {
        LogMessage($"⏳ Waiting {greetingDelay} seconds before greeting user...");
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
            
            LogMessage($"📢 Delivering greeting: {greetingMessage}");
            
            // Deliver greeting directly through voice system (bypassing full AI conversation)
            TriggerDirectAIMessage(greetingMessage);
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
        StartCoroutine(DelayedAutoGreeting());
    }
    
    public void StartVoiceInput()
    {
        LogMessage($"StartVoiceInput called - isConnectionActive: {isConnectionActive}, isTalking: {isTalking}, localMicTrack != null: {localMicTrack != null}");
        
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
        
        // Enable microphone for continuous listening
        if (localMicTrack != null && microphoneClip != null)
        {
            LogMessage($"Starting audio streaming to OpenAI");
            isTalking = true;
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
        if (!isConnectionActive)
        {
            OnError?.Invoke("Not connected to realtime session");
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
            
            // Send session configuration
            SendSessionConfiguration();
            
            LogMessage("Invoking OnConnectionEstablished event");
            
            // Trigger auto-greeting if enabled
            if (enableAutoGreeting)
            {
                LogMessage("🤝 Auto-greeting enabled - will greet user in a moment");
                StartCoroutine(DelayedAutoGreeting());
            }
            // Notify connection established
            OnConnectionEstablished?.Invoke();
        };
        
        dataChannel.OnMessage = (byte[] data) =>
        {
            lastResponseTime = Time.time;
            ProcessDataChannelMessage(data);
        };
        
        dataChannel.OnClose = () =>
        {
            LogMessage("Data channel closed");
            isConnectionActive = false;
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
        
        // Create local audio source
        AudioSource localAudioSource = gameObject.GetComponent<AudioSource>();
        if (localAudioSource == null)
        {
            localAudioSource = gameObject.AddComponent<AudioSource>();
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
            var message = Encoding.UTF8.GetString(data);
            var jo = JObject.Parse(message);
            var messageType = jo["type"]?.ToString();
            
            LogMessage($"Received message type: {messageType}");
            LogMessage($"Message content: {message.Substring(0, Math.Min(200, message.Length))}...");
            
            switch (messageType)
            {
                case "conversation.item.input_audio_transcription.completed":
                    HandleAudioTranscription(jo);
                    break;
                    
                case "conversation.item.input_audio_transcription.delta":
                    HandleAudioTranscriptionDelta(jo);
                    break;
                    
                case "response.created":
                    HandleResponseStart();
                    break;
                    
                case "response.done":
                    HandleResponseComplete();
                    break;
                    
                case "response.cancelled":
                    HandleResponseCancelled(jo);
                    break;
                    
                case "response.output_item.added":
                case "response.content_part.added":
                    HandleTextResponse(jo);
                    break;
                    
                case "response.audio.delta":
                    // Handle audio response deltas
                    HandleAudioResponseDelta(jo);
                    break;
                    
                case "response.audio_transcript.delta":
                    // Handle audio transcript deltas (this is what was actually spoken)
                    HandleAudioTranscriptDelta(jo);
                    break;
                    
                case "response.audio_transcript.done":
                    // Handle completed audio transcript
                    HandleAudioTranscriptDone(jo);
                    break;
                    
                case "response.output_item.done":
                case "response.content_part.done":
                    // Content part completed
                    LogMessage("Content part completed");
                    break;
                    
                case "response.function_call_arguments.delta":
                    HandleFunctionCallArgumentsDelta(jo);
                    break;
                    
                case "response.function_call_arguments.done":
                    HandleFunctionCallArgumentsDone(jo);
                    break;
                    
                case "session.created":
                case "session.updated":
                    LogMessage("Session event received");
                    break;
                    
                case "input_audio_buffer.speech_started":
                    HandleSpeechStarted(jo);
                    break;
                    
                case "input_audio_buffer.speech_stopped":
                    HandleSpeechStopped(jo);
                    break;
                    
                case "conversation.item.created":
                    LogMessage("Conversation item created");
                    break;
                    
                case "output_audio_buffer.started":
                    HandleOutputAudioBufferStarted(jo);
                    break;
                    
                case "output_audio_buffer.stopped":
                    HandleOutputAudioBufferStopped(jo);
                    break;
                    
                case "rate_limits.updated":
                    HandleRateLimitsUpdated(jo);
                    break;
                    
                case "error":
                    HandleErrorMessage(jo);
                    break;
                    
                default:
                    LogMessage($"Unhandled message type: {messageType}");
                    // Still try to extract text from unknown message types
                    HandleTextResponse(jo);
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
            
            // Store user input in RAG memory
            StoreUserMessage(transcript);
            
            // Update UI - transcript received, now waiting for AI response
            if (companionUI != null)
            {
                companionUI.ShowTranscript(transcript);
                companionUI.AddMessage(transcript, "user");
                companionUI.ShowProcessingIndicator(false);
                
                // Show that we're now waiting for AI response
                companionUI.UpdateStatusText("AI is thinking...");
            }
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
        isAIResponding = true;
        LogMessage("AI response started - transitioning to Responding state");
        
        // Update UI to Responding state initially (shows INTERRUPT button)
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.SetToRespondingState();
        }
    }
    
    private void HandleResponseComplete()
    {
        isAIResponding = false;
        LogMessage("✅ AI response COMPLETELY finished - resetting to idle state");
        
        // NOW it's safe to reset UI state - the entire response is done
        if (companionUI != null)
        {
            companionUI.ShowAudioPlaybackIndicator(false);
            companionUI.ShowProcessingIndicator(false);
            companionUI.UpdateStatusText("Ready - Tap to talk");
            
            // Reset UI state for continuous conversation
            companionUI.ResetToIdleState();
        }
    }
    
    private void HandleAudioResponseDelta(JObject message)
    {
        // Handle audio response chunks
        LogMessage("Audio response delta received - AI is speaking");
        
        // Update UI to show audio is being received
        if (companionUI != null && !isAIResponding)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(true);
            
            // Transition to PlayingAudio state for interrupt capability
            companionUI.SetToPlayingAudioState();
            isAIResponding = true;
        }
        
        // Ensure remote audio source is playing
        if (remoteAudioSource != null && !remoteAudioSource.isPlaying)
        {
            remoteAudioSource.Play();
            LogMessage("Started remote audio playback");
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
        
        LogError($"OpenAI error: {fullError}");
        OnError?.Invoke($"OpenAI error: {errorMessage}");
        
        // Reset UI state
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(false);
        }
    }
    
    private void HandleTextResponse(JObject message)
    {
        var text = ExtractTextFromMessage(message);
        if (!string.IsNullOrEmpty(text))
        {
            LogMessage($"Text response (may differ from audio): {text.Substring(0, Math.Min(50, text.Length))}...");
            OnAIResponseReceived?.Invoke(text);
            
            // Don't store text response - wait for audio transcript instead
            // The audio transcript (what was actually spoken) will be stored in HandleAudioTranscriptDone
            
            // Update UI - switch from processing to responding
            if (companionUI != null)
            {
                companionUI.ShowProcessingIndicator(false);
                // Don't add text message here - wait for audio transcript
                
                // If we weren't already responding, start now
                if (!isAIResponding)
                {
                    isAIResponding = true;
                    companionUI.ShowAudioPlaybackIndicator(true);
                }
            }
        }
    }
    
    private string ExtractTextFromMessage(JObject message)
    {
        // Extract text content from various message types
        var transcript = message["part"]?["transcript"]?.ToString();
        if (!string.IsNullOrEmpty(transcript)) return transcript;
        
        var content = message["part"]?["content"]?.ToString();
        if (!string.IsNullOrEmpty(content)) return content;
        
        return "";
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
            companionUI.SetToPlayingAudioState();
        }
    }
    
    private void HandleOutputAudioBufferStopped(JObject message)
    {
        LogMessage("AI audio buffer chunk stopped - continuing to wait for response.done");
        
        // IMPORTANT: Don't reset UI state here! This just means one audio chunk ended.
        // The AI might still be generating more audio chunks.
        // Only response.done should reset the UI state.
        
        // Keep the audio playback indicator active until response.done
        // This prevents premature audio cutoff
    }
    
    private void HandleResponseCancelled(JObject message)
    {
        LogMessage("AI response was cancelled - ready for user input");
        
        // Reset UI state after cancellation
        if (companionUI != null)
        {
            companionUI.ShowAudioPlaybackIndicator(false);
            companionUI.ShowProcessingIndicator(false);
        }
        
        // Mark response as no longer active
        isAIResponding = false;
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
                companionUI.AddMessage(transcript, "assistant");
            }
            
            currentAITranscript?.Clear();
        }
    }
    
    private void HandleFunctionCallArgumentsDelta(JObject message)
    {
        // Handle incremental function call arguments
        LogMessage("Function call arguments delta received");
    }
    
    private void HandleFunctionCallArgumentsDone(JObject message)
    {
        LogMessage("Function call arguments complete");
        
        // Extract function call details
        var call_id = message["call_id"]?.ToString();
        var name = message["name"]?.ToString();
        var arguments = message["arguments"]?.ToString();
        
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
            ["user_id"] = userId
        };
        
        using (UnityWebRequest request = new UnityWebRequest($"{ragApiUrl}/generate_image", "POST"))
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
                    var imageUrl = response["image_url"].ToString();
                    LogMessage($"Image generated: {imageUrl}");
                    
                    // Download and display the image
                    yield return StartCoroutine(DownloadAndDisplayImage(imageUrl, prompt));
                    
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
        
        using (UnityWebRequest request = new UnityWebRequest($"{ragApiUrl}/analyze_image", "POST"))
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
    
    private IEnumerator DownloadAndDisplayImage(string imageUrl, string prompt)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success) 
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                
                // Display image in UI
                if (companionUI != null)
                {
                    companionUI.AddImageMessage(texture, prompt, false); // false = AI message
                }
                
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
            LogMessage($"Sent function call result for {callId}");
        }
        
        // Trigger response generation
        var responseCreate = new JObject
        {
            ["type"] = "response.create"
        };
        
        if (dataChannel?.ReadyState == RTCDataChannelState.Open)
        {
            dataChannel.Send(Encoding.UTF8.GetBytes(responseCreate.ToString(Formatting.None)));
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
                    ["prefix_padding_ms"] = 300,
                    ["silence_duration_ms"] = 2000
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
            
            // Fall back to UI display only
            if (companionUI != null)
            {
                companionUI.AddMessage(message, "assistant", true);
                companionUI.UpdateStatusText("Message delivered (no audio connection)");
            }
            return;
        }
        
        try
        {
            // Create a simple response event to make AI speak the message directly
            var responseEvent = new
            {
                type = "response.create",
                response = new
                {
                    modalities = new[] { "text", "audio" },
                    instructions = $"Simply say this message exactly as written, in a warm and friendly tone: '{message}'"
                }
            };
            
            string responseJson = JsonConvert.SerializeObject(responseEvent);
            
            // Send through data channel
            if (dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
            {
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                dataChannel.Send(responseBytes);
                
                LogMessage($"✅ Sent direct AI message through data channel");
                
                // Update UI to show AI is speaking
                if (companionUI != null)
                {
                    companionUI.AddMessage(message, "assistant", true);
                    companionUI.SetToPlayingAudioState();
                    companionUI.UpdateStatusText("AI is speaking...");
                }
            }
            else
            {
                LogError("Data channel not available for direct message delivery");
                
                // Fall back to UI display
                if (companionUI != null)
                {
                    companionUI.AddMessage(message, "assistant", true);
                    companionUI.UpdateStatusText("Message delivered (audio unavailable)");
                }
            }
        }
        catch (System.Exception e)
        {
            LogError($"Failed to trigger direct AI message: {e.Message}");
            
            // Fall back to UI display
            if (companionUI != null)
            {
                companionUI.AddMessage(message, "assistant", true);
                companionUI.UpdateStatusText("Message delivered (fallback mode)");
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
            
            // Fall back to UI display only
            if (companionUI != null)
            {
                companionUI.AddMessage(aiMessage, "assistant", true);
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
                
                LogMessage($"✅ Sent AI reminder message through data channel");
                
                // Also trigger response generation
                var responseEvent = new
                {
                    type = "response.create",
                    response = new
                    {
                        modalities = new[] { "text", "audio" },
                        instructions = "You just delivered a reminder to the user. Speak this message naturally and warmly."
                    }
                };
                
                string responseJson = JsonConvert.SerializeObject(responseEvent);
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                dataChannel.Send(responseBytes);
                
                // Update UI to show AI is speaking
                if (companionUI != null)
                {
                    companionUI.AddMessage(aiMessage, "assistant", true);
                    companionUI.SetToPlayingAudioState();
                    companionUI.UpdateStatusText("Delivering your reminder...");
                }
            }
            else
            {
                LogError("Data channel not available for reminder delivery");
                
                // Fall back to UI display
                if (companionUI != null)
                {
                    companionUI.AddMessage(aiMessage, "assistant", true);
                    companionUI.UpdateStatusText("Reminder delivered (audio unavailable)");
                }
            }
        }
        catch (Exception e)
        {
            LogError($"Failed to trigger AI reminder delivery: {e.Message}");
            
            // Fall back to UI display
            if (companionUI != null)
            {
                companionUI.AddMessage(aiMessage, "assistant", true);
                companionUI.UpdateStatusText("Reminder delivered (fallback mode)");
            }
        }
    }
    
    // Public properties
    public bool IsConnected => isConnectionActive;
    public bool IsTalking => isTalking;
    public bool IsAIResponding => isAIResponding;
    public AudioSource RemoteAudioSource => remoteAudioSource;
    
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
}