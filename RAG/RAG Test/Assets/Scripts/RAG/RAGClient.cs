using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[System.Serializable]
public class RAGResult
{
    public string chunk_id;
    public string text;
    public string doc_title;
    public string section;
    public string[] tags;
    public string source_path;
    public float score;
}

[System.Serializable]
public class RAGFilters
{
    public string[] user_scope = { "global" };
    public string[] safety_level = { "public" };
}

[System.Serializable]
public class RAGQueryRequest
{
    public string query;
    public string user_id;
    public int top_k = 5;
    public RAGFilters filters = new RAGFilters();
}

[System.Serializable]
public class RAGQueryResponse
{
    public List<RAGResult> results;
    public float? query_embedding_ms;
    public float? db_lookup_ms;
}

[System.Serializable]
public class MemoryRequest
{
    public string user_id;
    public string memory_type;
    public string content;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class MemoryQueryRequest
{
    public string user_id;
    public string query;
    public string[] memory_types;
    public int top_k = 3;
}

[System.Serializable]
public class MemoryResult
{
    public string memory_id;
    public string memory_type;
    public string content;
    public Dictionary<string, object> metadata;
    public float score;
}

[System.Serializable]
public class MemoryQueryResponse
{
    public List<MemoryResult> results;
}

[System.Serializable]
public class ConversationTurnRequest
{
    public string turn_id;
    public string session_id;
    public string user_id;
    public int turn_index;
    public string user_message;
    public string assistant_response;
    public string[] retrieved_chunks;
    public Dictionary<string, object> metadata;
}

[System.Serializable]
public class ServiceResponse
{
    public string status;
    public string message;
}

public class RAGClient : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string baseUrl = "http://localhost:8077";
    [SerializeField] private int timeoutSeconds = 30;
    [SerializeField] private bool enableLogging = true;
    
    // Cache for recent queries
    private Dictionary<string, RAGQueryResponse> queryCache = new Dictionary<string, RAGQueryResponse>();
    private const int MAX_CACHE_SIZE = 100;
    private Queue<string> cacheKeys = new Queue<string>();
    
    // Performance tracking
    private float lastQueryTime = 0f;
    private int totalQueries = 0;
    
    public event Action<string> OnError;
    public event Action<RAGQueryResponse> OnQueryCompleted;
    public event Action<MemoryQueryResponse> OnMemoryQueryCompleted;
    
    private void Start()
    {
        baseUrl = baseUrl.TrimEnd('/');
        LogMessage($"RAG Client initialized with base URL: {baseUrl}");
    }
    
