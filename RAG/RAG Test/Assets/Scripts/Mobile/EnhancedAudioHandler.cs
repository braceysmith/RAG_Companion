using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.WebRTC;
using Newtonsoft.Json.Linq;

namespace RAGCompanion.Mobile
{
    /// <summary>
    /// Enhanced audio handler that prevents cutoffs by implementing proper chunking,
    /// flow control, and timeout management for WebRTC audio streaming.
    /// </summary>
    public class EnhancedAudioHandler : MonoBehaviour
    {
        [Header("Audio Configuration")]
        [SerializeField] private int maxChunkSize = 16384; // 16KB chunks for WebRTC
        [SerializeField] private int maxMessageSize = 50000; // 50KB for text messages
        [SerializeField] private float safetyTimeout = 60f; // 60 second safety timeout instead of 10s
        
        [Header("Flow Control")]
        [SerializeField] private int maxBufferSize = 1024 * 1024; // 1MB buffer
        [SerializeField] private float flowControlThreshold = 0.8f; // 80% threshold
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;
        
        // Private fields
        private RTCDataChannel dataChannel;
        private Queue<byte[]> audioBuffer = new Queue<byte[]>();
        private Queue<string> messageBuffer = new Queue<string>();
        private int currentBufferSize = 0;
        private bool isFlowControlled = false;
        private Coroutine safetyTimeoutCoroutine;
        
        // Events
        public event Action<string> OnDebugMessage;
        public event Action<bool> OnFlowControlChanged;
        
