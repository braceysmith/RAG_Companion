using System;
using System.IO;
using UnityEngine;

[System.Serializable]
public class RAGSettings
{
    [Header("API Configuration")]
    public string openAIAPIKey = "";
    public string modelName = "gpt-4o-mini";
    public float temperature = 0.7f;
    public int maxTokens = 1000;
    
    [Header("RAG Settings")]
    public bool enableRAG = true;
    public int maxRetrievalResults = 5;
    public float relevanceThreshold = 0.1f;
    public int maxContextLength = 2000;
    
    [Header("Companion Settings")]
    public string companionName = "Assistant";
    public string companionPersonality = "helpful, friendly, and knowledgeable";
    public bool enableQuickCommands = true;
    public bool enableVoiceInput = false;
    
    [Header("UI Settings")]
    public int maxMessagesDisplayed = 50;
    public bool showDocumentRetrievals = true;
    public bool enableTypingIndicator = true;
    public float autoScrollDelay = 0.1f;
    
    [Header("Documents")]
    public string documentsPath = "Documents";
    public string[] supportedFileTypes = { ".txt", ".md", ".json" };
    public bool autoLoadDocuments = true;
    public bool enableDocumentWatching = false;
}

[CreateAssetMenu(fileName = "RAGConfiguration", menuName = "RAG Companion/Configuration")]
public class RAGConfiguration : ScriptableObject
{
    [SerializeField] private RAGSettings settings = new RAGSettings();
    
    public RAGSettings Settings => settings;
    
    private static RAGConfiguration instance;
    public static RAGConfiguration Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<RAGConfiguration>("RAGConfiguration");
                if (instance == null)
                {
                    instance = CreateInstance<RAGConfiguration>();
                    Debug.LogWarning("RAGConfiguration not found in Resources. Using default settings.");
                }
            }
            return instance;
        }
    }
    
    public void SaveSettings()
    {
        try
        {
            string json = JsonUtility.ToJson(settings, true);
            string path = Path.Combine(Application.persistentDataPath, "rag_settings.json");
            File.WriteAllText(path, json);
            Debug.Log($"RAG settings saved to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save RAG settings: {ex.Message}");
        }
    }
    
    public void LoadSettings()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "rag_settings.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(json, settings);
                Debug.Log("RAG settings loaded from persistent storage");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load RAG settings: {ex.Message}");
        }
    }
    
    public void ResetToDefaults()
    {
        settings = new RAGSettings();
        Debug.Log("RAG settings reset to defaults");
    }
    
    public void ValidateSettings()
    {
        if (settings.maxTokens < 100)
            settings.maxTokens = 100;
        
        if (settings.maxTokens > 4000)
            settings.maxTokens = 4000;
        
        if (settings.temperature < 0f)
            settings.temperature = 0f;
        
        if (settings.temperature > 2f)
            settings.temperature = 2f;
        
        if (settings.maxRetrievalResults < 1)
            settings.maxRetrievalResults = 1;
        
        if (settings.maxRetrievalResults > 20)
            settings.maxRetrievalResults = 20;
        
        if (settings.relevanceThreshold < 0f)
            settings.relevanceThreshold = 0f;
        
        if (settings.relevanceThreshold > 1f)
            settings.relevanceThreshold = 1f;
        
        if (settings.maxContextLength < 500)
            settings.maxContextLength = 500;
        
        if (settings.maxContextLength > 8000)
            settings.maxContextLength = 8000;
        
        if (settings.maxMessagesDisplayed < 10)
            settings.maxMessagesDisplayed = 10;
        
        if (settings.maxMessagesDisplayed > 1000)
            settings.maxMessagesDisplayed = 1000;
        
        if (string.IsNullOrEmpty(settings.companionName))
            settings.companionName = "Assistant";
        
        if (string.IsNullOrEmpty(settings.companionPersonality))
            settings.companionPersonality = "helpful, friendly, and knowledgeable";
        
        if (string.IsNullOrEmpty(settings.documentsPath))
            settings.documentsPath = "Documents";
    }
    
    private void OnValidate()
    {
        ValidateSettings();
    }
}