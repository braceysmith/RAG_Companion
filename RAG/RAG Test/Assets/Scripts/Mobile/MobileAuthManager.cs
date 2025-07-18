using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Security.Cryptography;
using System.Text;

[System.Serializable]
public class MobileAuthToken
{
    public string token;
    public DateTime expiryTime;
    public string userId;
    public Dictionary<string, string> metadata;
    
    public bool IsValid => DateTime.UtcNow < expiryTime;
}

[System.Serializable]
public class MobileUserProfile
{
    public string userId;
    public string deviceId;
    public string platform;
    public DateTime lastActivity;
    public Dictionary<string, object> preferences;
    public bool isGuest;
}

public class MobileAuthManager : MonoBehaviour
{
    [Header("Authentication Configuration")]
    // [SerializeField] private string authServiceUrl = "https://your-auth-service.com"; // Commented out - unused
    [SerializeField] private bool enableGuestMode = true;
    [SerializeField] private bool enableDeviceFingerprinting = true;
    [SerializeField] private int tokenExpiryHours = 24;
    
    [Header("Security Settings")]
    [SerializeField] private bool enableEncryption = true;
    // [SerializeField] private bool enableBiometrics = false; // Commented out - unused
    [SerializeField] private bool requireReauthentication = false;
    [SerializeField] private int reauthenticationMinutes = 60;
    
    [Header("Privacy Settings")]
    // [SerializeField] private bool enableAnonymousMode = true; // Commented out - unused
    // [SerializeField] private bool enableDataCollection = false; // Commented out - unused
    // [SerializeField] private bool enableCrashReporting = false; // Commented out - unused
    
    // Authentication state
    private MobileAuthToken currentToken;
    private MobileUserProfile currentUser;
    private string deviceId;
    private bool isAuthenticated = false;
    private DateTime lastAuthTime;
    
    // Security
    private string encryptionKey;
    private readonly string AUTH_TOKEN_KEY = "mobile_auth_token";
    private readonly string USER_PROFILE_KEY = "mobile_user_profile";
    private readonly string DEVICE_ID_KEY = "mobile_device_id";
    
    // Events
    public event Action<bool> OnAuthenticationChanged;
    public event Action<MobileUserProfile> OnUserProfileUpdated;
    public event Action<string> OnAuthError;
    public event Action OnTokenExpired;
    
    private void Start()
    {
        InitializeAuthentication();
    }
    
    private void InitializeAuthentication()
    {
        try
        {
            // Generate or load device ID
            GenerateDeviceId();
            
            // Initialize encryption
            InitializeEncryption();
            
            // Load saved authentication state
            LoadAuthenticationState();
            
            // Check if we need to reauthenticate
            if (requireReauthentication && ShouldReauthenticate())
            {
                RefreshAuthentication();
            }
            
            // Enable guest mode if no authentication
            if (!isAuthenticated && enableGuestMode)
            {
                AuthenticateAsGuest();
            }
            
            LogMessage("Authentication manager initialized");
        }
        catch (Exception ex)
        {
            LogError($"Authentication initialization failed: {ex.Message}");
            OnAuthError?.Invoke($"Authentication initialization failed: {ex.Message}");
        }
    }
    
    private void GenerateDeviceId()
    {
        deviceId = PlayerPrefs.GetString(DEVICE_ID_KEY, "");
        
        if (string.IsNullOrEmpty(deviceId))
        {
            if (enableDeviceFingerprinting)
            {
                deviceId = GenerateDeviceFingerprint();
            }
            else
            {
                deviceId = Guid.NewGuid().ToString();
            }
            
            PlayerPrefs.SetString(DEVICE_ID_KEY, deviceId);
            PlayerPrefs.Save();
        }
        
        LogMessage($"Device ID: {deviceId.Substring(0, 8)}...");
    }
    
