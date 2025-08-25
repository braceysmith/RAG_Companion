using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using Newtonsoft.Json;

// Mobile notifications are only available when the Unity Mobile Notifications package is installed
// These imports are wrapped in platform-specific defines to prevent compilation errors
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
using Unity.Notifications.Android;
#endif

#if UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
using Unity.Notifications.iOS;
#endif

[System.Serializable]
public class ReminderData
{
    public string id;
    public string content;
    public string datetime;
    public bool triggered;
    public string status;
    public int overdue_minutes;
    public int minutes_until;
}

[System.Serializable]
public class ReminderResponse
{
    public string status;
    public List<ReminderData> due_reminders;
    public List<ReminderData> pending_reminders;
    public int total_due;
    public int total_pending;
}

public class ReminderManager : MonoBehaviour
{
    [Header("Reminder Settings")]
    [SerializeField] private string ragApiUrl = "https://ragcompanion-production-bf25.up.railway.app";
    [SerializeField] private string userId = "mobile-user";
    [SerializeField] private float checkInterval = 60f; // Check every minute
    
    [Header("UI References")]
    [SerializeField] private GameObject reminderNotificationPanel;
    [SerializeField] private TextMeshProUGUI reminderText;
    [SerializeField] private TextMeshProUGUI reminderTimeText;
    
    [Header("Mobile Notifications")]
    [SerializeField] private bool enableMobileNotifications = true;
    
    // Events
    public System.Action<List<ReminderData>> OnRemindersReceived;
    public System.Action<ReminderData> OnReminderDue;
    
    private List<ReminderData> pendingReminders = new List<ReminderData>();
    private List<ReminderData> dueReminders = new List<ReminderData>();
    private Coroutine reminderCheckCoroutine;
    
    void Start()
    {
        // Wait for time sync before starting reminder operations
        StartCoroutine(InitializeWithTimeSync());
    }
    
