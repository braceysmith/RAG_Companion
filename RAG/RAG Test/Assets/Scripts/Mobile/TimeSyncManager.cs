using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[System.Serializable]
public class ServerTimeResponse
{
    public string status;
    public ServerTimeData server_time;
    public string message;
}

[System.Serializable]
public class ServerTimeData
{
    public string utc_iso;
    public double unix_timestamp;
    public string utc_datetime;
}

public class TimeSyncManager : MonoBehaviour
{
    [Header("Time Sync Settings")]
    [SerializeField] private string ragApiUrl = "https://ragcompanion-production-bf25.up.railway.app";
    [SerializeField] private bool enableTimeSync = true;
    [SerializeField] private float syncTimeoutSeconds = 10f;
    
    [Header("Debug Info")]
    [SerializeField] private double timeOffsetSeconds = 0.0;
    [SerializeField] private bool isSynced = false;
    [SerializeField] private string lastSyncTime = "";
    
    // Events
    public System.Action<double> OnTimeSyncComplete;
    public System.Action<string> OnTimeSyncFailed;
    
    private static TimeSyncManager instance;
    public static TimeSyncManager Instance 
    { 
        get 
        { 
            if (instance == null)
                instance = FindFirstObjectByType<TimeSyncManager>();
            return instance; 
        } 
    }
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        if (enableTimeSync)
        {
            StartCoroutine(SynchronizeTimeWithServer());
        }
    }
    
    public IEnumerator SynchronizeTimeWithServer()
    {
        if (string.IsNullOrEmpty(ragApiUrl))
        {
            Debug.LogWarning("TimeSync: RAG API URL not set");
            OnTimeSyncFailed?.Invoke("RAG API URL not configured");
            yield break;
        }
        
        Debug.Log("🕐 Starting time synchronization with server...");
        
        string url = $"{ragApiUrl}/time/sync";
        
        // Record client time before request
        DateTime clientTimeBeforeRequest = DateTime.UtcNow;
        double clientUnixTimestamp = ((DateTimeOffset)clientTimeBeforeRequest).ToUnixTimeSeconds();
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = (int)syncTimeoutSeconds;
            yield return request.SendWebRequest();
            
            // Record client time after request
            DateTime clientTimeAfterRequest = DateTime.UtcNow;
            double networkLatency = (clientTimeAfterRequest - clientTimeBeforeRequest).TotalSeconds;
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    ServerTimeResponse response = JsonConvert.DeserializeObject<ServerTimeResponse>(request.downloadHandler.text);
                    
                    if (response.status == "success")
                    {
                        // Calculate time offset accounting for network latency
                        double serverUnixTimestamp = response.server_time.unix_timestamp;
                        double estimatedServerTime = serverUnixTimestamp + (networkLatency / 2.0);
                        
                        // Current client time
                        double currentClientTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                        
                        // Calculate offset (server time - client time)
                        timeOffsetSeconds = estimatedServerTime - currentClientTime;
                        
                        isSynced = true;
                        lastSyncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        
                        Debug.Log($"✅ Time sync successful!");
                        Debug.Log($"   Server time: {response.server_time.utc_datetime}");
                        Debug.Log($"   Client time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                        Debug.Log($"   Network latency: {networkLatency:F3}s");
                        Debug.Log($"   Time offset: {timeOffsetSeconds:F3} seconds");
                        
                        if (Math.Abs(timeOffsetSeconds) > 1.0)
                        {
                            Debug.LogWarning($"⚠️  Significant time difference detected: {timeOffsetSeconds:F1} seconds");
                        }
                        
                        OnTimeSyncComplete?.Invoke(timeOffsetSeconds);
                    }
                    else
                    {
                        string errorMsg = $"Server returned error: {response.message}";
                        Debug.LogError($"❌ Time sync failed: {errorMsg}");
                        OnTimeSyncFailed?.Invoke(errorMsg);
                    }
                }
                catch (System.Exception e)
                {
                    string errorMsg = $"Failed to parse server response: {e.Message}";
                    Debug.LogError($"❌ Time sync parsing error: {errorMsg}");
                    OnTimeSyncFailed?.Invoke(errorMsg);
                }
            }
            else
            {
                string errorMsg = $"Network request failed: {request.error}";
                Debug.LogError($"❌ Time sync network error: {errorMsg}");
                OnTimeSyncFailed?.Invoke(errorMsg);
            }
        }
    }
    
    /// <summary>
    /// Get the current synchronized server time
    /// </summary>
    public DateTime GetSynchronizedServerTime()
    {
        if (!isSynced)
        {
            Debug.LogWarning("Time not synced with server, returning local time");
            return DateTime.UtcNow;
        }
        
        // Apply offset to current UTC time to get server time
        return DateTime.UtcNow.AddSeconds(timeOffsetSeconds);
    }
    
    /// <summary>
    /// Get synchronized Unix timestamp
    /// </summary>
    public double GetSynchronizedUnixTimestamp()
    {
        return ((DateTimeOffset)GetSynchronizedServerTime()).ToUnixTimeSeconds();
    }
    
    /// <summary>
    /// Check if time is synchronized
    /// </summary>
    public bool IsTimeSynchronized()
    {
        return isSynced;
    }
    
    /// <summary>
    /// Get the current time offset in seconds
    /// </summary>
    public double GetTimeOffsetSeconds()
    {
        return timeOffsetSeconds;
    }
    
    /// <summary>
    /// Force a re-sync with the server
    /// </summary>
    public void ForceResync()
    {
        if (enableTimeSync)
        {
            StartCoroutine(SynchronizeTimeWithServer());
        }
    }
    
    // Public methods for UI integration
    public string GetSyncStatus()
    {
        if (!isSynced)
            return "Not synchronized";
        
        string offsetStr = Math.Abs(timeOffsetSeconds) < 0.1 ? "Perfect sync" : $"{timeOffsetSeconds:+0.0}s offset";
        return $"Synced ({offsetStr})";
    }
    
    public string GetLastSyncTime()
    {
        return string.IsNullOrEmpty(lastSyncTime) ? "Never" : lastSyncTime;
    }
}