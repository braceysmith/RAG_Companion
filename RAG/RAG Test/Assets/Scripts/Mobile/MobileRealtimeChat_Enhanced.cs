using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
        
        [Header("RAG Integration")]
        [SerializeField] private bool enableRAGContext = true;
        [SerializeField] private bool enableAutoGreeting = true;
        
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
            yield return new WaitForSeconds(2f);
            
            if (isConnectionActive && enableAutoGreeting)
            {
                LogMessage("🤝 Sending auto-greeting...");
                SendTextMessage("Hello! I'm your AI companion. How can I help you today?");
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
        
        private void HandleAudioResponseDone(JObject message)
        {
            LogMessage("Audio response complete");
            
            // Update UI to show we're ready
            if (companionUI != null)
            {
                companionUI.ShowAudioPlaybackIndicator(false);
                companionUI.ShowProcessingIndicator(false);
                companionUI.UpdateStatusText("Ready - Tap to talk");
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
            LogMessage("Response complete");
            
            // Process any queued audio/messages
            if (enableEnhancedAudio && enhancedAudioHandler != null)
            {
                enhancedAudioHandler.ProcessQueuedAudio();
                enhancedAudioHandler.ProcessQueuedMessages();
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
            if (!isConnectionActive)
            {
                LogError("Cannot send message - connection not active");
                return;
            }
            
            if (enableEnhancedAudio && enhancedAudioHandler != null)
            {
                enhancedAudioHandler.SendTextMessage(message, "user_message");
            }
            else
            {
                // Fallback to direct sending
                SendDirectTextMessage(message);
            }
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
        
        public void StartRecording()
        {
            if (!isConnectionActive)
            {
                LogError("Cannot start recording - connection not active");
                return;
            }
            
            if (localMicTrack != null)
            {
                localMicTrack.Enabled = true;
                LogMessage("Started recording");
                
                // Start audio streaming coroutine
                if (audioStreamingCoroutine != null)
                {
                    StopCoroutine(audioStreamingCoroutine);
                }
                audioStreamingCoroutine = StartCoroutine(AudioStreamingCoroutine());
                
                // Update UI
                if (companionUI != null)
                {
                    companionUI.ShowRecordingIndicator(true);
                    companionUI.UpdateStatusText("Recording... Tap to stop");
                }
            }
        }
        
        public void StopRecording()
        {
            if (localMicTrack != null)
            {
                localMicTrack.Enabled = false;
                LogMessage("Stopped recording");
                
                // Stop audio streaming
                if (audioStreamingCoroutine != null)
                {
                    StopCoroutine(audioStreamingCoroutine);
                    audioStreamingCoroutine = null;
                }
                
                // Update UI
                if (companionUI != null)
                {
                    companionUI.ShowRecordingIndicator(false);
                    companionUI.ShowProcessingIndicator(true);
                    companionUI.UpdateStatusText("Processing speech...");
                }
            }
        }
        
        private IEnumerator AudioStreamingCoroutine()
        {
            while (localMicTrack.Enabled && isConnectionActive)
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
        
        public bool IsConnected()
        {
            return isConnectionActive && dataChannel?.ReadyState == RTCDataChannelState.Open;
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
        
        private void LogMessage(string message)
        {
            Debug.Log($"[MobileRealtimeChat_Enhanced] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[MobileRealtimeChat_Enhanced] {message}");
        }
        
        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