    IEnumerator InitializeWithTimeSync()
    {
        // Wait for TimeSyncManager to be available
        while (TimeSyncManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Wait for time synchronization to complete (max 15 seconds)
        float waitTime = 0f;
        while (!TimeSyncManager.Instance.IsTimeSynchronized() && waitTime < 15f)
        {
            yield return new WaitForSeconds(0.5f);
            waitTime += 0.5f;
        }
        
        if (TimeSyncManager.Instance.IsTimeSynchronized())
        {
            Debug.Log($"🕐 ReminderManager initialized with synchronized time (offset: {TimeSyncManager.Instance.GetTimeOffsetSeconds():F1}s)");
        }
        else
        {
            Debug.LogWarning("⚠️ ReminderManager starting without time sync - may have timing issues");
        }
        
        // Now start reminder operations
        StartCoroutine(CheckRemindersOnStartup());
        
        // Start periodic reminder checking
        reminderCheckCoroutine = StartCoroutine(PeriodicReminderCheck());
        
        // Initialize mobile notifications if available
        if (enableMobileNotifications)
        {
            InitializeMobileNotifications();
        }
    }
    
    void OnDestroy()
    {
        if (reminderCheckCoroutine != null)
        {
            StopCoroutine(reminderCheckCoroutine);
        }
    }
    
    IEnumerator CheckRemindersOnStartup()
    {
        Debug.Log("🔔 Checking for reminders on app startup...");
        yield return StartCoroutine(CheckReminders());
        
        // If there are due reminders, show them immediately
        if (dueReminders.Count > 0)
        {
            ShowStartupReminderWelcome();
        }
    }
    
    IEnumerator PeriodicReminderCheck()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            yield return StartCoroutine(CheckReminders());
        }
    }
    
    public IEnumerator CheckReminders()
    {
        if (string.IsNullOrEmpty(ragApiUrl) || string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("ReminderManager: API URL or User ID not set");
            yield break;
        }
        
        string url = $"{ragApiUrl}/reminders/check/{userId}";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    ReminderResponse response = JsonConvert.DeserializeObject<ReminderResponse>(request.downloadHandler.text);
                    
                    if (response.status == "success")
                    {
                        // Update local reminder lists
                        List<ReminderData> newDueReminders = response.due_reminders ?? new List<ReminderData>();
                        pendingReminders = response.pending_reminders ?? new List<ReminderData>();
                        
                        // Check for newly due reminders
                        bool hasNewReminders = false;
                        foreach (var newReminder in newDueReminders)
                        {
                            bool isNewlyDue = !dueReminders.Exists(r => r.id == newReminder.id);
                            if (isNewlyDue)
                            {
                                Debug.Log($"🔔 New reminder due: {newReminder.content}");
                                OnReminderDue?.Invoke(newReminder);
                                hasNewReminders = true;
                                
                                // Show notification
                                ShowReminderNotification(newReminder);
                                
                                // Send mobile push notification if app is in background
                                if (enableMobileNotifications && !Application.isFocused)
                                {
                                    SendMobileNotification(newReminder);
                                }
                            }
                        }
                        
                        // If we have new due reminders, trigger AI delivery
                        // Note: Removed Application.isFocused check to allow delivery regardless of app state
                        if (hasNewReminders)
                        {
                            Debug.Log("🤖 Triggering AI reminder delivery...");
                            StartCoroutine(DeliverRemindersThroughAI());
                        }
                        
                        // Also check if we have any due reminders on this check (not just new ones)
                        // This handles cases where reminders were due before but delivery failed
                        else if (newDueReminders.Count > 0)
                        {
                            Debug.Log($"🔔 Found {newDueReminders.Count} existing due reminders - attempting delivery");
                            StartCoroutine(DeliverRemindersThroughAI());
                        }
                        
                        dueReminders = newDueReminders;
                        OnRemindersReceived?.Invoke(dueReminders);
                        
                        Debug.Log($"📅 Reminder check: {response.total_due} due, {response.total_pending} pending");
                    }
                    else
                    {
                        // Try to get the error message if available
                        string errorMessage = response.status;
                        try 
                        {
                            var responseDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                            if (responseDict.ContainsKey("message"))
                            {
                                errorMessage = responseDict["message"].ToString();
                            }
                        }
                        catch 
                        {
                            // If we can't parse the message, just use the status
                        }
                        Debug.LogError($"Reminder check failed: {errorMessage}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to parse reminder response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"Reminder check request failed: {request.error}");
            }
        }
    }
    
    private void ShowReminderNotification(ReminderData reminder)
    {
        if (reminderNotificationPanel != null)
        {
            reminderNotificationPanel.SetActive(true);
            
            if (reminderText != null)
            {
                reminderText.text = $"🔔 Reminder: {reminder.content}";
            }
            
            if (reminderTimeText != null)
            {
                if (reminder.overdue_minutes > 0)
                {
                    reminderTimeText.text = $"(was due {reminder.overdue_minutes} minutes ago)";
                }
                else
                {
                    reminderTimeText.text = "(due now)";
                }
            }
            
            // Auto-hide after 10 seconds
            StartCoroutine(HideReminderNotificationAfterDelay(10f));
        }
    }
    
    private void ShowStartupReminderWelcome()
    {
        Debug.Log($"🔔 Welcome back! You have {dueReminders.Count} reminder(s) waiting.");
        
        // Trigger AI to proactively deliver the reminders
        Debug.Log("🤖 Triggering AI startup reminder delivery...");
        StartCoroutine(DeliverRemindersThroughAI());
    }
    
    IEnumerator HideReminderNotificationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (reminderNotificationPanel != null)
        {
            reminderNotificationPanel.SetActive(false);
        }
    }
    
    public void DismissReminderNotification()
    {
        if (reminderNotificationPanel != null)
        {
            reminderNotificationPanel.SetActive(false);
        }
    }
    
    public void GetAllReminders(System.Action<List<ReminderData>> callback)
    {
        StartCoroutine(GetAllRemindersCoroutine(callback));
    }
    
    IEnumerator GetAllRemindersCoroutine(System.Action<List<ReminderData>> callback)
    {
        string url = $"{ragApiUrl}/reminders/all/{userId}";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                    
                    if (response["status"].ToString() == "success")
                    {
                        var remindersJson = response["reminders"];
                        var reminders = JsonConvert.DeserializeObject<List<ReminderData>>(remindersJson.ToString());
                        callback?.Invoke(reminders);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to get all reminders: {e.Message}");
                    callback?.Invoke(new List<ReminderData>());
                }
            }
            else
            {
                Debug.LogError($"Get all reminders failed: {request.error}");
                callback?.Invoke(new List<ReminderData>());
            }
        }
    }
    
    private void InitializeMobileNotifications()
    {
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
        // Android notification setup
        try
        {
            var channel = new AndroidNotificationChannel()
            {
                Id = "reminder_channel",
                Name = "AI Reminders",
                Importance = Importance.High,
                Description = "Notifications for AI reminders",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            Debug.Log("📱 Android notifications initialized");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"📱 Android notifications failed: {ex.Message}");
        }
#elif UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
        // iOS notification setup
        try
        {
            iOSNotificationCenter.RequestAuthorizationAsync(
                AuthorizationOption.Alert |
                AuthorizationOption.Badge |
                AuthorizationOption.Sound
            );
            Debug.Log("📱 iOS notifications initialized");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"📱 iOS notifications failed: {ex.Message}");
        }
