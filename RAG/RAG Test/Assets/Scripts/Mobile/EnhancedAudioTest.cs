using UnityEngine;
using RAGCompanion.Mobile;

namespace RAGCompanion.Mobile
{
    /// <summary>
    /// Simple test script to verify the enhanced audio system is working
    /// </summary>
    public class EnhancedAudioTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool runTestsOnStart = true;
        [SerializeField] private bool testAudioHandler = true;
        [SerializeField] private bool testRealtimeChat = true;
        
        private EnhancedAudioHandler audioHandler;
        private MobileRealtimeChat_Enhanced realtimeChat;
        
        private void Start()
        {
            if (runTestsOnStart)
            {
                StartCoroutine(RunTests());
            }
        }
        
        private System.Collections.IEnumerator RunTests()
        {
            Debug.Log("[EnhancedAudioTest] Starting enhanced audio system tests...");
            
            yield return new WaitForSeconds(1f);
            
            if (testAudioHandler)
            {
                yield return StartCoroutine(TestAudioHandler());
            }
            
            if (testRealtimeChat)
            {
                yield return StartCoroutine(TestRealtimeChat());
            }
            
            Debug.Log("[EnhancedAudioTest] All tests completed!");
        }
        
        private System.Collections.IEnumerator TestAudioHandler()
        {
            Debug.Log("[EnhancedAudioTest] Testing EnhancedAudioHandler...");
            
            // Get or add the audio handler
            audioHandler = GetComponent<EnhancedAudioHandler>();
            if (audioHandler == null)
            {
                audioHandler = gameObject.AddComponent<EnhancedAudioHandler>();
                Debug.Log("[EnhancedAudioTest] Added EnhancedAudioHandler component");
            }
            
            // Test the audio handler status
            var status = audioHandler.GetSystemStatus();
            Debug.Log($"[EnhancedAudioTest] Audio Handler Status: Buffer Usage {status.bufferUsagePercentage:F1}%");
            
            yield return new WaitForSeconds(0.5f);
            
            Debug.Log("[EnhancedAudioTest] EnhancedAudioHandler test completed successfully!");
        }
        
        private System.Collections.IEnumerator TestRealtimeChat()
        {
            Debug.Log("[EnhancedAudioTest] Testing MobileRealtimeChat_Enhanced...");
            
            // Get or add the realtime chat
            realtimeChat = GetComponent<MobileRealtimeChat_Enhanced>();
            if (realtimeChat == null)
            {
                realtimeChat = gameObject.AddComponent<MobileRealtimeChat_Enhanced>();
                Debug.Log("[EnhancedAudioTest] Added MobileRealtimeChat_Enhanced component");
            }
            
            // Test basic functionality
            bool isConnected = realtimeChat.IsConnected();
            Debug.Log($"[EnhancedAudioTest] Realtime Chat Connection Status: {isConnected}");
            
            // Test audio system status
            var audioStatus = realtimeChat.GetAudioSystemStatus();
            Debug.Log($"[EnhancedAudioTest] Audio System Status: Buffer Usage {audioStatus.bufferUsagePercentage:F1}%");
            
            yield return new WaitForSeconds(0.5f);
            
            Debug.Log("[EnhancedAudioTest] MobileRealtimeChat_Enhanced test completed successfully!");
        }
        
        [ContextMenu("Run Tests Manually")]
        public void RunTestsManually()
        {
            StartCoroutine(RunTests());
        }
        
        [ContextMenu("Test Audio Handler Only")]
        public void TestAudioHandlerOnly()
        {
            StartCoroutine(TestAudioHandler());
        }
        
        [ContextMenu("Test Realtime Chat Only")]
        public void TestRealtimeChatOnly()
        {
            StartCoroutine(TestRealtimeChat());
        }
        
        private void OnDestroy()
        {
            // Clean up test components if they were added by this test
            if (audioHandler != null && !GetComponent<EnhancedAudioHandler>())
            {
                DestroyImmediate(audioHandler);
            }
            
            if (realtimeChat != null && !GetComponent<MobileRealtimeChat_Enhanced>())
            {
                DestroyImmediate(realtimeChat);
            }
        }
    }
}