    private string GenerateDeviceFingerprint()
    {
        var fingerprint = new StringBuilder();
        
        // Device information
        fingerprint.Append(SystemInfo.deviceModel);
        fingerprint.Append(SystemInfo.deviceName);
        fingerprint.Append(SystemInfo.operatingSystem);
        fingerprint.Append(SystemInfo.processorType);
        fingerprint.Append(SystemInfo.systemMemorySize);
        fingerprint.Append(Screen.width);
        fingerprint.Append(Screen.height);
        fingerprint.Append(Application.platform);
        
        // Generate hash
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
            return Convert.ToBase64String(hashBytes);
        }
    }
    
    private void InitializeEncryption()
    {
        if (enableEncryption)
        {
            encryptionKey = PlayerPrefs.GetString("encryption_key", "");
            
            if (string.IsNullOrEmpty(encryptionKey))
            {
                encryptionKey = GenerateEncryptionKey();
                PlayerPrefs.SetString("encryption_key", encryptionKey);
                PlayerPrefs.Save();
            }
        }
    }
    
    private string GenerateEncryptionKey()
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] keyBytes = new byte[32]; // 256-bit key
            rng.GetBytes(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }
    }
    
    private void LoadAuthenticationState()
    {
        try
        {
            // Load token
            string tokenData = PlayerPrefs.GetString(AUTH_TOKEN_KEY, "");
            if (!string.IsNullOrEmpty(tokenData))
            {
                if (enableEncryption)
                {
                    tokenData = DecryptData(tokenData);
                }
                
                currentToken = JsonUtility.FromJson<MobileAuthToken>(tokenData);
                
                // Check if token is still valid
                if (currentToken != null && currentToken.IsValid)
                {
                    isAuthenticated = true;
                    LogMessage("Authentication restored from saved token");
                }
                else
                {
                    currentToken = null;
                    PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
                }
            }
            
            // Load user profile
            string profileData = PlayerPrefs.GetString(USER_PROFILE_KEY, "");
            if (!string.IsNullOrEmpty(profileData))
            {
                if (enableEncryption)
                {
                    profileData = DecryptData(profileData);
                }
                
                currentUser = JsonUtility.FromJson<MobileUserProfile>(profileData);
                LogMessage($"User profile loaded: {currentUser.userId}");
            }
            
            lastAuthTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            LogError($"Failed to load authentication state: {ex.Message}");
            // Clear corrupted data
            PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
            PlayerPrefs.DeleteKey(USER_PROFILE_KEY);
        }
    }
    
    private void SaveAuthenticationState()
    {
        try
        {
            // Save token
            if (currentToken != null)
            {
                string tokenData = JsonUtility.ToJson(currentToken);
                
                if (enableEncryption)
                {
                    tokenData = EncryptData(tokenData);
                }
                
                PlayerPrefs.SetString(AUTH_TOKEN_KEY, tokenData);
            }
            
            // Save user profile
            if (currentUser != null)
            {
                string profileData = JsonUtility.ToJson(currentUser);
                
                if (enableEncryption)
                {
                    profileData = EncryptData(profileData);
                }
                
                PlayerPrefs.SetString(USER_PROFILE_KEY, profileData);
            }
            
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            LogError($"Failed to save authentication state: {ex.Message}");
        }
    }
    
    public bool AuthenticateAsGuest()
    {
        try
        {
            // Create guest user profile
            currentUser = new MobileUserProfile
            {
                userId = $"guest_{deviceId}",
                deviceId = deviceId,
                platform = Application.platform.ToString(),
                lastActivity = DateTime.UtcNow,
                preferences = new Dictionary<string, object>(),
                isGuest = true
            };
            
            // Create guest token
            currentToken = new MobileAuthToken
            {
                token = GenerateGuestToken(),
                expiryTime = DateTime.UtcNow.AddHours(tokenExpiryHours),
                userId = currentUser.userId,
                metadata = new Dictionary<string, string>
                {
                    ["type"] = "guest",
                    ["device_id"] = deviceId,
                    ["platform"] = Application.platform.ToString()
                }
            };
            
            isAuthenticated = true;
            lastAuthTime = DateTime.UtcNow;
            
            // Save state
            SaveAuthenticationState();
            
            // Notify listeners
            OnAuthenticationChanged?.Invoke(true);
            OnUserProfileUpdated?.Invoke(currentUser);
            
            LogMessage("Guest authentication successful");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Guest authentication failed: {ex.Message}");
            OnAuthError?.Invoke($"Guest authentication failed: {ex.Message}");
            return false;
        }
    }
    
    private string GenerateGuestToken()
    {
        var tokenData = new
        {
            userId = currentUser.userId,
            deviceId = deviceId,
            timestamp = DateTime.UtcNow.Ticks,
            platform = Application.platform.ToString()
        };
        
        string tokenString = JsonUtility.ToJson(tokenData);
        
        // Create simple hash for guest token
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenString));
            return Convert.ToBase64String(hashBytes);
        }
    }
    
    public string GetAuthTokenAsync()
    {
        if (!isAuthenticated || currentToken == null)
        {
            return "";
        }
        
        // Check if token is expired
        if (!currentToken.IsValid)
        {
            OnTokenExpired?.Invoke();
            
            // Try to refresh token
            if (currentUser != null && currentUser.isGuest)
            {
                AuthenticateAsGuest();
                return currentToken?.token ?? "";
            }
            else
            {
                RefreshAuthentication();
                return currentToken?.token ?? "";
            }
        }
        
        return currentToken.token;
    }
    
    public bool RefreshAuthentication()
    {
        try
        {
            if (currentUser == null)
            {
                return false;
            }
            
            if (currentUser.isGuest)
            {
                return AuthenticateAsGuest();
            }
            
            // For regular users, implement token refresh logic here
            // This would typically involve calling your auth service
            
            LogMessage("Authentication refresh attempted");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Authentication refresh failed: {ex.Message}");
            return false;
        }
    }
    
    public bool Logout()
    {
        try
        {
            // Clear authentication state
            isAuthenticated = false;
            currentToken = null;
            currentUser = null;
            
            // Clear saved data
            PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
            PlayerPrefs.DeleteKey(USER_PROFILE_KEY);
            PlayerPrefs.Save();
            
            // Notify listeners
            OnAuthenticationChanged?.Invoke(false);
            
            LogMessage("User logged out successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Logout failed: {ex.Message}");
            return false;
        }
    }
    
    private bool ShouldReauthenticate()
    {
        if (!requireReauthentication)
        {
            return false;
        }
        
        return DateTime.UtcNow - lastAuthTime > TimeSpan.FromMinutes(reauthenticationMinutes);
    }
    
    public void UpdateUserPreference(string key, object value)
    {
        if (currentUser == null)
        {
            return;
        }
        
        if (currentUser.preferences == null)
        {
            currentUser.preferences = new Dictionary<string, object>();
        }
        
        currentUser.preferences[key] = value;
        currentUser.lastActivity = DateTime.UtcNow;
        
        // Save updated profile
        SaveAuthenticationState();
        
        OnUserProfileUpdated?.Invoke(currentUser);
        LogMessage($"User preference updated: {key}");
    }
    
    public T GetUserPreference<T>(string key, T defaultValue = default(T))
    {
        if (currentUser?.preferences == null || !currentUser.preferences.ContainsKey(key))
        {
            return defaultValue;
        }
        
        try
        {
            return (T)currentUser.preferences[key];
        }
        catch
        {
            return defaultValue;
        }
    }
    
    private string EncryptData(string data)
    {
        if (!enableEncryption || string.IsNullOrEmpty(encryptionKey))
        {
            return data;
        }
        
        try
        {
            // Simple encryption implementation
            // In production, use proper encryption libraries
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] keyBytes = Convert.FromBase64String(encryptionKey);
            
            for (int i = 0; i < dataBytes.Length; i++)
            {
                dataBytes[i] ^= keyBytes[i % keyBytes.Length];
            }
            
            return Convert.ToBase64String(dataBytes);
        }
        catch (Exception ex)
        {
            LogError($"Data encryption failed: {ex.Message}");
            return data;
        }
    }
    
    private string DecryptData(string encryptedData)
    {
        if (!enableEncryption || string.IsNullOrEmpty(encryptionKey))
        {
            return encryptedData;
        }
        
        try
        {
            byte[] dataBytes = Convert.FromBase64String(encryptedData);
            byte[] keyBytes = Convert.FromBase64String(encryptionKey);
            
            for (int i = 0; i < dataBytes.Length; i++)
            {
                dataBytes[i] ^= keyBytes[i % keyBytes.Length];
            }
            
            return Encoding.UTF8.GetString(dataBytes);
        }
        catch (Exception ex)
        {
            LogError($"Data decryption failed: {ex.Message}");
            return encryptedData;
        }
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileAuthManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileAuthManager] {message}");
    }
    
    private void OnDestroy()
    {
        // Save state before destroying
        if (isAuthenticated)
        {
            SaveAuthenticationState();
        }
    }
    
    // Public getters for monitoring
    public bool IsAuthenticated => isAuthenticated;
    public MobileUserProfile CurrentUser => currentUser;
    public string DeviceId => deviceId;
    public bool IsGuest => currentUser?.isGuest ?? false;
    public DateTime LastAuthTime => lastAuthTime;
    public bool TokenValid => currentToken?.IsValid ?? false;
}