#else
        Debug.Log("📱 Mobile notifications not available on this platform or package not installed");
#endif
        
        Debug.Log("📱 Mobile notifications initialized");
    }
    
    private void SendMobileNotification(ReminderData reminder)
    {
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
        try
        {
            var notification = new AndroidNotification();
            notification.Title = "AI Reminder";
            notification.Text = reminder.content;
            notification.SmallIcon = "icon_small";
            notification.LargeIcon = "icon_large";
            notification.FireTime = System.DateTime.Now;
            
            AndroidNotificationCenter.SendNotification(notification, "reminder_channel");
            Debug.Log($"📱 Sent Android notification: {reminder.content}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"📱 Android notification failed: {ex.Message}");
        }
#elif UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
        try
        {
            var notification = new iOSNotification()
            {
                Title = "AI Reminder",
                Body = reminder.content,
                ShowInForeground = false,
                ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                CategoryIdentifier = "reminder_category",
                ThreadIdentifier = "reminder_thread",
                Trigger = new iOSNotificationTimeIntervalTrigger()
                {
                    TimeInterval = new System.TimeSpan(0, 0, 1),
                    Repeats = false
                }
            };
            
            iOSNotificationCenter.ScheduleNotification(notification);
            Debug.Log($"📱 Sent iOS notification: {reminder.content}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"📱 iOS notification failed: {ex.Message}");
        }
#else
        // Fallback for unsupported platforms or when Unity Mobile Notifications package is not installed
        Debug.Log($"📱 Notification (fallback): {reminder.content}");
        
        // Use cross-platform notification as fallback
        PlatformConfig.ShowCrossPlatformNotification("AI Reminder", reminder.content);
#endif
    }
    
    private IEnumerator DeliverRemindersThroughAI()
    {
        string url = $"{ragApiUrl}/reminders/deliver/{userId}";
        Debug.Log($"🔗 Attempting to deliver reminders via: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                    string status = response["status"].ToString();
                    
                    if (status == "success")
                    {
                        string aiMessage = response["message"].ToString();
                        int reminderCount = int.Parse(response["reminders_delivered"].ToString());
                        
                        Debug.Log($"🤖 AI delivered {reminderCount} reminder(s): {aiMessage}");
                        
                        // Trigger AI to speak this message through the conversation system
                        var mobileChat = FindFirstObjectByType<MobileRealtimeChat>();
                        if (mobileChat != null)
                        {
                            // Check if the connection is active before attempting AI delivery
                            if (mobileChat.IsConnected)
                            {
                                // Send the AI message as if it's starting a new conversation
                                mobileChat.TriggerAIReminderDelivery(aiMessage);
                            }
                            else
                            {
                                Debug.LogWarning("🔔 MobileRealtimeChat connection not active - AI reminder message cannot be delivered audibly");
                                // Fallback: just log the reminder content
                                Debug.Log($"📝 Reminder content (no audio): {aiMessage}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("🔔 MobileRealtimeChat not found - AI reminder message cannot be delivered audibly");
                        }
                    }
                    else if (status == "no_due_reminders")
                    {
                        Debug.Log("📅 No due reminders to deliver");
                    }
                    else
                    {
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Unknown error";
                        Debug.LogError($"AI reminder delivery failed: {errorMessage}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to parse AI reminder delivery response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"AI reminder delivery request failed: {request.error}");
            }
        }
    }
    
    // Manual testing method - force trigger reminder delivery for testing
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void TestReminderDelivery()
    {
        Debug.Log("🧪 MANUAL TEST: Forcing reminder delivery test...");
        StartCoroutine(DeliverRemindersThroughAI());
    }
    
    // Public methods for UI integration
    public int GetDueReminderCount() => dueReminders.Count;
    public int GetPendingReminderCount() => pendingReminders.Count;
    public List<ReminderData> GetDueReminders() => new List<ReminderData>(dueReminders);
    public List<ReminderData> GetPendingReminders() => new List<ReminderData>(pendingReminders);
}