using UnityEngine;

/// <summary>
/// Utility script to ensure TimeSyncManager and ReminderManager are properly set up in the scene
/// This script can be attached to any GameObject or run manually to configure the managers
/// </summary>
public class ManagerSetup : MonoBehaviour
{
    [Header("Manager Configuration")]
    [SerializeField] private string ragApiUrl = "https://your-rag-api.up.railway.app";
    [SerializeField] private string userId = "mobile-user";
    [SerializeField] private bool autoSetupOnStart = true;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugLogs = true;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupManagers();
        }
    }
    
    /// <summary>
    /// Call this method to set up both TimeSyncManager and ReminderManager
    /// Can be called from UI button for manual setup
    /// </summary>
    public void SetupManagers()
    {
        SetupTimeSyncManager();
        SetupReminderManager();
    }
    
    /// <summary>
    /// Ensures TimeSyncManager exists and is properly configured
    /// </summary>
    public void SetupTimeSyncManager()
    {
        TimeSyncManager timeSyncManager = FindFirstObjectByType<TimeSyncManager>();
        
        if (timeSyncManager == null)
        {
            // Create TimeSyncManager GameObject
            GameObject timeSyncObj = new GameObject("TimeSyncManager");
            timeSyncManager = timeSyncObj.AddComponent<TimeSyncManager>();
            
            // Make it persistent across scenes
            DontDestroyOnLoad(timeSyncObj);
            
            if (showDebugLogs)
            {
                Debug.Log("✅ Created TimeSyncManager GameObject");
            }
        }
        
        // Configure TimeSyncManager using reflection to access private fields
        var timeSyncType = typeof(TimeSyncManager);
        var ragApiUrlField = timeSyncType.GetField("ragApiUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (ragApiUrlField != null)
        {
            ragApiUrlField.SetValue(timeSyncManager, ragApiUrl);
            if (showDebugLogs)
            {
                Debug.Log($"✅ Configured TimeSyncManager with RAG API URL: {ragApiUrl}");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ Could not find ragApiUrl field in TimeSyncManager");
        }
    }
    
    /// <summary>
    /// Ensures ReminderManager exists and is properly configured
    /// Note: MobileRealtimeChat also creates this automatically, but this ensures it's available immediately
    /// </summary>
    public void SetupReminderManager()
    {
        ReminderManager reminderManager = FindFirstObjectByType<ReminderManager>();
        
        if (reminderManager == null)
        {
            // Create ReminderManager GameObject
            GameObject reminderObj = new GameObject("ReminderManager");
            reminderManager = reminderObj.AddComponent<ReminderManager>();
            
            if (showDebugLogs)
            {
                Debug.Log("✅ Created ReminderManager GameObject");
            }
        }
        
        // Configure ReminderManager using reflection to access private fields
        var reminderType = typeof(ReminderManager);
        var ragApiUrlField = reminderType.GetField("ragApiUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var userIdField = reminderType.GetField("userId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (ragApiUrlField != null)
        {
            ragApiUrlField.SetValue(reminderManager, ragApiUrl);
            if (showDebugLogs)
            {
                Debug.Log($"✅ Configured ReminderManager with RAG API URL: {ragApiUrl}");
            }
        }
        
        if (userIdField != null)
        {
            userIdField.SetValue(reminderManager, userId);
            if (showDebugLogs)
            {
                Debug.Log($"✅ Configured ReminderManager with User ID: {userId}");
            }
        }
    }
    
    /// <summary>
    /// Check the current status of both managers - useful for debugging
    /// </summary>
    [ContextMenu("Check Manager Status")]
    public void CheckManagerStatus()
    {
        TimeSyncManager timeSync = FindFirstObjectByType<TimeSyncManager>();
        ReminderManager reminderMgr = FindFirstObjectByType<ReminderManager>();
        
        Debug.Log("=== Manager Status ===");
        Debug.Log($"TimeSyncManager: {(timeSync != null ? "✅ Found" : "❌ Missing")}");
        if (timeSync != null)
        {
            Debug.Log($"  - Is Synchronized: {timeSync.IsTimeSynchronized()}");
            Debug.Log($"  - Time Offset: {timeSync.GetTimeOffsetSeconds():F2}s");
            Debug.Log($"  - Sync Status: {timeSync.GetSyncStatus()}");
        }
        
        Debug.Log($"ReminderManager: {(reminderMgr != null ? "✅ Found" : "❌ Missing")}");
        if (reminderMgr != null)
        {
            Debug.Log($"  - Due Reminders: {reminderMgr.GetDueReminderCount()}");
            Debug.Log($"  - Pending Reminders: {reminderMgr.GetPendingReminderCount()}");
        }
        Debug.Log("===================");
    }
    
    /// <summary>
    /// Force a manual reminder check - useful for testing
    /// </summary>
    public void TestReminderCheck()
    {
        ReminderManager reminderMgr = FindFirstObjectByType<ReminderManager>();
        if (reminderMgr != null)
        {
            StartCoroutine(reminderMgr.CheckReminders());
            Debug.Log("🧪 Manually triggered reminder check");
        }
        else
        {
            Debug.LogError("❌ ReminderManager not found - cannot test reminder check");
        }
    }
}