        private void Awake()
        {
            // Ensure this component persists
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// Initialize the enhanced audio handler with a data channel
        /// </summary>
        public void Initialize(RTCDataChannel channel)
        {
            dataChannel = channel;
            LogMessage("Enhanced Audio Handler initialized");
            
            // Start the safety timeout coroutine
            if (safetyTimeoutCoroutine != null)
            {
                StopCoroutine(safetyTimeoutCoroutine);
            }
            safetyTimeoutCoroutine = StartCoroutine(SafetyTimeoutCoroutine());
        }
        
        /// <summary>
        /// Send audio data with proper chunking and flow control
        /// </summary>
        public void SendAudioData(float[] audioData)
        {
            if (dataChannel?.ReadyState != RTCDataChannelState.Open)
            {
                LogMessage("Data channel not ready for audio transmission");
                return;
            }
            
            try
            {
                // Convert float audio to PCM16 bytes
                byte[] pcm16Data = ConvertAudioToPCM16(audioData);
                
                // Check if we can send without overflowing the buffer
                if (!CanSendMessage(pcm16Data.Length))
                {
                    // Buffer is full, queue the audio data
                    QueueAudioData(pcm16Data);
                    LogMessage($"Audio queued - buffer at {currentBufferSize}/{maxBufferSize} bytes");
                    return;
                }
                
                // Send audio in chunks
                SendAudioInChunks(pcm16Data);
                
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending audio data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Send text message with proper chunking and flow control
        /// </summary>
        public void SendTextMessage(string message, string messageType = "message")
        {
            LogMessage($"🎵 EnhancedAudioHandler.SendTextMessage called: {messageType} - {message.Substring(0, Math.Min(30, message.Length))}...");
            
            if (dataChannel?.ReadyState != RTCDataChannelState.Open)
            {
                LogMessage("❌ Data channel not ready for text transmission");
                return;
            }
            
            LogMessage("✅ Data channel ready - proceeding with message send...");
            
            try
            {
                // Check if message needs chunking
                if (message.Length > maxMessageSize)
                {
                    LogMessage($"📦 Message needs chunking ({message.Length} chars > {maxMessageSize})");
                    SendChunkedMessage(message, messageType);
                }
                else
                {
                    LogMessage($"📤 Sending single message ({message.Length} chars)");
                    SendSingleMessage(message, messageType);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error sending text message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process queued audio data when buffer space becomes available
        /// </summary>
        public void ProcessQueuedAudio()
        {
            if (audioBuffer.Count == 0 || isFlowControlled)
                return;
                
            StartCoroutine(ProcessQueuedAudioCoroutine());
        }
        
        /// <summary>
        /// Process queued messages when buffer space becomes available
        /// </summary>
        public void ProcessQueuedMessages()
        {
            if (messageBuffer.Count == 0 || isFlowControlled)
                return;
                
            StartCoroutine(ProcessQueuedMessagesCoroutine());
        }
        
        /// <summary>
        /// Reset the safety timeout (call this when audio activity is detected)
        /// </summary>
        public void ResetSafetyTimeout()
        {
            if (safetyTimeoutCoroutine != null)
            {
                StopCoroutine(safetyTimeoutCoroutine);
            }
            safetyTimeoutCoroutine = StartCoroutine(SafetyTimeoutCoroutine());
        }
        
        /// <summary>
        /// Get current system status for monitoring
        /// </summary>
        public AudioSystemStatus GetSystemStatus()
        {
            return new AudioSystemStatus
            {
                isFlowControlled = isFlowControlled,
                currentBufferSize = currentBufferSize,
                maxBufferSize = maxBufferSize,
                audioBufferCount = audioBuffer.Count,
                messageBufferCount = messageBuffer.Count,
                bufferUsagePercentage = (float)currentBufferSize / maxBufferSize * 100f
            };
        }
        
        #region Private Methods
        
        private byte[] ConvertAudioToPCM16(float[] audioData)
        {
            byte[] pcm16Data = new byte[audioData.Length * 2];
            for (int i = 0; i < audioData.Length; i++)
            {
                short sample = (short)(audioData[i] * 32767f);
                pcm16Data[i * 2] = (byte)(sample & 0xFF);
                pcm16Data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return pcm16Data;
        }
        
        private bool CanSendMessage(int messageSize)
        {
            if (currentBufferSize + messageSize > maxBufferSize * flowControlThreshold)
            {
                if (!isFlowControlled)
                {
                    isFlowControlled = true;
                    OnFlowControlChanged?.Invoke(true);
                    LogMessage("Flow control activated - buffer near capacity");
                }
                return false;
            }
            
            if (isFlowControlled && currentBufferSize < maxBufferSize * 0.5f)
            {
                isFlowControlled = false;
                OnFlowControlChanged?.Invoke(false);
                LogMessage("Flow control deactivated - buffer space available");
            }
            
            return true;
        }
        
        private void QueueAudioData(byte[] audioData)
        {
            audioBuffer.Enqueue(audioData);
            currentBufferSize += audioData.Length;
        }
        
        private void QueueMessage(string message)
        {
            messageBuffer.Enqueue(message);
            currentBufferSize += message.Length;
        }
        
        private void SendAudioInChunks(byte[] audioData)
        {
            int totalChunks = (audioData.Length + maxChunkSize - 1) / maxChunkSize;
            
            for (int i = 0; i < totalChunks; i++)
            {
                int startIndex = i * maxChunkSize;
                int endIndex = Math.Min(startIndex + maxChunkSize, audioData.Length);
                int chunkSize = endIndex - startIndex;
                
                byte[] chunk = new byte[chunkSize];
                Array.Copy(audioData, startIndex, chunk, 0, chunkSize);
                
                // Create chunked audio event
                var audioEvent = new JObject
                {
                    ["type"] = "input_audio_buffer.append",
                    ["audio"] = Convert.ToBase64String(chunk),
                    ["chunk_info"] = new JObject
                    {
                        ["chunk_id"] = $"audio_{Time.time}",
                        ["sequence"] = i,
                        ["total"] = totalChunks,
                        ["is_final"] = (i == totalChunks - 1)
                    }
                };
                
                string eventJson = audioEvent.ToString(Newtonsoft.Json.Formatting.None);
                byte[] eventBytes = Encoding.UTF8.GetBytes(eventJson);
                
                // Send the chunk
                dataChannel.Send(eventBytes);
                currentBufferSize += eventBytes.Length;
                
                LogMessage($"Sent audio chunk {i + 1}/{totalChunks} ({chunkSize} bytes)");
                
                // Small delay between chunks to prevent overwhelming
                if (i < totalChunks - 1)
                {
                    StartCoroutine(DelayedChunkSend());
                }
            }
        }
        
        private void SendChunkedMessage(string message, string messageType)
        {
            int totalChunks = (message.Length + maxMessageSize - 1) / maxMessageSize;
            
            for (int i = 0; i < totalChunks; i++)
            {
                int startIndex = i * maxMessageSize;
                int endIndex = Math.Min(startIndex + maxMessageSize, message.Length);
                string chunk = message.Substring(startIndex, endIndex - startIndex);
                
                // Create chunked message event
                var messageEvent = new JObject
                {
                    ["type"] = "conversation.item.create",
                    ["item"] = new JObject
                    {
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "input_text",
                                ["text"] = chunk
                            }
                        }
                    },
                    ["chunk_info"] = new JObject
                    {
                        ["chunk_id"] = $"message_{Time.time}",
                        ["sequence"] = i,
                        ["total"] = totalChunks,
                        ["is_final"] = (i == totalChunks - 1)
                    }
                };
                
                string eventJson = messageEvent.ToString(Newtonsoft.Json.Formatting.None);
                byte[] eventBytes = Encoding.UTF8.GetBytes(eventJson);
                
                // Send the chunk
                dataChannel.Send(eventBytes);
                currentBufferSize += eventBytes.Length;
                
                LogMessage($"Sent message chunk {i + 1}/{totalChunks} ({chunk.Length} chars)");
                
                // Small delay between chunks
                if (i < totalChunks - 1)
                {
                    StartCoroutine(DelayedChunkSend());
                }
            }
        }
        
        private void SendSingleMessage(string message, string messageType)
        {
            LogMessage($"📤 SendSingleMessage: Creating message event for '{messageType}'");
            
            var messageEvent = new JObject
            {
                ["type"] = "conversation.item.create",
                ["item"] = new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "input_text",
                            ["text"] = message
                        }
                    }
                }
            };
            
            string eventJson = messageEvent.ToString(Newtonsoft.Json.Formatting.None);
            byte[] eventBytes = Encoding.UTF8.GetBytes(eventJson);
            
            LogMessage($"📡 Sending {eventBytes.Length} bytes through data channel...");
            dataChannel.Send(eventBytes);
            currentBufferSize += eventBytes.Length;
            
            LogMessage($"✅ Single message sent successfully ({message.Length} chars)");
        }
        
        private IEnumerator DelayedChunkSend()
        {
            yield return new WaitForSeconds(0.01f); // 10ms delay between chunks
        }
        
        private IEnumerator ProcessQueuedAudioCoroutine()
        {
            while (audioBuffer.Count > 0 && !isFlowControlled)
            {
                if (audioBuffer.TryDequeue(out byte[] audioData))
                {
                    currentBufferSize -= audioData.Length;
                    SendAudioInChunks(audioData);
                    yield return new WaitForSeconds(0.05f); // 50ms between processing chunks
                }
            }
        }
        
        private IEnumerator ProcessQueuedMessagesCoroutine()
        {
            while (messageBuffer.Count > 0 && !isFlowControlled)
            {
                if (messageBuffer.TryDequeue(out string message))
                {
                    currentBufferSize -= message.Length;
                    SendTextMessage(message);
                    yield return new WaitForSeconds(0.05f); // 50ms between processing messages
                }
            }
        }
        
        private IEnumerator SafetyTimeoutCoroutine()
        {
            yield return new WaitForSeconds(safetyTimeout);
            
            LogMessage($"Safety timeout reached ({safetyTimeout}s) - checking system state");
            
            // Only reset if no recent activity
            if (audioBuffer.Count == 0 && messageBuffer.Count == 0 && currentBufferSize < maxBufferSize * 0.1f)
            {
                LogMessage("System idle - resetting safety timeout");
                ResetSafetyTimeout();
            }
            else
            {
                LogMessage("System active - extending safety timeout");
                ResetSafetyTimeout();
            }
        }
        
        private void LogMessage(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[EnhancedAudioHandler] {message}");
                OnDebugMessage?.Invoke(message);
            }
        }
        
        #endregion
        
        #region Public Data Structures
        
        [System.Serializable]
        public class AudioSystemStatus
        {
            public bool isFlowControlled;
            public int currentBufferSize;
            public int maxBufferSize;
            public int audioBufferCount;
            public int messageBufferCount;
            public float bufferUsagePercentage;
        }
        
        #endregion
    }
}
