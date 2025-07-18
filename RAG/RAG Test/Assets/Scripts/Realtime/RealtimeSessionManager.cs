using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Net.WebSockets;
using System.Threading;
using Newtonsoft.Json;

[System.Serializable]
public class RealtimeSessionConfig
{
    public string model = "gpt-4o-mini-realtime";
    public string voice = "alloy";
    public string input_audio_format = "pcm16";
    public string output_audio_format = "pcm16";
    public bool turn_detection_enabled = true;
    public float turn_detection_threshold = 0.5f;
    public int turn_detection_silence_duration_ms = 800;
    public int max_response_output_tokens = 1000;
    public float temperature = 0.7f;
}

[System.Serializable]
public class RealtimeEvent
{
    public string type;
    public string event_id;
    public object data;
    
    public RealtimeEvent(string eventType, object eventData = null)
    {
        type = eventType;
        event_id = Guid.NewGuid().ToString();
        data = eventData;
    }
}

[System.Serializable]
public class ConversationItem
{
    public string id;
    public string type; // "message", "function_call", "function_call_output"
    public string role; // "user", "assistant", "system"
    public List<ContentItem> content;
    public string status; // "completed", "incomplete", "failed"
    
    public ConversationItem(string itemType, string itemRole, List<ContentItem> itemContent)
    {
        id = Guid.NewGuid().ToString();
        type = itemType;
        role = itemRole;
        content = itemContent;
        status = "completed";
    }
}

[System.Serializable]
public class ContentItem
{
    public string type; // "text", "audio", "input_text", "input_audio"
    public string text;
    public string audio; // base64 encoded
    public string transcript;
    
    public ContentItem(string contentType, string contentText = null, string contentAudio = null)
    {
        type = contentType;
        text = contentText;
        audio = contentAudio;
    }
}

