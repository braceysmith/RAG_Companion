using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

[System.Serializable]
public class ConversationSummary
{
    public string user_id;
    public string summary;
    public List<RecentExchange> last_exchanges;
    public string last_updated;
    public int exchange_count;
}

[System.Serializable]
public class RecentExchange
{
    public string user_message;
    public string ai_response;
    public string timestamp;
}

[System.Serializable]
public class CloudConversationResponse
{
    public string user_id;
    public List<CloudConversation> conversations;
    public int total_conversations;
}

[System.Serializable]
public class CloudConversation
{
    public string id;
    public string user_id;
    public string user_message;
    public string assistant_message;
    public string timestamp;
}

[System.Serializable]
public class CachedConversation
{
    public string id;
    public string userId;
    public string userMessage;
    public string assistantMessage;
    public DateTime timestamp;
    public string sessionId;
    public Dictionary<string, object> metadata;
    public bool synced;
}

[System.Serializable]
public class CachedMemory
{
    public string id;
    public string userId;
    public string memoryType;
    public string content;
    public DateTime timestamp;
    public Dictionary<string, object> metadata;
    public bool synced;
}

[System.Serializable]
public class CachedConversationList
{
    public List<CachedConversation> conversations = new List<CachedConversation>();
}

[System.Serializable]
public class CachedMemoryList
{
    public List<CachedMemory> memories = new List<CachedMemory>();
}

public class MobileConversationCache : MonoBehaviour
{
    [Header("Cache Configuration")]
    [SerializeField] private int maxCachedConversations = 100; // Reduced for PlayerPrefs
    [SerializeField] private int maxCachedMemories = 50; // Reduced for PlayerPrefs
    [SerializeField] private int cacheCleanupDays = 7; // Reduced for mobile
    
    [Header("Performance Settings")]
    [SerializeField] private bool enableAutoCleanup = true;
    [SerializeField] private float cleanupInterval = 1800f; // 30 minutes
    [SerializeField] private int batchSize = 10; // Reduced for mobile
    
    // Cache storage keys
    private readonly string CONVERSATIONS_KEY = "mobile_conversations";
    private readonly string MEMORIES_KEY = "mobile_memories";
    private readonly string CACHE_STATS_KEY = "mobile_cache_stats";
    
    // Cache data
    private CachedConversationList conversationCache;
    private CachedMemoryList memoryCache;
    private bool isInitialized = false;
    
    // Cache statistics
    private int totalCachedConversations = 0;
    private int totalCachedMemories = 0;
    private int cacheHits = 0;
    private int cacheMisses = 0;
    
    // Events
    public event Action OnCacheReady;
    public event Action<int> OnCacheCleanupCompleted;
    public event Action<string> OnCacheError;
    public event Action<List<CloudConversation>> OnConversationsLoaded;
    public event Action<ConversationSummary> OnSummaryLoaded;
    
    // Summary management
    private ConversationSummary currentSummary;
    private int exchangeCounter = 0;
    [SerializeField] private int summaryUpdateInterval = 5; // Update summary every 5 exchanges
    
    // Get the current conversation summary
    public ConversationSummary GetCurrentSummary()
    {
        return currentSummary;
    }
    
