using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

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
        if (!isInitialized)
        {
            LogError("Cache not initialized");
            return false;
        }
        
        try
        {
            LogMessage($"Loading conversations from cloud for user: {userId}");
            
            // Get cloud RAG URL from companion system
            var companionSystem = FindObjectOfType<MobileRAGCompanionSystem>();
            if (companionSystem == null)
            {
                LogError("Companion system not found");
                return false;
            }
            
            string cloudUrl = companionSystem.GetCloudRAGUrl();
            string url = $"{cloudUrl}/conversations/{userId}?limit={limit}";
            
            using (var request = UnityWebRequest.Get(url))
            {
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonConvert.DeserializeObject<CloudConversationResponse>(request.downloadHandler.text);
                    
                    if (response != null && response.conversations != null)
                    {
                        LogMessage($"Loaded {response.conversations.Count} conversations from cloud");
                        
                        // Convert cloud conversations to local format and store
                        foreach (var cloudConv in response.conversations)
                        {
                            // Skip conversations with missing required fields
                            if (string.IsNullOrEmpty(cloudConv.id) || 
                                string.IsNullOrEmpty(cloudConv.user_message) || 
                                string.IsNullOrEmpty(cloudConv.assistant_message))
                            {
                                LogMessage($"Skipping conversation with missing fields: id={cloudConv.id}, user_msg={!string.IsNullOrEmpty(cloudConv.user_message)}, assistant_msg={!string.IsNullOrEmpty(cloudConv.assistant_message)}");
                                continue;
                            }
                            
                            // Parse timestamp safely
                            DateTime parsedTimestamp = DateTime.UtcNow; // Default to now if parsing fails
                            if (!string.IsNullOrEmpty(cloudConv.timestamp))
                            {
                                try
                                {
                                    parsedTimestamp = DateTime.Parse(cloudConv.timestamp);
                                }
                                catch (Exception parseEx)
                                {
                                    LogMessage($"Failed to parse timestamp '{cloudConv.timestamp}', using current time: {parseEx.Message}");
                                }
                            }
                            
                            var cachedConv = new CachedConversation
                            {
                                id = cloudConv.id,
                                userId = cloudConv.user_id ?? userId, // Fallback to current userId if null
                                userMessage = cloudConv.user_message,
                                assistantMessage = cloudConv.assistant_message,
                                timestamp = parsedTimestamp,
                                synced = true
                            };
                            
                            // Store in local cache
                            await StoreConversationAsync(cachedConv.userId, cachedConv.userMessage, cachedConv.assistantMessage, null);
                        }
                        
                        // Notify UI to populate chat with loaded conversations
                        if (response.conversations.Count > 0)
                        {
                            var companionUI = FindObjectOfType<MobileCompanionUI>();
                            if (companionUI != null)
                            {
                                companionUI.PopulateChatWithHistory(response.conversations);
                            }
                        }
                        
                        return true;
                    }
                }
                else
                {
                    LogError($"Failed to load conversations from cloud: {request.error}");
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Failed to load conversations from cloud: {ex.Message}");
            return false;
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