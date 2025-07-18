# Unity RAG Companion System - Mobile Deployment Guide

## 📱 **Mobile Architecture Overview**

The mobile version uses a **hybrid cloud architecture** that's optimized for iOS and Android:

```
[Mobile Unity App] → [Cloud RAG Service] → [Vector Database]
       ↓                     ↓
[Local SQLite Cache] ← [Mobile Optimizations]
```

### **Key Mobile Features:**
- ✅ **Offline Mode** with local SQLite cache
- ✅ **Battery Optimization** with adaptive performance
- ✅ **Network Optimization** with data usage monitoring
- ✅ **Cloud Integration** with automatic sync
- ✅ **Platform-specific** iOS and Android features

---

## **🚀 Phase 1: Cloud RAG Service Deployment**

### **Step 1: Choose Cloud Platform**

#### **Option A: Railway (Recommended for beginners)**
```bash
# Install Railway CLI
npm install -g @railway/cli

# Login to Railway
railway login

# Deploy RAG service
cd rag_service
railway up
```

#### **Option B: Heroku**
```bash
# Install Heroku CLI
# Create Heroku app
heroku create your-rag-app

# Deploy
git push heroku main
```

#### **Option C: AWS (Advanced)**
```bash
# Use AWS ECS or Lambda
# Configure CloudFormation template
# Deploy with AWS CLI
```

### **Step 2: Configure Cloud Database**

#### **For Railway:**
```bash
# Add PostgreSQL service
railway add postgresql

# Add pgvector extension
railway run -- psql $DATABASE_URL -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

#### **For Heroku:**
```bash
# Add Heroku Postgres
heroku addons:create heroku-postgresql:hobby-dev

# Add pgvector
heroku pg:psql -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

#### **For AWS:**
```bash
# Use RDS with pgvector
# Configure connection string
# Set up security groups
```

### **Step 3: Update RAG Service for Mobile**

Edit `rag_service/rag_api.py`:
```python
# Add mobile-specific endpoints
@app.post("/mobile/query")
async def mobile_query(request: MobileRAGQueryRequest):
    # Mobile-optimized query handling
    response = await process_mobile_query(request)
    return response

@app.post("/mobile/sync")
async def mobile_sync(request: MobileSyncRequest):
    # Sync mobile cache with cloud
    return await sync_mobile_data(request)

# Add CORS for mobile
from fastapi.middleware.cors import CORSMiddleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
```

### **Step 4: Configure Environment Variables**

Create `.env` file:
```env
# Cloud Configuration
OPENAI_API_KEY=your_openai_api_key
DATABASE_URL=your_cloud_database_url
REDIS_URL=your_redis_url  # Optional for caching

# Mobile Settings
MOBILE_TOKEN_LIMIT=4000
MOBILE_CACHE_EXPIRY=1800
ENABLE_MOBILE_OPTIMIZATIONS=true

# Security
JWT_SECRET=your_jwt_secret
CORS_ORIGINS=*
```

### **Step 5: Deploy and Test**

```bash
# Deploy to cloud
railway deploy  # or heroku deploy

# Test cloud API
curl -X POST https://your-app.railway.app/mobile/query \
  -H "Content-Type: application/json" \
  -d '{"query": "Hello", "user_id": "mobile-test", "top_k": 3}'
```

---

## **📲 Phase 2: Unity Mobile Project Setup**

### **Step 1: Create Mobile Scene**

1. **Create new scene** named "MobileRAGScene"
2. **Add Mobile Canvas** with appropriate settings:
   ```csharp
   // Canvas settings for mobile
   Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
   CanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
   CanvasScaler.referenceResolution = new Vector2(1080, 1920);
   ```

### **Step 2: Configure Mobile RAG System**

Create main GameObject with `MobileRAGCompanionSystem`:
```csharp
// In Unity Inspector:
cloudRAGUrl = "https://your-app.railway.app"
userId = "mobile-user-123"
enableOfflineMode = true
enableAutoSync = true
enableBatteryOptimization = true
```

