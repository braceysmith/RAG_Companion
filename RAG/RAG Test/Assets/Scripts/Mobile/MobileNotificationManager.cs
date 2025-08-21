using System;
using System.Collections.Generic;
using UnityEngine;

public class MobileNotificationManager : MonoBehaviour
{
    [Header("Notification Settings")]
    [SerializeField] private bool enableNotifications = true;
    [SerializeField] private bool enableBadgeUpdates = true;
    [SerializeField] private bool enableSoundAlerts = true;
    [SerializeField] private bool enableVibration = true;
    
    [Header("Notification Types")]
    [SerializeField] private bool enableMessageNotifications = true;
    [SerializeField] private bool enableSystemNotifications = true;
    [SerializeField] private bool enableErrorNotifications = true;
    [SerializeField] private bool enableConnectionNotifications = false;
    
    [Header("Mobile Optimization")]
    [SerializeField] private int maxNotificationsPerHour = 10;
    [SerializeField] private bool enableQuietHours = true;
    [SerializeField] private int quietHoursStart = 22; // 10 PM
    [SerializeField] private int quietHoursEnd = 7; // 7 AM
    
    // Notification tracking
    private List<DateTime> recentNotifications = new List<DateTime>();
    private Dictionary<string, int> notificationCounts = new Dictionary<string, int>();
    private bool isInQuietHours = false;
    
    // Events
    public event Action<string, string> OnNotificationShown;
    // public event Action<string> OnNotificationClicked; // Commented out - unused
    public event Action<int> OnBadgeCountChanged;
    
    private void Start()
    {
        InitializeMobileNotifications();
        InvokeRepeating(nameof(UpdateQuietHours), 0f, 60f); // Check every minute
    }
    
    private void InitializeMobileNotifications()
    {
        // Initialize platform-specific notifications
#if UNITY_ANDROID && !UNITY_EDITOR
        InitializeAndroidNotifications();
#elif UNITY_IOS && !UNITY_EDITOR
        InitializeiOSNotifications();
#else
        // Initialize for other platforms (Quest, PC, etc.)
        InitializeCrossPlatformNotifications();
#endif
        
        LogMessage("Mobile notification manager initialized");
    }
    
#if UNITY_ANDROID && !UNITY_EDITOR
    private void InitializeAndroidNotifications()
    {
        // Android notification setup
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            // Create notification channel
            AndroidJavaClass notificationManagerClass = new AndroidJavaClass("android.app.NotificationManager");
            AndroidJavaObject notificationManager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
            
            LogMessage("Android notifications initialized");
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize Android notifications: {ex.Message}");
        }
    }
#else
    private void InitializeAndroidNotifications()
    {
        // Placeholder for non-Android platforms
        LogMessage("Android notifications not available on this platform");
    }
#endif
    
#if UNITY_IOS && !UNITY_EDITOR
    private void InitializeiOSNotifications()
    {
        // iOS notification setup
        try
        {
            // Request notification permissions
            // This would integrate with Unity's Mobile Notifications package
            LogMessage("iOS notifications initialized");
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize iOS notifications: {ex.Message}");
        }
    }
#else
    private void InitializeiOSNotifications()
    {
        // Placeholder for non-iOS platforms
        LogMessage("iOS notifications not available on this platform");
    }
#endif
    
    private void InitializeCrossPlatformNotifications()
    {
        // Initialize for Quest, PC, and other platforms
        LogMessage("Cross-platform notifications initialized");
    }
    
    public void ShowNotification(string title, string message, string type = "default")
    {
        if (!enableNotifications)
        {
            return;
        }
        
        // Check notification limits
        if (!CanShowNotification(type))
        {
            LogMessage($"Notification blocked: {type} - {title}");
            return;
        }
        
        // Check quiet hours
        if (isInQuietHours && enableQuietHours)
        {
            LogMessage($"Notification blocked (quiet hours): {title}");
            return;
        }
        
        try
        {
            // Show platform-specific notification
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowAndroidNotification(title, message, type);
#elif UNITY_IOS && !UNITY_EDITOR
            ShowiOSNotification(title, message, type);
#else
            ShowCrossPlatformNotification(title, message, type);
#endif
            
            // Track notification
            TrackNotification(type);
            
            // Trigger events
            OnNotificationShown?.Invoke(title, message);
            
            LogMessage($"Notification shown: {title} - {message}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to show notification: {ex.Message}");
        }
    }
    
