using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class OpenAIMessage
{
    public string role;
    public string content;
}

[System.Serializable]
public class OpenAIRequest
{
    public string model;
    public List<OpenAIMessage> messages;
    public float temperature;
    public int max_tokens;
    public bool stream;
}

[System.Serializable]
public class OpenAIChoice
{
    public OpenAIMessage message;
    public string finish_reason;
    public int index;
}

[System.Serializable]
public class OpenAIResponse
{
    public string id;
    public string @object;
    public long created;
    public string model;
    public List<OpenAIChoice> choices;
}

public class OpenAIAPIClient : MonoBehaviour
{
    [Header("API Configuration")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string model = "gpt-4o-mini";
    [SerializeField] private float temperature = 0.7f;
    [SerializeField] private int maxTokens = 1000;
    
    private const string API_URL = "https://api.openai.com/v1/chat/completions";
    private List<OpenAIMessage> conversationHistory = new List<OpenAIMessage>();
    
    public event Action<string> OnResponseReceived;
    public event Action<string> OnError;
    
    private void Start()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("OpenAI API Key not set. Please set it in the inspector or through code.");
        }
    }
    
    public void SetAPIKey(string key)
    {
        apiKey = key;
    }
    
    public void SendMessage(string userMessage, string systemContext = "")
    {
        StartCoroutine(SendMessageCoroutine(userMessage, systemContext));
    }
    
    private IEnumerator SendMessageCoroutine(string userMessage, string systemContext)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            OnError?.Invoke("API Key not set");
            yield break;
        }
        
        var messages = new List<OpenAIMessage>();
        
        if (!string.IsNullOrEmpty(systemContext))
        {
            messages.Add(new OpenAIMessage { role = "system", content = systemContext });
        }
        
        messages.AddRange(conversationHistory);
        messages.Add(new OpenAIMessage { role = "user", content = userMessage });
        
        var request = new OpenAIRequest
        {
            model = model,
            messages = messages,
            temperature = temperature,
            max_tokens = maxTokens,
            stream = false
        };
        
        string jsonRequest = JsonUtility.ToJson(request);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(API_URL, "POST"))
        {
            byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonRequest);
            webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = webRequest.downloadHandler.text;
                ProcessResponse(responseText, userMessage);
            }
            else
            {
                OnError?.Invoke($"API Error: {webRequest.error}");
            }
        }
    }
    
    private void ProcessResponse(string responseJson, string userMessage)
    {
        try
        {
            OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);
            
            if (response.choices != null && response.choices.Count > 0)
            {
                string assistantResponse = response.choices[0].message.content;
                
                conversationHistory.Add(new OpenAIMessage { role = "user", content = userMessage });
                conversationHistory.Add(new OpenAIMessage { role = "assistant", content = assistantResponse });
                
                if (conversationHistory.Count > 20)
                {
                    conversationHistory.RemoveRange(0, 4);
                }
                
                OnResponseReceived?.Invoke(assistantResponse);
            }
            else
            {
                OnError?.Invoke("No response choices received");
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error processing response: {ex.Message}");
        }
    }
    
    public void ClearConversationHistory()
    {
        conversationHistory.Clear();
    }
    
    public void AddSystemMessage(string systemMessage)
    {
        conversationHistory.Insert(0, new OpenAIMessage { role = "system", content = systemMessage });
    }
}