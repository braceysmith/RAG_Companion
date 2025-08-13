using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;
using Newtonsoft.Json.Linq;
using RAGCompanion.Mobile;

namespace RAGCompanion.Mobile
{
    /// <summary>
    /// Enhanced Mobile Realtime Chat that integrates with EnhancedAudioHandler
    /// to prevent audio cutoffs and provide better WebRTC audio streaming.
    /// </summary>
    public class MobileRealtimeChat_Enhanced : MonoBehaviour
    {
        [Header("WebRTC Configuration")]
        [SerializeField] private string ragApiUrl = "https://ragcompanion-production-bf25.up.railway.app";
        [SerializeField] private string realtimeModel = "gpt-4o-realtime-preview-2024-10-01";
        [SerializeField] private string iceServers = "stun:stun.l.google.com:19302";
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float autoConnectDelay = 2f;
        
        [Header("Audio Configuration")]
        [SerializeField] private bool enableEnhancedAudio = true;
        [SerializeField] private float audioSampleRate = 24000f;
        [SerializeField] private float silenceThreshold = 1.0f; // Seconds of silence before marking response complete
        
        [Header("RAG Integration")]
        [SerializeField] private bool enableRAGContext = true;
        [SerializeField] private bool enableAutoGreeting = true;
        [SerializeField] private float greetingDelay = 1.5f; // Delay before greeting (seconds)
        
        [Header("User Configuration")]
        [SerializeField] private string userId = "unity_user";
        
        // Component references
        private MobileCompanionUI companionUI;
        private MobileRAGClient ragClient;
        private RAGConfiguration ragConfig;
        private ReminderManager reminderManager;
        
        // WebRTC components
        private RTCPeerConnection peerConnection;
        private RTCDataChannel dataChannel;
        private AudioStreamTrack localMicTrack;
        private AudioSource remoteAudioSource;
        
        // Enhanced Audio Handler
        private EnhancedAudioHandler enhancedAudioHandler;
        
        // Connection state
        private bool isConnectionActive = false;
        private string ephemeralKey;
        private float lastResponseTime = 0f;
        private int reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 3;
        
        // Audio-based conversation timing
        private bool isAIResponding = false;
        private Coroutine responseCompletionTimer;
        
        // Voice input state
        private bool isTalking = false;
        
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
        public event Action<string> OnError;
        public event Action<string> OnTranscriptReceived;
        public event Action<string> OnAIResponseReceived;
        
        private void Start()
        {
            // Initialize Unity audio settings for better WebRTC compatibility
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = (int)audioSampleRate;
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
        }
        
        private void InitializeMobileRealtime()
        {
            // Get component references
            companionUI = GetComponent<MobileCompanionUI>() ?? FindFirstObjectByType<MobileCompanionUI>();
            ragClient = GetComponent<MobileRAGClient>() ?? FindFirstObjectByType<MobileRAGClient>();
            ragConfig = RAGConfiguration.Instance;
            
            // Initialize Enhanced Audio Handler
            if (enableEnhancedAudio)
            {
                enhancedAudioHandler = GetComponent<EnhancedAudioHandler>();
                if (enhancedAudioHandler == null)
                {
                    enhancedAudioHandler = gameObject.AddComponent<EnhancedAudioHandler>();
                }
                
                // Subscribe to enhanced audio handler events
                enhancedAudioHandler.OnDebugMessage += (message) => LogMessage($"🎵 {message}");
                enhancedAudioHandler.OnFlowControlChanged += (isControlled) => 
                {
                    if (companionUI != null)
                    {
                        companionUI.UpdateStatusText(isControlled ? "Audio buffering..." : "Ready - Tap to talk");
                    }
                };
            }
            
            // Debug RAG configuration
            LogMessage($"RAG Client initialized: {ragClient != null}");
            LogMessage($"RAG Config initialized: {ragConfig != null}");
            LogMessage($"RAG Context enabled: {enableRAGContext}");
            LogMessage($"Enhanced Audio enabled: {enableEnhancedAudio}");
            LogMessage($"RAG API URL: {ragApiUrl}");
            LogMessage($"User ID: {userId}");
            
            if (enableRAGContext && ragClient == null)
            {
                LogError("RAG context is enabled but no MobileRAGClient found! Memory will not work.");
            }
        }
        