#if UNITY_ANDROID && !UNITY_EDITOR
    private void ShowAndroidNotification(string title, string message, string type)
    {
        // Android notification implementation
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            // Create notification
            AndroidJavaClass notificationBuilderClass = new AndroidJavaClass("android.app.Notification$Builder");
            AndroidJavaObject notificationBuilder = new AndroidJavaObject("android.app.Notification$Builder", activity);
            
            // Configure notification
            notificationBuilder.Call<AndroidJavaObject>("setContentTitle", title);
            notificationBuilder.Call<AndroidJavaObject>("setContentText", message);
            // Use a default icon - you may need to provide your own icon resource
            notificationBuilder.Call<AndroidJavaObject>("setSmallIcon", 0x1080093); // Default info icon
            
            // Add sound if enabled
            if (enableSoundAlerts)
            {
                notificationBuilder.Call<AndroidJavaObject>("setDefaults", 1); // DEFAULT_SOUND
            }
            
            // Add vibration if enabled
            if (enableVibration)
            {
                notificationBuilder.Call<AndroidJavaObject>("setVibrate", new long[] { 0, 300, 100, 300 });
            }
            
            // Build and show notification
            AndroidJavaObject notification = notificationBuilder.Call<AndroidJavaObject>("build");
            AndroidJavaObject notificationManager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
            notificationManager.Call("notify", 1, notification);
        }
        catch (Exception ex)
        {
            LogError($"Android notification failed: {ex.Message}");
            // Fallback to cross-platform notification
            ShowCrossPlatformNotification(title, message, type);
        }
    }
#else
    private void ShowAndroidNotification(string title, string message, string type)
    {
        // Placeholder for non-Android platforms
        ShowCrossPlatformNotification(title, message, type);
    }
#endif
    
#if UNITY_IOS && !UNITY_EDITOR
    private void ShowiOSNotification(string title, string message, string type)
    {
        // iOS notification implementation
        // This would use Unity's Mobile Notifications package
        
        // For now, use basic local notification
        if (enableSoundAlerts)
        {
            // Play notification sound
        }
        
        if (enableVibration)
        {
            Handheld.Vibrate();
        }
    }
#else
    private void ShowiOSNotification(string title, string message, string type)
    {
        // Placeholder for non-iOS platforms
        ShowCrossPlatformNotification(title, message, type);
    }