### **Step 3: Setup Mobile Components**

The system will auto-create these components:
- **MobileRAGClient** - Cloud communication
- **MobileNetworkManager** - Network monitoring
- **MobileConversationCache** - Local SQLite storage
- **MobileAuthManager** - Authentication
- **MobileContextAssembler** - Context optimization

### **Step 4: Configure Unity Project Settings**

#### **Player Settings:**
```csharp
// Build Settings
PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
PlayerSettings.statusBarHidden = false;
PlayerSettings.allowedAutorotateToPortrait = true;
PlayerSettings.allowedAutorotateToLandscapeLeft = false;
PlayerSettings.allowedAutorotateToLandscapeRight = false;
```

#### **iOS Settings:**
```csharp
PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
PlayerSettings.iOS.minimumVersionSupported = "12.0";
PlayerSettings.iOS.requiresFullScreen = false;
```

#### **Android Settings:**
```csharp
PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel21;
PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
PlayerSettings.Android.androidTargetArchitectures = AndroidArchitecture.ARM64;
```

### **Step 5: Add Required Packages**

Update `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.unity.inputsystem": "1.7.0",
    "com.unity.mobile.android-provider": "1.0.11",
    "com.unity.mobile.notifications": "2.3.2",
    "com.unity.services.core": "1.12.5"
  }
}
```

---

## **🎯 Phase 3: Mobile-Specific Features**

### **Step 1: iOS Implementation**

Create `Plugins/iOS/MobileRAGiOS.mm`:
```objc
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" {
    // iOS-specific functions
    void requestMicrophonePermission() {
        [[AVAudioSession sharedInstance] requestRecordPermission:^(BOOL granted) {
            // Handle permission response
        }];
    }
    
    void enableBackgroundModes() {
        // Configure background app refresh
    }
    
    void optimizeForBattery() {
        // Implement iOS battery optimizations
    }
}
```

### **Step 2: Android Implementation**

Create `Plugins/Android/MobileRAGAndroid.java`:
```java
package com.yourcompany.mobilerag;

import android.content.Context;
import android.os.PowerManager;
import android.net.ConnectivityManager;

public class MobileRAGAndroid {
    
    public static void requestPermissions(Context context) {
        // Request Android permissions
    }
    
    public static void optimizeForBattery(Context context) {
        PowerManager pm = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
        // Implement battery optimizations
    }
    
    public static boolean isNetworkConnected(Context context) {
        ConnectivityManager cm = (ConnectivityManager) context.getSystemService(Context.CONNECTIVITY_MANAGER);
        return cm.getActiveNetworkInfo() != null;
    }
}
```

### **Step 3: Platform-Specific Code**

```csharp
public class MobilePlatformManager : MonoBehaviour
{
    void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        RequestiOSPermissions();
#elif UNITY_ANDROID && !UNITY_EDITOR
        RequestAndroidPermissions();
#endif
    }
    
    void RequestiOSPermissions()
    {
        // iOS permission requests
        requestMicrophonePermission();
    }
    
    void RequestAndroidPermissions()
    {
        // Android permission requests
        if (Application.platform == RuntimePlatform.Android)
        {
            AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject context = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
            
            AndroidJavaClass mobileRAG = new AndroidJavaClass("com.yourcompany.mobilerag.MobileRAGAndroid");
            mobileRAG.CallStatic("requestPermissions", context);
        }
    }
}
```

---

## **🔧 Phase 4: Testing and Optimization**

### **Step 1: Local Testing**

```bash
# Test in Unity Editor
1. Play the scene
2. Check console for initialization messages
3. Test basic queries
4. Verify offline mode

# Test network simulation
1. Disable network in editor
2. Verify offline functionality
3. Re-enable network
4. Check sync functionality
```

