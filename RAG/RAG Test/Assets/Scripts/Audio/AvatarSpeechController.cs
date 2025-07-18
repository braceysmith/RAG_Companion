using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[System.Serializable]
public class VisemeMapping
{
    public string phoneme;
    public string viseme;
    public float intensity;
}

[System.Serializable]
public class AudioSegment
{
    public byte[] audioData;
    public float duration;
    public string transcript;
    public VisemeMapping[] visemes;
    public float timestamp;
    
    public AudioSegment(byte[] data, float segmentDuration, string segmentTranscript = "")
    {
        audioData = data;
        duration = segmentDuration;
        transcript = segmentTranscript;
        visemes = new VisemeMapping[0];
        timestamp = Time.time;
    }
}

public class AvatarSpeechController : MonoBehaviour
{
    [Header("Audio Components")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ttsClip;
    [SerializeField] private int sampleRate = 24000;
    [SerializeField] private int channels = 1;
    
    [Header("Avatar Animation")]
    [SerializeField] private Animator avatarAnimator;
    [SerializeField] private SkinnedMeshRenderer faceRenderer;
    [SerializeField] private bool enableLipSync = true;
    [SerializeField] private bool enableFacialExpressions = true;
    
    [Header("Lip Sync Settings")]
    [SerializeField] private float lipSyncSensitivity = 1.0f;
    [SerializeField] private float lipSyncSmoothing = 0.1f;
    [SerializeField] private AnimationCurve lipSyncCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Facial Expression Settings")]
    [SerializeField] private string[] expressionBlendShapes = { "happy", "sad", "surprised", "angry", "neutral" };
    // [SerializeField] private float expressionTransitionSpeed = 2.0f; // Unused - commented out
    // [SerializeField] private float expressionIntensity = 1.0f; // Unused - commented out
    
    [Header("Viseme Mapping")]
    [SerializeField] private VisemeMapping[] visemeMappings = new VisemeMapping[]
    {
        new VisemeMapping { phoneme = "a", viseme = "A", intensity = 1.0f },
        new VisemeMapping { phoneme = "e", viseme = "E", intensity = 1.0f },
        new VisemeMapping { phoneme = "i", viseme = "I", intensity = 1.0f },
        new VisemeMapping { phoneme = "o", viseme = "O", intensity = 1.0f },
        new VisemeMapping { phoneme = "u", viseme = "U", intensity = 1.0f },
        new VisemeMapping { phoneme = "m", viseme = "M", intensity = 1.0f },
        new VisemeMapping { phoneme = "p", viseme = "P", intensity = 1.0f },
        new VisemeMapping { phoneme = "b", viseme = "B", intensity = 1.0f },
        new VisemeMapping { phoneme = "f", viseme = "F", intensity = 1.0f },
        new VisemeMapping { phoneme = "v", viseme = "V", intensity = 1.0f },
        new VisemeMapping { phoneme = "th", viseme = "TH", intensity = 1.0f },
        new VisemeMapping { phoneme = "t", viseme = "T", intensity = 1.0f },
        new VisemeMapping { phoneme = "d", viseme = "D", intensity = 1.0f },
        new VisemeMapping { phoneme = "k", viseme = "K", intensity = 1.0f },
        new VisemeMapping { phoneme = "g", viseme = "G", intensity = 1.0f },
        new VisemeMapping { phoneme = "s", viseme = "S", intensity = 1.0f },
        new VisemeMapping { phoneme = "z", viseme = "Z", intensity = 1.0f },
        new VisemeMapping { phoneme = "sh", viseme = "SH", intensity = 1.0f },
        new VisemeMapping { phoneme = "ch", viseme = "CH", intensity = 1.0f },
        new VisemeMapping { phoneme = "l", viseme = "L", intensity = 1.0f },
        new VisemeMapping { phoneme = "r", viseme = "R", intensity = 1.0f },
        new VisemeMapping { phoneme = "n", viseme = "N", intensity = 1.0f },
        new VisemeMapping { phoneme = "ng", viseme = "NG", intensity = 1.0f }
    };
    
    // Audio processing
    private Queue<AudioSegment> audioQueue = new Queue<AudioSegment>();
    private Queue<float[]> audioBufferQueue = new Queue<float[]>();
    private bool isPlaying = false;
    private AudioSegment currentSegment;
    // private float currentPlaybackTime = 0f; // Unused - commented out
    
    // Lip sync state
    private float currentLipSyncValue = 0f;
    private float targetLipSyncValue = 0f;
    private string currentViseme = "neutral";
    private float currentVisemeIntensity = 0f;
    
    // Facial expression state
    private string currentExpression = "neutral";
    private float currentExpressionIntensity = 0f;
    private Dictionary<string, float> blendShapeWeights = new Dictionary<string, float>();
    
    // Performance tracking
    private float audioLatency = 0f;
    private int processedSegments = 0;
    private float totalAudioDuration = 0f;
    
    // Events
    public event Action<string> OnSpeechStarted;
    public event Action<string> OnSpeechEnded;
    public event Action<string> OnVisemeChanged;
    public event Action<string> OnExpressionChanged;
    public event Action<float> OnAudioLatencyMeasured;
    public event Action<string> OnError;
    
    private void Start()
    {
        InitializeComponents();
        SetupAudioSource();
        InitializeBlendShapes();
    }
    
    private void InitializeComponents()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (avatarAnimator == null)
        {
            avatarAnimator = GetComponent<Animator>();
        }
        
        if (faceRenderer == null)
        {
            faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
    }
    
    private void SetupAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 1.0f;
        audioSource.spatialBlend = 0.0f; // 2D audio
        audioSource.priority = 128;
    }
    