public class RealtimeSessionManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private RealtimeSessionConfig config = new RealtimeSessionConfig();
    [SerializeField] private string openAIApiKey;
    [SerializeField] private bool enableLogging = true;
    
    [Header("Audio Settings")]
    [SerializeField] private int sampleRate = 24000;
    // [SerializeField] private int channels = 1; // Unused - commented out
    // [SerializeField] private int bufferSize = 4096; // Unused - commented out
    
    // WebSocket connection
    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnected = false;
    private bool isSessionActive = false;
    
    // Audio processing
    private AudioSource audioSource;
    private AudioClip microphoneClip;
    private int microphonePosition = 0;
    private Queue<float[]> audioQueue = new Queue<float[]>();
    private Queue<byte[]> playbackQueue = new Queue<byte[]>();
    
    // Session state
    private List<ConversationItem> conversationItems = new List<ConversationItem>();
    private string currentResponseId;
    private StringBuilder currentTranscript = new StringBuilder();
    
    // Events
    public event Action<string> OnTranscriptReceived;
    public event Action<string> OnResponseReceived;
    // public event Action<byte[]> OnAudioReceived; // Unused - commented out
    public event Action<string> OnError;
    public event Action OnSessionConnected;
    public event Action OnSessionDisconnected;
    public event Action<string> OnUserSpeechDetected;
    public event Action OnUserSpeechEnded;
    
    // Constants
    private const string OPENAI_REALTIME_URL = "wss://api.openai.com/v1/realtime";
    
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Load API key from configuration if not set
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            var ragConfig = RAGConfiguration.Instance;
            openAIApiKey = ragConfig.Settings.openAIAPIKey;
        }
    }
    
    private void OnDestroy()
    {
        DisconnectSession();
    }
    
    public async Task<bool> ConnectSession()
    {
        if (isConnected)
        {
            LogMessage("Session already connected");
            return true;
        }
        
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            LogError("OpenAI API key not provided");
            return false;
        }
        
        try
        {
            webSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();
            
            // Add authorization header
            webSocket.Options.SetRequestHeader("Authorization", $"Bearer {openAIApiKey}");
            webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");
            
            // Connect to WebSocket
            await webSocket.ConnectAsync(new Uri($"{OPENAI_REALTIME_URL}?model={config.model}"), cancellationTokenSource.Token);
            
            isConnected = true;
            LogMessage("WebSocket connected successfully");
            
            // Start listening for messages
            _ = Task.Run(ListenForMessages);
            
            // Send session configuration
            await SendSessionUpdate();
            
            OnSessionConnected?.Invoke();
            return true;
            
        }
        catch (Exception ex)
        {
            LogError($"Connection failed: {ex.Message}");
            OnError?.Invoke($"Connection failed: {ex.Message}");
            return false;
        }
    }
    
    public async void DisconnectSession()
    {
        if (!isConnected) return;
        
        try
        {
            isConnected = false;
            isSessionActive = false;
            
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            }
            
            cancellationTokenSource?.Cancel();
            
            // Stop microphone
            if (Microphone.IsRecording(null))
            {
                Microphone.End(null);
            }
            
            OnSessionDisconnected?.Invoke();
            LogMessage("Session disconnected");
            
        }
        catch (Exception ex)
        {
            LogError($"Disconnection error: {ex.Message}");
        }
    }
    
    public async Task SendSessionUpdate()
    {
        var sessionEvent = new RealtimeEvent("session.update", new
        {
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions = GetSystemInstructions(),
                voice = config.voice,
                input_audio_format = config.input_audio_format,
                output_audio_format = config.output_audio_format,
                input_audio_transcription = new { model = "whisper-1" },
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = config.turn_detection_threshold,
                    prefix_padding_ms = 300,
                    silence_duration_ms = config.turn_detection_silence_duration_ms
                },
                tools = new object[] { },
                tool_choice = "auto",
                temperature = config.temperature,
                max_response_output_tokens = config.max_response_output_tokens
            }
        });
        
        await SendEvent(sessionEvent);
    }
    
    public void StartAudioCapture()
    {
        if (!isConnected)
        {
            LogError("Not connected to session");
            return;
        }
        
        try
        {
            // Get available microphones
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                LogError("No microphone devices found");
                return;
            }
            
            // Start recording
            string deviceName = devices[0]; // Use first available device
            microphoneClip = Microphone.Start(deviceName, true, 1, sampleRate);
            
            if (microphoneClip == null)
            {
                LogError("Failed to start microphone");
                return;
            }
            
            LogMessage($"Started microphone capture on device: {deviceName}");
            
            // Start audio processing coroutine
            StartCoroutine(ProcessAudioInput());
            
        }
        catch (Exception ex)
        {
            LogError($"Audio capture error: {ex.Message}");
        }
    }
    
    public void StopAudioCapture()
    {
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
            LogMessage("Stopped microphone capture");
        }
    }
    
    public async Task AddContextItem(string content, string role = "system")
    {
        var contentItem = new ContentItem("text", content);
        var conversationItem = new ConversationItem("message", role, new List<ContentItem> { contentItem });
        
        conversationItems.Add(conversationItem);
        
        var createEvent = new RealtimeEvent("conversation.item.create", new
        {
            item = conversationItem
        });
        
        await SendEvent(createEvent);
    }
    
    public async Task AddUserItem(string message)
    {
        var contentItem = new ContentItem("input_text", message);
        var conversationItem = new ConversationItem("message", "user", new List<ContentItem> { contentItem });
        
        conversationItems.Add(conversationItem);
        
        var createEvent = new RealtimeEvent("conversation.item.create", new
        {
            item = conversationItem
        });
        
        await SendEvent(createEvent);
    }
    
    public async Task RequestModelResponse()
    {
        var responseEvent = new RealtimeEvent("response.create", new
        {
            response = new
            {
                modalities = new[] { "text", "audio" },
                instructions = "Please respond naturally and helpfully."
            }
        });
        
        await SendEvent(responseEvent);
    }
    
    private async Task SendEvent(RealtimeEvent eventObj)
    {
        if (!isConnected || webSocket.State != WebSocketState.Open)
        {
            LogError("Cannot send event: WebSocket not connected");
            return;
        }
        
        try
        {
            string json = JsonConvert.SerializeObject(eventObj);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
            
            if (enableLogging)
            {
                LogMessage($"Sent event: {eventObj.type}");
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Send event error: {ex.Message}");
        }
    }
    
    private async Task ListenForMessages()
    {
        var buffer = new byte[1024 * 4];
        
        while (isConnected && webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessIncomingMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle binary audio data
                    byte[] audioData = new byte[result.Count];
                    Array.Copy(buffer, audioData, result.Count);
                    playbackQueue.Enqueue(audioData);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    LogMessage("WebSocket closed by server");
                    break;
                }
                
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogError($"Listen error: {ex.Message}");
                break;
            }
        }
    }
    
    private void ProcessIncomingMessage(string message)
    {
        try
        {
            var eventData = JsonConvert.DeserializeObject<RealtimeEvent>(message);
            
            if (enableLogging)
            {
                LogMessage($"Received event: {eventData.type}");
            }
            
            switch (eventData.type)
            {
                case "session.created":
                    isSessionActive = true;
                    LogMessage("Session created successfully");
                    break;
                    
                case "conversation.item.input_audio_transcription.completed":
                    HandleTranscriptionCompleted(eventData);
                    break;
                    
                case "response.text.delta":
                    HandleResponseTextDelta(eventData);
                    break;
                    
                case "response.audio.delta":
                    HandleResponseAudioDelta(eventData);
                    break;
                    
                case "response.done":
                    HandleResponseDone(eventData);
                    break;
                    
                case "input_audio_buffer.speech_started":
                    OnUserSpeechDetected?.Invoke("Speech detected");
                    break;
                    
                case "input_audio_buffer.speech_stopped":
                    OnUserSpeechEnded?.Invoke();
                    break;
                    
                case "error":
                    HandleError(eventData);
                    break;
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Message processing error: {ex.Message}");
        }
    }
    
    private void HandleTranscriptionCompleted(RealtimeEvent eventData)
    {
        // Handle transcription completion
        var transcript = eventData.data.ToString(); // Simplified - would need proper parsing
        OnTranscriptReceived?.Invoke(transcript);
    }
    
    private void HandleResponseTextDelta(RealtimeEvent eventData)
    {
        // Handle streaming text response
        var textData = eventData.data.ToString(); // Simplified - would need proper parsing
        currentTranscript.Append(textData);
    }
    
    private void HandleResponseAudioDelta(RealtimeEvent eventData)
    {
        // Handle streaming audio response
        // Would need to decode base64 audio data and queue for playback
    }
    
    private void HandleResponseDone(RealtimeEvent eventData)
    {
        string finalResponse = currentTranscript.ToString();
        OnResponseReceived?.Invoke(finalResponse);
        currentTranscript.Clear();
    }
    
    private void HandleError(RealtimeEvent eventData)
    {
        string errorMessage = eventData.data.ToString();
        LogError($"Server error: {errorMessage}");
        OnError?.Invoke(errorMessage);
    }
    
    private System.Collections.IEnumerator ProcessAudioInput()
    {
        while (Microphone.IsRecording(null))
        {
            int currentPos = Microphone.GetPosition(null);
            
            if (currentPos != microphonePosition)
            {
                // Get audio data
                float[] audioData = new float[currentPos - microphonePosition];
                microphoneClip.GetData(audioData, microphonePosition);
                
                // Convert to bytes and send
                byte[] audioBytes = ConvertFloatArrayToByteArray(audioData);
                _ = SendAudioData(audioBytes);
                
                microphonePosition = currentPos;
            }
            
            yield return null;
        }
    }
    
    private async Task SendAudioData(byte[] audioData)
    {
        if (!isConnected || webSocket.State != WebSocketState.Open)
            return;
        
        try
        {
            string base64Audio = Convert.ToBase64String(audioData);
            var audioEvent = new RealtimeEvent("input_audio_buffer.append", new
            {
                audio = base64Audio
            });
            
            await SendEvent(audioEvent);
            
        }
        catch (Exception ex)
        {
            LogError($"Audio send error: {ex.Message}");
        }
    }
    
    private byte[] ConvertFloatArrayToByteArray(float[] floatArray)
    {
        byte[] byteArray = new byte[floatArray.Length * 2]; // 16-bit PCM
        
        for (int i = 0; i < floatArray.Length; i++)
        {
            short sample = (short)(floatArray[i] * 32767f);
            byteArray[i * 2] = (byte)(sample & 0xFF);
            byteArray[i * 2 + 1] = (byte)(sample >> 8);
        }
        
        return byteArray;
    }
    
    private string GetSystemInstructions()
    {
        return "You are a helpful AI companion. Respond naturally and conversationally. " +
               "Use the provided knowledge context when available to give accurate information. " +
               "If you don't know something, say so rather than guessing.";
    }
    
    private void LogMessage(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[RealtimeSession] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[RealtimeSession] {message}");
    }
    
    // Public getters
    public bool IsConnected => isConnected;
    public bool IsSessionActive => isSessionActive;
    public List<ConversationItem> ConversationItems => conversationItems;
}