using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class RAGCompanionSystem : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] private RAGClient ragClient;
    [SerializeField] private RealtimeSessionManager realtimeSession;
    [SerializeField] private ContextAssembler contextAssembler;
    [SerializeField] private ConversationState conversationState;
    [SerializeField] private TurnCoordinator turnCoordinator;
    [SerializeField] private AvatarSpeechController avatarSpeech;
    
    [Header("System Configuration")]
    [SerializeField] private string userId = "default-user";
    [SerializeField] private bool autoStartSession = true;
    [SerializeField] private bool enableSystemLogging = true;
    [SerializeField] private float systemStartupDelay = 2f;
    
    [Header("Service URLs")]
    [SerializeField] private string ragServiceUrl = "https://ragcompanion-production-bf25.up.railway.app";
    [SerializeField] private string openAIApiKey = "";
    
    [Header("Audio Settings")]
    [SerializeField] private bool enableAudioCapture = true;
    [SerializeField] private bool enableTTSPlayback = true;
    [SerializeField] private bool enableLipSync = true;
    
    // System state
    private bool isSystemReady = false;
    private bool isSessionActive = false;
    private string currentSessionId;
    private float systemUptime = 0f;
    
    // Performance tracking
    private Dictionary<string, float> componentLatencies = new Dictionary<string, float>();
    private int totalInteractions = 0;
    // private float averageResponseTime = 0f; // Unused - commented out
    
    // Events
    public event Action OnSystemReady;
    public event Action OnSystemStarted;
    public event Action OnSystemStopped;
    public event Action<string> OnInteractionStarted;
    public event Action<string> OnInteractionCompleted;
    public event Action<string> OnSystemError;
    
    private void Start()
    {
        InitializeSystem();
    }
    
    private void Update()
    {
        if (isSystemReady)
        {
            systemUptime += Time.deltaTime;
        }
    }
    
    private void OnDestroy()
    {
        StopSystem();
    }
    
    private async void InitializeSystem()
    {
        try
        {
            LogMessage("Initializing RAG Companion System...");
            
            // Wait for startup delay
            await Task.Delay(Mathf.RoundToInt(systemStartupDelay * 1000));
            
            // Step 1: Initialize components
            await InitializeComponents();
            
            // Step 2: Setup event handlers
            SetupEventHandlers();
            
            // Step 3: Validate configuration
            ValidateConfiguration();
            
            // Step 4: Start services
            await StartServices();
            
            // Step 5: Start session if auto-start enabled
            if (autoStartSession)
            {
                StartNewSession();
            }
            
            isSystemReady = true;
            LogMessage("RAG Companion System initialized successfully");
            
            OnSystemReady?.Invoke();
            OnSystemStarted?.Invoke();
            
        }
        catch (Exception ex)
        {
            LogError($"System initialization failed: {ex.Message}");
            OnSystemError?.Invoke($"System initialization failed: {ex.Message}");
        }
    }
    
    private Task InitializeComponents()
    {
        LogMessage("Initializing components...");
        
        // Find components if not assigned
        if (ragClient == null)
            ragClient = FindFirstObjectByType<RAGClient>();
        
        if (realtimeSession == null)
            realtimeSession = FindFirstObjectByType<RealtimeSessionManager>();
        
        if (contextAssembler == null)
            contextAssembler = FindFirstObjectByType<ContextAssembler>();
        
        if (conversationState == null)
            conversationState = FindFirstObjectByType<ConversationState>();
        
        if (turnCoordinator == null)
            turnCoordinator = FindFirstObjectByType<TurnCoordinator>();
        
        if (avatarSpeech == null)
            avatarSpeech = FindFirstObjectByType<AvatarSpeechController>();
        
        // Create missing components
        if (ragClient == null)
        {
            var ragObject = new GameObject("RAG Client");
            ragObject.transform.SetParent(transform);
            ragClient = ragObject.AddComponent<RAGClient>();
        }
        
        if (contextAssembler == null)
        {
            var contextObject = new GameObject("Context Assembler");
            contextObject.transform.SetParent(transform);
            contextAssembler = contextObject.AddComponent<ContextAssembler>();
        }
        
        if (conversationState == null)
        {
            var stateObject = new GameObject("Conversation State");
            stateObject.transform.SetParent(transform);
            conversationState = stateObject.AddComponent<ConversationState>();
        }
        
        if (turnCoordinator == null)
        {
            var coordinatorObject = new GameObject("Turn Coordinator");
            coordinatorObject.transform.SetParent(transform);
            turnCoordinator = coordinatorObject.AddComponent<TurnCoordinator>();
        }
        
        // Configure components
        ragClient.SetBaseUrl(ragServiceUrl);
        
        LogMessage("Components initialized");
        
        return Task.CompletedTask;
    }
    
    private void SetupEventHandlers()
    {
        LogMessage("Setting up event handlers...");
        
        // RAG Client events
        if (ragClient != null)
        {
            ragClient.OnError += HandleRagError;
            ragClient.OnQueryCompleted += HandleQueryCompleted;
        }
        
        // Realtime Session events
        if (realtimeSession != null)
        {
            realtimeSession.OnSessionConnected += HandleSessionConnected;
            realtimeSession.OnSessionDisconnected += HandleSessionDisconnected;
            realtimeSession.OnResponseReceived += HandleResponseReceived;
            realtimeSession.OnError += HandleRealtimeError;
        }
        
        // Turn Coordinator events
        if (turnCoordinator != null)
        {
            turnCoordinator.OnUserUtteranceFinal += HandleUserUtterance;
            turnCoordinator.OnResponseGenerated += HandleResponseGenerated;
            turnCoordinator.OnTurnCompleted += HandleTurnCompleted;
            turnCoordinator.OnError += HandleTurnError;
        }
        
        // Context Assembler events
        if (contextAssembler != null)
        {
            contextAssembler.OnContextAssembled += HandleContextAssembled;
            contextAssembler.OnError += HandleContextError;
        }
        
        // Conversation State events
        if (conversationState != null)
        {
            conversationState.OnTurnAdded += HandleTurnAdded;
            conversationState.OnFactExtracted += HandleFactExtracted;
            conversationState.OnError += HandleStateError;
        }
        
        // Avatar Speech events
        if (avatarSpeech != null)
        {
            avatarSpeech.OnSpeechStarted += HandleSpeechStarted;
            avatarSpeech.OnSpeechEnded += HandleSpeechEnded;
            avatarSpeech.OnError += HandleSpeechError;
        }
        
        LogMessage("Event handlers configured");
    }
    
    private void ValidateConfiguration()
    {
        LogMessage("Validating configuration...");
        
        // Check required components
        if (ragClient == null)
            throw new Exception("RAG Client not found");
        
        if (turnCoordinator == null)
            throw new Exception("Turn Coordinator not found");
        
        if (contextAssembler == null)
            throw new Exception("Context Assembler not found");
        
        // Check configuration
        if (string.IsNullOrEmpty(ragServiceUrl))
            throw new Exception("RAG Service URL not configured");
        
        if (string.IsNullOrEmpty(userId))
            throw new Exception("User ID not configured");
        
        // Load API key from configuration if not set
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            var ragConfig = RAGConfiguration.Instance;
            openAIApiKey = ragConfig.Settings.openAIAPIKey;
        }
        
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            LogMessage("Warning: OpenAI API key not configured - realtime features will be limited");
        }
        
        LogMessage("Configuration validated");
    }
    
    private async Task StartServices()
    {
        LogMessage("Starting services...");
        
        // Check RAG service health
        bool ragHealthy = await ragClient.CheckHealthAsync();
        if (!ragHealthy)
        {
            LogMessage("Warning: RAG service not responding - will retry connections");
        }
        
        // Connect to realtime session if available
        if (realtimeSession != null && !string.IsNullOrEmpty(openAIApiKey))
        {
            bool connected = await realtimeSession.ConnectSession();
            if (!connected)
            {
                LogMessage("Warning: Realtime session connection failed - text mode only");
            }
        }
        
        LogMessage("Services started");
    }
    
    public void StartNewSession()
    {
        try
        {
            LogMessage($"Starting new session for user: {userId}");
            
            currentSessionId = Guid.NewGuid().ToString();
            
            // Initialize conversation state
            if (conversationState != null)
            {
                conversationState.StartNewSession(userId);
            }
            
            // Configure turn coordinator
            if (turnCoordinator != null)
            {
                turnCoordinator.SetUserId(userId);
                turnCoordinator.StartNewSession();
            }
            
            // Start audio capture if enabled
            if (enableAudioCapture && realtimeSession != null)
            {
                realtimeSession.StartAudioCapture();
            }
            
            isSessionActive = true;
            LogMessage($"Session started: {currentSessionId}");
            
        }
        catch (Exception ex)
        {
            LogError($"Failed to start session: {ex.Message}");
            OnSystemError?.Invoke($"Failed to start session: {ex.Message}");
        }
    }
    
    public void StopCurrentSession()
    {
        if (!isSessionActive)
            return;
        
        try
        {
            LogMessage("Stopping current session...");
            
            // Stop audio capture
            if (realtimeSession != null)
            {
                realtimeSession.StopAudioCapture();
            }
            
            // Stop avatar speech
            if (avatarSpeech != null)
            {
                avatarSpeech.StopSpeech();
            }
            
            // End conversation state
            if (conversationState != null)
            {
                conversationState.EndCurrentSession();
            }
            
            isSessionActive = false;
            currentSessionId = null;
            
            LogMessage("Session stopped");
            
        }
        catch (Exception ex)
        {
            LogError($"Failed to stop session: {ex.Message}");
        }
    }
    
    public new void SendMessage(string message)
    {
        if (!isSystemReady)
        {
            LogError("System not ready - cannot send message");
            return;
        }
        
        try
        {
            LogMessage($"Sending message: {message}");
            OnInteractionStarted?.Invoke(message);
            
            // Let the turn coordinator handle the message
            if (turnCoordinator != null)
            {
                turnCoordinator.ProcessUserUtteranceFinal(message);
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Failed to send message: {ex.Message}");
            OnSystemError?.Invoke($"Failed to send message: {ex.Message}");
        }
    }
    
    public void StopSystem()
    {
        if (!isSystemReady)
            return;
        
        LogMessage("Stopping RAG Companion System...");
        
        // Stop current session
        if (isSessionActive)
        {
            StopCurrentSession();
        }
        
        // Disconnect realtime session
        if (realtimeSession != null)
        {
            realtimeSession.DisconnectSession();
        }
        
        isSystemReady = false;
        OnSystemStopped?.Invoke();
        
        LogMessage("System stopped");
    }
    
    // Event handlers
    private void HandleRagError(string error)
    {
        LogError($"RAG Error: {error}");
        OnSystemError?.Invoke($"RAG Error: {error}");
    }
    
    private void HandleQueryCompleted(RAGQueryResponse response)
    {
        LogMessage($"Query completed: {response.results.Count} results");
        componentLatencies["rag_query"] = response.query_embedding_ms ?? 0f;
    }
    
    private void HandleSessionConnected()
    {
        LogMessage("Realtime session connected");
    }
    
    private void HandleSessionDisconnected()
    {
        LogMessage("Realtime session disconnected");
    }
    
    private void HandleResponseReceived(string response)
    {
        LogMessage($"Response received: {response}");
        
        // Play through avatar if enabled
        if (enableTTSPlayback && avatarSpeech != null)
        {
            // Convert response to audio (simplified - would need TTS service)
            // avatarSpeech.PlayAudioSegment(audioData, duration, response);
        }
    }
    
    private void HandleRealtimeError(string error)
    {
        LogError($"Realtime Error: {error}");
        OnSystemError?.Invoke($"Realtime Error: {error}");
    }
    
    private void HandleUserUtterance(string utterance)
    {
        LogMessage($"User utterance: {utterance}");
        OnInteractionStarted?.Invoke(utterance);
    }
    
    private void HandleResponseGenerated(string response)
    {
        LogMessage($"Response generated: {response}");
    }
    
    private void HandleTurnCompleted(string response)
    {
        LogMessage($"Turn completed: {response}");
        totalInteractions++;
        OnInteractionCompleted?.Invoke(response);
    }
    
    private void HandleTurnError(string error)
    {
        LogError($"Turn Error: {error}");
        OnSystemError?.Invoke($"Turn Error: {error}");
    }
    
    private void HandleContextAssembled(string context)
    {
        LogMessage($"Context assembled: {context.Length} characters");
    }
    
    private void HandleContextError(string error)
    {
        LogError($"Context Error: {error}");
    }
    
    private void HandleTurnAdded(ConversationTurn turn)
    {
        LogMessage($"Turn added: {turn.role} - {turn.tokenCount} tokens");
    }
    
    private void HandleFactExtracted(UserFact fact)
    {
        LogMessage($"Fact extracted: {fact.category}.{fact.key} = {fact.value}");
    }
    
    private void HandleStateError(string error)
    {
        LogError($"State Error: {error}");
    }
    
    private void HandleSpeechStarted(string transcript)
    {
        LogMessage($"Speech started: {transcript}");
    }
    
    private void HandleSpeechEnded(string transcript)
    {
        LogMessage($"Speech ended: {transcript}");
    }
    
    private void HandleSpeechError(string error)
    {
        LogError($"Speech Error: {error}");
    }
    
    // Public API methods
    public void SetUserId(string newUserId)
    {
        userId = newUserId;
        if (turnCoordinator != null)
        {
            turnCoordinator.SetUserId(newUserId);
        }
    }
    
    public void SetRagServiceUrl(string newUrl)
    {
        ragServiceUrl = newUrl;
        if (ragClient != null)
        {
            ragClient.SetBaseUrl(newUrl);
        }
    }
    
    public void SetOpenAIApiKey(string newKey)
    {
        openAIApiKey = newKey;
        // Update realtime session if needed
    }
    
    public void ToggleAudioCapture(bool enabled)
    {
        enableAudioCapture = enabled;
        // Update realtime session if needed
    }
    
    public void ToggleTTSPlayback(bool enabled)
    {
        enableTTSPlayback = enabled;
        if (avatarSpeech != null)
        {
            avatarSpeech.SetLipSyncEnabled(enabled && enableLipSync);
        }
    }
    
    public void ToggleLipSync(bool enabled)
    {
        enableLipSync = enabled;
        if (avatarSpeech != null)
        {
            avatarSpeech.SetLipSyncEnabled(enabled);
        }
    }
    
    private void LogMessage(string message)
    {
        if (enableSystemLogging)
        {
            Debug.Log($"[RAGCompanionSystem] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[RAGCompanionSystem] {message}");
    }
    
    // Public getters for monitoring
    public bool IsSystemReady => isSystemReady;
    public bool IsSessionActive => isSessionActive;
    public string CurrentSessionId => currentSessionId;
    public float SystemUptime => systemUptime;
    public int TotalInteractions => totalInteractions;
    public Dictionary<string, float> ComponentLatencies => componentLatencies;
    
    // Component accessors
    public RAGClient RagClient => ragClient;
    public RealtimeSessionManager RealtimeSession => realtimeSession;
    public ContextAssembler ContextAssembler => contextAssembler;
    public ConversationState ConversationState => conversationState;
    public TurnCoordinator TurnCoordinator => turnCoordinator;
    public AvatarSpeechController AvatarSpeech => avatarSpeech;
}