        private IEnumerator DelayedAutoConnect()
        {
            yield return new WaitForSeconds(autoConnectDelay);
            
            if (!isConnectionActive)
            {
                LogMessage("🔗 Starting auto-connect sequence...");
                yield return StartCoroutine(EstablishWebRTCConnection());
            }
        }
        
        public IEnumerator EstablishWebRTCConnection()
        {
            if (isConnectionActive)
            {
                LogMessage("WebRTC connection already active");
                yield break;
            }
            
            LogMessage("🔗 Establishing WebRTC connection...");
            
            // Get ephemeral key from server
            yield return StartCoroutine(GetEphemeralKey());
            
            if (string.IsNullOrEmpty(ephemeralKey))
            {
                LogError("Failed to get ephemeral key from server");
                yield break;
            }
            
            // Setup WebRTC connection
            yield return StartCoroutine(SetupWebRTCConnection());
            
            // Wait for connection to be established
            float timeout = Time.time + 30f;
            while (!isConnectionActive && Time.time < timeout)
            {
                yield return null;
            }
            
            if (!isConnectionActive)
            {
                LogError("WebRTC connection failed to establish within timeout");
            }
            else
            {
                LogMessage("✅ WebRTC connection established successfully");
            }
        }
        
        private IEnumerator GetEphemeralKey()
        {
            var url = $"{ragApiUrl}/realtime/session";
            var requestData = new JObject
            {
                ["user_id"] = userId
            };
            
            var request = new UnityEngine.Networking.UnityWebRequest(url, "POST")
            {
                uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(Encoding.UTF8.GetBytes(requestData.ToString())),
                downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer()
            };
            
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var response = JObject.Parse(request.downloadHandler.text);
                ephemeralKey = response["ephemeral_key"]?.ToString();
                LogMessage($"Received ephemeral key: {ephemeralKey?.Substring(0, 10)}...");
            }
            else
            {
                LogError($"Failed to get ephemeral key: {request.error}");
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
                
                // Initialize Enhanced Audio Handler
                if (enableEnhancedAudio && enhancedAudioHandler != null)
                {
                    enhancedAudioHandler.Initialize(dataChannel);
                    LogMessage("Enhanced Audio Handler initialized with data channel");
                }
                
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
                LogError($"Data channel error: {error.message}");
            };
        }
        
        private IEnumerator InitializeMicrophone()
        {
            // Request microphone permission
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
            
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                LogError("Microphone permission denied");
                yield break;
            }
            
            // Check if microphone is available
            if (Microphone.devices.Length == 0)
            {
                LogError("No microphone device found");
                yield break;
            }
            
            // Start microphone with standard voice sample rate
            microphoneClip = Microphone.Start(null, true, 10, (int)audioSampleRate);
            
            LogMessage($"Started microphone - Sample rate: {(int)audioSampleRate}, Device: {(Microphone.devices.Length > 0 ? Microphone.devices[0] : "default")}");
            
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
            
            // Initialize remote audio source for incoming audio
            remoteAudioSource = gameObject.GetComponent<AudioSource>();
            if (remoteAudioSource == null)
            {
                remoteAudioSource = gameObject.AddComponent<AudioSource>();
            }
            
            remoteAudioSource.playOnAwake = false;
            remoteAudioSource.loop = false;
            remoteAudioSource.volume = 1.0f;
            remoteAudioSource.spatialBlend = 0f; // 2D audio
            remoteAudioSource.priority = 128;
            
            // Create WebRTC audio track
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
            var request = new UnityEngine.Networking.UnityWebRequest(url, "POST")
            {
                uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(Encoding.UTF8.GetBytes(sdp)),
                downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer()
            };
            