    private void InitializeBlendShapes()
    {
        if (faceRenderer == null || faceRenderer.sharedMesh == null)
        {
            return;
        }
        
        // Initialize blend shape weights
        for (int i = 0; i < faceRenderer.sharedMesh.blendShapeCount; i++)
        {
            string shapeName = faceRenderer.sharedMesh.GetBlendShapeName(i);
            blendShapeWeights[shapeName] = 0f;
        }
    }
    
    private void Update()
    {
        ProcessAudioQueue();
        UpdateLipSync();
        UpdateFacialExpressions();
        UpdateBlendShapes();
    }
    
    public void PlayAudioSegment(byte[] audioData, float duration, string transcript = "")
    {
        var segment = new AudioSegment(audioData, duration, transcript);
        audioQueue.Enqueue(segment);
        
        LogMessage($"Queued audio segment: {duration:F2}s, transcript: '{transcript}'");
    }
    
    public void PlayAudioStream(float[] audioSamples, string transcript = "")
    {
        audioBufferQueue.Enqueue(audioSamples);
        
        if (!isPlaying)
        {
            StartCoroutine(PlayStreamedAudio());
        }
    }
    
    public void SetExpression(string expression, float intensity = 1.0f)
    {
        if (Array.IndexOf(expressionBlendShapes, expression) >= 0)
        {
            currentExpression = expression;
            currentExpressionIntensity = intensity;
            OnExpressionChanged?.Invoke(expression);
            
            LogMessage($"Set expression: {expression} (intensity: {intensity:F2})");
        }
        else
        {
            LogError($"Unknown expression: {expression}");
        }
    }
    
    public void SetViseme(string viseme, float intensity = 1.0f)
    {
        currentViseme = viseme;
        currentVisemeIntensity = intensity;
        OnVisemeChanged?.Invoke(viseme);
    }
    
    public void StopSpeech()
    {
        audioSource.Stop();
        isPlaying = false;
        
        // Clear queues
        audioQueue.Clear();
        audioBufferQueue.Clear();
        
        // Reset to neutral
        SetExpression("neutral");
        SetViseme("neutral");
        
        OnSpeechEnded?.Invoke("Speech stopped");
        LogMessage("Speech stopped");
    }
    
    public void SetLipSyncEnabled(bool enabled)
    {
        enableLipSync = enabled;
        
        if (!enabled)
        {
            currentLipSyncValue = 0f;
            targetLipSyncValue = 0f;
        }
    }
    
    public void SetFacialExpressionsEnabled(bool enabled)
    {
        enableFacialExpressions = enabled;
        
        if (!enabled)
        {
            SetExpression("neutral");
        }
    }
    
    private void ProcessAudioQueue()
    {
        if (audioQueue.Count > 0 && !isPlaying)
        {
            currentSegment = audioQueue.Dequeue();
            StartCoroutine(PlayAudioSegment(currentSegment));
        }
    }
    
