using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private bool enableRAGContext = false; // Temporarily disabled
    [SerializeField] private int maxRAGResults = 3;
    
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
    
    // Events
    public event Action OnConnectionEstablished;
    public event Action OnConnectionLost;
    public event Action<string> OnTranscriptReceived;
    public event Action<string> OnAIResponseReceived;
    public event Action<string> OnError;
    
    private void Start()
    {
        InitializeMobileRealtime();
    }
    
    private void InitializeMobileRealtime()
    {
        // Get component references
        companionUI = GetComponent<MobileCompanionUI>() ?? FindFirstObjectByType<MobileCompanionUI>();
        ragClient = GetComponent<MobileRAGClient>() ?? FindFirstObjectByType<MobileRAGClient>();
        ragConfig = RAGConfiguration.Instance;
        
        // Setup audio source for remote audio
        remoteAudioSource = gameObject.GetComponent<AudioSource>();
        if (remoteAudioSource == null)
        {
            remoteAudioSource = gameObject.AddComponent<AudioSource>();
        }
        remoteAudioSource.playOnAwake = false;
        remoteAudioSource.loop = true;
        
        // Subscribe to UI events
        if (companionUI != null)
        {
            // The UI will call our public methods directly
        }
        
        LogMessage("Mobile Realtime Chat initialized");
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
        StartCoroutine(CreateSessionAndConnect());
    }
    
    public void StartVoiceInput()
    {
        if (!isConnectionActive)
        {
            OnError?.Invoke("Not connected to realtime session");
            return;
        }
        
        if (isTalking)
        {
            LogMessage("Already recording voice input");
            return;
        }
        
        // Enable microphone
        if (localMicTrack != null)
        {
            localMicTrack.Enabled = true;
            isTalking = true;
            currentTranscript.Clear();
            
            LogMessage("Voice input started");
            
            // Update UI state
            if (companionUI != null)
            {
                companionUI.ShowRecordingIndicator(true);
            }
        }
        else
        {
            OnError?.Invoke("Microphone not initialized");
        }
    }
    
    public void StopVoiceInput()
    {
        if (!isTalking)
        {
            return;
        }
        
        // Disable microphone
        if (localMicTrack != null)
        {
            localMicTrack.Enabled = false;
            isTalking = false;
            
            LogMessage("Voice input stopped");
            
            // Update UI state
            if (companionUI != null)
            {
                companionUI.ShowRecordingIndicator(false);
                companionUI.ShowProcessingIndicator(true);
            }
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
            if (e.Track is AudioStreamTrack track)
            {
                remoteAudioSource.SetTrack(track);
                LogMessage("Remote audio track connected");
            }
        };
        
        peerConnection.OnIceConnectionChange = (RTCIceConnectionState state) =>
        {
            LogMessage($"ICE connection state: {state}");
            
            switch (state)
            {
                case RTCIceConnectionState.Connected:
                case RTCIceConnectionState.Completed:
                    isConnectionActive = true;
                    lastResponseTime = Time.time;
                    reconnectAttempts = 0;
                    OnConnectionEstablished?.Invoke();
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
            LogMessage("Data channel opened");
            isConnectionActive = true;
            
            // Update UI
            if (companionUI != null)
            {
                companionUI.ShowProcessingIndicator(false);
            }
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
        
        // Start microphone
        int sampleRate = AudioSettings.outputSampleRate;
        AudioClip micClip = Microphone.Start(null, true, 10, sampleRate);
        
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
        
        localAudioSource.clip = micClip;
        localAudioSource.loop = true;
        localAudioSource.mute = true; // Mute local playback
        localAudioSource.Play();
        
        // Create WebRTC audio track (disabled by default)
        localMicTrack = new AudioStreamTrack(localAudioSource);
        localMicTrack.Enabled = false;
        peerConnection.AddTrack(localMicTrack);
        
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
            LogMessage($"Received: {message.Substring(0, Math.Min(100, message.Length))}...");
            
            var jo = JObject.Parse(message);
            var messageType = jo["type"]?.ToString();
            
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
                    
                default:
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
            LogMessage($"Audio transcript: {transcript}");
            currentTranscript.Clear();
            currentTranscript.Append(transcript);
            
            OnTranscriptReceived?.Invoke(transcript);
            
            // Update UI
            if (companionUI != null)
            {
                companionUI.ShowTranscript(transcript);
                companionUI.AddMessage(transcript, "user");
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
        LogMessage("AI response started");
        
        // Update UI
        if (companionUI != null)
        {
            companionUI.ShowProcessingIndicator(false);
            companionUI.ShowAudioPlaybackIndicator(true);
        }
    }
    
    private void HandleResponseComplete()
    {
        isAIResponding = false;
        LogMessage("AI response completed");
        
        // Update UI
        if (companionUI != null)
        {
            companionUI.ShowAudioPlaybackIndicator(false);
        }
    }
    
    private void HandleTextResponse(JObject message)
    {
        var text = ExtractTextFromMessage(message);
        if (!string.IsNullOrEmpty(text))
        {
            OnAIResponseReceived?.Invoke(text);
            
            // Update UI
            if (companionUI != null)
            {
                companionUI.AddMessage(text, "assistant");
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
        if (!enableRAGContext || ragClient == null)
        {
            callback?.Invoke(GetDefaultInstructions());
            return;
        }
        
        StartCoroutine(GetRAGInstructionsCoroutine(callback));
    }
    
    private IEnumerator GetRAGInstructionsCoroutine(System.Action<string> callback)
    {
        // For now, use default instructions since we can't easily mix async with coroutines
        // TODO: Implement proper coroutine-based RAG query that uses ragApiUrl, userId, maxRAGResults
        
        callback?.Invoke(GetDefaultInstructions());
        yield break;
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
        
        // Trigger response
        var responseEvt = new JObject { ["type"] = "response.create" };
        dataChannel.Send(Encoding.UTF8.GetBytes(responseEvt.ToString(Formatting.None)));
        
        LogMessage($"Sent text message: {message}");
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
        return "You are a helpful AI assistant with access to the user's personal information and context. " +
               "Respond naturally and conversationally, incorporating relevant personal details when appropriate. " +
               "Keep responses concise but informative.";
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileRealtimeChat] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileRealtimeChat] {message}");
    }
    
    // Public properties
    public bool IsConnected => isConnectionActive;
    public bool IsTalking => isTalking;
    public bool IsAIResponding => isAIResponding;
    
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