using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ChatPostPrefab : MonoBehaviour, IPointerClickHandler
{
    public enum ChatPostType { text, confirmation, goal, resource, image }
    
    public ChatPostType type = ChatPostType.text; // Default to text post type
    public bool human;
    public string goalID;
    public string resourceLink;
    public string resourceID;
    public DateTime originalPostTime;
    
    // Category and subcategory information for handling user data
    [HideInInspector] public string categoryName;
    [HideInInspector] public string subcategoryName;
    [HideInInspector] public int tierIndex;
    
    // Property to access the text content
    public string text { 
        get { return bodyDisplay != null ? bodyDisplay.text : ""; }
        set { 
            if (bodyDisplay != null) 
            {
                bodyDisplay.text = value;
                Debug.Log($"[ChatPostPrefab] Text set successfully: '{value.Substring(0, Math.Min(50, value.Length))}...' (Length: {value.Length})");
            }
            else 
            {
                Debug.LogError($"[ChatPostPrefab] Cannot set text: bodyDisplay is null. Text was: '{value.Substring(0, Math.Min(50, value.Length))}...'");
            }
        }
    }
    
    // Property to access the image content
    public Image image {
        get { return contentImage; }
        set { contentImage = value; }
    }
    
    // Property to access the raw image content (for Texture2D)
    public RawImage rawImage {
        get { return contentRawImage; }
        set { contentRawImage = value; }
    }

    // UI Components
    
    public Color goalBackgroundColor;
    public Color resourceBackgroundColor;
    public Color goalColor = new Color(0.8f, 0.6f, 0.2f);
    public Color resourceColor = new Color(0.2f, 0.6f, 0.8f);
    public TMP_Text typeDisplay;
    public TMP_Text titleDisplay;
    public TMP_Text bodyDisplay;
    public Image backgroundImage;
    public Image contentImage;
    public RawImage contentRawImage;  // For Texture2D content
    
    // Background sprites for different types of posts
    //public Sprite humanBackgroundSprite;  // Custom background for human messages
    public Sprite aiBackgroundSprite;     // Optional custom background for AI messages
    
    private void Start()
    {
        // Auto-find components if not assigned
        EnsureComponentsFound();
        
        // Initialize UI
        UpdateUI();
    }
    
    // Ensure all required components are found
    private void EnsureComponentsFound()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
            
        if (contentImage == null && type == ChatPostType.image)
            contentImage = transform.Find("ContentImage")?.GetComponent<Image>();
            
        if (contentRawImage == null && type == ChatPostType.image)
            contentRawImage = transform.Find("ContentRawImage")?.GetComponent<RawImage>();
            
        if (bodyDisplay == null)
        {
            bodyDisplay = transform.Find("Body")?.GetComponent<TMP_Text>();
            if (bodyDisplay == null)
            {
                bodyDisplay = GetComponentInChildren<TMP_Text>();
            }
            Debug.Log($"[ChatPostPrefab] bodyDisplay found: {(bodyDisplay != null ? "YES" : "NO")} (Path: {transform.name})");
        }
            
        if (typeDisplay == null)
            typeDisplay = transform.Find("Type")?.GetComponent<TMP_Text>();
            
        if (titleDisplay == null)
            titleDisplay = transform.Find("Title")?.GetComponent<TMP_Text>();
    }
    
    // Handle tap/click events on the chat post
    public void OnPointerClick(PointerEventData eventData)
    {
        // Debug log the category information when clicking
        Debug.Log($"Post clicked: Type={type}, Text={text.Substring(0, Math.Min(20, text.Length))}... Category={categoryName}, Subcategory={subcategoryName}, TierIndex={tierIndex}");
        
        // Get the current need information from NeedsManager if category info is missing
        if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(subcategoryName))
        {
            /*
            NeedsManager needsManager = NeedsManager.Instance;
            if (needsManager != null && needsManager.currentState == NeedsManager.ManagerState.ChatWindow)
            {
                int categoryIndex = needsManager.currentCategoryIndex;
                int needIndex = needsManager.currentNeedIndex;
                
                if (categoryIndex >= 0 && categoryIndex < needsManager.categoriesData.Length)
                {
                    categoryName = needsManager.categoriesData[categoryIndex].Name;
                    
                    if (needIndex >= 0 && needIndex < needsManager.categoriesData[categoryIndex].needs.Length)
                    {
                        subcategoryName = needsManager.categoriesData[categoryIndex].needs[needIndex].Name;
                        tierIndex = needsManager.categoriesData[categoryIndex].needs[needIndex].currentTier;
                        
                        Debug.Log($"Updated category info from NeedsManager: Category={categoryName}, Subcategory={subcategoryName}, TierIndex={tierIndex}");
                    }
                }
            }
            */
        }
        
        // Handle different types of posts
        if (!human)
        {
            /*
            if (type == ChatPostType.resource && ResourceMenu.Instance != null)
            {
                // Open resource menu for resource posts
                ResourceMenu.Instance.OpenMenu(this, categoryName, subcategoryName, tierIndex);
            }
            else if (type == ChatPostType.text && PostMenu.Instance != null)
            {
                // Open post menu for text posts
                PostMenu.Instance.OpenMenu(this, categoryName, subcategoryName, tierIndex);
            }
            */
            // For other types (e.g., goal, confirmation), just handle as normal clicks
        }
        // If there's a resource link, open it in a browser
        else if (!string.IsNullOrEmpty(resourceLink))
        {
            OpenURL(resourceLink);
        }
        // Otherwise, copy the text content to clipboard
        else if (!string.IsNullOrEmpty(text))
        {
            CopyToClipboard(text);
        }
    }
    
    // Helper method to open a URL in the default browser
    private void OpenURL(string url)
    {
        // Make sure the URL has a proper scheme
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }
        
        Debug.Log("Opening URL: " + url);
        Application.OpenURL(url);
    }
    
    // Helper method to copy text to the clipboard
    private void CopyToClipboard(string textToCopy)
    {
        GUIUtility.systemCopyBuffer = textToCopy;
        Debug.Log("Copied to clipboard: " + textToCopy);
        
        // Show visual feedback
        StartCoroutine(FlashBackground());
    }
    
    // Visual feedback when text is copied
    private System.Collections.IEnumerator FlashBackground()
    {
        if (backgroundImage == null) yield break;
        
        // Store original color
        Color originalColor = backgroundImage.color;
        
        // Flash to highlight color
        backgroundImage.color = new Color(0.4f, 0.8f, 1f);
        
        // Wait briefly
        yield return new WaitForSeconds(0.2f);
        
        // Return to original color
        backgroundImage.color = originalColor;
    }
    
    public void UpdateUI()
    {
        // Update text displays
        if (bodyDisplay != null)
            bodyDisplay.gameObject.SetActive(!string.IsNullOrEmpty(bodyDisplay.text));
            
        // Make sure title display is visible if it has content
        if (titleDisplay != null)
            titleDisplay.gameObject.SetActive(!string.IsNullOrEmpty(titleDisplay.text));
            
        // Handle type-specific styling
        if (typeDisplay != null)
        {
            switch (type)
            {
                case ChatPostType.goal:
                    // Just display "GOAL" - no need to repeat information from the title
                    typeDisplay.text = "GOAL";
                    typeDisplay.gameObject.SetActive(true);
                    
                    // Make sure the type display has the goal color
                    typeDisplay.color = goalColor;
                    
                    // Set the background color
                    if (backgroundImage != null)
                        backgroundImage.color = goalBackgroundColor;
                    break;
                    
                case ChatPostType.resource:
                    // Just display "RESOURCE" - no need to repeat information from the title
                    typeDisplay.text = "RESOURCE";
                    typeDisplay.gameObject.SetActive(true);
                    
                    // Make sure the type display has the resource color
                    typeDisplay.color = resourceColor;
                    
                    // Set the background color
                    if (backgroundImage != null)
                        backgroundImage.color = resourceBackgroundColor;
                    break;
                    
                case ChatPostType.confirmation:
                    typeDisplay.text = "CONFIRM";
                    typeDisplay.gameObject.SetActive(true);
                    break;
                    
                default:
                    typeDisplay.gameObject.SetActive(false);
                    break;
            }
        }
        
        // Set image if this is an image type post
        if (type == ChatPostType.image && contentImage != null)
        {
            contentImage.gameObject.SetActive(true);
            
            // Make sure the image preserves its aspect ratio
            if (contentImage.sprite != null)
            {
                // Set the image component to preserve aspect ratio
                contentImage.preserveAspect = true;
                
                // Adjust the content image's RectTransform to fit the actual image dimensions
                RectTransform imageRT = contentImage.GetComponent<RectTransform>();
                if (imageRT != null)
                {
                    // Get the sprite dimensions
                    float spriteWidth = contentImage.sprite.rect.width;
                    float spriteHeight = contentImage.sprite.rect.height;
                    float aspectRatio = spriteWidth / spriteHeight;
                    
                    // Set a maximum width (you might want to adjust this based on your UI)
                    float maxWidth = 400f;
                    
                    // Calculate the height based on the aspect ratio
                    float width = Mathf.Min(maxWidth, spriteWidth);
                    float height = width / aspectRatio;
                    
                    // Set the size
                    imageRT.sizeDelta = new Vector2(width, height);
                }
            }
        }
        else if (contentImage != null)
        {
            contentImage.gameObject.SetActive(false);
        }
        
        // Style based on human/AI
        if (backgroundImage != null)
        {
            if (human)
            {
                // For human posts, turn off the background image completely
                backgroundImage.enabled = false;
            }
            else
            {
                // Apply AI-specific styling
                // Set the custom background sprite if provided
                if (aiBackgroundSprite != null)
                {
                    backgroundImage.sprite = aiBackgroundSprite;
                    backgroundImage.color = Color.white; // Reset to white to show sprite properly
                }
                else
                {
                    // Fallback to color if no sprite is set
                    backgroundImage.color = new Color(0.9f, 0.9f, 0.9f);
                }
            }
        }
        
        // Position differently based on human/AI
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // Different alignment for human vs AI messages
            if (human)
            {
                // For human messages, align to the right
                rt.pivot = new Vector2(1, 0.5f);
                rt.anchorMin = new Vector2(1, 0.5f);
                rt.anchorMax = new Vector2(1, 0.5f);
                
                // Add some right margin
                rt.anchoredPosition = new Vector2(-10f, rt.anchoredPosition.y);
                
                // Make text right-aligned
                if (bodyDisplay != null)
                {
                    bodyDisplay.alignment = TextAlignmentOptions.Right;
                }
                if (typeDisplay != null)
                {
                    typeDisplay.alignment = TextAlignmentOptions.Right;
                }
                if (titleDisplay != null)
                {
                    titleDisplay.alignment = TextAlignmentOptions.Right;
                }
            }
            else
            {
                // For AI messages, align to the left
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                
                // Add some left margin
                rt.anchoredPosition = new Vector2(10f, rt.anchoredPosition.y);
                
                // Make text left-aligned
                if (bodyDisplay != null)
                {
                    bodyDisplay.alignment = TextAlignmentOptions.Left;
                }
                if (typeDisplay != null)
                {
                    typeDisplay.alignment = TextAlignmentOptions.Left;
                }
                if (titleDisplay != null)
                {
                    titleDisplay.alignment = TextAlignmentOptions.Left;
                }
            }
        }
    }
    
    // Method to initialize the post with data (no image)
    public void Initialize(ChatPostType postType, string postText, bool isHuman, 
                          string postGoalID = "", string postResourceID = "", 
                          string link = "", string category = "", 
                          string subcategory = "", int tier = 0)
    {
        type = postType;
        human = isHuman;
        
        goalID = postGoalID;
        resourceID = postResourceID;
        resourceLink = link;
        originalPostTime = DateTime.Now;
        
        // Set category information for user data management
        categoryName = category;
        subcategoryName = subcategory;
        tierIndex = tier;
        
        // Ensure components are found before setting text
        EnsureComponentsFound();
        
        // Now set the text after components are guaranteed to be found
        text = postText;
        
        // Hide both image components for text-only posts
        if (contentImage != null) contentImage.gameObject.SetActive(false);
        if (contentRawImage != null) contentRawImage.gameObject.SetActive(false);
        
        UpdateUI();
    }
    
    // Method to initialize the post with Image/Sprite data
    public void InitializeWithImage(ChatPostType postType, string postText, bool isHuman, 
                                   Image postImage, string postGoalID = "", 
                                   string postResourceID = "", string link = "",
                                   string category = "", string subcategory = "", int tier = 0)
    {
        type = postType;
        human = isHuman;
        
        goalID = postGoalID;
        resourceID = postResourceID;
        resourceLink = link;
        originalPostTime = DateTime.Now;
        
        // Set category information for user data management
        categoryName = category;
        subcategoryName = subcategory;
        tierIndex = tier;
        
        // Ensure components are found before setting text
        EnsureComponentsFound();
        
        // Now set the text after components are guaranteed to be found
        text = postText;
        
        if (postImage != null && contentImage != null)
        {
            // Set the sprite
            contentImage.sprite = postImage.sprite;
            
            // Make sure the aspect ratio is preserved
            contentImage.preserveAspect = true;
            
            // Show Image component, hide RawImage
            contentImage.gameObject.SetActive(true);
            if (contentRawImage != null) contentRawImage.gameObject.SetActive(false);
        }
        
        UpdateUI();
    }
    
    // Method to initialize the post with Texture2D data
    public void InitializeWithTexture(ChatPostType postType, string postText, bool isHuman, 
                                     Texture2D postTexture, string postGoalID = "", 
                                     string postResourceID = "", string link = "",
                                     string category = "", string subcategory = "", int tier = 0)
    {
        type = postType;
        human = isHuman;
        
        goalID = postGoalID;
        resourceID = postResourceID;
        resourceLink = link;
        originalPostTime = DateTime.Now;
        
        // Set category information for user data management
        categoryName = category;
        subcategoryName = subcategory;
        tierIndex = tier;
        
        // Ensure components are found before setting text
        EnsureComponentsFound();
        
        // Now set the text after components are guaranteed to be found
        text = postText;
        
        if (postTexture != null && contentRawImage != null)
        {
            // Set the texture directly - no conversion needed!
            contentRawImage.texture = postTexture;
            
            // Preserve aspect ratio
            contentRawImage.SetNativeSize();
            
            // Show RawImage component, hide Image
            contentRawImage.gameObject.SetActive(true);
            if (contentImage != null) contentImage.gameObject.SetActive(false);
        }
        
        UpdateUI();
    }
    
    /// <summary>
    /// Updates just the text content of this post
    /// </summary>
    public void UpdateText(string newText)
    {
        text = newText; // This uses the property which updates bodyDisplay
        UpdateUI();
    }
}