    public async Task<List<RAGResult>> QueryAsync(string query, string userId, int topK = 5, 
                                                 string[] userScopes = null, string[] safetyLevels = null)
    {
        // Check cache first
        string cacheKey = $"{query}_{userId}_{topK}";
        if (queryCache.ContainsKey(cacheKey))
        {
            LogMessage($"Cache hit for query: {query}");
            return queryCache[cacheKey].results;
        }
        
        try
        {
            var request = new RAGQueryRequest
            {
                query = query,
                user_id = userId,
                top_k = topK,
                filters = new RAGFilters
                {
                    user_scope = userScopes ?? new[] { "global", userId },
                    safety_level = safetyLevels ?? new[] { "public" }
                }
            };
            
            float startTime = Time.time;
            var response = await PostRequest<RAGQueryResponse>("/query", request);
            lastQueryTime = Time.time - startTime;
            totalQueries++;
            
            if (response != null && response.results != null)
            {
                // Cache the response
                CacheResponse(cacheKey, response);
                
                LogMessage($"Query completed: {response.results.Count} results in {lastQueryTime:F2}s");
                OnQueryCompleted?.Invoke(response);
                
                return response.results;
            }
            else
            {
                LogError("Empty response from RAG service");
                return new List<RAGResult>();
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Query error: {ex.Message}");
            OnError?.Invoke($"Query failed: {ex.Message}");
            return new List<RAGResult>();
        }
    }
    
    public async Task<bool> StoreMemoryAsync(string userId, string memoryType, string content, 
                                           Dictionary<string, object> metadata = null)
    {
        try
        {
            var request = new MemoryRequest
            {
                user_id = userId,
                memory_type = memoryType,
                content = content,
                metadata = metadata ?? new Dictionary<string, object>()
            };
            
            var response = await PostRequest<ServiceResponse>("/memory/store", request);
            
            if (response != null && response.status == "success")
            {
                LogMessage($"Memory stored: {memoryType} for user {userId}");
                return true;
            }
            else
            {
                LogError($"Memory storage failed: {response?.message ?? "Unknown error"}");
                return false;
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Memory storage error: {ex.Message}");
            OnError?.Invoke($"Memory storage failed: {ex.Message}");
            return false;
        }
    }
    
    public async Task<List<MemoryResult>> QueryMemoryAsync(string userId, string query, 
                                                          string[] memoryTypes = null, int topK = 3)
    {
        try
        {
            var request = new MemoryQueryRequest
            {
                user_id = userId,
                query = query,
                memory_types = memoryTypes,
                top_k = topK
            };
            
            var response = await PostRequest<MemoryQueryResponse>("/memory/query", request);
            
            if (response != null && response.results != null)
            {
                LogMessage($"Memory query completed: {response.results.Count} results");
                OnMemoryQueryCompleted?.Invoke(response);
                return response.results;
            }
            else
            {
                LogError("Empty memory query response");
                return new List<MemoryResult>();
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Memory query error: {ex.Message}");
            OnError?.Invoke($"Memory query failed: {ex.Message}");
            return new List<MemoryResult>();
        }
    }
    
    public async Task<bool> LogConversationAsync(string turnId, string sessionId, string userId, 
                                               int turnIndex, string userMessage, string assistantResponse, 
                                               string[] retrievedChunks, Dictionary<string, object> metadata = null)
    {
        try
        {
            var request = new ConversationTurnRequest
            {
                turn_id = turnId,
                session_id = sessionId,
                user_id = userId,
                turn_index = turnIndex,
                user_message = userMessage,
                assistant_response = assistantResponse,
                retrieved_chunks = retrievedChunks ?? new string[0],
                metadata = metadata ?? new Dictionary<string, object>()
            };
            
            var response = await PostRequest<ServiceResponse>("/conversation/log", request);
            
            if (response != null && response.status == "success")
            {
                LogMessage($"Conversation logged: turn {turnIndex}");
                return true;
            }
            else
            {
                LogError($"Conversation logging failed: {response?.message ?? "Unknown error"}");
                return false;
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Conversation logging error: {ex.Message}");
            OnError?.Invoke($"Conversation logging failed: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> IngestDocumentsAsync(string directoryPath, string userScope = "global", 
                                               string safetyLevel = "public")
    {
        try
        {
            var request = new
            {
                directory_path = directoryPath,
                user_scope = userScope,
                safety_level = safetyLevel
            };
            
            var response = await PostRequest<ServiceResponse>("/ingest", request);
            
            if (response != null && response.status == "started")
            {
                LogMessage($"Document ingestion started for: {directoryPath}");
                return true;
            }
            else
            {
                LogError($"Document ingestion failed: {response?.message ?? "Unknown error"}");
                return false;
            }
            
        }
        catch (Exception ex)
        {
            LogError($"Document ingestion error: {ex.Message}");
            OnError?.Invoke($"Document ingestion failed: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await GetRequest<Dictionary<string, object>>("/health");
            
            if (response != null && response.ContainsKey("status"))
            {
                string status = response["status"].ToString();
                return status == "healthy";
            }
            
            return false;
            
        }
        catch (Exception ex)
        {
            LogError($"Health check error: {ex.Message}");
            return false;
        }
    }
    
    private async Task<T> PostRequest<T>(string endpoint, object data) where T : class
    {
        string url = baseUrl + endpoint;
        string json = JsonConvert.SerializeObject(data);
        
        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;
            
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            else
            {
                LogError($"HTTP error: {request.error} - {request.downloadHandler.text}");
                return null;
            }
        }
    }
    
    private async Task<T> GetRequest<T>(string endpoint) where T : class
    {
        string url = baseUrl + endpoint;
        
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = timeoutSeconds;
            
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<T>(responseText);
            }
            else
            {
                LogError($"HTTP error: {request.error} - {request.downloadHandler.text}");
                return null;
            }
        }
    }
    
    private void CacheResponse(string key, RAGQueryResponse response)
    {
        // Remove oldest entries if cache is full
        while (queryCache.Count >= MAX_CACHE_SIZE)
        {
            string oldestKey = cacheKeys.Dequeue();
            queryCache.Remove(oldestKey);
        }
        
        queryCache[key] = response;
        cacheKeys.Enqueue(key);
    }
    
    public void ClearCache()
    {
        queryCache.Clear();
        cacheKeys.Clear();
        LogMessage("Query cache cleared");
    }
    
    public void SetBaseUrl(string newBaseUrl)
    {
        baseUrl = newBaseUrl.TrimEnd('/');
        LogMessage($"Base URL updated to: {baseUrl}");
    }
    
    private void LogMessage(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[RAGClient] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[RAGClient] {message}");
    }
    
    // Public getters for monitoring
    public float LastQueryTime => lastQueryTime;
    public int TotalQueries => totalQueries;
    public int CacheSize => queryCache.Count;
}