// Add this to your existing MobileRAGClient.cs

[System.Serializable]
public class MobileVoiceResponse
{
    public string transcript;
    public string response_text;
    public string audio_response;
    public string source;           // "local" or "cloud"
    public string sensitivity;      // "personal", "private", "general"
    public object tool_results;
    public object personal_info_extracted;
    public string status;
}

// Add this method to MobileRAGClient class:
public async Task<MobileVoiceResponse> ProcessVoiceQuery(byte[] audioData, string userId, string audioFormat = "wav")
{
    string endpoint = $"{cloudApiUrl}/mobile/voice";
    
    WWWForm form = new WWWForm();
    form.AddBinaryData("audio", audioData, $"voice.{audioFormat}", $"audio/{audioFormat}");
    form.AddField("user_id", userId);
    form.AddField("audio_format", audioFormat);
    
    using (UnityWebRequest request = UnityWebRequest.Post(endpoint, form))
    {
        request.timeout = requestTimeoutSeconds;
        
        await request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            return JsonConvert.DeserializeObject<MobileVoiceResponse>(jsonResponse);
        }
        else
        {
            throw new Exception($"Voice request failed: {request.error}");
        }
    }
}