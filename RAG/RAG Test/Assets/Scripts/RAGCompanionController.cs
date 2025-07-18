using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class RAGCompanionController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private OpenAIAPIClient openAIClient;
    [SerializeField] private RAGDocumentRetrieval documentRetrieval;
    [SerializeField] private CompanionUI companionUI;
    
    [Header("Companion Settings")]
    [SerializeField] private string companionName = "Assistant";
    [SerializeField] private string companionPersonality = "helpful, friendly, and knowledgeable";
    [SerializeField] private bool enableRAG = true;
    [SerializeField] private int maxContextLength = 2000;
    
    private bool isProcessing = false;
    private Queue<string> messageQueue = new Queue<string>();
    
    public event Action<string> OnCompanionResponse;
    public event Action<bool> OnProcessingStatusChanged;
    
    private void Start()
    {
        InitializeComponents();
        SetupEventHandlers();
    }
    
    private void InitializeComponents()
    {
        if (openAIClient == null)
            openAIClient = GetComponent<OpenAIAPIClient>();
        
        if (documentRetrieval == null)
            documentRetrieval = GetComponent<RAGDocumentRetrieval>();
        
        if (companionUI == null)
            companionUI = FindFirstObjectByType<CompanionUI>();
        
        if (openAIClient == null || documentRetrieval == null)
        {
            Debug.LogError("Missing required components for RAG Companion Controller");
        }
    }
    
    private void SetupEventHandlers()
    {
        if (openAIClient != null)
        {
            openAIClient.OnResponseReceived += OnOpenAIResponseReceived;
            openAIClient.OnError += OnOpenAIError;
        }
        
        if (documentRetrieval != null)
        {
            documentRetrieval.OnDocumentsRetrieved += OnDocumentsRetrieved;
        }
        
        if (companionUI != null)
        {
            companionUI.OnUserMessageSubmitted += OnUserMessageSubmitted;
        }
    }
    
    private void OnDestroy()
    {
        if (openAIClient != null)
        {
            openAIClient.OnResponseReceived -= OnOpenAIResponseReceived;
            openAIClient.OnError -= OnOpenAIError;
        }
        
        if (documentRetrieval != null)
        {
            documentRetrieval.OnDocumentsRetrieved -= OnDocumentsRetrieved;
        }
        
        if (companionUI != null)
        {
            companionUI.OnUserMessageSubmitted -= OnUserMessageSubmitted;
        }
    }
    
    public new void SendMessage(string userMessage)
    {
        if (isProcessing)
        {
            messageQueue.Enqueue(userMessage);
            return;
        }
        
        ProcessMessage(userMessage);
    }
    
    private void ProcessMessage(string userMessage)
    {
        SetProcessingStatus(true);
        
        if (enableRAG)
        {
            var relevantDocuments = documentRetrieval.RetrieveRelevantDocuments(userMessage);
            string context = BuildRAGContext(relevantDocuments);
            string systemPrompt = BuildSystemPrompt(context);
            
            openAIClient.SendMessage(userMessage, systemPrompt);
        }
        else
        {
            string basicSystemPrompt = BuildBasicSystemPrompt();
            openAIClient.SendMessage(userMessage, basicSystemPrompt);
        }
    }
    
    private string BuildRAGContext(List<Document> documents)
    {
        if (documents == null || documents.Count == 0)
            return "";
        
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("RELEVANT KNOWLEDGE BASE:");
        contextBuilder.AppendLine();
        
        foreach (var doc in documents)
        {
            contextBuilder.AppendLine($"Document: {doc.title}");
            contextBuilder.AppendLine($"Content: {doc.content}");
            contextBuilder.AppendLine($"Relevance Score: {doc.relevanceScore:F2}");
            contextBuilder.AppendLine();
        }
        
        return contextBuilder.ToString();
    }
    
    private string BuildSystemPrompt(string ragContext)
    {
        var promptBuilder = new StringBuilder();
        
        promptBuilder.AppendLine($"You are {companionName}, a {companionPersonality} AI companion.");
        promptBuilder.AppendLine("You have access to a knowledge base to help answer questions accurately.");
        promptBuilder.AppendLine();
        
        if (!string.IsNullOrEmpty(ragContext))
        {
            promptBuilder.AppendLine(ragContext);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Use this knowledge base to inform your responses when relevant.");
            promptBuilder.AppendLine("If information from the knowledge base is not relevant to the question, respond based on your general knowledge.");
            promptBuilder.AppendLine("Always be helpful and provide accurate information.");
        }
        
        promptBuilder.AppendLine("Keep responses conversational and engaging.");
        promptBuilder.AppendLine("If you reference information from the knowledge base, mention the source document when appropriate.");
        
        string fullPrompt = promptBuilder.ToString();
        
        if (fullPrompt.Length > maxContextLength)
        {
            fullPrompt = fullPrompt.Substring(0, maxContextLength) + "...";
        }
        
        return fullPrompt;
    }
    
    private string BuildBasicSystemPrompt()
    {
        return $"You are {companionName}, a {companionPersonality} AI companion. " +
               "Provide helpful, accurate, and engaging responses to user questions. " +
               "Keep responses conversational and friendly.";
    }
    
    private void OnOpenAIResponseReceived(string response)
    {
        SetProcessingStatus(false);
        
        OnCompanionResponse?.Invoke(response);
        
        if (companionUI != null)
        {
            companionUI.DisplayCompanionMessage(response);
        }
        
        ProcessNextMessage();
    }
    
    private void OnOpenAIError(string error)
    {
        SetProcessingStatus(false);
        
        string errorMessage = $"I encountered an error: {error}. Please try again.";
        OnCompanionResponse?.Invoke(errorMessage);
        
        if (companionUI != null)
        {
            companionUI.DisplayErrorMessage(errorMessage);
        }
        
        ProcessNextMessage();
    }
    
    private void OnDocumentsRetrieved(List<Document> documents)
    {
        Debug.Log($"Retrieved {documents.Count} relevant documents");
        
        if (companionUI != null)
        {
            companionUI.DisplayRetrievedDocuments(documents);
        }
    }
    
    private void OnUserMessageSubmitted(string message)
    {
        SendMessage(message);
    }
    
    private void ProcessNextMessage()
    {
        if (messageQueue.Count > 0)
        {
            string nextMessage = messageQueue.Dequeue();
            ProcessMessage(nextMessage);
        }
    }
    
    private void SetProcessingStatus(bool processing)
    {
        isProcessing = processing;
        OnProcessingStatusChanged?.Invoke(processing);
        
        if (companionUI != null)
        {
            companionUI.SetProcessingStatus(processing);
        }
    }
    
    public void SetAPIKey(string apiKey)
    {
        if (openAIClient != null)
        {
            openAIClient.SetAPIKey(apiKey);
        }
    }
    
    public void ToggleRAG(bool enabled)
    {
        enableRAG = enabled;
    }
    
    public void ClearConversation()
    {
        if (openAIClient != null)
        {
            openAIClient.ClearConversationHistory();
        }
        
        if (companionUI != null)
        {
            companionUI.ClearConversation();
        }
    }
    
    public void AddDocumentToRAG(string title, string content, string[] tags = null)
    {
        if (documentRetrieval != null)
        {
            var document = new Document(Guid.NewGuid().ToString(), title, content, tags);
            documentRetrieval.AddDocument(document);
        }
    }
}