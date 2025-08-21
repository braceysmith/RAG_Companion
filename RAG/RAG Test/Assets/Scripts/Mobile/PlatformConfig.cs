using UnityEngine;

/// <summary>
/// Platform configuration and compatibility layer for mobile features
/// Ensures scripts compile for all platforms while maintaining platform-specific functionality
/// </summary>
public static class PlatformConfig
{
    // Check if Unity Mobile Notifications package is available
    private const bool UNITY_MOBILE_NOTIFICATIONS_AVAILABLE = 
#if UNITY_MOBILE_NOTIFICATIONS
        true;
#else
        false;
#endif
    
    // Platform-specific defines
    public static bool IsAndroid => Application.platform == RuntimePlatform.Android;
    public static bool IsIOS => Application.platform == RuntimePlatform.IPhonePlayer;
    public static bool IsQuest => IsAndroid && IsVRPlatform();
    public static bool IsMobile => IsAndroid || IsIOS;
    public static bool IsEditor => Application.isEditor;
    
    // Feature availability flags
    public static bool SupportsMobileNotifications => IsMobile && !IsEditor && UNITY_MOBILE_NOTIFICATIONS_AVAILABLE;
    public static bool SupportsNativeGallery => IsMobile && !IsEditor;
    public static bool SupportsHapticFeedback => IsMobile && !IsEditor;
    public static bool SupportsMicrophone => true; // All platforms support microphone
    
    // VR platform detection
    private static bool IsVRPlatform()
    {
        // Check if we're running on a VR platform
        return UnityEngine.XR.XRSettings.enabled || 
               Application.platform == RuntimePlatform.Android; // Quest runs on Android
    }
    
    // Cross-platform haptic feedback
    public static void TriggerHapticFeedback(float intensity = 0.5f, float duration = 0.1f)
    {
        if (!SupportsHapticFeedback) return;
        
        try
        {
            if (IsAndroid)
            {
                // Android haptic feedback
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    if (vibrator != null)
                    {
                        long vibrateTime = (long)(duration * 1000); // Convert to milliseconds
                        vibrator.Call("vibrate", vibrateTime);
                    }
                }
            }
            else if (IsIOS)
            {
                // iOS haptic feedback
                Handheld.Vibrate();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PlatformConfig] Haptic feedback failed: {ex.Message}");
        }
    }
    
    // Cross-platform notification (fallback)
    public static void ShowCrossPlatformNotification(string title, string message)
    {
        if (SupportsMobileNotifications)
        {
            // Use platform-specific notifications
            Debug.Log($"[NOTIFICATION] {title}: {message}");
        }
        else
        {
            // Fallback for unsupported platforms
            Debug.Log($"[NOTIFICATION] {title}: {message}");
        }
    }
    
    // Platform-specific logging
    public static void LogPlatformInfo()
    {
        Debug.Log($"[PlatformConfig] Platform: {Application.platform}");
        Debug.Log($"[PlatformConfig] IsAndroid: {IsAndroid}");
        Debug.Log($"[PlatformConfig] IsIOS: {IsIOS}");
        Debug.Log($"[PlatformConfig] IsQuest: {IsQuest}");
        Debug.Log($"[PlatformConfig] IsMobile: {IsMobile}");
        Debug.Log($"[PlatformConfig] IsEditor: {IsEditor}");
        Debug.Log($"[PlatformConfig] SupportsMobileNotifications: {SupportsMobileNotifications}");
        Debug.Log($"[PlatformConfig] SupportsNativeGallery: {SupportsNativeGallery}");
        Debug.Log($"[PlatformConfig] SupportsHapticFeedback: {SupportsHapticFeedback}");
    }
}
