using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

[System.Serializable]
public class StoredImage
{
    public string content_id;
    public string file_name;
    public long file_size;
    public string created_at;
    public string generation_prompt;
    public string generation_tool;
    public Dictionary<string, object> metadata;
    public List<string> tags;
    public string file_path;
    
    // New fields for better image access
    public string cloud_url;
    public string access_method;
    public string access_url;
}

[System.Serializable]
public class UserImagesResponse
{
    public bool success;
    public string user_id;
    public List<StoredImage> images;
    public int total_count;
    public int limit;
    public int offset;
}

public class MobileImageManager : MonoBehaviour
{
    [Header("RAG Configuration")]
    [SerializeField] private string ragApiUrl = "https://ragcompanion-production-bf25.up.railway.app";
    [SerializeField] private string userId = "mobile-bracey02"; // Updated to match actual user ID
    
    [Header("UI References")]
    [SerializeField] private Transform imageGalleryContainer;
    [SerializeField] private GameObject imageItemPrefab;
    [SerializeField] private ScrollRect galleryScrollRect;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button uploadButton;
    [SerializeField] private TMP_Text statusText;
    
    [Header("Image Settings")]
    [SerializeField] private int maxImagesToLoad = 50;
    [SerializeField] private bool autoRefreshOnStart = true;
    [SerializeField] private float refreshInterval = 300f; // 5 minutes
    
    // Private variables
    private List<StoredImage> userImages = new List<StoredImage>();
    private Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
    private Coroutine refreshCoroutine;
    private bool isLoading = false;
    
    // Events
    public event Action<List<StoredImage>> OnImagesLoaded;
    public event Action<StoredImage> OnImageSelected;
    public event Action<string> OnError;
    
    private void Start()
    {
        SetupUI();
        ValidateComponents();
        
        if (autoRefreshOnStart)
        {
            LoadUserImages();
        }
    }
    
    private void ValidateComponents()
    {
        Debug.Log("[MobileImageManager] Validating components...");
        
        if (imageGalleryContainer == null)
            Debug.LogError("[MobileImageManager] imageGalleryContainer is null!");
        else
            Debug.Log($"[MobileImageManager] imageGalleryContainer: {imageGalleryContainer.name}");
            
        if (imageItemPrefab == null)
            Debug.LogError("[MobileImageManager] imageItemPrefab is null!");
        else
            Debug.Log($"[MobileImageManager] imageItemPrefab: {imageItemPrefab.name}");
            
        if (galleryScrollRect == null)
            Debug.LogWarning("[MobileImageManager] galleryScrollRect is null");
        else
            Debug.Log($"[MobileImageManager] galleryScrollRect: {galleryScrollRect.name}");
            
        if (refreshButton == null)
            Debug.LogWarning("[MobileImageManager] refreshButton is null");
        else
            Debug.Log($"[MobileImageManager] refreshButton: {refreshButton.name}");
            
        if (uploadButton == null)
            Debug.LogWarning("[MobileImageManager] uploadButton is null");
        else
            Debug.Log($"[MobileImageManager] uploadButton: {uploadButton.name}");
            
        if (statusText == null)
            Debug.LogWarning("[MobileImageManager] statusText is null");
        else
            Debug.Log($"[MobileImageManager] statusText: {statusText.name}");
            
        Debug.Log($"[MobileImageManager] RAG API URL: {ragApiUrl}");
        Debug.Log($"[MobileImageManager] User ID: {userId}");
    }
    
    public void TestConnection()
    {
        StartCoroutine(TestConnectionCoroutine());
    }
    
    public void TestRawResponse()
    {
        StartCoroutine(TestRawResponseCoroutine());
    }
    
    private IEnumerator TestRawResponseCoroutine()
    {
        UpdateStatus("Testing raw response...");
        Debug.Log("[MobileImageManager] Testing raw server response...");
        
        string url = $"{ragApiUrl}/user_images/{userId}?limit=1&content_type=image";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"[MobileImageManager] Raw response test - Status: {request.responseCode}, Result: {request.result}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[MobileImageManager] Raw response text: {responseText}");
                