### **Step 2: Device Testing**

#### **iOS Testing:**
```bash
# Build and deploy to iOS device
1. Connect iOS device
2. Build and Run
3. Test on device
4. Check Xcode console for logs

# Test iOS-specific features
1. Background app refresh
2. Battery optimization
3. Network switching
4. Microphone permissions
```

#### **Android Testing:**
```bash
# Build and deploy to Android device
1. Connect Android device
2. Build and Run
3. Test on device
4. Check logcat for logs

# Test Android-specific features
1. Background processing
2. Battery optimization
3. Network changes
4. Permission handling
```

### **Step 3: Performance Optimization**

#### **Memory Optimization:**
```csharp
// In MobileRAGCompanionSystem
[Header("Memory Management")]
[SerializeField] private int maxMemoryUsageMB = 100;
[SerializeField] private bool enableMemoryProfiling = true;

private void OptimizeMemory()
{
    // Clear unused caches
    Resources.UnloadUnusedAssets();
    GC.Collect();
    
    // Monitor memory usage
    long memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024;
    if (memoryUsage > maxMemoryUsageMB)
    {
        ClearCaches();
    }
}
```

#### **Battery Optimization:**
```csharp
private void OptimizeForBattery()
{
    float batteryLevel = SystemInfo.batteryLevel;
    
    if (batteryLevel < 0.2f)
    {
        // Aggressive optimization
        Application.targetFrameRate = 30;
        QualitySettings.SetQualityLevel(0);
        
        // Reduce network requests
        maxConcurrentRequests = 1;
        requestTimeout = 10f;
    }
    else if (batteryLevel < 0.5f)
    {
        // Moderate optimization
        Application.targetFrameRate = 45;
        QualitySettings.SetQualityLevel(1);
    }
}
```

---

## **📊 Phase 5: Monitoring and Analytics**

### **Step 1: Performance Monitoring**

```csharp
public class MobileAnalytics : MonoBehaviour
{
    [Header("Analytics")]
    [SerializeField] private bool enableAnalytics = true;
    [SerializeField] private string analyticsEndpoint = "https://your-analytics.com";
    
    public void TrackEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (!enableAnalytics) return;
        
        var eventData = new
        {
            event_name = eventName,
            platform = Application.platform.ToString(),
            app_version = Application.version,
            user_id = userId,
            timestamp = DateTime.UtcNow,
            parameters = parameters
        };
        
        // Send to analytics service
        _ = SendAnalyticsEvent(eventData);
    }
    
    private async Task SendAnalyticsEvent(object eventData)
    {
        try
        {
            string json = JsonConvert.SerializeObject(eventData);
            // Send to analytics service
        }
        catch (Exception ex)
        {
            Debug.LogError($"Analytics error: {ex.Message}");
        }
    }
}
```

### **Step 2: Crash Reporting**

```csharp
public class MobileCrashReporter : MonoBehaviour
{
    void Start()
    {
        Application.logMessageReceived += HandleLogMessage;
    }
    
    void HandleLogMessage(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            ReportCrash(logString, stackTrace);
        }
    }
    
    private void ReportCrash(string error, string stackTrace)
    {
        var crashData = new
        {
            error = error,
            stack_trace = stackTrace,
            platform = Application.platform.ToString(),
            device_model = SystemInfo.deviceModel,
            os_version = SystemInfo.operatingSystem,
            app_version = Application.version,
            timestamp = DateTime.UtcNow
        };
        
        // Send to crash reporting service
        _ = SendCrashReport(crashData);
    }
}
```

---

## **🚀 Phase 6: App Store Deployment**

### **Step 1: iOS App Store**

