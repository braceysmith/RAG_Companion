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
    [SerializeField] private string userId = "mobile-user";
    
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
        if (autoRefreshOnStart)
        {
            LoadUserImages();
        }
    }
    
    private void SetupUI()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(LoadUserImages);
        
        if (uploadButton != null)
            uploadButton.onClick.AddListener(OpenImageUpload);
        
        if (statusText != null)
            statusText.text = "Ready to load images";
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
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<UserImagesResponse>(request.downloadHandler.text);
                    
                    if (response.success)
                    {
                        userImages = response.images;
                        UpdateStatus($"Loaded {userImages.Count} images");
                        DisplayImages();
                        OnImagesLoaded?.Invoke(userImages);
                    }
                    else
                    {
                        UpdateStatus("Failed to load images");
                        OnError?.Invoke("Failed to load images from server");
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error parsing response: {ex.Message}");
                    OnError?.Invoke($"Error parsing response: {ex.Message}");
                }
            }
            else
            {
                UpdateStatus($"Error loading images: {request.error}");
                OnError?.Invoke($"Error loading images: {request.error}");
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
            imageComponent.sprite = Sprite.Create(imageCache[image.content_id], 
                new Rect(0, 0, imageCache[image.content_id].width, imageCache[image.content_id].height), 
                Vector2.one * 0.5f);
            yield break;
        }
        
        // Load image from RAG server
        string imageUrl = $"{ragApiUrl}/image/{image.content_id}";
        
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                
                // Cache the texture
                imageCache[image.content_id] = texture;
                
                // Create sprite and assign to image component
                if (imageComponent != null)
                {
                    imageComponent.sprite = Sprite.Create(texture, 
                        new Rect(0, 0, texture.width, texture.height), 
                        Vector2.one * 0.5f);
                }
            }
            else
            {
                Debug.LogError($"Failed to load image {image.content_id}: {request.error}");
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