            request.SetRequestHeader("Content-Type", "application/sdp");
            request.SetRequestHeader("Authorization", $"Bearer {ephemeralKey}");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
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
                LogError($"Failed to send offer to OpenAI: {request.error}");
            }
            
            request.Dispose();
        }
        
        private void SendSessionConfiguration()
        {
            if (dataChannel?.ReadyState != RTCDataChannelState.Open)
            {
                return;
            }
            
            try
            {
                var configEvt = new JObject
                {
                    ["type"] = "session.update",
                    ["session"] = new JObject
                    {
                        ["modalities"] = new JArray { "text", "audio" },
                        ["instructions"] = GetDefaultInstructions(),
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
                            ["threshold"] = 0.3, // Lower threshold for better sensitivity
                            ["prefix_padding_ms"] = 1000, // 1 second padding
                            ["silence_duration_ms"] = 2000 // 2 seconds silence detection
                        },
                        ["temperature"] = 0.8,
                        ["max_response_output_tokens"] = "inf"
                    }
                };
                
                if (enableEnhancedAudio && enhancedAudioHandler != null)
                {
                    enhancedAudioHandler.SendTextMessage(configEvt.ToString(Newtonsoft.Json.Formatting.None), "session_config");
                }
                else
                {
                    dataChannel.Send(Encoding.UTF8.GetBytes(configEvt.ToString(Newtonsoft.Json.Formatting.None)));
                }
                
                LogMessage("Session configuration sent");
                
            }
            catch (Exception ex)
            {
                LogError($"Error sending session configuration: {ex.Message}");
            }
        }
        
        private string GetDefaultInstructions()
        {
            var instructions = new StringBuilder();
            instructions.AppendLine("You are a conversational AI companion with memory capabilities.");
            instructions.AppendLine("You DO have access to personal information and conversation history about this user.");
            instructions.AppendLine("IMPORTANT: You MUST use the provided user information in your responses.");
            instructions.AppendLine("DO NOT claim you don't have memory or can't remember things.");
            instructions.AppendLine("Key behaviors:");
            instructions.AppendLine("- ALWAYS acknowledge and use any provided user information");
            instructions.AppendLine("- Reference their name when it's provided");
            instructions.AppendLine("- Build on previous conversation topics when provided");
            instructions.AppendLine("- Ask follow-up questions to learn more about the user");
            instructions.AppendLine("- Be genuinely interested in their life, work, and interests");
            instructions.AppendLine("- Respond in a warm, engaging, and personal way");
            
            return instructions.ToString();
        }
        
        private IEnumerator DelayedAutoGreeting()
        {
            LogMessage("🤝 Auto-greeting coroutine started");
            
            LogMessage($"⏳ Waiting {greetingDelay} seconds before greeting user...");
            yield return new WaitForSeconds(greetingDelay);
            
            LogMessage("⏰ Greeting delay completed - checking connection...");
            
            if (!isConnectionActive)
            {
                LogMessage("⚠️ Connection lost before greeting could be delivered");
                yield break;
            }
            
            LogMessage("✅ Connection still active - proceeding with greeting...");
            
            // Get user's name from RAG system using coroutine
            yield return StartCoroutine(GetUserNameCoroutine((userName) => {
                // Generate appropriate greeting based on whether we know the name
                string greetingMessage = GenerateGreetingMessage(userName);
                
                LogMessage($"📢 Generated greeting: {greetingMessage}");
                
                // Deliver greeting directly through voice system (bypassing full AI conversation)
                LogMessage("📤 Triggering direct AI greeting...");
                TriggerDirectAIMessage(greetingMessage);
                LogMessage("✅ Direct AI greeting triggered successfully");
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
            using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get($"{ragApiUrl}/user/{userId}/profile"))
            {
                yield return request.SendWebRequest();
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(request.downloadHandler.text);
                        
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
        
        private IEnumerator AttemptReconnection()
        {
            if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
            {
                LogError($"Max reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached");
                yield break;
            }
            
            reconnectAttempts++;
            LogMessage($"Attempting reconnection {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}");
            
            yield return new WaitForSeconds(2f * reconnectAttempts); // Exponential backoff
            
            if (!isConnectionActive)
            {
                yield return StartCoroutine(EstablishWebRTCConnection());
            }
        }
        
        private void ProcessDataChannelMessage(byte[] data)
        {
            try
            {
                string message = Encoding.UTF8.GetString(data);
                var jsonMessage = JObject.Parse(message);
                
                string messageType = jsonMessage["type"]?.ToString() ?? "unknown";
                
                switch (messageType)
                {
                    case "session.created":
                    case "session.updated":
                        LogMessage($"Session {messageType}");
                        break;
                        
                    case "conversation.created":
                        LogMessage("Conversation created");
                        break;
                        
                    case "conversation.item.input_audio_transcription.completed":
                        HandleAudioTranscription(jsonMessage);
                        break;
                        
                    case "conversation.item.input_audio_transcription.delta":
                        HandleAudioTranscriptionDelta(jsonMessage);
                        break;
                        
                    case "response.audio.delta":
                        HandleAudioResponseDelta(jsonMessage);
                        break;
                        
                    case "response.audio.done":
                        HandleAudioResponseDone(jsonMessage);
                        break;
                        
                    case "response.function_call_arguments.delta":
                        HandleFunctionCallDelta(jsonMessage);
                        break;
                        
                    case "response.function_call_arguments.done":
                        HandleFunctionCallDone(jsonMessage);
                        break;
                        
                    case "response.done":
                        HandleResponseDone(jsonMessage);
                        break;
                        
                    case "error":
                        HandleErrorMessage(jsonMessage);
                        break;
                        
                    default:
                        LogMessage($"Unhandled message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error processing data channel message: {ex.Message}");
            }
        }
        
        private void HandleAudioResponseDelta(JObject message)
        {
            LogMessage("Audio response delta received - AI is speaking");
            
            // Start AI response tracking
            if (!isAIResponding)
            {
                isAIResponding = true;
                LogMessage("🎯 AI response started - audio detected");
                
                // Cancel any existing completion timer
                if (responseCompletionTimer != null)
                {
                    StopCoroutine(responseCompletionTimer);
                    responseCompletionTimer = null;
                }
            }
            
            // Reset safety timeout when audio activity is detected
            if (enableEnhancedAudio && enhancedAudioHandler != null)
            {
                enhancedAudioHandler.ResetSafetyTimeout();
            }
            
            // Update UI to show audio is being received
            if (companionUI != null)
            {
                companionUI.ShowProcessingIndicator(false);
                companionUI.ShowAudioPlaybackIndicator(true);
                companionUI.UpdateStatusText("AI is speaking...");
            }
            
            // Ensure remote audio source is playing
            if (remoteAudioSource != null && !remoteAudioSource.isPlaying)
            {
                remoteAudioSource.Play();
                LogMessage("Started remote audio playback");
            }
        }
        
        private void HandleTranscriptReceived(string transcript)
        {
            LogMessage($"Transcript received: {transcript}");
            OnTranscriptReceived?.Invoke(transcript);
        }
        
        private void HandleAIResponseReceived(string response)
        {
            LogMessage($"AI response received: {response.Substring(0, Math.Min(50, response.Length))}...");
            OnAIResponseReceived?.Invoke(response);
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
        
        private void HandleAudioResponseDone(JObject message)
        {
            LogMessage("Audio response complete");
            
            // Start silence timer to detect if response is truly complete
            if (isAIResponding)
            {
                LogMessage("🔇 Audio stopped - starting silence timer...");
                responseCompletionTimer = StartCoroutine(ResponseCompletionTimer());
            }
            
            // Process any queued audio/messages
            if (enableEnhancedAudio && enhancedAudioHandler != null)
            {
                enhancedAudioHandler.ProcessQueuedAudio();
                enhancedAudioHandler.ProcessQueuedMessages();
            }
        }
        
        private void HandleFunctionCallDelta(JObject message)
        {
            // Handle streaming tool call arguments
            LogMessage("Function call arguments delta received");
        }
        
        private void HandleFunctionCallDone(JObject message)
        {
            // Execute completed tool call
            LogMessage("Function call arguments complete");
        }
        
        private void HandleResponseDone(JObject message)
        {
            LogMessage("🎵 RESPONSE.DONE received - AI response COMPLETELY finished");
            LogMessage($"🎵 Previous isAIResponding state: {isAIResponding}");
            
            isAIResponding = false;
            LogMessage("✅ AI response COMPLETELY finished - resetting to idle state");
            
            // NOW it's safe to reset UI state - the entire response is done
            if (companionUI != null)
            {
                LogMessage("🎵 Turning off audio playback indicator - response is fully complete");
                companionUI.ShowAudioPlaybackIndicator(false);
                companionUI.ShowProcessingIndicator(false);
                companionUI.UpdateStatusText("Ready - Tap to talk");
            }
            
            // Add safety timeout to ensure UI resets even if audio events are missed
            StartCoroutine(SafetyResetUIAfterDelay(10f));
            
            LogMessage("🎵 Audio playback should now be complete and UI reset to idle");
        }
        
        private IEnumerator SafetyResetUIAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Only reset if AI is not currently responding to something new
            if (!isAIResponding && companionUI != null)
            {
                LogMessage("🔄 Safety timeout - resetting UI to idle state");
                companionUI.ShowAudioPlaybackIndicator(false);
                companionUI.ShowProcessingIndicator(false);
                companionUI.UpdateStatusText("Ready - Tap to talk");
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
        
        // Public methods for external use
        
        public void SendTextMessage(string message)
        {
            LogMessage($"📤 SendTextMessage called with: {message.Substring(0, Math.Min(50, message.Length))}...");
            
            if (!isConnectionActive)
            {
                LogError("Cannot send message - connection not active");
                return;
            }
            
            LogMessage("✅ Connection active - proceeding with message send...");
            
            if (enableEnhancedAudio && enhancedAudioHandler != null)
            {
                LogMessage("🎵 Using Enhanced Audio Handler to send message");
                enhancedAudioHandler.SendTextMessage(message, "user_message");
            }
            else
            {
                LogMessage("📡 Using direct data channel to send message");
                // Fallback to direct sending
                SendDirectTextMessage(message);
            }
            
            LogMessage("✅ SendTextMessage completed");
        }
        
        private void SendDirectTextMessage(string message)
        {
            if (dataChannel?.ReadyState != RTCDataChannelState.Open)
            {
                LogError("Data channel not ready");
                return;
            }
            
            try
            {
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
                
                dataChannel.Send(Encoding.UTF8.GetBytes(evt.ToString(Newtonsoft.Json.Formatting.None)));
                LogMessage($"Sent text message: {message.Substring(0, Math.Min(50, message.Length))}...");
                
            }
            catch (Exception ex)
            {
                LogError($"Error sending text message: {ex.Message}");
            }
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
        
        // Legacy method names for compatibility
        public void StartRecording()
        {
            StartVoiceInput();
        }
        
        public void StopRecording()
        {
            StopVoiceInput();
        }
        
        private IEnumerator StreamAudioToOpenAI()
        {
            LogMessage("🎤 StreamAudioToOpenAI coroutine started");
            
            while (isTalking && isConnectionActive)
            {
                // Get microphone data
                int currentPosition = Microphone.GetPosition(null);
                if (currentPosition > lastMicrophonePosition)
                {
                    int samplesToRead = currentPosition - lastMicrophonePosition;
                    if (samplesToRead > 0)
                    {
                        float[] audioData = new float[samplesToRead];
                        microphoneClip.GetData(audioData, lastMicrophonePosition);
                        
                        // Send audio data through enhanced handler
                        if (enableEnhancedAudio && enhancedAudioHandler != null)
                        {
                            enhancedAudioHandler.SendAudioData(audioData);
                        }
                        
                        lastMicrophonePosition = currentPosition;
                    }
                }
                
                yield return new WaitForSeconds(0.1f); // 100ms intervals
            }
            
            LogMessage("🎤 StreamAudioToOpenAI coroutine ended");
        }
        
        private void SendClearAudioBuffer()
        {
            if (dataChannel?.ReadyState != RTCDataChannelState.Open)
            {
                LogMessage("Data channel not ready for clear audio buffer");
                return;
            }
            
            try
            {
                var clearEvt = new JObject
                {
                    ["type"] = "input_audio_buffer.clear"
                };
                
                string eventJson = clearEvt.ToString(Newtonsoft.Json.Formatting.None);
                byte[] eventBytes = Encoding.UTF8.GetBytes(eventJson);
                
                dataChannel.Send(eventBytes);
                LogMessage("✅ Sent clear audio buffer event");
                
            }
            catch (Exception ex)
            {
                LogError($"Error sending clear audio buffer: {ex.Message}");
            }
        }
        
        public void Disconnect()
        {
            LogMessage("Disconnecting WebRTC connection...");
            
            // Stop recording if active
            if (localMicTrack != null)
            {
                localMicTrack.Enabled = false;
            }
            
            // Stop audio streaming
            if (audioStreamingCoroutine != null)
            {
                StopCoroutine(audioStreamingCoroutine);
                audioStreamingCoroutine = null;
            }
            
            // Close data channel
            if (dataChannel != null)
            {
                dataChannel.Close();
                dataChannel = null;
            }
            
            // Close peer connection
            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection = null;
            }
            
            isConnectionActive = false;
            LogMessage("WebRTC connection closed");
        }
        

        
        public EnhancedAudioHandler.AudioSystemStatus GetAudioSystemStatus()
        {
            if (enableEnhancedAudio && enhancedAudioHandler != null)
            {
                return enhancedAudioHandler.GetSystemStatus();
            }
            
            return new EnhancedAudioHandler.AudioSystemStatus
            {
                isFlowControlled = false,
                currentBufferSize = 0,
                maxBufferSize = 0,
                audioBufferCount = 0,
                messageBufferCount = 0,
                bufferUsagePercentage = 0f
            };
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
                
                string responseJson = Newtonsoft.Json.JsonConvert.SerializeObject(responseEvent);
                
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
                        companionUI.ShowAudioPlaybackIndicator(true);
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
        
        /// <summary>
        /// Test method to send a simple message for debugging
        /// </summary>
        [ContextMenu("Test Send Message")]
        public void TestSendMessage()
        {
            if (!isConnectionActive)
            {
                LogError("Cannot test message - WebRTC connection not active");
                return;
            }
            
            LogMessage("🧪 Testing message send...");
            SendTextMessage("Hello! This is a test message.");
        }
        
        /// <summary>
        /// Test method to trigger greeting for debugging
        /// </summary>
        [ContextMenu("Test Greeting")]
        public void TestGreeting()
        {
            LogMessage("🧪 Testing greeting system...");
            TriggerGreeting();
        }
        
        /// <summary>
        /// Test method to send a greeting in the exact format the AI expects
        /// </summary>
        [ContextMenu("Test Direct Greeting")]
        public void TestDirectGreeting()
        {
            if (!isConnectionActive)
            {
                LogError("Cannot test direct greeting - WebRTC connection not active");
                return;
            }
            
            LogMessage("🧪 Testing direct greeting format...");
            
            // Create a simple greeting message in the exact format the AI expects
            var greetingEvent = new JObject
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
                            ["text"] = "Hello! I'm your AI companion. How can I help you today?"
                        }
                    }
                }
            };
            
            string eventJson = greetingEvent.ToString(Newtonsoft.Json.Formatting.None);
            byte[] eventBytes = Encoding.UTF8.GetBytes(eventJson);
            
            if (dataChannel?.ReadyState == RTCDataChannelState.Open)
            {
                LogMessage($"📡 Sending direct greeting: {eventBytes.Length} bytes");
                dataChannel.Send(eventBytes);
                LogMessage("✅ Direct greeting sent through data channel");
            }
            else
            {
                LogError("❌ Data channel not ready for direct greeting");
            }
        }
        
        /// <summary>
        /// Check if it's a good time to interact (simple audio-based check)
        /// </summary>
        public bool IsGoodTimeForInteraction()
        {
            // Don't interrupt if AI is currently responding
            if (isAIResponding)
            {
                LogMessage("⏳ AI is currently responding - wait for completion");
                return false;
            }
            
            LogMessage("✅ Good time for interaction - AI is not responding");
            return true;
        }
        
        /// <summary>
        /// Send message with simple audio-based timing check
        /// </summary>
        public void SendTextMessageWithTiming(string message)
        {
            if (!IsGoodTimeForInteraction())
            {
                LogMessage("⏳ Waiting for AI response to complete before sending message...");
                StartCoroutine(SendMessageWhenReady(message));
                return;
            }
            
            // Send immediately if timing is good
            SendTextMessage(message);
        }
        
        private IEnumerator SendMessageWhenReady(string message)
        {
            // Wait for AI response to complete
            while (isAIResponding)
            {
                yield return new WaitForSeconds(0.1f); // Check every 100ms
            }
            
            LogMessage("✅ AI response complete - sending message now");
            SendTextMessage(message);
        }
        
        /// <summary>
        /// Timer that waits for silence threshold before marking response complete
        /// </summary>
        private IEnumerator ResponseCompletionTimer()
        {
            LogMessage($"⏱️ Waiting {silenceThreshold}s of silence before marking response complete...");
            yield return new WaitForSeconds(silenceThreshold);
            
            // Check if new audio started during the silence period
            if (isAIResponding)
            {
                LogMessage("✅ Silence threshold reached - response marked complete");
                
                // Mark response as complete
                isAIResponding = false;
                responseCompletionTimer = null;
                
                // Update UI to show we're ready
                if (companionUI != null)
                {
                    companionUI.ShowAudioPlaybackIndicator(false);
                    companionUI.ShowProcessingIndicator(false);
                    companionUI.UpdateStatusText("Ready - Tap to talk");
                }
                
                LogMessage("🎯 User can now interact - response cycle complete");
            }
        }
        
        private void LogMessage(string message)
        {
            Debug.Log($"[MobileRealtimeChat_Enhanced] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[MobileRealtimeChat_Enhanced] {message}");
        }
        
        // Public properties for external access
        public bool IsConnected => isConnectionActive; // Alias for UI compatibility
        
        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
