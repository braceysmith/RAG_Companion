using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class TurnCoordinator : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private RAGClient ragClient;
    [SerializeField] private RealtimeSessionManager realtimeSession;
    [SerializeField] private ContextAssembler contextAssembler;
    [SerializeField] private ConversationState conversationState;
    
    [Header("Turn Settings")]
    [SerializeField] private string userId = "user-123";
    // [SerializeField] private float ragTimeoutSeconds = 2.0f; // Unused - commented out
    // [SerializeField] private float transcriptConfidenceThreshold = 0.8f; // Unused - commented out
    [SerializeField] private bool enableQueryClassification = true;
    [SerializeField] private bool enableMemoryRetrieval = true;
    
    [Header("Query Classification")]
    [SerializeField] private string[] knowledgeKeywords = { "what is", "how do", "explain", "treehouse", "energy", "guide" };
    [SerializeField] private string[] memoryKeywords = { "remember", "last time", "before", "previously", "my", "favorite" };
    
    // State management
    private bool isProcessingTurn = false;
    private string currentSessionId;
    private int currentTurnIndex = 0;
    private Queue<string> pendingTranscripts = new Queue<string>();
    
    // Performance tracking
    private float lastRagLatency = 0f;
    private float lastTurnLatency = 0f;
    private int totalTurns = 0;
    
    // Events
    public event Action<string> OnUserUtteranceDetected;
    public event Action<string> OnUserUtteranceFinal;
    public event Action<List<RAGResult>> OnKnowledgeRetrieved;
    public event Action<List<MemoryResult>> OnMemoryRetrieved;
    public event Action<string> OnContextAssembled;
    public event Action<string> OnResponseGenerated;
    public event Action<string> OnTurnCompleted;
    public event Action<string> OnError;
    
    private void Start()
    {
        InitializeComponents();
        SetupEventHandlers();
        currentSessionId = Guid.NewGuid().ToString();
    }
    
    private void InitializeComponents()
    {
        // Find components if not assigned
        if (ragClient == null)
            ragClient = FindFirstObjectByType<RAGClient>();
        
        if (realtimeSession == null)
            realtimeSession = FindFirstObjectByType<RealtimeSessionManager>();
        
        if (contextAssembler == null)
            contextAssembler = FindFirstObjectByType<ContextAssembler>();
        
        if (conversationState == null)
            conversationState = FindFirstObjectByType<ConversationState>();
        
        // Validate required components
        if (ragClient == null || realtimeSession == null || contextAssembler == null)
        {
            Debug.LogError("[TurnCoordinator] Missing required components");
        }
    }
    
    private void SetupEventHandlers()
    {
        if (realtimeSession != null)
        {
            realtimeSession.OnTranscriptReceived += HandleTranscriptReceived;
            realtimeSession.OnUserSpeechDetected += HandleUserSpeechDetected;
            realtimeSession.OnUserSpeechEnded += HandleUserSpeechEnded;
            realtimeSession.OnResponseReceived += HandleResponseReceived;
            realtimeSession.OnError += HandleRealtimeError;
        }
        
        if (ragClient != null)
        {
            ragClient.OnError += HandleRagError;
        }
    }
    
    private void OnDestroy()
    {
        if (realtimeSession != null)
        {
            realtimeSession.OnTranscriptReceived -= HandleTranscriptReceived;
            realtimeSession.OnUserSpeechDetected -= HandleUserSpeechDetected;
            realtimeSession.OnUserSpeechEnded -= HandleUserSpeechEnded;
            realtimeSession.OnResponseReceived -= HandleResponseReceived;
            realtimeSession.OnError -= HandleRealtimeError;
        }
        
        if (ragClient != null)
        {
            ragClient.OnError -= HandleRagError;
        }
    }
    
    public async void ProcessUserUtteranceFinal(string transcript)
    {
        if (isProcessingTurn)
        {
            LogMessage($"Queuing transcript: {transcript}");
            pendingTranscripts.Enqueue(transcript);
            return;
        }
        
        await ProcessUserUtterance(transcript);
    }
    
    private async Task ProcessUserUtterance(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            LogMessage("Empty transcript received, skipping");
            return;
        }
        
        isProcessingTurn = true;
        float turnStartTime = Time.time;
        
        try
        {
            LogMessage($"Processing user utterance: {transcript}");
            OnUserUtteranceFinal?.Invoke(transcript);
            
            // Step 1: Query Classification
            bool needsKnowledgeRetrieval = NeedsKnowledgeRetrieval(transcript);
            bool needsMemoryRetrieval = NeedsMemoryRetrieval(transcript);
            
            LogMessage($"Query classification - Knowledge: {needsKnowledgeRetrieval}, Memory: {needsMemoryRetrieval}");
            
            // Step 2: Parallel retrieval
            var retrievalTasks = new List<Task>();
            List<RAGResult> knowledgeResults = new List<RAGResult>();
            List<MemoryResult> memoryResults = new List<MemoryResult>();
            
            if (needsKnowledgeRetrieval)
            {
                retrievalTasks.Add(RetrieveKnowledge(transcript).ContinueWith(t => knowledgeResults = t.Result));
            }
            
            if (needsMemoryRetrieval && enableMemoryRetrieval)
            {
                retrievalTasks.Add(RetrieveMemory(transcript).ContinueWith(t => memoryResults = t.Result));
            }
            
            // Wait for retrieval with timeout
            float ragStartTime = Time.time;
            await Task.WhenAll(retrievalTasks);
            lastRagLatency = Time.time - ragStartTime;
            
            // Step 3: Context Assembly
            string assembledContext = contextAssembler.AssembleContext(
                userId, knowledgeResults, memoryResults, transcript);
            
            OnContextAssembled?.Invoke(assembledContext);
            
            // Step 4: Update Realtime Session
            await realtimeSession.AddContextItem(assembledContext, "system");
            await realtimeSession.AddUserItem(transcript);
            
            // Step 5: Request Model Response
            await realtimeSession.RequestModelResponse();
            
            // Step 6: Update conversation state
            contextAssembler.AddConversationTurn("user", transcript);
            
            // Step 7: Log turn (will be completed when response is received)
            currentTurnIndex++;
            totalTurns++;
            lastTurnLatency = Time.time - turnStartTime;
            
            LogMessage($"Turn processing completed in {lastTurnLatency:F2}s (RAG: {lastRagLatency:F2}s)");
            
        }
        catch (Exception ex)
        {
            LogError($"Turn processing failed: {ex.Message}");
            OnError?.Invoke($"Turn processing failed: {ex.Message}");
        }
        finally
        {
            isProcessingTurn = false;
            ProcessNextPendingTranscript();
        }
    }
    
    private async Task<List<RAGResult>> RetrieveKnowledge(string query)
    {
        try
        {
            LogMessage($"Retrieving knowledge for: {query}");
            var results = await ragClient.QueryAsync(query, userId, topK: 5);
            
            OnKnowledgeRetrieved?.Invoke(results);
            LogMessage($"Retrieved {results.Count} knowledge chunks");
            
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Knowledge retrieval failed: {ex.Message}");
            return new List<RAGResult>();
        }
    }
    
    private async Task<List<MemoryResult>> RetrieveMemory(string query)
    {
        try
        {
            LogMessage($"Retrieving memory for: {query}");
            var results = await ragClient.QueryMemoryAsync(userId, query, topK: 3);
            
            OnMemoryRetrieved?.Invoke(results);
            LogMessage($"Retrieved {results.Count} memory items");
            
            return results;
        }
        catch (Exception ex)
        {
            LogError($"Memory retrieval failed: {ex.Message}");
            return new List<MemoryResult>();
        }
    }
    
    private bool NeedsKnowledgeRetrieval(string transcript)
    {
        if (!enableQueryClassification)
            return true;
        
        string lowerTranscript = transcript.ToLowerInvariant();
        
        // Check for knowledge keywords
        bool hasKnowledgeKeywords = knowledgeKeywords.Any(keyword => lowerTranscript.Contains(keyword));
        
        // Check for question patterns
        bool isQuestion = lowerTranscript.Contains("?") || 
                         lowerTranscript.StartsWith("what") || 
                         lowerTranscript.StartsWith("how") || 
                         lowerTranscript.StartsWith("why") || 
                         lowerTranscript.StartsWith("where") || 
                         lowerTranscript.StartsWith("when");
        
        // Check for help requests
        bool isHelpRequest = lowerTranscript.Contains("help") || 
                            lowerTranscript.Contains("explain") || 
                            lowerTranscript.Contains("guide");
        
        return hasKnowledgeKeywords || isQuestion || isHelpRequest;
    }
    
    private bool NeedsMemoryRetrieval(string transcript)
    {
        if (!enableQueryClassification || !enableMemoryRetrieval)
            return false;
        
        string lowerTranscript = transcript.ToLowerInvariant();
        
        // Check for memory keywords
        bool hasMemoryKeywords = memoryKeywords.Any(keyword => lowerTranscript.Contains(keyword));
        
        // Check for personal references
        bool hasPersonalReference = lowerTranscript.Contains("i ") || 
                                   lowerTranscript.Contains("my ") || 
                                   lowerTranscript.Contains("me ");
        
        // Check for temporal references
        bool hasTemporalReference = lowerTranscript.Contains("last") || 
                                   lowerTranscript.Contains("previous") || 
                                   lowerTranscript.Contains("before") || 
                                   lowerTranscript.Contains("earlier");
        
        return hasMemoryKeywords || (hasPersonalReference && hasTemporalReference);
    }
    
    private void HandleTranscriptReceived(string transcript)
    {
        OnUserUtteranceDetected?.Invoke(transcript);
        LogMessage($"Transcript received: {transcript}");
        
        // For now, treat all transcripts as final
        // In production, you'd check confidence levels
        ProcessUserUtteranceFinal(transcript);
    }
    
    private void HandleUserSpeechDetected(string message)
    {
        LogMessage("User speech detected");
    }
    
    private void HandleUserSpeechEnded()
    {
        LogMessage("User speech ended");
    }
    
    private async void HandleResponseReceived(string response)
    {
        try
        {
            LogMessage($"Response received: {response}");
            OnResponseGenerated?.Invoke(response);
            
            // Update conversation state
            contextAssembler.AddConversationTurn("assistant", response);
            
            // Log the completed turn
            await LogConversationTurn(response);
            
            // Store response in memory for future retrieval
            await StoreResponseInMemory(response);
            
            OnTurnCompleted?.Invoke(response);
            
        }
        catch (Exception ex)
        {
            LogError($"Response handling failed: {ex.Message}");
        }
    }
    
    private async Task LogConversationTurn(string response)
    {
        try
        {
            string turnId = Guid.NewGuid().ToString();
            var lastUserMessage = contextAssembler.ConversationTurns > 0 ? "User message" : ""; // Simplified
            
            await ragClient.LogConversationAsync(
                turnId: turnId,
                sessionId: currentSessionId,
                userId: userId,
                turnIndex: currentTurnIndex,
                userMessage: lastUserMessage,
                assistantResponse: response,
                retrievedChunks: new string[0], // Would track actual chunks used
                metadata: new Dictionary<string, object>
                {
                    ["rag_latency"] = lastRagLatency,
                    ["turn_latency"] = lastTurnLatency,
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }
            );
            
            LogMessage($"Turn logged: {turnId}");
            
        }
        catch (Exception ex)
        {
            LogError($"Turn logging failed: {ex.Message}");
        }
    }
    
    private async Task StoreResponseInMemory(string response)
    {
        try
        {
            // Store semantic memory of the interaction
            await ragClient.StoreMemoryAsync(
                userId: userId,
                memoryType: "semantic",
                content: $"Assistant provided information: {response}",
                metadata: new Dictionary<string, object>
                {
                    ["response_type"] = "informational",
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["turn_index"] = currentTurnIndex
                }
            );
            
            LogMessage("Response stored in semantic memory");
            
        }
        catch (Exception ex)
        {
            LogError($"Memory storage failed: {ex.Message}");
        }
    }
    
    private void ProcessNextPendingTranscript()
    {
        if (pendingTranscripts.Count > 0)
        {
            string nextTranscript = pendingTranscripts.Dequeue();
            LogMessage($"Processing queued transcript: {nextTranscript}");
            _ = ProcessUserUtterance(nextTranscript);
        }
    }
    
    private void HandleRealtimeError(string error)
    {
        LogError($"Realtime session error: {error}");
        OnError?.Invoke($"Realtime error: {error}");
    }
    
    private void HandleRagError(string error)
    {
        LogError($"RAG service error: {error}");
        OnError?.Invoke($"RAG error: {error}");
    }
    
    public void SetUserId(string newUserId)
    {
        userId = newUserId;
        LogMessage($"User ID set to: {userId}");
    }
    
    public void StartNewSession()
    {
        currentSessionId = Guid.NewGuid().ToString();
        currentTurnIndex = 0;
        contextAssembler.ClearConversationHistory();
        LogMessage($"Started new session: {currentSessionId}");
    }
    
    public void UpdateQueryClassificationKeywords(string[] knowledgeWords, string[] memoryWords)
    {
        if (knowledgeWords != null)
            knowledgeKeywords = knowledgeWords;
        
        if (memoryWords != null)
            memoryKeywords = memoryWords;
        
        LogMessage("Query classification keywords updated");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[TurnCoordinator] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[TurnCoordinator] {message}");
    }
    
    // Public getters for monitoring
    public bool IsProcessingTurn => isProcessingTurn;
    public string CurrentSessionId => currentSessionId;
    public int CurrentTurnIndex => currentTurnIndex;
    public float LastRagLatency => lastRagLatency;
    public float LastTurnLatency => lastTurnLatency;
    public int TotalTurns => totalTurns;
    public int PendingTranscripts => pendingTranscripts.Count;
}