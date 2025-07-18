using System;
using System.Collections;
using UnityEngine;

public class MobileNetworkManager : MonoBehaviour
{
    [Header("Network Configuration")]
    [SerializeField] private float connectionCheckInterval = 2f;
    [SerializeField] private string pingUrl = "https://www.google.com";
    [SerializeField] private float pingTimeout = 5f;
    [SerializeField] private int maxPingAttempts = 3;
    
    [Header("Battery Optimization")]
    [SerializeField] private bool enableBatteryOptimization = true;
    [SerializeField] private float lowBatteryThreshold = 0.2f;
    [SerializeField] private float batteryCheckInterval = 30f;
    
    [Header("Data Usage Optimization")]
    [SerializeField] private bool enableDataUsageOptimization = true;
    [SerializeField] private long maxDataUsagePerSession = 50 * 1024 * 1024; // 50MB
    [SerializeField] private bool restrictOnCellular = true;
    
    // Network state
    private bool isConnected = false;
    private bool isWifi = false;
    private bool isLowBattery = false;
    private long currentDataUsage = 0;
    private NetworkReachability lastReachability;
    
    // Performance tracking
    private float lastPingTime = 0f;
    private int connectionDrops = 0;
    private int totalConnectionChecks = 0;
    
    // Events
    public event Action<bool> OnConnectionChanged;
    public event Action<bool> OnWifiStatusChanged;
    public event Action<bool> OnLowBatteryStatusChanged;
    public event Action<long> OnDataUsageUpdate;
    public event Action OnDataLimitReached;
    
    private void Start()
    {
        InitializeNetworkMonitoring();
        StartCoroutine(MonitorNetwork());
        
        if (enableBatteryOptimization)
        {
            StartCoroutine(MonitorBattery());
        }
    }
    
    private void InitializeNetworkMonitoring()
    {
        lastReachability = Application.internetReachability;
        UpdateConnectionStatus();
        
        LogMessage("Network monitoring initialized");
    }
    
    private IEnumerator MonitorNetwork()
    {
        while (true)
        {
            yield return new WaitForSeconds(connectionCheckInterval);
            
            totalConnectionChecks++;
            
            // Check basic connectivity
            NetworkReachability currentReachability = Application.internetReachability;
            bool wasConnected = isConnected;
            
            UpdateConnectionStatus();
            
            // Detect connection changes
            if (wasConnected != isConnected)
            {
                if (!isConnected)
                {
                    connectionDrops++;
                }
                
                OnConnectionChanged?.Invoke(isConnected);
                LogMessage($"Connection status changed: {(isConnected ? "Connected" : "Disconnected")}");
            }
            
            // Check if network type changed
            if (lastReachability != currentReachability)
            {
                bool wasWifi = isWifi;
                isWifi = currentReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
                
                if (wasWifi != isWifi)
                {
                    OnWifiStatusChanged?.Invoke(isWifi);
                    LogMessage($"Network type changed: {(isWifi ? "WiFi" : "Cellular")}");
                }
                
                lastReachability = currentReachability;
            }
            
            // Perform ping test for more accurate connectivity
            if (isConnected && Time.time - lastPingTime > connectionCheckInterval * 2)
            {
                StartCoroutine(PerformPingTest());
            }
        }
    }
    
    private IEnumerator MonitorBattery()
    {
        while (true)
        {
            yield return new WaitForSeconds(batteryCheckInterval);
            
            float batteryLevel = SystemInfo.batteryLevel;
            bool wasLowBattery = isLowBattery;
            isLowBattery = batteryLevel < lowBatteryThreshold && batteryLevel > 0;
            
            if (wasLowBattery != isLowBattery)
            {
                OnLowBatteryStatusChanged?.Invoke(isLowBattery);
                LogMessage($"Battery status changed: {(isLowBattery ? "Low" : "Normal")} ({batteryLevel:P0})");
            }
        }
    }
    
    private void UpdateConnectionStatus()
    {
        NetworkReachability reachability = Application.internetReachability;
        
        isConnected = reachability != NetworkReachability.NotReachable;
        isWifi = reachability == NetworkReachability.ReachableViaLocalAreaNetwork;
        
        // Additional mobile-specific checks
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android-specific network checks
        isConnected = isConnected && CheckAndroidConnectivity();
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS-specific network checks
        isConnected = isConnected && CheckiOSConnectivity();
#endif
    }
    
