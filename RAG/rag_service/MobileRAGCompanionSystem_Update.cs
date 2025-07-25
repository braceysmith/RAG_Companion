// Add these methods to your existing MobileRAGCompanionSystem.cs

[Header("Voice Processing")]
[SerializeField] private bool enableVoiceMode = true;
[SerializeField] private string voiceUserId = "mobile_voice_user";

// Add this method for voice message processing
public async Task<bool> SendVoiceMessageAsync(byte[] audioData, string audioFormat = "wav")
{
    if (!isSystemReady || isProcessingRequest)
    {
        LogMessage("System not ready or already processing request");
        return false;
    }

    isProcessingRequest = true;
    
    try
    {
        float startTime = Time.time;
        
        // Show processing UI
        mobileUI?.ShowProcessingIndicator("Processing voice...");
        
        LogMessage("Processing voice message...");
        
        // Process voice with hybrid RAG
        var voiceResponse = await ragClient.ProcessVoiceQuery(audioData, voiceUserId, audioFormat);
        
        // Calculate response time
        float responseTime = Time.time - startTime;
        responseTimes.Add(responseTime);
        averageResponseTime = responseTimes.Average();
        
        // Log the interaction
        LogMessage($"Voice transcript: {voiceResponse.transcript}");
        LogMessage($"Response source: {voiceResponse.source} ({voiceResponse.sensitivity})");
        
        // Handle tool results
        if (voiceResponse.tool_results != null)
        {
            LogMessage($"Tool used: {voiceResponse.tool_results}");
        }
        
        // Handle personal info extraction
        if (voiceResponse.personal_info_extracted != null)
        {
            LogMessage($"Personal info learned: {voiceResponse.personal_info_extracted}");
        }
        
        // Update UI with transcript and response
        mobileUI?.ShowTranscript(voiceResponse.transcript);
        mobileUI?.ShowResponse(voiceResponse.response_text);
        
        // Play audio response if available
        if (!string.IsNullOrEmpty(voiceResponse.audio_response))
        {
            byte[] audioResponseData = System.Convert.FromBase64String(voiceResponse.audio_response);
            StartCoroutine(audioManager.PlayAudioFromBytes(audioResponseData, AudioType.MPEG));
        }
        
        // Cache the conversation
        conversationCache?.CacheInteraction(voiceResponse.transcript, voiceResponse.response_text);
        
        // Update performance tracking
        totalInteractions++;
        
        // Hide processing UI
        mobileUI?.HideProcessingIndicator();
        
        LogMessage($"Voice message processed successfully in {responseTime:F2}s");
        return true;
    }
    catch (Exception e)
    {
        LogMessage($"Error processing voice message: {e.Message}");
        mobileUI?.ShowError($"Voice processing failed: {e.Message}");
        mobileUI?.HideProcessingIndicator();
        return false;
    }
    finally
    {
        isProcessingRequest = false;
    }
}

// Add voice recording control methods
public void StartVoiceRecording()
{
    if (enableVoiceMode && audioManager != null)
    {
        audioManager.StartRecording();
        mobileUI?.ShowRecordingIndicator();
        LogMessage("Started voice recording");
    }
}

public async void StopVoiceRecording()
{
    if (enableVoiceMode && audioManager != null)
    {
        audioManager.StopRecording();
        mobileUI?.HideRecordingIndicator();
        
        // Get recorded audio and process it
        if (audioManager.recordedClip != null)
        {
            byte[] audioData = audioManager.ConvertAudioClipToWAV(audioManager.recordedClip);
            await SendVoiceMessageAsync(audioData, "wav");
        }
        
        LogMessage("Stopped voice recording");
    }
}

// Add to your existing Start() method
private void SubscribeToAudioEvents()
{
    if (audioManager != null)
    {
        audioManager.OnAudioRecorded += OnAudioRecorded;
        audioManager.OnRecordingStarted += OnRecordingStarted;
        audioManager.OnRecordingStopped += OnRecordingStopped;
        audioManager.OnVoiceDetected += OnVoiceDetected;
    }
}

private async void OnAudioRecorded(byte[] audioData)
{
    // Automatically process recorded audio
    await SendVoiceMessageAsync(audioData, "wav");
}

private void OnRecordingStarted()
{
    LogMessage("Voice recording started");
    mobileUI?.ShowRecordingIndicator();
}

private void OnRecordingStopped()
{
    LogMessage("Voice recording stopped");
    mobileUI?.HideRecordingIndicator();
}

private void OnVoiceDetected()
{
    LogMessage("Voice activity detected");
}