    private IEnumerator PlayAudioSegment(AudioSegment segment)
    {
        isPlaying = true;
        float startTime = Time.time;
        
        AudioClip clip = null;
        bool success = false;
        
        try
        {
            // Convert byte array to AudioClip
            clip = CreateAudioClipFromBytes(segment.audioData, segment.duration);
            
            if (clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                
                OnSpeechStarted?.Invoke(segment.transcript);
                success = true;
            }
            else
            {
                LogError("Failed to create audio clip");
            }
        }
        catch (Exception ex)
        {
            LogError($"Audio playback error: {ex.Message}");
            OnError?.Invoke($"Audio playback error: {ex.Message}");
        }
        
        if (success)
        {
            // Process lip sync and expressions during playback
            yield return StartCoroutine(ProcessSpeechSegment(segment));
            
            // Wait for audio to finish
            while (audioSource.isPlaying)
            {
                yield return null;
            }
            
            // Clean up
            if (clip != null)
            {
                Destroy(clip);
            }
            processedSegments++;
            totalAudioDuration += segment.duration;
            
            // Calculate latency
            audioLatency = Time.time - startTime;
            OnAudioLatencyMeasured?.Invoke(audioLatency);
            
            OnSpeechEnded?.Invoke(segment.transcript);
            
            LogMessage($"Completed audio segment: {segment.duration:F2}s, latency: {audioLatency:F2}s");
        }
        
        isPlaying = false;
    }
    
    private IEnumerator PlayStreamedAudio()
    {
        isPlaying = true;
        OnSpeechStarted?.Invoke("Streaming audio");
        
        while (audioBufferQueue.Count > 0 || isPlaying)
        {
            if (audioBufferQueue.Count > 0)
            {
                AudioClip streamClip = null;
                
                try
                {
                    float[] samples = audioBufferQueue.Dequeue();
                    
                    // Create temporary audio clip
                    streamClip = AudioClip.Create("StreamClip", samples.Length, channels, sampleRate, false);
                    streamClip.SetData(samples, 0);
                    
                    audioSource.clip = streamClip;
                    audioSource.Play();
                    
                    // Process lip sync based on audio amplitude
                    ProcessLipSyncFromSamples(samples);
                }
                catch (Exception ex)
                {
                    LogError($"Streaming audio error: {ex.Message}");
                    OnError?.Invoke($"Streaming audio error: {ex.Message}");
                    break;
                }
                
                // Wait for this segment to finish
                while (audioSource.isPlaying)
                {
                    yield return null;
                }
                
                if (streamClip != null)
                {
                    Destroy(streamClip);
                }
            }
            else
            {
                yield return null;
            }
        }
        
        isPlaying = false;
        OnSpeechEnded?.Invoke("Streaming audio ended");
    }
    
    private IEnumerator ProcessSpeechSegment(AudioSegment segment)
    {
        float segmentStartTime = Time.time;
        
        // Simple phoneme-based lip sync simulation
        if (enableLipSync && !string.IsNullOrEmpty(segment.transcript))
        {
            yield return StartCoroutine(ProcessTranscriptLipSync(segment.transcript, segment.duration));
        }
        else if (enableLipSync)
        {
            yield return StartCoroutine(ProcessAudioLipSync(segment.audioData, segment.duration));
        }
        
        // Expression processing based on content
        if (enableFacialExpressions)
        {
            ProcessEmotionalExpression(segment.transcript);
        }
    }
    
    private IEnumerator ProcessTranscriptLipSync(string transcript, float duration)
    {
        float timePerCharacter = duration / transcript.Length;
        
        foreach (char character in transcript.ToLower())
        {
            string phoneme = character.ToString();
            var mapping = Array.Find(visemeMappings, m => m.phoneme == phoneme);
            
            if (mapping != null)
            {
                SetViseme(mapping.viseme, mapping.intensity);
                targetLipSyncValue = mapping.intensity * lipSyncSensitivity;
            }
            else
            {
                targetLipSyncValue = 0.2f; // Default mouth movement
            }
            
            yield return new WaitForSeconds(timePerCharacter);
        }
        
        // Return to neutral
        SetViseme("neutral");
        targetLipSyncValue = 0f;
    }
    
    private IEnumerator ProcessAudioLipSync(byte[] audioData, float duration)
    {
        // Simple amplitude-based lip sync
        float[] samples = ConvertBytesToFloats(audioData);
        float timePerSample = duration / samples.Length;
        
        for (int i = 0; i < samples.Length; i += 100) // Sample every 100 samples
        {
            float amplitude = CalculateAmplitude(samples, i, 100);
            targetLipSyncValue = amplitude * lipSyncSensitivity;
            
            yield return new WaitForSeconds(timePerSample * 100);
        }
        
        targetLipSyncValue = 0f;
    }
    
