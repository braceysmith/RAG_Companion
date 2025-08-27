using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImageGalleryItem : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image imageDisplay;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text timestampText;
    [SerializeField] private TMP_Text fileSizeText;
    [SerializeField] private Button viewButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button shareButton;
    
    [Header("Settings")]
    [SerializeField] private bool showDeleteButton = true;
    [SerializeField] private bool showShareButton = true;
    
    // Data
    private StoredImage imageData;
    private Action<StoredImage> onViewCallback;
    private Action<string> onDeleteCallback;
    private Action<StoredImage> onShareCallback;
    
    public void Initialize(StoredImage image, Action<StoredImage> onView, Action<string> onDelete = null, Action<StoredImage> onShare = null)
    {
        imageData = image;
        onViewCallback = onView;
        onDeleteCallback = onDelete;
        onShareCallback = onShare;
        
        UpdateUI();
        SetupButtons();
    }
    
    private void UpdateUI()
    {
        if (imageData == null) return;
        
        // Set prompt text
        if (promptText != null)
        {
            promptText.text = imageData.generation_prompt;
        }
        
        // Set timestamp
        if (timestampText != null)
        {
            if (DateTime.TryParse(imageData.created_at, out DateTime createdTime))
            {
                timestampText.text = createdTime.ToString("MMM dd, yyyy HH:mm");
            }
            else
            {
                timestampText.text = "Unknown time";
            }
        }
        
        // Set file size
        if (fileSizeText != null)
        {
            fileSizeText.text = FormatFileSize(imageData.file_size);
        }
    }
    
    private void SetupButtons()
    {
        // View button
        if (viewButton != null)
        {
            viewButton.onClick.RemoveAllListeners();
            viewButton.onClick.AddListener(() => onViewCallback?.Invoke(imageData));
        }
        
        // Delete button
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(showDeleteButton && onDeleteCallback != null);
            if (showDeleteButton && onDeleteCallback != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => onDeleteCallback?.Invoke(imageData.content_id));
            }
        }
        
        // Share button
        if (shareButton != null)
        {
            shareButton.gameObject.SetActive(showShareButton && onShareCallback != null);
            if (showShareButton && onShareCallback != null)
            {
                shareButton.onClick.RemoveAllListeners();
                shareButton.onClick.AddListener(() => onShareCallback?.Invoke(imageData));
            }
        }
    }
    
    public void SetImageSprite(Sprite sprite)
    {
        if (imageDisplay != null && sprite != null)
        {
            imageDisplay.sprite = sprite;
        }
    }
    
    public void SetImageTexture(Texture2D texture)
    {
        if (imageDisplay != null && texture != null)
        {
            imageDisplay.sprite = Sprite.Create(texture, 
                new Rect(0, 0, texture.width, texture.height), 
                Vector2.one * 0.5f);
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
    
    public StoredImage GetImageData()
    {
        return imageData;
    }
    
    public void SetDeleteEnabled(bool enabled)
    {
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(enabled);
        }
    }
    
    public void SetShareEnabled(bool enabled)
    {
        if (shareButton != null)
        {
            shareButton.gameObject.SetActive(enabled);
        }
    }
    
    private void OnDestroy()
    {
        // Clean up button listeners
        if (viewButton != null)
            viewButton.onClick.RemoveAllListeners();
        if (deleteButton != null)
            deleteButton.onClick.RemoveAllListeners();
        if (shareButton != null)
            shareButton.onClick.RemoveAllListeners();
    }
}