                // Try to parse as JSON to see what we're getting
                try
                {
                    var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                    Debug.Log($"[MobileImageManager] Parsed JSON keys: {string.Join(", ", jsonResponse.Keys)}");
                    
                    if (jsonResponse.ContainsKey("success"))
                    {
                        Debug.Log($"[MobileImageManager] Success field: {jsonResponse["success"]}");
                    }
                    
                    if (jsonResponse.ContainsKey("images"))
                    {
                        var images = jsonResponse["images"];
                        Debug.Log($"[MobileImageManager] Images field type: {images?.GetType()}, Value: {images}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MobileImageManager] JSON parsing failed: {ex.Message}");
                }
                
                UpdateStatus("Raw response test completed - check console");
            }
            else
            {
                string errorMsg = $"Raw response test failed: {request.error} (HTTP {request.responseCode})";
                UpdateStatus(errorMsg);
                Debug.LogError($"[MobileImageManager] {errorMsg}");
            }
        }
    }
    
    private IEnumerator TestConnectionCoroutine()
    {
        UpdateStatus("Testing connection...");
        Debug.Log("[MobileImageManager] Testing connection to RAG server...");
        
        string testUrl = $"{ragApiUrl}/user_images/{userId}?limit=1&offset=0&content_type=image";
        
        using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
        {
            request.timeout = 10; // 10 second timeout
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"[MobileImageManager] Connection test - Response code: {request.responseCode}, Result: {request.result}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                UpdateStatus("Connection successful");
                Debug.Log("[MobileImageManager] Connection test successful");
            }
            else
            {
                string errorMsg = $"Connection failed: {request.error} (HTTP {request.responseCode})";
                UpdateStatus(errorMsg);
                Debug.LogError($"[MobileImageManager] {errorMsg}");
                
                if (request.responseCode == 0)
                {
                    Debug.LogError("[MobileImageManager] Response code 0 usually means the server is not reachable or the URL is incorrect");
                }
            }
        }
    }
    
    private void SetupUI()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(LoadUserImages);
            Debug.Log("[MobileImageManager] Refresh button configured");
        }
        else
        {
            Debug.LogWarning("[MobileImageManager] Refresh button not assigned");
        }
        
        if (uploadButton != null)
        {
            uploadButton.onClick.RemoveAllListeners();
            uploadButton.onClick.AddListener(OpenImageUpload);
            Debug.Log("[MobileImageManager] Upload button configured");
        }
        else
        {
            Debug.LogWarning("[MobileImageManager] Upload button not assigned");
        }
        
        if (statusText != null)
        {
            statusText.text = "Ready to load images";
            Debug.Log("[MobileImageManager] Status text configured");
        }
        else
        {
            Debug.LogWarning("[MobileImageManager] Status text not assigned");
        }
    }
    
    public void LoadUserImages()
    {
        if (isLoading) return;
        
        StartCoroutine(LoadUserImagesCoroutine());
    }
    
    private IEnumerator LoadUserImagesCoroutine()
    {
        isLoading = true;
        UpdateStatus("Loading images...");
        
        string url = $"{ragApiUrl}/user_images/{userId}?limit={maxImagesToLoad}&offset=0&content_type=image";
        Debug.Log($"[MobileImageManager] Requesting images from: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"[MobileImageManager] Response code: {request.responseCode}, Result: {request.result}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"[MobileImageManager] Response text: {responseText}");
                    
                    var response = JsonConvert.DeserializeObject<UserImagesResponse>(responseText);
                    
                    if (response.success)
                    {
                        userImages = response.images ?? new List<StoredImage>();
                        UpdateStatus($"Loaded {userImages.Count} images");
                        Debug.Log($"[MobileImageManager] Successfully loaded {userImages.Count} images");
                        
                        if (userImages.Count > 0)
                        {
                            DisplayImages();
                            OnImagesLoaded?.Invoke(userImages);
                        }
                        else
                        {
                            UpdateStatus("No images found for this user");
                            Debug.Log("[MobileImageManager] No images found in response");
                        }
                    }
                    else
                    {
                        string errorMsg = "Failed to load images from server";
                        if (response != null && response.images != null)
                        {
                            errorMsg = $"Server returned success=false with {response.images.Count} images";
                        }
                        UpdateStatus(errorMsg);
                        OnError?.Invoke(errorMsg);
                        Debug.LogError($"[MobileImageManager] {errorMsg}");
                        
                        // Log the full response for debugging
                        Debug.LogError($"[MobileImageManager] Full server response: {JsonConvert.SerializeObject(response, Formatting.Indented)}");
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error parsing response: {ex.Message}");
                    OnError?.Invoke($"Error parsing response: {ex.Message}");
                    Debug.LogError($"[MobileImageManager] JSON parsing error: {ex.Message}\nResponse: {request.downloadHandler.text}");
                }
            }
            else
            {
                string errorMsg = $"Error loading images: {request.error} (HTTP {request.responseCode})";
                UpdateStatus(errorMsg);
                OnError?.Invoke(errorMsg);
                Debug.LogError($"[MobileImageManager] {errorMsg}");
            }
        }
        
        isLoading = false;
    }
    
    private void DisplayImages()
    {
        if (imageGalleryContainer == null || imageItemPrefab == null) return;
        
        // Clear existing images
        foreach (Transform child in imageGalleryContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Create image items
        foreach (var image in userImages)
        {
            CreateImageItem(image);
        }
    }
    
    private void CreateImageItem(StoredImage image)
    {
        GameObject itemObj = Instantiate(imageItemPrefab, imageGalleryContainer);
        
        // Set up image item components
        var imageComponent = itemObj.GetComponent<Image>();
        var promptText = itemObj.GetComponentInChildren<TMP_Text>();
        var button = itemObj.GetComponent<Button>();
        
        if (promptText != null)
        {
            promptText.text = image.generation_prompt;
        }
        
        if (button != null)
        {
            button.onClick.AddListener(() => OnImageItemClicked(image));
        }
        
        // Load and display the image
        StartCoroutine(LoadImageForItem(image, imageComponent));
    }
    
    private IEnumerator LoadImageForItem(StoredImage image, Image imageComponent)
    {
        if (imageCache.ContainsKey(image.content_id))
        {
            // Use cached image
            if (imageComponent != null)
            {
                imageComponent.sprite = Sprite.Create(imageCache[image.content_id], 
                    new Rect(0, 0, imageCache[image.content_id].width, imageCache[image.content_id].height), 
                    Vector2.one * 0.5f);
                Debug.Log($"[MobileImageManager] Using cached image for {image.content_id}");
            }
            yield break;
        }
        
        // Determine the best way to load the image
        string imageUrl = null;
        
        if (!string.IsNullOrEmpty(image.access_url))
        {
            // Use the recommended access method
            if (image.access_method == "cloud" && !string.IsNullOrEmpty(image.cloud_url))
            {
                imageUrl = image.cloud_url;
                Debug.Log($"[MobileImageManager] Using cloud URL for {image.content_id}: {imageUrl}");
            }
            else if (image.access_method == "local")
            {
                imageUrl = $"{ragApiUrl}{image.access_url}";
                Debug.Log($"[MobileImageManager] Using local server for {image.content_id}: {imageUrl}");
            }
            else
            {
                imageUrl = $"{ragApiUrl}/image/{image.content_id}";
                Debug.Log($"[MobileImageManager] Using fallback server path for {image.content_id}: {imageUrl}");
            }
        }
        else
        {
            // Fallback to the old method
            imageUrl = $"{ragApiUrl}/image/{image.content_id}";
            Debug.Log($"[MobileImageManager] Using fallback method for {image.content_id}: {imageUrl}");
        }
        
        Debug.Log($"[MobileImageManager] Loading image from: {imageUrl}");
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            Debug.Log($"[MobileImageManager] Image load result: {request.result}, Response code: {request.responseCode}");
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                
                if (texture != null)
                {
                    // Cache the texture
                    imageCache[image.content_id] = texture;
                    
                    // Create sprite and assign to image component
                    if (imageComponent != null)
                    {
                        imageComponent.sprite = Sprite.Create(texture, 
                            new Rect(0, 0, texture.width, texture.height), 
                            Vector2.one * 0.5f);
                        Debug.Log($"[MobileImageManager] Successfully loaded image {image.content_id}: {texture.width}x{texture.height}");
                    }
                    else
                    {
                        Debug.LogWarning($"[MobileImageManager] Image component is null for {image.content_id}");
                    }
                }
                else
                {
                    Debug.LogError($"[MobileImageManager] Texture is null for {image.content_id}");
                }
            }
            else
            {
                Debug.LogError($"[MobileImageManager] Failed to load image {image.content_id}: {request.error} (HTTP {request.responseCode})");
                
                // Try to get more error details
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.LogError($"[MobileImageManager] Error response: {request.downloadHandler.text}");
                }
            }
        }
    }
    
    private void OnImageItemClicked(StoredImage image)
    {
        OnImageSelected?.Invoke(image);
        Debug.Log($"Image selected: {image.generation_prompt}");
    }
    
    public void DeleteImage(string contentId)
    {
        StartCoroutine(DeleteImageCoroutine(contentId));
    }
    
    private IEnumerator DeleteImageCoroutine(string contentId)
    {
        string url = $"{ragApiUrl}/image/{contentId}?user_id={userId}";
        
        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                // Remove from cache and list
                if (imageCache.ContainsKey(contentId))
                {
                    Destroy(imageCache[contentId]);
                    imageCache.Remove(contentId);
                }
                
                userImages.RemoveAll(img => img.content_id == contentId);
                
                // Refresh display
                DisplayImages();
                UpdateStatus($"Image {contentId} deleted successfully");
            }
            else
            {
                UpdateStatus($"Failed to delete image: {request.error}");
                OnError?.Invoke($"Failed to delete image: {request.error}");
            }
        }
    }
    
    private void OpenImageUpload()
    {
        // This would integrate with your existing image upload system
        Debug.Log("Image upload requested");
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[MobileImageManager] {message}");
    }
    
    public void RefreshImages()
    {
        LoadUserImages();
    }
    
    public List<StoredImage> GetUserImages()
    {
        return new List<StoredImage>(userImages);
    }
    
    public StoredImage GetImageById(string contentId)
    {
        return userImages.Find(img => img.content_id == contentId);
    }
    
    public void ClearImageCache()
    {
        foreach (var texture in imageCache.Values)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        imageCache.Clear();
    }
    
    private void OnDestroy()
    {
        ClearImageCache();
        
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && autoRefreshOnStart)
        {
            // Refresh images when app resumes
            LoadUserImages();
        }
    }
}