    // Add a new exchange to the summary
    public void AddExchange(string userMessage, string aiResponse)
    {
        if (currentSummary == null)
        {
            currentSummary = new ConversationSummary
            {
                user_id = "mobile-bracey02", // TODO: Get from system
                summary = "",
                last_exchanges = new List<RecentExchange>(),
                last_updated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                exchange_count = 0
            };
        }
        
        // Add to recent exchanges
        var exchange = new RecentExchange
        {
            user_message = userMessage,
            ai_response = aiResponse,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
        
        currentSummary.last_exchanges.Add(exchange);
        currentSummary.exchange_count++;
        
        // Keep only last 5 exchanges
        if (currentSummary.last_exchanges.Count > 5)
        {
            currentSummary.last_exchanges.RemoveAt(0);
        }
        
        // Update summary every N exchanges
        if (currentSummary.exchange_count % summaryUpdateInterval == 0)
        {
            UpdateSummary();
        }
        
        LogMessage($"📝 Added exchange {currentSummary.exchange_count}: User='{userMessage.Substring(0, Math.Min(30, userMessage.Length))}...', AI='{aiResponse.Substring(0, Math.Min(30, aiResponse.Length))}...'");
    }
    
    // Update the conversation summary
    private void UpdateSummary()
    {
        if (currentSummary == null || currentSummary.last_exchanges.Count == 0)
            return;
            
        // Create a simple bullet-point summary
        var summaryBuilder = new System.Text.StringBuilder();
        
        foreach (var exchange in currentSummary.last_exchanges)
        {
            summaryBuilder.AppendLine($"• User: {exchange.user_message}");
            summaryBuilder.AppendLine($"• AI: {exchange.ai_response}");
        }
        
        summaryBuilder.AppendLine($"• Key Topics: {ExtractKeyTopics()}");
        summaryBuilder.AppendLine($"• Total Exchanges: {currentSummary.exchange_count}");
        
        currentSummary.summary = summaryBuilder.ToString().Trim();
        currentSummary.last_updated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        
        LogMessage($"📝 Updated summary ({currentSummary.summary.Length} chars): {currentSummary.summary.Substring(0, Math.Min(100, currentSummary.summary.Length))}...");
        
        // Save to RAG server
        SaveSummaryToRAG();
    }
    
    // Extract key topics from recent exchanges
    private string ExtractKeyTopics()
    {
        var topics = new HashSet<string>();
        
        foreach (var exchange in currentSummary.last_exchanges)
        {
            // Simple topic extraction - could be enhanced with AI
            var combinedText = $"{exchange.user_message} {exchange.ai_response}".ToLower();
            
            if (combinedText.Contains("memory") || combinedText.Contains("remember"))
                topics.Add("Memory");
            if (combinedText.Contains("reminder") || combinedText.Contains("remind"))
                topics.Add("Reminders");
            if (combinedText.Contains("help") || combinedText.Contains("assist"))
                topics.Add("Help/Support");
            if (combinedText.Contains("hello") || combinedText.Contains("hi"))
                topics.Add("Greetings");
        }
        
        return topics.Count > 0 ? string.Join(", ", topics) : "General conversation";
    }
    
    // Save summary to RAG server
    private async void SaveSummaryToRAG()
    {
        if (currentSummary == null)
            return;
            
        try
        {
            var companionSystem = FindFirstObjectByType<MobileRAGCompanionSystem>();
            if (companionSystem == null)
            {
                LogError("❌ Companion system not found - cannot save summary to RAG");
                return;
            }
            
            string cloudUrl = companionSystem.GetCloudRAGUrl();
            string apiUrl = $"{cloudUrl}/conversations/{currentSummary.user_id}/summary";
            
            var summaryData = new
            {
                summary = currentSummary.summary,
                last_exchanges = currentSummary.last_exchanges,
                metadata = new
                {
                    last_updated = currentSummary.last_updated,
                    exchange_count = currentSummary.exchange_count
                }
            };
            
            string jsonPayload = JsonConvert.SerializeObject(summaryData);
            
            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    LogMessage($"✅ Summary saved to RAG server successfully");
                }
                else
                {
                    LogError($"❌ Failed to save summary to RAG: {request.error}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"❌ Exception saving summary to RAG: {ex.Message}");
        }
    }
    
    // Load summary from RAG server
    public async Task<bool> LoadSummaryFromRAG(string userId)
    {
        try
        {
            var companionSystem = FindFirstObjectByType<MobileRAGCompanionSystem>();
            if (companionSystem == null)
            {
                LogError("❌ Companion system not found - cannot load summary from RAG");
                return false;
            }
            
            string cloudUrl = companionSystem.GetCloudRAGUrl();
            string apiUrl = $"{cloudUrl}/conversations/{userId}/summary";
            
            using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ConversationSummary>(request.downloadHandler.text);
                        
                        if (response != null && !string.IsNullOrEmpty(response.summary))
                        {
                            currentSummary = response;
                            LogMessage($"✅ Loaded summary from RAG: {response.summary.Substring(0, Math.Min(100, response.summary.Length))}...");
                            
                            // Notify UI that summary is loaded
                            OnSummaryLoaded?.Invoke(currentSummary);
                            return true;
                        }
                        else
                        {
                            LogMessage("📝 No existing summary found - starting fresh");
                            return false;
                        }
                    }
                    catch (Exception parseEx)
                    {
                        LogError($"❌ Failed to parse summary response: {parseEx.Message}");
                        return false;
                    }
                }
                else
                {
                    LogMessage($"📝 No summary found on server: {request.error}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"❌ Exception loading summary from RAG: {ex.Message}");
            return false;
        }
        
        return false;
    }
    
    private void Start()
    {
        InitializeCache();
        
        if (enableAutoCleanup)
        {
            InvokeRepeating(nameof(PerformCleanup), cleanupInterval, cleanupInterval);
        }
    }
    
    private async void InitializeCache()
    {
        try
        {
            // Load existing cache data
            await LoadCacheData();
            
            // Load cache statistics
            LoadCacheStatistics();
            
            isInitialized = true;
            OnCacheReady?.Invoke();
            
            LogMessage("Mobile conversation cache initialized with PlayerPrefs");
        }
        catch (Exception ex)
        {
            LogError($"Cache initialization failed: {ex.Message}");
            OnCacheError?.Invoke($"Cache initialization failed: {ex.Message}");
        }
    }
    
    private Task LoadCacheData()
    {
        try
        {
            // Load conversations on main thread
            string conversationsJson = PlayerPrefs.GetString(CONVERSATIONS_KEY, "");
            if (!string.IsNullOrEmpty(conversationsJson))
            {
                conversationCache = JsonConvert.DeserializeObject<CachedConversationList>(conversationsJson);
            }
            
            if (conversationCache == null)
            {
                conversationCache = new CachedConversationList();
            }
            
            // Load memories on main thread
            string memoriesJson = PlayerPrefs.GetString(MEMORIES_KEY, "");
            if (!string.IsNullOrEmpty(memoriesJson))
            {
                memoryCache = JsonConvert.DeserializeObject<CachedMemoryList>(memoriesJson);
            }
            
            if (memoryCache == null)
            {
                memoryCache = new CachedMemoryList();
            }
            
            totalCachedConversations = conversationCache.conversations.Count;
            totalCachedMemories = memoryCache.memories.Count;
        }
        catch (Exception ex)
        {
            LogError($"Failed to load cache data: {ex.Message}");
            conversationCache = new CachedConversationList();
            memoryCache = new CachedMemoryList();
        }
        
        return Task.CompletedTask;
    }
    
    private void LoadCacheStatistics()
    {
        try
        {
            string statsJson = PlayerPrefs.GetString(CACHE_STATS_KEY, "");
            if (!string.IsNullOrEmpty(statsJson))
            {
                var stats = JsonConvert.DeserializeObject<Dictionary<string, int>>(statsJson);
                cacheHits = stats.GetValueOrDefault("hits", 0);
                cacheMisses = stats.GetValueOrDefault("misses", 0);
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load cache statistics: {ex.Message}");
        }
        
        LogMessage($"Cache statistics loaded: {totalCachedConversations} conversations, {totalCachedMemories} memories");
    }
    
    private Task SaveCacheData()
    {
        try
        {
            // Save conversations on main thread
            string conversationsJson = JsonConvert.SerializeObject(conversationCache);
            PlayerPrefs.SetString(CONVERSATIONS_KEY, conversationsJson);
            
            // Save memories on main thread
            string memoriesJson = JsonConvert.SerializeObject(memoryCache);
            PlayerPrefs.SetString(MEMORIES_KEY, memoriesJson);
            
            // Save statistics on main thread
            var stats = new Dictionary<string, int>
            {
                ["hits"] = cacheHits,
                ["misses"] = cacheMisses
            };
            string statsJson = JsonConvert.SerializeObject(stats);
            PlayerPrefs.SetString(CACHE_STATS_KEY, statsJson);
            
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            LogError($"Failed to save cache data: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
    
    public async Task<bool> StoreConversationAsync(string userId, string userMessage, string assistantMessage, string sessionId = null, Dictionary<string, object> metadata = null)
    {
        if (!isInitialized)
        {
            LogError("Cache not initialized");
            return false;
        }
        
        try
        {
            var conversation = new CachedConversation
            {
                id = Guid.NewGuid().ToString(),
                userId = userId,
                userMessage = userMessage,
                assistantMessage = assistantMessage,
                timestamp = DateTime.UtcNow,
                sessionId = sessionId,
                metadata = metadata ?? new Dictionary<string, object>(),
                synced = false
            };
            
            conversationCache.conversations.Add(conversation);
            totalCachedConversations++;
            
            // Cleanup if needed
            if (totalCachedConversations > maxCachedConversations)
            {
                _ = CleanupOldConversations();
            }
            
            // Save to PlayerPrefs
            await SaveCacheData();
            
            LogMessage($"Conversation stored for user: {userId}");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to store conversation: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> StoreMemoryAsync(string userId, string memoryType, string content, Dictionary<string, object> metadata = null)
    {
        if (!isInitialized)
        {
            LogError("Cache not initialized");
            return false;
        }
        
        try
        {
            var memory = new CachedMemory
            {
                id = Guid.NewGuid().ToString(),
                userId = userId,
                memoryType = memoryType,
                content = content,
                timestamp = DateTime.UtcNow,
                metadata = metadata ?? new Dictionary<string, object>(),
                synced = false
            };
            
            memoryCache.memories.Add(memory);
            totalCachedMemories++;
            
            // Cleanup if needed
            if (totalCachedMemories > maxCachedMemories)
            {
                await CleanupOldMemories();
            }
            
            // Save to PlayerPrefs
            await SaveCacheData();
            
            LogMessage($"Memory stored for user: {userId}, type: {memoryType}");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to store memory: {ex.Message}");
            return false;
        }
    }
    
    public List<CachedConversation> GetRecentConversationsAsync(string userId, int limit = 10)
    {
        if (!isInitialized)
        {
            LogError("Cache not initialized");
            return new List<CachedConversation>();
        }
        
        try
        {
            var userConversations = conversationCache.conversations
                .Where(c => c.userId == userId)
                .OrderByDescending(c => c.timestamp)
                .Take(limit)
                .ToList();
            
            cacheHits++;
            return userConversations;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get recent conversations: {ex.Message}");
            cacheMisses++;
            return new List<CachedConversation>();
        }
    }
    
    public async Task<bool> LoadConversationsFromCloudAsync(string userId, int limit = 20)
    {
        try
        {
            LogMessage($"🔄 Loading conversations from cloud for user: {userId}");
            
            // Get cloud RAG URL from companion system
            var companionSystem = FindObjectOfType<MobileRAGCompanionSystem>();
            if (companionSystem == null)
            {
                LogError("❌ Companion system not found");
                return false;
            }
            
            string cloudUrl = companionSystem.GetCloudRAGUrl();
            string apiUrl = $"{cloudUrl}/conversations/{userId}?limit={limit}";
            LogMessage($"📡 API URL: {apiUrl}");
            
            using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
            {
                // Add headers if needed
                request.SetRequestHeader("Content-Type", "application/json");
                
                // Send the request
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    LogMessage($"📥 Raw response received: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");
                    
                    try
                    {
                        // Parse the server response using the simple structure
                        var response = JsonConvert.DeserializeObject<CloudConversationResponse>(request.downloadHandler.text);
                        
                        if (response != null && response.conversations != null && response.conversations.Count > 0)
                        {
                            LogMessage($"✅ Successfully parsed {response.conversations.Count} conversations from cloud");
                            
                            // Debug: Log each conversation
                            for (int i = 0; i < response.conversations.Count; i++)
                            {
                                var conv = response.conversations[i];
                                LogMessage($"Conversation {i}: ID={conv.id}, User='{conv.user_message?.Substring(0, Math.Min(50, conv.user_message?.Length ?? 0))}...', AI='{conv.assistant_message?.Substring(0, Math.Min(50, conv.assistant_message?.Length ?? 0))}...'");
                            }
                            
                            // Filter out invalid conversations
                            var validConversations = response.conversations.Where(cloudConv =>
                                !string.IsNullOrEmpty(cloudConv.id) &&
                                !string.IsNullOrEmpty(cloudConv.user_message) &&
                                !string.IsNullOrEmpty(cloudConv.assistant_message)
                            ).ToList();
                            
                            LogMessage($"🔍 Filtered to {validConversations.Count} valid conversations");
                            
                            if (validConversations.Count > 0)
                            {
                                // Convert to local format and store
                                foreach (var cloudConv in validConversations)
                                {
                                    var localConv = new CachedConversation
                                    {
                                        id = cloudConv.id,
                                        userId = cloudConv.user_id,
                                        userMessage = cloudConv.user_message,
                                        assistantMessage = cloudConv.assistant_message,
                                        timestamp = DateTime.TryParse(cloudConv.timestamp, out DateTime parsedTime) ? parsedTime : DateTime.Now,
                                        sessionId = Guid.NewGuid().ToString(), // Generate new session ID for cloud conversations
                                        metadata = new Dictionary<string, object>(),
                                        synced = true
                                    };
                                    
                                    conversationCache.conversations.Add(localConv);
                                    LogMessage($"💾 Stored conversation: {localConv.userMessage.Substring(0, Math.Min(30, localConv.userMessage.Length))}... -> {localConv.assistantMessage.Substring(0, Math.Min(30, localConv.assistantMessage.Length))}...");
                                }
                                
                                // Save to local storage
                                await SaveCacheData();
                                
                                LogMessage($"✅ Successfully loaded and stored {validConversations.Count} conversations from cloud");
                                
                                // Notify UI to populate chat
                                if (OnConversationsLoaded != null)
                                {
                                    OnConversationsLoaded?.Invoke(validConversations);
                                }
                                
                                return true; // Return success
                            }
                            else
                            {
                                LogWarning("⚠️ No valid conversations found in cloud response");
                                return false; // Return no conversations
                            }
                        }
                        else
                        {
                            LogWarning("⚠️ No conversations found in cloud response");
                            return false; // Return no conversations
                        }
                    }
                    catch (System.Exception parseEx)
                    {
                        LogError($"❌ Failed to parse cloud response: {parseEx.Message}");
                        LogError($"📄 Response text: {responseText}");
                        return false; // Return parsing error
                    }
                }
                else
                {
                    LogError($"❌ Failed to load conversations from cloud: {request.error}");
                    return false; // Return network error
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"❌ Exception in LoadConversationsFromCloudAsync: {ex.Message}");
            return false; // Return exception error
        }
    }
    
    public List<CachedMemory> GetMemoriesAsync(string userId, string memoryType = null, int limit = 50)
    {
        if (!isInitialized)
        {
            LogError("Cache not initialized");
            return new List<CachedMemory>();
        }
        
        try
        {
            var userMemories = memoryCache.memories
                .Where(m => m.userId == userId)
                .Where(m => string.IsNullOrEmpty(memoryType) || m.memoryType == memoryType)
                .OrderByDescending(m => m.timestamp)
                .Take(limit)
                .ToList();
            
            cacheHits++;
            return userMemories;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get memories: {ex.Message}");
            cacheMisses++;
            return new List<CachedMemory>();
        }
    }
    
    public async Task<bool> MarkAsSyncedAsync(string id, bool isConversation = true)
    {
        if (!isInitialized)
        {
            return false;
        }
        
        try
        {
            if (isConversation)
            {
                var conversation = conversationCache.conversations.FirstOrDefault(c => c.id == id);
                if (conversation != null)
                {
                    conversation.synced = true;
                }
            }
            else
            {
                var memory = memoryCache.memories.FirstOrDefault(m => m.id == id);
                if (memory != null)
                {
                    memory.synced = true;
                }
            }
            
            await SaveCacheData();
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to mark as synced: {ex.Message}");
            return false;
        }
    }
    
    public List<CachedConversation> GetUnsyncedConversationsAsync(int limit = 100)
    {
        if (!isInitialized)
        {
            return new List<CachedConversation>();
        }
        
        try
        {
            var unsyncedConversations = conversationCache.conversations
                .Where(c => !c.synced)
                .OrderBy(c => c.timestamp)
                .Take(limit)
                .ToList();
            
            return unsyncedConversations;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get unsynced conversations: {ex.Message}");
            return new List<CachedConversation>();
        }
    }
    
    private async Task CleanupOldConversations()
    {
        try
        {
            DateTime cutoffDate = DateTime.UtcNow.AddDays(-cacheCleanupDays);
            
            var conversationsToRemove = conversationCache.conversations
                .Where(c => c.timestamp < cutoffDate && c.synced)
                .OrderBy(c => c.timestamp)
                .Take(batchSize)
                .ToList();
            
            foreach (var conversation in conversationsToRemove)
            {
                conversationCache.conversations.Remove(conversation);
                totalCachedConversations--;
            }
            
            if (conversationsToRemove.Count > 0)
            {
                await SaveCacheData();
                LogMessage($"Cleaned up {conversationsToRemove.Count} old conversations");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to cleanup old conversations: {ex.Message}");
        }
    }
    
    private async Task CleanupOldMemories()
    {
        try
        {
            DateTime cutoffDate = DateTime.UtcNow.AddDays(-cacheCleanupDays);
            
            var memoriesToRemove = memoryCache.memories
                .Where(m => m.timestamp < cutoffDate && m.synced)
                .OrderBy(m => m.timestamp)
                .Take(batchSize)
                .ToList();
            
            foreach (var memory in memoriesToRemove)
            {
                memoryCache.memories.Remove(memory);
                totalCachedMemories--;
            }
            
            if (memoriesToRemove.Count > 0)
            {
                await SaveCacheData();
                LogMessage($"Cleaned up {memoriesToRemove.Count} old memories");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to cleanup old memories: {ex.Message}");
        }
    }
    
    private async void PerformCleanup()
    {
        try
        {
            await CleanupOldConversations();
            await CleanupOldMemories();
            
            OnCacheCleanupCompleted?.Invoke(totalCachedConversations + totalCachedMemories);
        }
        catch (Exception ex)
        {
            LogError($"Cache cleanup failed: {ex.Message}");
        }
    }
    
    public void ClearCache()
    {
        if (!isInitialized)
        {
            return;
        }
        
        try
        {
            conversationCache = new CachedConversationList();
            memoryCache = new CachedMemoryList();
            
            totalCachedConversations = 0;
            totalCachedMemories = 0;
            cacheHits = 0;
            cacheMisses = 0;
            
            PlayerPrefs.DeleteKey(CONVERSATIONS_KEY);
            PlayerPrefs.DeleteKey(MEMORIES_KEY);
            PlayerPrefs.DeleteKey(CACHE_STATS_KEY);
            PlayerPrefs.Save();
            
            LogMessage("Cache cleared");
        }
        catch (Exception ex)
        {
            LogError($"Failed to clear cache: {ex.Message}");
        }
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileConversationCache] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileConversationCache] {message}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MobileConversationCache] {message}");
    }
    
    private void OnDestroy()
    {
        CancelInvoke();
        
        if (isInitialized)
        {
            _ = SaveCacheData();
        }
    }
    
    // Public getters for monitoring
    public bool IsInitialized => isInitialized;
    public int TotalCachedConversations => totalCachedConversations;
    public int TotalCachedMemories => totalCachedMemories;
    public int CacheHits => cacheHits;
    public int CacheMisses => cacheMisses;
    public float CacheHitRate => (cacheHits + cacheMisses) > 0 ? (float)cacheHits / (cacheHits + cacheMisses) : 0f;
}