#endif
    
    private void ShowCrossPlatformNotification(string title, string message, string type)
    {
        // Cross-platform notification for Quest, PC, etc.
        Debug.Log($"[NOTIFICATION] {title}: {message}");
        
        // Simple visual feedback
        if (Application.isEditor)
        {
            // Could show a simple UI notification here
        }
        
        // Haptic feedback for Quest
        if (enableVibration && Application.platform == RuntimePlatform.Android)
        {
            try
            {
                Handheld.Vibrate();
            }
            catch (Exception ex)
            {
                LogError($"Vibration failed: {ex.Message}");
            }
        }
    }
    
    private void ShowEditorNotification(string title, string message, string type)
    {
        // Editor/fallback notification
        Debug.Log($"[NOTIFICATION] {title}: {message}");
        
        // Simple visual feedback in editor
        if (Application.isEditor)
        {
            // Could show a simple UI notification here
        }
    }
    
    private bool CanShowNotification(string type)
    {
        // Check notification type permissions
        switch (type.ToLower())
        {
            case "message":
                if (!enableMessageNotifications) return false;
                break;
            case "system":
                if (!enableSystemNotifications) return false;
                break;
            case "error":
                if (!enableErrorNotifications) return false;
                break;
            case "connection":
                if (!enableConnectionNotifications) return false;
                break;
        }
        
        // Check rate limiting
        DateTime oneHourAgo = DateTime.Now.AddHours(-1);
        recentNotifications.RemoveAll(n => n < oneHourAgo);
        
        if (recentNotifications.Count >= maxNotificationsPerHour)
        {
            return false;
        }
        
        return true;
    }
    
    private void TrackNotification(string type)
    {
        // Track notification timing
        recentNotifications.Add(DateTime.Now);
        
        // Track notification counts
        if (!notificationCounts.ContainsKey(type))
        {
            notificationCounts[type] = 0;
        }
        notificationCounts[type]++;
        
        // Update badge count
        if (enableBadgeUpdates)
        {
            int totalCount = 0;
            foreach (var count in notificationCounts.Values)
            {
                totalCount += count;
            }
            
            UpdateBadgeCount(totalCount);
        }
    }
    
    private void UpdateBadgeCount(int count)
    {
        try
        {
#if UNITY_IOS && !UNITY_EDITOR
            // iOS badge update
            // This would use Unity's Mobile Notifications package
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android badge update (limited support)
#endif
            
            OnBadgeCountChanged?.Invoke(count);
            LogMessage($"Badge count updated: {count}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to update badge count: {ex.Message}");
        }
    }
    
    private void UpdateQuietHours()
    {
        int currentHour = DateTime.Now.Hour;
        
        bool wasInQuietHours = isInQuietHours;
        
        if (quietHoursStart > quietHoursEnd)
        {
            // Quiet hours span midnight (e.g., 22:00 to 07:00)
            isInQuietHours = currentHour >= quietHoursStart || currentHour < quietHoursEnd;
        }
        else
        {
            // Quiet hours within same day
            isInQuietHours = currentHour >= quietHoursStart && currentHour < quietHoursEnd;
        }
        
        if (wasInQuietHours != isInQuietHours)
        {
            LogMessage($"Quiet hours status changed: {(isInQuietHours ? "Enabled" : "Disabled")}");
        }
    }
    
    public void ShowMessageNotification(string message, string sender = "Assistant")
    {
        if (enableMessageNotifications)
        {
            ShowNotification($"Message from {sender}", message, "message");
        }
    }
    
    public void ShowSystemNotification(string message)
    {
        if (enableSystemNotifications)
        {
            ShowNotification("System", message, "system");
        }
    }
    
    public void ShowErrorNotification(string error)
    {
        if (enableErrorNotifications)
        {
            ShowNotification("Error", error, "error");
        }
    }
    
    public void ShowConnectionNotification(bool isConnected)
    {
        if (enableConnectionNotifications)
        {
            string message = isConnected ? "Connected to service" : "Disconnected from service";
            ShowNotification("Connection", message, "connection");
        }
    }
    
    public void ClearAllNotifications()
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Clear Android notifications
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject notificationManager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
            notificationManager.Call("cancelAll");
#elif UNITY_IOS && !UNITY_EDITOR
            // Clear iOS notifications
            // This would use Unity's Mobile Notifications package
#endif
            
            // Reset tracking
            recentNotifications.Clear();
            notificationCounts.Clear();
            
            // Reset badge
            if (enableBadgeUpdates)
            {
                UpdateBadgeCount(0);
            }
            
            LogMessage("All notifications cleared");
        }
        catch (Exception ex)
        {
            LogError($"Failed to clear notifications: {ex.Message}");
        }
    }
    
    public void SetNotificationSettings(bool enableNotifs, bool enableSound, bool enableVibr)
    {
        enableNotifications = enableNotifs;
        enableSoundAlerts = enableSound;
        enableVibration = enableVibr;
        
        LogMessage($"Notification settings updated - Notifications: {enableNotifs}, Sound: {enableSound}, Vibration: {enableVibr}");
    }
    
    public void SetQuietHours(int startHour, int endHour)
    {
        quietHoursStart = Mathf.Clamp(startHour, 0, 23);
        quietHoursEnd = Mathf.Clamp(endHour, 0, 23);
        
        UpdateQuietHours();
        LogMessage($"Quiet hours set: {quietHoursStart}:00 to {quietHoursEnd}:00");
    }
    
    public void EnableNotificationType(string type, bool enabled)
    {
        switch (type.ToLower())
        {
            case "message":
                enableMessageNotifications = enabled;
                break;
            case "system":
                enableSystemNotifications = enabled;
                break;
            case "error":
                enableErrorNotifications = enabled;
                break;
            case "connection":
                enableConnectionNotifications = enabled;
                break;
        }
        
        LogMessage($"Notification type '{type}' set to: {enabled}");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileNotificationManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileNotificationManager] {message}");
    }
    
    private void OnDestroy()
    {
        CancelInvoke();
    }
    
    // Public getters
    public bool IsNotificationsEnabled => enableNotifications;
    public bool IsInQuietHours => isInQuietHours;
    public int NotificationCount => recentNotifications.Count;
    public Dictionary<string, int> GetNotificationCounts() => new Dictionary<string, int>(notificationCounts);
}