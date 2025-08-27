# Unity Client Image Storage Troubleshooting Guide

This guide helps you fix common issues with the image storage system in your Unity client.

## 🚨 Common Issues & Solutions

### 1. "Failed to Load" Status

**Symptoms:**
- Status text shows "Failed to Load"
- No images appear in gallery
- Console shows connection errors

**Possible Causes & Solutions:**

#### A. RAG Server Not Running
```
❌ Error loading images: Cannot connect to destination host
```
**Solution:** Start your RAG server first, then try loading images again.

#### B. Wrong Server URL
```
❌ Error loading images: Cannot connect to destination host (HTTP 0)
```
**Solution:** Check the `ragApiUrl` in `MobileImageManager`:
```csharp
[SerializeField] private string ragApiUrl = "https://ragcompanion-production-bf25.up.railway.app";
```
- Verify the URL is correct
- Test if you can access it in a web browser
- Check if it's `http://localhost:8000` for local development

#### C. Wrong User ID
```
✅ Loaded 0 images
```
**Solution:** Check the `userId` in `MobileImageManager`:
```csharp
[SerializeField] private string userId = "mobile-user";
```
- This should match the user ID in your RAG server
- Check your database to see what user IDs exist

#### D. Network/Firewall Issues
```
❌ Error loading images: Request timeout
```
**Solution:** 
- Check your network connection
- Verify firewall allows the connection
- Try increasing timeout values

### 2. Images Don't Show in Gallery

**Symptoms:**
- Status shows "Loaded X images" but gallery is empty
- Console shows images loaded but nothing displays

**Possible Causes & Solutions:**

#### A. Missing UI Components
```
❌ imageGalleryContainer is null!
❌ imageItemPrefab is null!
```
**Solution:** Assign the required UI components in the inspector:
- `imageGalleryContainer`: Transform where images will be displayed
- `imageItemPrefab`: Prefab for individual image items
- `galleryScrollRect`: ScrollRect for the gallery (optional)
- `refreshButton`: Button to refresh images (optional)
- `statusText`: Text component to show status (optional)

#### B. Image Item Prefab Issues
```
❌ Image component is null for {content_id}
```
**Solution:** Check your `imageItemPrefab`:
- Must have an `Image` component
- Must have a `Button` component (optional)
- Must have `TMP_Text` components for prompt/status (optional)

#### C. Layout Issues
```
✅ Images loaded but not visible
```
**Solution:** Check the layout:
- Ensure `imageGalleryContainer` has proper layout components
- Check if images are being created but positioned off-screen
- Verify the container has sufficient size and proper anchoring

### 3. Generated Images Not Appearing

**Symptoms:**
- Images generate successfully in chat
- But don't appear in the gallery
- Status shows "No images found"

**Possible Causes & Solutions:**

#### A. Different User IDs
**Problem:** Chat uses one user ID, gallery uses another
**Solution:** Ensure both systems use the same user ID:
```csharp
// In MobileRealtimeChat
["user_id"] = userId

// In MobileImageManager  
[SerializeField] private string userId = "mobile-user";
```

#### B. Database Issues
**Problem:** Images saved but not retrievable
**Solution:** Check your RAG server:
- Verify database connection
- Check if `multimedia_content` table exists
- Verify images are being saved with correct user_id

#### C. File Storage Issues
**Problem:** Images saved to database but files missing
**Solution:** Check server storage:
- Verify `multimedia/images/` directory exists
- Check file permissions
- Look for server error logs

## 🔍 Debugging Steps

### Step 1: Check Console Logs
Look for these debug messages:
```
[MobileImageManager] Validating components...
[MobileImageManager] Requesting images from: {URL}
[MobileImageManager] Response code: {CODE}, Result: {RESULT}
[MobileImageManager] Response text: {JSON}
```

### Step 2: Test Server Connection
Use the debug script to test your RAG server:
```bash
cd RAG/rag_service
python debug_image_endpoints.py
```

### Step 3: Verify Unity Configuration
Check these settings in `MobileImageManager`:
- ✅ `ragApiUrl` points to your server
- ✅ `userId` matches your database
- ✅ All UI components are assigned
- ✅ `autoRefreshOnStart` is enabled

### Step 4: Test Individual Components
Test each part separately:
1. **Server connectivity** - Can you reach the server?
2. **Database access** - Are images being saved?
3. **File storage** - Are image files being created?
4. **API endpoints** - Do they return correct data?
5. **Unity UI** - Are components properly configured?

## 🛠️ Quick Fixes

### Fix 1: Reset and Reload
```csharp
// In MobileImageManager
public void ForceRefresh()
{
    ClearImageCache();
    userImages.Clear();
    LoadUserImages();
}
```

### Fix 2: Check User ID Match
```csharp
// Ensure this matches in both scripts
private string userId = "your-actual-user-id";
```

### Fix 3: Test Connection First
```csharp
// Call this before loading images
imageManager.TestConnection();
```

### Fix 4: Manual Image Load
```csharp
// Force load specific user
imageManager.LoadUserImages();
```

## 📋 Checklist

Before reporting an issue, verify:

- [ ] RAG server is running and accessible
- [ ] Database connection is working
- [ ] `multimedia/images/` directory exists and is writable
- [ ] User ID matches between chat and gallery
- [ ] All UI components are assigned in inspector
- [ ] Console shows no null reference errors
- [ ] Network requests are reaching the server
- [ ] Server is returning valid JSON responses

## 🆘 Still Having Issues?

If the problem persists:

1. **Check Unity Console** for specific error messages
2. **Run the debug script** to test server endpoints
3. **Verify database content** - are images actually being saved?
4. **Check server logs** for backend errors
5. **Test with a simple HTTP client** (Postman, curl) to isolate the issue

## 🔧 Advanced Debugging

### Enable Verbose Logging
```csharp
// Add this to see all network requests
Debug.Log($"[MobileImageManager] Full URL: {url}");
Debug.Log($"[MobileImageManager] Request headers: {request.GetRequestHeader("Content-Type")}");
```

### Test Individual Endpoints
```csharp
// Test just the user images endpoint
public void TestUserImagesEndpoint()
{
    StartCoroutine(TestUserImagesCoroutine());
}
```

### Monitor Network Traffic
Use Unity's Network Profiler or external tools to see exactly what's being sent/received.

---

**Remember:** Most issues are either configuration problems (wrong URLs, user IDs) or missing UI component assignments. Start with the basics and work your way up!