    private void ProcessLipSyncFromSamples(float[] samples)
    {
        float amplitude = CalculateAmplitude(samples, 0, samples.Length);
        targetLipSyncValue = amplitude * lipSyncSensitivity;
    }
    
    private void ProcessEmotionalExpression(string transcript)
    {
        if (string.IsNullOrEmpty(transcript))
            return;
        
        string lowerTranscript = transcript.ToLower();
        
        // Simple emotion detection
        if (lowerTranscript.Contains("happy") || lowerTranscript.Contains("glad") || lowerTranscript.Contains("!"))
        {
            SetExpression("happy", 0.7f);
        }
        else if (lowerTranscript.Contains("sad") || lowerTranscript.Contains("sorry") || lowerTranscript.Contains("unfortunately"))
        {
            SetExpression("sad", 0.6f);
        }
        else if (lowerTranscript.Contains("?") || lowerTranscript.Contains("really") || lowerTranscript.Contains("wow"))
        {
            SetExpression("surprised", 0.5f);
        }
        else
        {
            SetExpression("neutral", 0.3f);
        }
    }
    
    private void UpdateLipSync()
    {
        if (!enableLipSync)
            return;
        
        // Smooth lip sync value
        currentLipSyncValue = Mathf.Lerp(currentLipSyncValue, targetLipSyncValue, 
                                        Time.deltaTime / lipSyncSmoothing);
        
        // Apply lip sync curve
        float curvedValue = lipSyncCurve.Evaluate(currentLipSyncValue);
        
        // Apply to blend shapes or animator
        if (faceRenderer != null)
        {
            ApplyLipSyncToBlendShapes(curvedValue);
        }
        
        if (avatarAnimator != null)
        {
            avatarAnimator.SetFloat("LipSync", curvedValue);
        }
    }
    
    private void UpdateFacialExpressions()
    {
        if (!enableFacialExpressions)
            return;
        
        // Smooth expression transitions
        // Implementation depends on your expression system
    }
    
    private void UpdateBlendShapes()
    {
        if (faceRenderer == null || faceRenderer.sharedMesh == null)
            return;
        
        // Apply blend shape weights
        for (int i = 0; i < faceRenderer.sharedMesh.blendShapeCount; i++)
        {
            string shapeName = faceRenderer.sharedMesh.GetBlendShapeName(i);
            if (blendShapeWeights.ContainsKey(shapeName))
            {
                faceRenderer.SetBlendShapeWeight(i, blendShapeWeights[shapeName]);
            }
        }
    }
    
    private void ApplyLipSyncToBlendShapes(float value)
    {
        // Apply lip sync to mouth-related blend shapes
        string[] mouthShapes = { "mouth_open", "mouth_A", "mouth_O", "mouth_U" };
        
        foreach (string shapeName in mouthShapes)
        {
            if (blendShapeWeights.ContainsKey(shapeName))
            {
                blendShapeWeights[shapeName] = value * 100f; // Blend shapes are 0-100
            }
        }
    }
    
    private AudioClip CreateAudioClipFromBytes(byte[] audioData, float duration)
    {
        try
        {
            // Convert bytes to float array
            float[] samples = ConvertBytesToFloats(audioData);
            
            // Create audio clip
            AudioClip clip = AudioClip.Create("TTSClip", samples.Length, channels, sampleRate, false);
            clip.SetData(samples, 0);
            
            return clip;
        }
        catch (Exception ex)
        {
            LogError($"Audio clip creation failed: {ex.Message}");
            return null;
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
    
    private float CalculateAmplitude(float[] samples, int start, int length)
    {
        float sum = 0f;
        int end = Mathf.Min(start + length, samples.Length);
        
        for (int i = start; i < end; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        
        return sum / length;
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[AvatarSpeechController] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[AvatarSpeechController] {message}");
    }
    
    // Public getters for monitoring
    public bool IsPlaying => isPlaying;
    public float AudioLatency => audioLatency;
    public int ProcessedSegments => processedSegments;
    public float TotalAudioDuration => totalAudioDuration;
    public int QueuedSegments => audioQueue.Count;
    public float CurrentLipSyncValue => currentLipSyncValue;
    public string CurrentExpression => currentExpression;
    public string CurrentViseme => currentViseme;
}