    private IEnumerator PerformPingTest()
    {
        lastPingTime = Time.time;
        
        for (int attempt = 0; attempt < maxPingAttempts; attempt++)
        {
            using (var ping = new UnityEngine.Networking.UnityWebRequest(pingUrl, "HEAD"))
            {
                ping.timeout = Mathf.RoundToInt(pingTimeout);
                
                yield return ping.SendWebRequest();
                
                if (ping.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    // Ping successful, connection is good
                    if (!isConnected)
                    {
                        isConnected = true;
                        OnConnectionChanged?.Invoke(true);
                        LogMessage("Ping test successful - connection restored");
                    }
                    break;
                }
                else if (attempt == maxPingAttempts - 1)
                {
                    // All ping attempts failed
                    if (isConnected)
                    {
                        isConnected = false;
                        connectionDrops++;
                        OnConnectionChanged?.Invoke(false);
                        LogMessage("Ping test failed - connection lost");
                    }
                }
                
                yield return new WaitForSeconds(1f);
            }
        }
    }
    
#if UNITY_ANDROID && !UNITY_EDITOR
    private bool CheckAndroidConnectivity()
    {
        try
        {
            using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject context = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject connectivityManager = context.Call<AndroidJavaObject>("getSystemService", "connectivity");
                AndroidJavaObject networkInfo = connectivityManager.Call<AndroidJavaObject>("getActiveNetworkInfo");
                
                if (networkInfo != null)
                {
                    return networkInfo.Call<bool>("isConnectedOrConnecting");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Android connectivity check failed: {ex.Message}");
        }
        
        return false;
    }
#else
    private bool CheckAndroidConnectivity()
    {
        return true; // Fallback for non-Android platforms
    }
#endif
    
#if UNITY_IOS && !UNITY_EDITOR
    private bool CheckiOSConnectivity()
    {
        // iOS-specific connectivity checks would go here
        // For now, rely on Unity's built-in checks
        return true;
    }
#else
    private bool CheckiOSConnectivity()
    {
        return true; // Fallback for non-iOS platforms
    }
#endif
    
    public void TrackDataUsage(long bytes)
    {
        currentDataUsage += bytes;
        OnDataUsageUpdate?.Invoke(currentDataUsage);
        
        if (currentDataUsage > maxDataUsagePerSession)
        {
            OnDataLimitReached?.Invoke();
            LogMessage($"Data usage limit reached: {currentDataUsage:N0} bytes");
        }
    }
    
    public bool ShouldAllowRequest()
    {
        // Check if request should be allowed based on current conditions
        
        // Check connection
        if (!isConnected)
        {
            return false;
        }
        
        // Check battery optimization
        if (enableBatteryOptimization && isLowBattery)
        {
            return false;
        }
        
        // Check data usage limits
        if (enableDataUsageOptimization && currentDataUsage > maxDataUsagePerSession)
        {
            return false;
        }
        
        // Check cellular restrictions
        if (restrictOnCellular && !isWifi)
        {
            return false;
        }
        
        return true;
    }
    
    public void ResetDataUsage()
    {
        currentDataUsage = 0;
        OnDataUsageUpdate?.Invoke(currentDataUsage);
        LogMessage("Data usage reset");
    }
    
    public void OptimizeForBattery(bool enable)
    {
        enableBatteryOptimization = enable;
        
        if (enable)
        {
            // Reduce check frequency
            connectionCheckInterval = Mathf.Max(connectionCheckInterval * 2, 10f);
            batteryCheckInterval = Mathf.Max(batteryCheckInterval * 2, 60f);
        }
        else
        {
            // Restore normal frequency
            connectionCheckInterval = 2f;
            batteryCheckInterval = 30f;
        }
        
        LogMessage($"Battery optimization: {(enable ? "Enabled" : "Disabled")}");
    }
    
    public void OptimizeForDataUsage(bool enable)
    {
        enableDataUsageOptimization = enable;
        LogMessage($"Data usage optimization: {(enable ? "Enabled" : "Disabled")}");
    }
    
    public void SetCellularRestriction(bool restrict)
    {
        restrictOnCellular = restrict;
        LogMessage($"Cellular restriction: {(restrict ? "Enabled" : "Disabled")}");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileNetworkManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileNetworkManager] {message}");
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
    
    // Public getters for monitoring
    public bool IsConnected => isConnected;
    public bool IsWifi => isWifi;
    public bool IsLowBattery => isLowBattery;
    public long CurrentDataUsage => currentDataUsage;
    public int ConnectionDrops => connectionDrops;
    public int TotalConnectionChecks => totalConnectionChecks;
    public float ConnectionStability => totalConnectionChecks > 0 ? 1f - ((float)connectionDrops / totalConnectionChecks) : 1f;
    public NetworkReachability CurrentReachability => Application.internetReachability;
}