#### **Prepare iOS Build:**
```bash
# In Unity
1. File > Build Settings
2. Select iOS platform
3. Configure Player Settings:
   - Bundle Identifier: com.yourcompany.mobilerag
   - Version: 1.0.0
   - Minimum iOS Version: 12.0
4. Build

# In Xcode
1. Open generated Xcode project
2. Configure signing & capabilities
3. Add required permissions in Info.plist:
   - NSMicrophoneUsageDescription
   - NSNetworkVolumesUsageDescription
4. Archive and upload to App Store Connect
```

#### **App Store Metadata:**
```
App Name: RAG Companion
Subtitle: AI Assistant with Knowledge
Description: Intelligent AI companion with advanced knowledge retrieval capabilities, optimized for mobile devices.

Keywords: AI, assistant, knowledge, RAG, companion, mobile, intelligent, chat

App Category: Productivity
Content Rating: 4+
```

### **Step 2: Google Play Store**

#### **Prepare Android Build:**
```bash
# In Unity
1. File > Build Settings
2. Select Android platform
3. Configure Player Settings:
   - Package Name: com.yourcompany.mobilerag
   - Version: 1.0.0
   - Minimum API Level: 21
4. Build AAB (Android App Bundle)

# Upload to Google Play Console
1. Create app in Google Play Console
2. Upload AAB file
3. Configure app details
4. Set up content rating
5. Submit for review
```

#### **Google Play Metadata:**
```
App Title: RAG Companion
Short Description: AI assistant with knowledge retrieval
Full Description: Advanced AI companion with retrieval-augmented generation capabilities, offline mode, and mobile optimization.

Category: Productivity
Content Rating: Everyone
```

---

## **🔧 Troubleshooting Guide**

### **Common Issues:**

#### **1. Cloud Connection Issues**
```bash
# Check cloud service status
curl https://your-app.railway.app/health

# Verify network connectivity
# Check authentication tokens
# Review API endpoints
```

#### **2. SQLite Issues**
```bash
# Check database file permissions
# Verify SQLite dll inclusion
# Review database connection strings
```

#### **3. Performance Issues**
```bash
# Monitor memory usage
# Check battery optimization
# Review network usage
# Optimize token budgets
```

#### **4. Platform-Specific Issues**
```bash
# iOS: Check Xcode logs
# Android: Check logcat
# Review platform permissions
# Verify native plugins
```

---

## **✅ Deployment Checklist**

### **Pre-Deployment:**
- [ ] Cloud RAG service deployed and tested
- [ ] Database configured with pgvector
- [ ] Unity project builds successfully
- [ ] All mobile components integrated
- [ ] Performance testing completed
- [ ] Memory optimization implemented
- [ ] Battery optimization configured
- [ ] Network optimization enabled

### **iOS Deployment:**
- [ ] iOS build configured
- [ ] App Store metadata prepared
- [ ] Permissions configured
- [ ] Testing on physical devices
- [ ] Crash reporting enabled
- [ ] Analytics integrated

### **Android Deployment:**
- [ ] Android build configured
- [ ] Google Play metadata prepared
- [ ] Permissions configured
- [ ] Testing on physical devices
- [ ] Crash reporting enabled
- [ ] Analytics integrated

---

## **🎯 Success Metrics**

### **Technical Metrics:**
- **Response Time**: < 2 seconds average
- **Cache Hit Rate**: > 80%
- **Battery Usage**: < 5% per hour
- **Memory Usage**: < 100MB
- **Network Usage**: < 10MB per session

### **User Experience:**
- **App Rating**: > 4.0 stars
- **Session Duration**: > 5 minutes average
- **Retention Rate**: > 60% after 7 days
- **Crash Rate**: < 0.5%

---

## **🚀 Next Steps**

1. **Deploy cloud RAG service** to your chosen platform
2. **Configure Unity mobile project** with mobile components
3. **Test on physical devices** for both iOS and Android
4. **Optimize performance** based on testing results
5. **Submit to app stores** with proper metadata
6. **Monitor analytics** and gather user feedback
7. **Iterate and improve** based on user data

Your mobile RAG companion system is now ready for production deployment! 🎉