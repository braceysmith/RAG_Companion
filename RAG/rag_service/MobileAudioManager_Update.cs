// Add these methods to your existing MobileAudioManager.cs

public byte[] ConvertAudioClipToWAV(AudioClip clip)
{
    var samples = new float[clip.samples * clip.channels];
    clip.GetData(samples, 0);
    
    return ConvertSamplesToWAV(samples, clip.frequency, clip.channels);
}

private byte[] ConvertSamplesToWAV(float[] samples, int frequency, int channels)
{
    var sampleCount = samples.Length;
    var byteCount = sampleCount * 2; // 16-bit
    var totalSize = byteCount + 44; // WAV header is 44 bytes
    
    var bytes = new byte[totalSize];
    
    // WAV header
    Array.Copy(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, bytes, 0, 4);
    Array.Copy(System.BitConverter.GetBytes(totalSize - 8), 0, bytes, 4, 4);
    Array.Copy(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, bytes, 8, 4);
    Array.Copy(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, bytes, 12, 4);
    Array.Copy(System.BitConverter.GetBytes(16), 0, bytes, 16, 4); // PCM
    Array.Copy(System.BitConverter.GetBytes((short)1), 0, bytes, 20, 2); // Format
    Array.Copy(System.BitConverter.GetBytes((short)channels), 0, bytes, 22, 2);
    Array.Copy(System.BitConverter.GetBytes(frequency), 0, bytes, 24, 4);
    Array.Copy(System.BitConverter.GetBytes(frequency * channels * 2), 0, bytes, 28, 4);
    Array.Copy(System.BitConverter.GetBytes((short)(channels * 2)), 0, bytes, 32, 2);
    Array.Copy(System.BitConverter.GetBytes((short)16), 0, bytes, 34, 2);
    Array.Copy(System.Text.Encoding.ASCII.GetBytes("data"), 0, bytes, 36, 4);
    Array.Copy(System.BitConverter.GetBytes(byteCount), 0, bytes, 40, 4);
    
    // Convert samples to 16-bit PCM
    for (int i = 0; i < sampleCount; i++)
    {
        var sample = Mathf.Clamp(samples[i], -1f, 1f);
        var intSample = (short)(sample * short.MaxValue);
        var sampleBytes = System.BitConverter.GetBytes(intSample);
        Array.Copy(sampleBytes, 0, bytes, 44 + i * 2, 2);
    }
    
    return bytes;
}

public IEnumerator PlayAudioFromBytes(byte[] audioData, AudioType audioType = AudioType.MPEG)
{
    // Save to temporary file
    string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, $"response.{(audioType == AudioType.MPEG ? "mp3" : "wav")}");
    System.IO.File.WriteAllBytes(tempPath, audioData);
    
    // Load and play
    using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", audioType))
    {
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();
            
            OnAudioPlaybackCompleted?.Invoke();
            
            // Wait for playback to complete
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            Debug.LogError($"Failed to load audio: {www.error}");
        }
    }
    
    // Clean up temp file
    if (System.IO.File.Exists(tempPath))
        System.IO.File.Delete(tempPath);
}