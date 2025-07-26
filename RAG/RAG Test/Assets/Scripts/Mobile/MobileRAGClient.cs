using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[System.Serializable]
public class MobileRAGQueryRequest
{
    public string query;
    public string user_id;
    public int top_k = 5;
    public Dictionary<string, object> filters = new Dictionary<string, object>();
}

[System.Serializable]
public class MobileRAGQueryResponse
{
    public List<RAGResult> results;
    public float? query_embedding_ms;
    public int? total_chunks;
    public string session_id;
    public bool from_cache;
}

[System.Serializable]
public class MobileMemoryRequest
{
    public string user_id;
    public string memory_type;
    public string content;
    public Dictionary<string, object> metadata = new Dictionary<string, object>();
}

[System.Serializable]
public class MobileVoiceResponse
{
    public string transcript;
    public string response_text;
    public string audio_response;
    public string source;           // "local" or "cloud"
    public string sensitivity;      // "personal", "private", "general"
    public object tool_results;
    public object personal_info_extracted;
    public string status;
}

public class MobileRAGClient : MonoBehaviour
{
    [Header("Cloud Configuration")]
    [SerializeField] private string cloudApiUrl = "https://your-rag-api.com";
    [SerializeField] private string apiKey = "";
    [SerializeField] private int requestTimeoutSeconds = 30;
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryDelaySeconds = 1.0f;
    
    [Header("Mobile Optimization")]
    [SerializeField] private bool enableLocalCache = true;
    [SerializeField] private bool enableOfflineMode = true;
    [SerializeField] private int maxCacheSize = 100;
    // [SerializeField] private float cacheExpiryHours = 24f; // Commented out - unused
    
    [Header("Network Monitoring")]
    [SerializeField] private bool enableNetworkMonitoring = true;
    [SerializeField] private float networkCheckInterval = 5f;
    
    // Mobile-specific components
    private MobileNetworkManager networkManager;
    private MobileConversationCache conversationCache;
    private MobileAuthManager authManager;
    
    // State management
    private bool isOnline = true;
    private Queue<MobileRAGQueryRequest> offlineQueue = new Queue<MobileRAGQueryRequest>();
    private Dictionary<string, MobileRAGQueryResponse> queryCache = new Dictionary<string, MobileRAGQueryResponse>();
    
    // Performance tracking
    private float lastRequestTime = 0f;
    private int totalRequests = 0;
    private int successfulRequests = 0;
    private int cachedRequests = 0;
    
    // Events
    public event Action<MobileRAGQueryResponse> OnQueryCompleted;
    public event Action<bool> OnConnectionStatusChanged;
    public event Action<string> OnError;
    public event Action<string> OnCacheHit;
    public event Action<int> OnOfflineQueueChanged;
    
    private void Start()
    {
        InitializeMobileComponents();
        StartNetworkMonitoring();
    }
    
    private void InitializeMobileComponents()
    {
        // Initialize mobile-specific managers
        networkManager = GetComponent<MobileNetworkManager>() ?? gameObject.AddComponent<MobileNetworkManager>();
        conversationCache = GetComponent<MobileConversationCache>() ?? gameObject.AddComponent<MobileConversationCache>();
        authManager = GetComponent<MobileAuthManager>() ?? gameObject.AddComponent<MobileAuthManager>();
        
        // Setup event handlers
        if (networkManager != null)
        {
            networkManager.OnConnectionChanged += HandleConnectionChanged;
        }
        
        if (conversationCache != null)
        {
            conversationCache.OnCacheReady += HandleCacheReady;
        }
        
        LogMessage("Mobile RAG Client initialized");
    }
    
    private void StartNetworkMonitoring()
    {
        if (enableNetworkMonitoring)
        {
            InvokeRepeating(nameof(CheckNetworkStatus), 0f, networkCheckInterval);
        }
    }
    
