using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class MobileRAGCompanionSystem : MonoBehaviour
{
    [Header("Mobile Configuration")]
    [SerializeField] private string cloudRAGUrl = "https://ragcompanion-production-bf25.up.railway.app";
    [SerializeField] private string userId = "mobile-user";
    [SerializeField] private bool enableOfflineMode = true;
    [SerializeField] private bool enableAutoSync = true;
    [SerializeField] private bool enableBatteryOptimization = true;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxConcurrentRequests = 3;
    // [SerializeField] private float requestTimeout = 30f; // Commented out - unused
    // [SerializeField] private bool enableRequestBatching = true; // Commented out - unused
    // [SerializeField] private int batchSize = 5; // Commented out - unused
    
    [Header("UI References")]
    [SerializeField] private MobileCompanionUI mobileUI;
    [SerializeField] private MobileAudioManager audioManager;
    [SerializeField] private MobileNotificationManager notificationManager;
    
    // Core mobile components
    private MobileRAGClient ragClient;
    private MobileNetworkManager networkManager;
    private MobileConversationCache conversationCache;
    private MobileAuthManager authManager;
    private MobileContextAssembler contextAssembler;
    
    // System state
    private bool isSystemReady = false;
    private bool isProcessingRequest = false;
    private Queue<string> requestQueue = new Queue<string>();
    private List<Task> activeRequests = new List<Task>();
    
    // Performance tracking
    private float systemStartTime;
    private int totalInteractions = 0;
    private int offlineInteractions = 0;
    private float averageResponseTime = 0f;
    private List<float> responseTimes = new List<float>();
    
    // Events
    public event Action OnSystemReady;
    public event Action<bool> OnSystemStatusChanged;
    public event Action<string> OnUserMessage;
    public event Action<string> OnAssistantResponse;
    public event Action<bool> OnOfflineModeChanged;
    public event Action<string> OnError;
    
    private void Start()
    {
        systemStartTime = Time.time;
        InitializeMobileSystem();
    }
    
    private async void InitializeMobileSystem()
    {
        try
        {
            LogMessage("Initializing Mobile RAG Companion System...");
            
            // Initialize core components
            await InitializeComponents();
            
            // Setup event handlers
            SetupEventHandlers();
            
            // Configure mobile-specific settings
            ConfigureMobileSettings();
            
            // Perform initial sync if online
            if (networkManager.IsConnected && enableAutoSync)
            {
                await PerformInitialSync();
            }
            
            isSystemReady = true;
            OnSystemReady?.Invoke();
            OnSystemStatusChanged?.Invoke(true);
            
            LogMessage("Mobile RAG Companion System initialized successfully");
        }
        catch (Exception ex)
        {
            LogError($"Mobile system initialization failed: {ex.Message}");
            OnError?.Invoke($"System initialization failed: {ex.Message}");
        }
    }
    
    private async Task InitializeComponents()
    {
        // Initialize RAG client
        ragClient = GetComponent<MobileRAGClient>() ?? gameObject.AddComponent<MobileRAGClient>();
        ragClient.SetCloudApiUrl(cloudRAGUrl);
        
        // Initialize network manager
        networkManager = GetComponent<MobileNetworkManager>() ?? gameObject.AddComponent<MobileNetworkManager>();
        
        // Initialize conversation cache
        conversationCache = GetComponent<MobileConversationCache>() ?? gameObject.AddComponent<MobileConversationCache>();
        
        // Initialize auth manager
        authManager = GetComponent<MobileAuthManager>() ?? gameObject.AddComponent<MobileAuthManager>();
        
        // Initialize context assembler
        contextAssembler = GetComponent<MobileContextAssembler>() ?? gameObject.AddComponent<MobileContextAssembler>();
        
        // Initialize UI components
        if (mobileUI == null)
            mobileUI = FindFirstObjectByType<MobileCompanionUI>();
        
        if (audioManager == null)
            audioManager = FindFirstObjectByType<MobileAudioManager>();
        
        if (notificationManager == null)
            notificationManager = FindFirstObjectByType<MobileNotificationManager>();
        
        // Create missing UI components if needed
        if (mobileUI == null)
        {
            var uiObject = new GameObject("Mobile UI");
            uiObject.transform.SetParent(transform);
            mobileUI = uiObject.AddComponent<MobileCompanionUI>();
        }
        
        if (audioManager == null)
        {
            var audioObject = new GameObject("Mobile Audio Manager");
            audioObject.transform.SetParent(transform);
            audioManager = audioObject.AddComponent<MobileAudioManager>();
        }
        
        if (notificationManager == null)
        {
            var notificationObject = new GameObject("Mobile Notification Manager");
            notificationObject.transform.SetParent(transform);
            notificationManager = notificationObject.AddComponent<MobileNotificationManager>();
        }
        
        // Wait for components to initialize
        await Task.Delay(100);
        
        LogMessage("Mobile components initialized");
    }
    
    private void SetupEventHandlers()
    {
        // Network events
        if (networkManager != null)
        {
            networkManager.OnConnectionChanged += HandleConnectionChanged;
            networkManager.OnLowBatteryStatusChanged += HandleLowBatteryChanged;
            networkManager.OnDataLimitReached += HandleDataLimitReached;
        }
        
        // RAG client events
        if (ragClient != null)
        {
            ragClient.OnQueryCompleted += HandleQueryCompleted;
            ragClient.OnConnectionStatusChanged += HandleRAGConnectionChanged;
            ragClient.OnError += HandleRAGError;
            ragClient.OnCacheHit += HandleCacheHit;
            ragClient.OnOfflineQueueChanged += HandleOfflineQueueChanged;
        }
        
        // Auth events
        if (authManager != null)
        {
            authManager.OnAuthenticationChanged += HandleAuthenticationChanged;
            authManager.OnAuthError += HandleAuthError;
            authManager.OnTokenExpired += HandleTokenExpired;
        }
        
        // Cache events
        if (conversationCache != null)
        {
            conversationCache.OnCacheReady += HandleCacheReady;
            conversationCache.OnCacheError += HandleCacheError;
        }
        
        // UI events
        if (mobileUI != null)
        {
            mobileUI.OnUserMessageSubmitted += HandleUserMessageSubmitted;
            mobileUI.OnSettingsChanged += HandleSettingsChanged;
        }
        
        // Audio events
        if (audioManager != null)
        {
            audioManager.OnAudioRecorded += HandleAudioRecorded;
            audioManager.OnAudioPlaybackCompleted += HandleAudioPlaybackCompleted;
        }
        
        LogMessage("Event handlers configured");
    }
    
    private void ConfigureMobileSettings()
    {
        // Configure based on device capabilities
        if (enableBatteryOptimization)
        {
            ConfigureBatteryOptimization();
        }
        
        // Configure network settings
        if (networkManager != null)
        {
            networkManager.OptimizeForBattery(enableBatteryOptimization);
            networkManager.OptimizeForDataUsage(true);
        }
        
        // Configure context assembler for mobile
        if (contextAssembler != null)
        {
            contextAssembler.SetMobileOptimizations(true);
        }
        
        LogMessage("Mobile settings configured");
    }
    
    private void ConfigureBatteryOptimization()
    {
        float batteryLevel = SystemInfo.batteryLevel;
        
        if (batteryLevel < 0.2f && batteryLevel > 0) // Low battery
        {
            // Reduce functionality
            maxConcurrentRequests = 1;
            // requestTimeout = 15f; // Commented out - unused variable
            // enableRequestBatching = false; // Commented out - unused variable
            
            LogMessage("Battery optimization enabled - reduced functionality");
        }
        else if (batteryLevel < 0.5f && batteryLevel > 0) // Medium battery
        {
            // Moderate optimization
            maxConcurrentRequests = 2;
            // requestTimeout = 20f; // Commented out - unused variable
            
            LogMessage("Battery optimization enabled - moderate functionality");
        }
    }
    
    private async Task PerformInitialSync()
    {
        try
        {
            if (conversationCache == null)
                return;
            
            LogMessage("Performing initial sync...");
            
            // Load conversations from cloud first
            if (networkManager.IsConnected)
            {
                LogMessage("Loading conversations from cloud...");
                bool loadedFromCloud = await conversationCache.LoadConversationsFromCloudAsync(userId, 20);
                if (loadedFromCloud)
                {
                    LogMessage("Successfully loaded conversations from cloud");
                }
                else
                {
                    LogMessage("Failed to load conversations from cloud, using local cache only");
                }
            }
            
            // Sync unsynced conversations
            var unsyncedConversations = conversationCache.GetUnsyncedConversationsAsync(50);
            
            foreach (var conversation in unsyncedConversations)
            {
                if (!networkManager.IsConnected)
                    break;
                
                // TODO: Implement sync to cloud service
                await Task.Delay(100); // Simulate sync
                
                await conversationCache.MarkAsSyncedAsync(conversation.id, true);
            }
            
            LogMessage($"Initial sync completed: {unsyncedConversations.Count} conversations synced");
        }
        catch (Exception ex)
        {
            LogError($"Initial sync failed: {ex.Message}");
        }
    }
    
    public async Task<bool> SendMessageAsync(string message)
    {
        if (!isSystemReady)
        {
            LogError("System not ready - cannot send message");
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(message))
        {
            LogError("Empty message - cannot send");
            return false;
        }
        
        try
        {
            // Queue message if system is busy
            if (isProcessingRequest && activeRequests.Count >= maxConcurrentRequests)
            {
                requestQueue.Enqueue(message);
                LogMessage($"Message queued: {message}");
                return true;
            }
            
            float startTime = Time.time;
            isProcessingRequest = true;
            
            OnUserMessage?.Invoke(message);
            
            // Store user message in cache
            if (conversationCache != null)
            {
                await conversationCache.StoreConversationAsync(userId, message, "", null);
            }
            
            // Get auth token
            string authToken = authManager.GetAuthTokenAsync();
            
            // Query RAG system
            var ragResults = await ragClient.QueryAsync(message, userId, 5);
            LogMessage($"RAG query completed, received {ragResults?.Count ?? 0} results");
            
            // Assemble context
            string context = contextAssembler.AssembleContextAsync(userId, ragResults, message);
            
            // Generate response using RAG results
            string response = GenerateResponseFromRAGResults(ragResults, message);
            LogMessage($"Generated response: {response}");
            
            // Store response in cache
            if (conversationCache != null)
            {
                await conversationCache.StoreConversationAsync(userId, message, response, null);
            }
            
            // Update conversation history in context assembler
            if (contextAssembler != null)
            {
                contextAssembler.UpdateConversationHistory(userId, message, response);
            }
            
            // Track performance
            float responseTime = Time.time - startTime;
            responseTimes.Add(responseTime);
            if (responseTimes.Count > 100)
            {
                responseTimes.RemoveAt(0);
            }
            
            averageResponseTime = responseTimes.Count > 0 ? 
                responseTimes.ToArray().Average() : 0f;
            
            totalInteractions++;
            
            if (!networkManager.IsConnected)
            {
                offlineInteractions++;
            }
            
            try
            {
                OnAssistantResponse?.Invoke(response);
                LogMessage($"Assistant response invoked successfully");
                
                // Also add the response to the UI directly
                if (mobileUI != null)
                {
                    mobileUI.AddMessage(response, "assistant");
                    mobileUI.ShowTypingIndicator(false);
                    LogMessage($"Assistant response added to UI: {response.Substring(0, Math.Min(50, response.Length))}...");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error invoking assistant response: {ex.Message}");
            }
            
            LogMessage($"Message processed in {responseTime:F2}s: {message}");
            
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Message processing failed: {ex.Message}");
            OnError?.Invoke($"Message processing failed: {ex.Message}");
            return false;
        }
        finally
        {
            isProcessingRequest = false;
            ProcessNextQueuedMessage();
        }
    }
    
    private string GenerateResponseFromRAGResults(List<RAGResult> ragResults, string message)
    {
        // Use the actual RAG API response
        if (ragResults != null && ragResults.Count > 0)
        {
            // Use the text from the first (highest scoring) result
            return ragResults[0].text;
        }
        
        return $"I understand you're asking about: {message}. However, I don't have enough context to provide a detailed response at the moment.";
    }
    
    private string GenerateResponse(string context, string message)
    {
        // Simplified response generation - kept for backward compatibility
        // In production, this would integrate with your AI service
        
        if (string.IsNullOrEmpty(context))
        {
            return $"I understand you're asking about: {message}. However, I don't have enough context to provide a detailed response at the moment.";
        }
        
        return $"Based on the available information, regarding '{message}': {context.Substring(0, Math.Min(200, context.Length))}...";
    }
    
    private async void ProcessNextQueuedMessage()
    {
        if (requestQueue.Count > 0 && !isProcessingRequest)
        {
            string nextMessage = requestQueue.Dequeue();
            await SendMessageAsync(nextMessage);
        }
    }
    
    // Event handlers
    private void HandleConnectionChanged(bool isConnected)
    {
        LogMessage($"Connection changed: {(isConnected ? "Online" : "Offline")}");
        OnOfflineModeChanged?.Invoke(!isConnected);
        
        if (isConnected && enableAutoSync)
        {
            _ = PerformInitialSync();
        }
    }
    
    private void HandleLowBatteryChanged(bool isLowBattery)
    {
        if (isLowBattery && enableBatteryOptimization)
        {
            ConfigureBatteryOptimization();
            LogMessage("Low battery detected - optimizing performance");
        }
    }
    
    private void HandleDataLimitReached()
    {
        LogMessage("Data limit reached - switching to offline mode");
        enableOfflineMode = true;
        OnOfflineModeChanged?.Invoke(true);
    }
    
    private void HandleQueryCompleted(MobileRAGQueryResponse response)
    {
        LogMessage($"RAG query completed: {response.results.Count} results");
    }
    
    private void HandleRAGConnectionChanged(bool isConnected)
    {
        LogMessage($"RAG connection changed: {(isConnected ? "Connected" : "Disconnected")}");
    }
    
    private void HandleRAGError(string error)
    {
        LogError($"RAG error: {error}");
        OnError?.Invoke($"RAG error: {error}");
    }
    
    private void HandleCacheHit(string cacheKey)
    {
        LogMessage($"Cache hit: {cacheKey}");
    }
    
    private void HandleOfflineQueueChanged(int queueSize)
    {
        LogMessage($"Offline queue size: {queueSize}");
    }
    
    private void HandleAuthenticationChanged(bool isAuthenticated)
    {
        LogMessage($"Authentication changed: {(isAuthenticated ? "Authenticated" : "Not authenticated")}");
    }
    
    private void HandleAuthError(string error)
    {
        LogError($"Auth error: {error}");
    }
    
    private void HandleTokenExpired()
    {
        LogMessage("Auth token expired - attempting refresh");
    }
    
    private void HandleCacheReady()
    {
        LogMessage("Conversation cache ready");
    }
    
    private void HandleCacheError(string error)
    {
        LogError($"Cache error: {error}");
    }
    
    private async void HandleUserMessageSubmitted(string message)
    {
        LogMessage($"HandleUserMessageSubmitted called with message: {message}");
        bool success = await SendMessageAsync(message);
        LogMessage($"SendMessageAsync completed with success: {success}");
    }
    
    private void HandleSettingsChanged(Dictionary<string, object> settings)
    {
        LogMessage("Mobile settings updated");
        
        // Update settings based on user preferences
        if (settings.ContainsKey("enableOfflineMode"))
        {
            enableOfflineMode = (bool)settings["enableOfflineMode"];
        }
        
        if (settings.ContainsKey("enableBatteryOptimization"))
        {
            enableBatteryOptimization = (bool)settings["enableBatteryOptimization"];
            ConfigureBatteryOptimization();
        }
    }
    
    private async void HandleAudioRecorded(byte[] audioData)
    {
        LogMessage("Audio recorded - processing with hybrid RAG...");
        
        try
        {
            if (!isSystemReady)
            {
                LogError("System not ready - cannot process voice input");
                return;
            }
            
            if (isProcessingRequest)
            {
                LogMessage("Already processing request - queuing voice input");
                // Queue the voice input for later processing
                requestQueue.Enqueue("[Voice Input]");
                return;
            }
            
            isProcessingRequest = true;
            OnUserMessage?.Invoke("[Voice Input]");
            
            // Process voice through hybrid RAG system
            var voiceResponse = await ragClient.ProcessVoiceQuery(audioData, userId);
            
            if (voiceResponse != null)
            {
                LogMessage($"Voice processing complete - Response: {voiceResponse.response_text}");
                LogMessage($"Source: {voiceResponse.source}, Sensitivity: {voiceResponse.sensitivity}");
                
                // Store conversation in cache
                if (conversationCache != null)
                {
                    await conversationCache.StoreConversationAsync(userId, voiceResponse.transcript, voiceResponse.response_text, null);
                }
                
                // Update conversation history in context assembler
                if (contextAssembler != null)
                {
                    contextAssembler.UpdateConversationHistory(userId, voiceResponse.transcript, voiceResponse.response_text);
                }
                
                // Invoke response events
                OnAssistantResponse?.Invoke(voiceResponse.response_text);
                
                // Add to UI
                if (mobileUI != null)
                {
                    mobileUI.AddMessage(voiceResponse.transcript, "user");
                    mobileUI.AddMessage(voiceResponse.response_text, "assistant");
                    mobileUI.ShowTypingIndicator(false);
                }
                
                // Play audio response if available
                if (!string.IsNullOrEmpty(voiceResponse.audio_response) && audioManager != null)
                {
                    byte[] audioResponseData = System.Convert.FromBase64String(voiceResponse.audio_response);
                    StartCoroutine(audioManager.PlayAudioFromBytes(audioResponseData));
                }
                
                totalInteractions++;
            }
            else
            {
                LogError("Voice processing returned null response");
                OnError?.Invoke("Voice processing failed");
            }
        }
        catch (Exception ex)
        {
            LogError($"Voice processing failed: {ex.Message}");
            OnError?.Invoke($"Voice processing failed: {ex.Message}");
        }
        finally
        {
            isProcessingRequest = false;
            // Process any queued messages
            ProcessNextQueuedMessage();
            // Reset audio recording state
            if (audioManager != null)
            {
                audioManager.ResetRecordingState();
            }
        }
    }
    
    private void HandleAudioPlaybackCompleted()
    {
        LogMessage("Audio playback completed");
    }
    
    public void SetUserId(string newUserId)
    {
        userId = newUserId;
        LogMessage($"User ID set to: {userId}");
    }
    
    public void SetCloudRAGUrl(string newUrl)
    {
        cloudRAGUrl = newUrl;
        if (ragClient != null)
        {
            ragClient.SetCloudApiUrl(newUrl);
        }
        LogMessage($"Cloud RAG URL set to: {newUrl}");
    }
    
    public string GetCloudRAGUrl()
    {
        return cloudRAGUrl;
    }
    
    public void ToggleOfflineMode(bool enabled)
    {
        enableOfflineMode = enabled;
        OnOfflineModeChanged?.Invoke(enabled);
        LogMessage($"Offline mode: {(enabled ? "Enabled" : "Disabled")}");
    }
    
    public void ClearCache()
    {
        if (conversationCache != null)
        {
            conversationCache.ClearCache();
        }
        
        if (ragClient != null)
        {
            ragClient.ClearCache();
        }
        
        LogMessage("Cache cleared");
    }
    
    public bool ExportConversations()
    {
        if (conversationCache == null)
            return false;
        
        try
        {
            var conversations = conversationCache.GetRecentConversationsAsync(userId, 1000);
            
            // Export to persistent storage or cloud
            string exportData = JsonUtility.ToJson(conversations);
            
            // Save to persistent data path
            string exportPath = System.IO.Path.Combine(Application.persistentDataPath, "conversations_export.json");
            System.IO.File.WriteAllText(exportPath, exportData);
            
            LogMessage($"Conversations exported to: {exportPath}");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Export failed: {ex.Message}");
            return false;
        }
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileRAGCompanionSystem] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileRAGCompanionSystem] {message}");
    }
    
    private void OnDestroy()
    {
        // Cleanup event handlers
        if (networkManager != null)
        {
            networkManager.OnConnectionChanged -= HandleConnectionChanged;
            networkManager.OnLowBatteryStatusChanged -= HandleLowBatteryChanged;
            networkManager.OnDataLimitReached -= HandleDataLimitReached;
        }
        
        if (ragClient != null)
        {
            ragClient.OnQueryCompleted -= HandleQueryCompleted;
            ragClient.OnConnectionStatusChanged -= HandleRAGConnectionChanged;
            ragClient.OnError -= HandleRAGError;
            ragClient.OnCacheHit -= HandleCacheHit;
            ragClient.OnOfflineQueueChanged -= HandleOfflineQueueChanged;
        }
        
        if (authManager != null)
        {
            authManager.OnAuthenticationChanged -= HandleAuthenticationChanged;
            authManager.OnAuthError -= HandleAuthError;
            authManager.OnTokenExpired -= HandleTokenExpired;
        }
        
        if (conversationCache != null)
        {
            conversationCache.OnCacheReady -= HandleCacheReady;
            conversationCache.OnCacheError -= HandleCacheError;
        }
        
        if (mobileUI != null)
        {
            mobileUI.OnUserMessageSubmitted -= HandleUserMessageSubmitted;
            mobileUI.OnSettingsChanged -= HandleSettingsChanged;
        }
        
        if (audioManager != null)
        {
            audioManager.OnAudioRecorded -= HandleAudioRecorded;
            audioManager.OnAudioPlaybackCompleted -= HandleAudioPlaybackCompleted;
        }
    }
    
    // Voice control methods
    public void StartVoiceRecording()
    {
        if (audioManager != null && !audioManager.IsRecording)
        {
            audioManager.StartRecording();
            LogMessage("Voice recording started");
        }
        else
        {
            LogMessage("Cannot start voice recording - audio manager not available or already recording");
        }
    }
    
    public void StopVoiceRecording()
    {
        if (audioManager != null && audioManager.IsRecording)
        {
            audioManager.StopRecording();
            LogMessage("Voice recording stopped");
        }
        else
        {
            LogMessage("Cannot stop voice recording - audio manager not available or not recording");
        }
    }
    
    public bool ProcessVoiceInputAsync(byte[] audioData)
    {
        if (!isSystemReady || audioData == null || audioData.Length == 0)
        {
            LogError("Cannot process voice input - system not ready or invalid audio data");
            return false;
        }
        
        try
        {
            HandleAudioRecorded(audioData);
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Voice input processing failed: {ex.Message}");
            return false;
        }
    }
    
    // Public getters for monitoring
    public bool IsSystemReady => isSystemReady;
    public bool IsProcessingRequest => isProcessingRequest;
    public int TotalInteractions => totalInteractions;
    public int OfflineInteractions => offlineInteractions;
    public float AverageResponseTime => averageResponseTime;
    public int QueuedMessages => requestQueue.Count;
    public bool IsOnline => networkManager?.IsConnected ?? false;
    public bool IsLowBattery => networkManager?.IsLowBattery ?? false;
    public float SystemUptime => Time.time - systemStartTime;
    public int CachedConversations => conversationCache?.TotalCachedConversations ?? 0;
    public float CacheHitRate => ragClient?.CacheSize > 0 ? (float)ragClient.CachedRequests / ragClient.TotalRequests : 0f;
    public bool IsVoiceRecording => audioManager?.IsRecording ?? false;
    public bool IsPlayingAudio => audioManager?.IsPlaying ?? false;
}