using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MobileAudioManager : MonoBehaviour
{
    [Header("Audio Configuration")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private int sampleRate = 16000; // Optimized for mobile
    [SerializeField] private int maxRecordingLength = 30; // seconds
    [SerializeField] private float silenceThreshold = 0.01f;
    
    [Header("Mobile Optimization")]
    [SerializeField] private bool enableBatteryOptimization = true;
    // [SerializeField] private bool enableEchoCancellation = true; // Commented out - unused
    [SerializeField] private bool enableNoiseReduction = true;
    // [SerializeField] private bool enableAudioCompression = true; // Commented out - unused
    
    [Header("Voice Activity Detection")]
    [SerializeField] private bool enableVAD = true;
    [SerializeField] private float vadThreshold = 0.02f;
    [SerializeField] private float vadMinDuration = 0.5f;
    [SerializeField] private float vadMaxSilence = 2.0f;
    
    // Recording state
    private bool isRecording = false;
    private bool isPlaying = false;
    private AudioClip recordedClip;
    private string microphoneDevice;
    private float recordingStartTime;
    
    // VAD state
    private bool isVoiceDetected = false;
    private float lastVoiceTime = 0f;
    private float voiceStartTime = 0f;
    private Queue<float> audioBuffer = new Queue<float>();
    private int bufferSize = 1024;
    
    // Battery optimization
    private bool isBatteryOptimized = false;
    private float originalSampleRate;
    
    // Events
    public event Action<byte[]> OnAudioRecorded;
    public event Action<float[]> OnAudioSamples;
    public event Action OnRecordingStarted;
    public event Action OnRecordingStopped;
    public event Action OnAudioPlaybackCompleted;
    public event Action OnVoiceDetected;
    public event Action OnVoiceEnded;
    public event Action<string> OnError;
    
    private void Start()
    {
        InitializeMobileAudio();
        RequestMicrophonePermission();
    }
    
    private void InitializeMobileAudio()
    {
        // Initialize audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Configure audio source for mobile
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 1.0f;
        audioSource.spatialBlend = 0.0f; // 2D audio
        
        // Store original sample rate
        originalSampleRate = sampleRate;
        
        // Configure for mobile performance
        if (enableBatteryOptimization)
        {
            OptimizeForBattery();
        }
        
        LogMessage("Mobile audio manager initialized");
    }
    
    private void RequestMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android permission request
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS permission is handled automatically by Unity
#endif
        
        // Check available microphones
        CheckMicrophoneDevices();
    }
    
    private void CheckMicrophoneDevices()
    {
        string[] devices = Microphone.devices;
        
        if (devices.Length == 0)
        {
            LogError("No microphone devices found");
            OnError?.Invoke("No microphone devices available");
            return;
        }
        
        // Use first available device
        microphoneDevice = devices[0];
        LogMessage($"Using microphone device: {microphoneDevice}");
    }
    
    private void OptimizeForBattery()
    {
        float batteryLevel = SystemInfo.batteryLevel;
        
        if (batteryLevel < 0.2f && batteryLevel > 0) // Low battery
        {
            // Aggressive optimization
            sampleRate = 8000; // Reduce sample rate
            maxRecordingLength = 15; // Reduce max recording time
            bufferSize = 512; // Reduce buffer size
            isBatteryOptimized = true;
            
            LogMessage("Battery optimization enabled - aggressive mode");
        }
        else if (batteryLevel < 0.5f && batteryLevel > 0) // Medium battery
        {
            // Moderate optimization
            sampleRate = 12000; // Slightly reduce sample rate
            maxRecordingLength = 20; // Slightly reduce max recording time
            isBatteryOptimized = true;
            
            LogMessage("Battery optimization enabled - moderate mode");
        }
    }
    
    public void StartRecording()
    {
        if (isRecording)
        {
            LogMessage("Already recording");
            return;
        }
        
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            LogError("No microphone device available");
            OnError?.Invoke("No microphone device available");
            return;
        }
        
        try
        {
            // Start microphone recording
            recordedClip = Microphone.Start(microphoneDevice, false, maxRecordingLength, sampleRate);
            
            if (recordedClip == null)
            {
                LogError("Failed to start microphone recording");
                OnError?.Invoke("Failed to start microphone recording");
                return;
            }
            
            isRecording = true;
            recordingStartTime = Time.time;
            
            // Start VAD monitoring
            if (enableVAD)
            {
                StartCoroutine(MonitorVoiceActivity());
            }
            
            OnRecordingStarted?.Invoke();
            LogMessage("Recording started");
        }
        catch (Exception ex)
        {
            LogError($"Failed to start recording: {ex.Message}");
            OnError?.Invoke($"Failed to start recording: {ex.Message}");
        }
    }
    
    public void StopRecording()
    {
        if (!isRecording)
        {
            LogMessage("Not currently recording");
            return;
        }
        
        try
        {
            // Stop microphone
            Microphone.End(microphoneDevice);
            
            isRecording = false;
            
            // Process recorded audio
            if (recordedClip != null)
            {
                ProcessRecordedAudio();
            }
            
            OnRecordingStopped?.Invoke();
            LogMessage("Recording stopped");
        }
        catch (Exception ex)
        {
            LogError($"Failed to stop recording: {ex.Message}");
            OnError?.Invoke($"Failed to stop recording: {ex.Message}");
        }
    }
    
    private void ProcessRecordedAudio()
    {
        try
        {
            // Get audio samples
            float[] samples = new float[recordedClip.samples * recordedClip.channels];
            recordedClip.GetData(samples, 0);
            
            // Apply noise reduction if enabled
            if (enableNoiseReduction)
            {
                samples = ApplyNoiseReduction(samples);
            }
            
            // Convert to bytes
            byte[] audioBytes = ConvertFloatsToBytes(samples);
            
            // Notify listeners
            OnAudioSamples?.Invoke(samples);
            OnAudioRecorded?.Invoke(audioBytes);
            
            LogMessage($"Audio processed: {audioBytes.Length} bytes, {samples.Length} samples");
        }
        catch (Exception ex)
        {
            LogError($"Failed to process recorded audio: {ex.Message}");
            OnError?.Invoke($"Failed to process recorded audio: {ex.Message}");
        }
    }
    
    private float[] ApplyNoiseReduction(float[] samples)
    {
        // Simple noise reduction algorithm
        float[] processedSamples = new float[samples.Length];
        
        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) < silenceThreshold)
            {
                processedSamples[i] = 0f; // Remove low-level noise
            }
            else
            {
                processedSamples[i] = samples[i];
            }
        }
        
        return processedSamples;
    }
    
    private byte[] ConvertFloatsToBytes(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2]; // 16-bit PCM
        
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(samples[i] * 32767f);
            bytes[i * 2] = (byte)(sample & 0xFF);
            bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        return bytes;
    }
    
    private IEnumerator MonitorVoiceActivity()
    {
        while (isRecording)
        {
            yield return new WaitForSeconds(0.1f); // Check every 100ms
            
            if (recordedClip != null)
            {
                // Get current audio level
                float[] samples = new float[bufferSize];
                int microphonePos = Microphone.GetPosition(microphoneDevice);
                
                if (microphonePos > bufferSize)
                {
                    recordedClip.GetData(samples, microphonePos - bufferSize);
                    
                    // Calculate audio level
                    float audioLevel = CalculateAudioLevel(samples);
                    
                    // Voice activity detection
                    bool voiceDetected = audioLevel > vadThreshold;
                    
                    if (voiceDetected && !isVoiceDetected)
                    {
                        // Voice started
                        isVoiceDetected = true;
                        voiceStartTime = Time.time;
                        OnVoiceDetected?.Invoke();
                        LogMessage("Voice detected");
                    }
                    else if (!voiceDetected && isVoiceDetected)
                    {
                        // Check for silence duration
                        if (Time.time - lastVoiceTime > vadMaxSilence)
                        {
                            // Voice ended
                            isVoiceDetected = false;
                            OnVoiceEnded?.Invoke();
                            LogMessage("Voice ended");
                            
                            // Auto-stop recording after silence
                            if (Time.time - voiceStartTime > vadMinDuration)
                            {
                                StopRecording();
                                break;
                            }
                        }
                    }
                    
                    if (voiceDetected)
                    {
                        lastVoiceTime = Time.time;
                    }
                }
            }
        }
    }
    
    private float CalculateAudioLevel(float[] samples)
    {
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += Mathf.Abs(sample);
        }
        return sum / samples.Length;
    }
    
    public void PlayAudio(byte[] audioData)
    {
        if (isPlaying)
        {
            LogMessage("Already playing audio");
            return;
        }
        
        try
        {
            // Convert bytes to float array
            float[] samples = ConvertBytesToFloats(audioData);
            
            // Create audio clip
            AudioClip clip = AudioClip.Create("MobileAudio", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            
            // Play audio
            audioSource.clip = clip;
            audioSource.Play();
            
            isPlaying = true;
            StartCoroutine(WaitForPlaybackCompletion());
            
            LogMessage($"Playing audio: {audioData.Length} bytes");
        }
        catch (Exception ex)
        {
            LogError($"Failed to play audio: {ex.Message}");
            OnError?.Invoke($"Failed to play audio: {ex.Message}");
        }
    }
    
    private float[] ConvertBytesToFloats(byte[] bytes)
    {
        float[] floats = new float[bytes.Length / 2]; // 16-bit PCM
        
        for (int i = 0; i < floats.Length; i++)
        {
            short sample = (short)((bytes[i * 2 + 1] << 8) | bytes[i * 2]);
            floats[i] = sample / 32768f;
        }
        
        return floats;
    }
    
    private IEnumerator WaitForPlaybackCompletion()
    {
        while (audioSource.isPlaying)
        {
            yield return null;
        }
        
        isPlaying = false;
        OnAudioPlaybackCompleted?.Invoke();
        LogMessage("Audio playback completed");
    }
    
    public void StopPlayback()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            isPlaying = false;
            LogMessage("Audio playback stopped");
        }
    }
    
    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }
    
    public void EnableBatteryOptimization(bool enable)
    {
        enableBatteryOptimization = enable;
        
        if (enable)
        {
            OptimizeForBattery();
        }
        else
        {
            // Restore original settings
            sampleRate = (int)originalSampleRate;
            maxRecordingLength = 30;
            bufferSize = 1024;
            isBatteryOptimized = false;
        }
        
        LogMessage($"Battery optimization: {(enable ? "Enabled" : "Disabled")}");
    }
    
    public void EnableVAD(bool enable)
    {
        enableVAD = enable;
        LogMessage($"Voice Activity Detection: {(enable ? "Enabled" : "Disabled")}");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileAudioManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileAudioManager] {message}");
    }
    
    private void OnDestroy()
    {
        if (isRecording)
        {
            StopRecording();
        }
        
        if (isPlaying)
        {
            StopPlayback();
        }
    }
    
    // Public getters
    public bool IsRecording => isRecording;
    public bool IsPlaying => isPlaying;
    public bool IsVoiceDetected => isVoiceDetected;
    public bool IsBatteryOptimized => isBatteryOptimized;
    public float RecordingDuration => isRecording ? Time.time - recordingStartTime : 0f;
    public string MicrophoneDevice => microphoneDevice;
    public int CurrentSampleRate => sampleRate;
}