    private void CheckNetworkStatus()
    {
        bool previousStatus = isOnline;
        isOnline = Application.internetReachability != NetworkReachability.NotReachable;
        
        if (previousStatus != isOnline)
        {
            OnConnectionStatusChanged?.Invoke(isOnline);
            LogMessage($"Network status changed: {(isOnline ? "Online" : "Offline")}");
            
            if (isOnline && offlineQueue.Count > 0)
            {
                ProcessOfflineQueue();
            }
        }
    }
    
    public async Task<List<RAGResult>> QueryAsync(string query, string userId, int topK = 5)
    {
        totalRequests++;
        
        try
        {
            // Check cache first
            if (enableLocalCache)
            {
                string cacheKey = GenerateCacheKey(query, userId, topK);
                if (queryCache.ContainsKey(cacheKey))
                {
                    var cachedResponse = queryCache[cacheKey];
                    if (IsCacheValid(cachedResponse))
                    {
                        cachedRequests++;
                        OnCacheHit?.Invoke(cacheKey);
                        OnQueryCompleted?.Invoke(cachedResponse);
                        return cachedResponse.results;
                    }
                    else
                    {
                        queryCache.Remove(cacheKey);
                    }
                }
            }
            
            // Create request
            var request = new MobileRAGQueryRequest
            {
                query = query,
                user_id = userId,
                top_k = topK,
                filters = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["platform"] = Application.platform.ToString(),
                    ["app_version"] = Application.version
                }
            };
            
            // Handle offline mode
            if (!isOnline && enableOfflineMode)
            {
                return HandleOfflineQuery(request);
            }
            
            // Make online request
            var response = await MakeRAGRequest(request);
            
            // Cache successful response
            if (enableLocalCache && response != null)
            {
                CacheResponse(request, response);
            }
            
            successfulRequests++;
            OnQueryCompleted?.Invoke(response);
            return response?.results ?? new List<RAGResult>();
        }
        catch (Exception ex)
        {
            LogError($"Query failed: {ex.Message}");
            OnError?.Invoke($"Query failed: {ex.Message}");
            
            // Try to return cached or offline result
            if (enableOfflineMode)
            {
                return HandleOfflineQuery(new MobileRAGQueryRequest
                {
                    query = query,
                    user_id = userId,
                    top_k = topK
                });
            }
            
            return new List<RAGResult>();
        }
    }
    
    public async Task<MobileVoiceResponse> ProcessVoiceQuery(byte[] audioData, string userId, string audioFormat = "wav")
    {
        string endpoint = $"{cloudApiUrl}/mobile/voice";
        
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", audioData, $"voice.{audioFormat}", $"audio/{audioFormat}");
        form.AddField("user_id", userId);
        form.AddField("audio_format", audioFormat);
        
        using (UnityWebRequest request = UnityWebRequest.Post(endpoint, form))
        {
            request.timeout = requestTimeoutSeconds;
            
            // Add auth token if available
            string authToken = authManager.GetAuthTokenAsync();
            if (!string.IsNullOrEmpty(authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            }
            
            await request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                totalRequests++;
                successfulRequests++;
                return JsonConvert.DeserializeObject<MobileVoiceResponse>(jsonResponse);
            }
            else
            {
                LogError($"Voice request failed: {request.error}");
                throw new Exception($"Voice request failed: {request.error}");
            }
        }
    }
    
    private async Task<MobileRAGQueryResponse> MakeRAGRequest(MobileRAGQueryRequest request)
    {
        string jsonPayload = JsonConvert.SerializeObject(request);
        
        for (int attempt = 0; attempt < maxRetryAttempts; attempt++)
        {
            try
            {
                using (UnityWebRequest webRequest = new UnityWebRequest($"{cloudApiUrl}/query", "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.timeout = requestTimeoutSeconds;
                    
                    // Set headers
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    
                    // Add auth token if available
                    string authToken = authManager.GetAuthTokenAsync();
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        webRequest.SetRequestHeader("X-Auth-Token", authToken);
                    }
                    
                    lastRequestTime = Time.time;
                    await webRequest.SendWebRequest();
                    
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = webRequest.downloadHandler.text;
                        LogMessage($"RAG API Response: {responseText}");
                        
                        var response = JsonConvert.DeserializeObject<MobileRAGQueryResponse>(responseText);
                        
                        LogMessage($"RAG query successful (attempt {attempt + 1}): {response.results.Count} results");
                        if (response.results.Count > 0)
                        {
                            LogMessage($"First result text: {response.results[0].text}");
                        }
                        return response;
                    }
                    else
                    {
                        LogError($"RAG request failed (attempt {attempt + 1}): {webRequest.error}");
                        
                        if (attempt < maxRetryAttempts - 1)
                        {
                            await Task.Delay(Mathf.RoundToInt(retryDelaySeconds * 1000 * (attempt + 1)));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"RAG request exception (attempt {attempt + 1}): {ex.Message}");
                
                if (attempt < maxRetryAttempts - 1)
                {
                    await Task.Delay(Mathf.RoundToInt(retryDelaySeconds * 1000 * (attempt + 1)));
                }
            }
        }
        
        throw new Exception($"RAG request failed after {maxRetryAttempts} attempts");
    }
    
    private List<RAGResult> HandleOfflineQuery(MobileRAGQueryRequest request)
    {
        LogMessage($"Handling offline query: {request.query}");
        
        // Add to offline queue for when connection returns
        if (offlineQueue.Count < maxCacheSize)
        {
            offlineQueue.Enqueue(request);
            OnOfflineQueueChanged?.Invoke(offlineQueue.Count);
        }
        
        // Try to get cached conversations for context
        if (conversationCache != null)
        {
            var cachedConversations = conversationCache.GetRecentConversationsAsync(request.user_id, 5);
            
            // Generate basic response from cached data
            var offlineResults = new List<RAGResult>();
            
            foreach (var conversation in cachedConversations)
            {
                if (conversation.assistantMessage.ToLower().Contains(request.query.ToLower()) ||
                    conversation.userMessage.ToLower().Contains(request.query.ToLower()))
                {
                    offlineResults.Add(new RAGResult
                    {
                        chunk_id = $"cached_{conversation.id}",
                        text = conversation.assistantMessage,
                        doc_title = "Cached Conversation",
                        section = "Previous Response",
                        score = 0.8f
                    });
                }
            }
            
            LogMessage($"Offline query returned {offlineResults.Count} cached results");
            return offlineResults;
        }
        
        return new List<RAGResult>();
    }
    
    private async void ProcessOfflineQueue()
    {
        LogMessage($"Processing offline queue: {offlineQueue.Count} items");
        
        var processedItems = new List<MobileRAGQueryRequest>();
        
        while (offlineQueue.Count > 0 && isOnline)
        {
            try
            {
                var request = offlineQueue.Dequeue();
                var response = await MakeRAGRequest(request);
                
                if (response != null)
                {
                    processedItems.Add(request);
                    OnQueryCompleted?.Invoke(response);
                    
                    // Cache the response
                    if (enableLocalCache)
                    {
                        CacheResponse(request, response);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to process offline item: {ex.Message}");
                break;
            }
        }
        
        OnOfflineQueueChanged?.Invoke(offlineQueue.Count);
        LogMessage($"Processed {processedItems.Count} offline items");
    }
    
    public async Task<bool> StoreMemoryAsync(string userId, string memoryType, string content, Dictionary<string, object> metadata = null)
    {
        try
        {
            if (!isOnline && enableOfflineMode)
            {
                // Store in local cache for later sync
                if (conversationCache != null)
                {
                    await conversationCache.StoreMemoryAsync(userId, memoryType, content, metadata);
                    return true;
                }
            }
            
            var request = new MobileMemoryRequest
            {
                user_id = userId,
                memory_type = memoryType,
                content = content,
                metadata = metadata ?? new Dictionary<string, object>()
            };
            
            string jsonPayload = JsonConvert.SerializeObject(request);
            
            using (UnityWebRequest webRequest = new UnityWebRequest($"{cloudApiUrl}/memory/store", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = requestTimeoutSeconds;
                
                webRequest.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                
                string authToken = authManager.GetAuthTokenAsync();
                if (!string.IsNullOrEmpty(authToken))
                {
                    webRequest.SetRequestHeader("X-Auth-Token", authToken);
                }
                
                await webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    LogMessage($"Memory stored successfully: {memoryType}");
                    return true;
                }
                else
                {
                    string responseText = webRequest.downloadHandler?.text ?? "No response body";
                    LogError($"Memory storage failed: {webRequest.error}");
                    LogError($"Server response: {responseText}");
                    LogError($"Request URL: {webRequest.url}");
                    LogError($"Request payload: {jsonPayload}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Memory storage exception: {ex.Message}");
            return false;
        }
    }
    
    private void CacheResponse(MobileRAGQueryRequest request, MobileRAGQueryResponse response)
    {
        string cacheKey = GenerateCacheKey(request.query, request.user_id, request.top_k);
        
        // Add cache metadata
        response.from_cache = false;
        
        queryCache[cacheKey] = response;
        
        // Cleanup old cache entries
        if (queryCache.Count > maxCacheSize)
        {
            CleanupCache();
        }
    }
    
    private string GenerateCacheKey(string query, string userId, int topK)
    {
        return $"{userId}_{query.GetHashCode()}_{topK}";
    }
    
    private bool IsCacheValid(MobileRAGQueryResponse cachedResponse)
    {
        // Simple time-based cache validation
        return true; // Simplified for now
    }
    
    private void CleanupCache()
    {
        // Remove oldest entries (simplified implementation)
        if (queryCache.Count > maxCacheSize)
        {
            var keysToRemove = new List<string>();
            int itemsToRemove = queryCache.Count - maxCacheSize;
            
            foreach (var key in queryCache.Keys)
            {
                if (itemsToRemove <= 0) break;
                keysToRemove.Add(key);
                itemsToRemove--;
            }
            
            foreach (var key in keysToRemove)
            {
                queryCache.Remove(key);
            }
        }
    }
    
    private void HandleConnectionChanged(bool isConnected)
    {
        isOnline = isConnected;
        LogMessage($"Connection changed: {(isConnected ? "Connected" : "Disconnected")}");
    }
    
    private void HandleCacheReady()
    {
        LogMessage("Local cache ready");
    }
    
    public void SetCloudApiUrl(string url)
    {
        cloudApiUrl = url;
        LogMessage($"Cloud API URL set to: {url}");
    }
    
    public void SetApiKey(string key)
    {
        apiKey = key;
        LogMessage("API key updated");
    }
    
    public void ClearCache()
    {
        queryCache.Clear();
        if (conversationCache != null)
        {
            conversationCache.ClearCache();
        }
        LogMessage("Cache cleared");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileRAGClient] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileRAGClient] {message}");
    }
    
    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnConnectionChanged -= HandleConnectionChanged;
        }
        
        if (conversationCache != null)
        {
            conversationCache.OnCacheReady -= HandleCacheReady;
        }
        
        CancelInvoke();
    }
    
    // Public getters for monitoring
    public bool IsOnline => isOnline;
    public int TotalRequests => totalRequests;
    public int SuccessfulRequests => successfulRequests;
    public int CachedRequests => cachedRequests;
    public int OfflineQueueCount => offlineQueue.Count;
    public int CacheSize => queryCache.Count;
    public float LastRequestTime => lastRequestTime;
    public float SuccessRate => totalRequests > 0 ? (float)successfulRequests / totalRequests : 0f;
}