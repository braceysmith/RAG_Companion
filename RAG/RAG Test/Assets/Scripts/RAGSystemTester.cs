using System.Collections;
using UnityEngine;

public class RAGSystemTester : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runTestsOnStart = false;
    [SerializeField] private bool enableDetailedLogging = true;
    // [SerializeField] private float testDelay = 2f; // Unused - commented out
    
    [Header("Test Messages")]
    [SerializeField] private string[] testMessages = {
        "Hello, can you help me?",
        "What is Unity?",
        "How do I create a game object?",
        "Explain object-oriented programming",
        "What can you tell me about AI companions?"
    };
    
    private RAGCompanionController companionController;
    private RAGDocumentRetrieval documentRetrieval;
    private OpenAIAPIClient apiClient;
    private CompanionUI companionUI;
    
    private int currentTestIndex = 0;
    private bool testsRunning = false;
    
    private void Start()
    {
        StartCoroutine(InitializeAndTest());
    }
    
    private IEnumerator InitializeAndTest()
    {
        yield return new WaitForSeconds(1f); // Wait for system initialization
        
        FindComponents();
        
        if (runTestsOnStart)
        {
            StartCoroutine(RunSystemTests());
        }
    }
    
    private void FindComponents()
    {
        companionController = FindFirstObjectByType<RAGCompanionController>();
        documentRetrieval = FindFirstObjectByType<RAGDocumentRetrieval>();
        apiClient = FindFirstObjectByType<OpenAIAPIClient>();
        companionUI = FindFirstObjectByType<CompanionUI>();
        
        LogTest("System Components Found:");
        LogTest($"- RAGCompanionController: {(companionController != null ? "✓" : "✗")}");
        LogTest($"- RAGDocumentRetrieval: {(documentRetrieval != null ? "✓" : "✗")}");
        LogTest($"- OpenAIAPIClient: {(apiClient != null ? "✓" : "✗")}");
        LogTest($"- CompanionUI: {(companionUI != null ? "✓" : "✗")}");
    }
    
    private IEnumerator RunSystemTests()
    {
        if (testsRunning) yield break;
        
        testsRunning = true;
        LogTest("Starting RAG System Tests...");
        
        // Test 1: Component Initialization
        yield return StartCoroutine(TestComponentInitialization());
        
        // Test 2: Document Retrieval
        yield return StartCoroutine(TestDocumentRetrieval());
        
        // Test 3: Configuration System
        yield return StartCoroutine(TestConfigurationSystem());
        
        // Test 4: UI System
        yield return StartCoroutine(TestUISystem());
        
        // Test 5: API Integration (if key is set)
        yield return StartCoroutine(TestAPIIntegration());
        
        LogTest("RAG System Tests Complete!");
        testsRunning = false;
    }
    
    private IEnumerator TestComponentInitialization()
    {
        LogTest("Testing Component Initialization...");
        
        bool allComponentsReady = companionController != null && 
                                 documentRetrieval != null && 
                                 apiClient != null;
        
        LogTest($"Components Ready: {(allComponentsReady ? "PASS" : "FAIL")}");
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator TestDocumentRetrieval()
    {
        LogTest("Testing Document Retrieval...");
        
        if (documentRetrieval == null)
        {
            LogTest("Document Retrieval: FAIL (Component not found)");
            yield break;
        }
        
        try
        {
            var documents = documentRetrieval.RetrieveRelevantDocuments("Unity");
            LogTest($"Document Retrieval: {(documents.Count > 0 ? "PASS" : "WARN")} ({documents.Count} documents found)");
            
            foreach (var doc in documents)
            {
                LogTest($"  - {doc.title} (Score: {doc.relevanceScore:F2})");
            }
        }
        catch (System.Exception ex)
        {
            LogTest($"Document Retrieval: FAIL ({ex.Message})");
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator TestConfigurationSystem()
    {
        LogTest("Testing Configuration System...");
        
        try
        {
            var config = RAGConfiguration.Instance;
            var settings = config.Settings;
            
            LogTest($"Configuration Loaded: PASS");
            LogTest($"  - Model: {settings.modelName}");
            LogTest($"  - RAG Enabled: {settings.enableRAG}");
            LogTest($"  - Max Tokens: {settings.maxTokens}");
            LogTest($"  - Companion Name: {settings.companionName}");
            
            config.ValidateSettings();
            LogTest("Configuration Validation: PASS");
        }
        catch (System.Exception ex)
        {
            LogTest($"Configuration System: FAIL ({ex.Message})");
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator TestUISystem()
    {
        LogTest("Testing UI System...");
        
        if (companionUI == null)
        {
            LogTest("UI System: WARN (CompanionUI not found)");
            yield break;
        }
        
        bool testPassed = false;
        string errorMessage = "";
        
        try
        {
            companionUI.DisplayCompanionMessage("Test message from RAG System Tester");
            LogTest("UI Message Display: PASS");
            
            companionUI.SetProcessingStatus(true);
            testPassed = true;
        }
        catch (System.Exception ex)
        {
            errorMessage = ex.Message;
        }
        
        if (testPassed)
        {
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                companionUI.SetProcessingStatus(false);
                LogTest("UI Processing Status: PASS");
            }
            catch (System.Exception ex)
            {
                LogTest($"UI Processing Status: FAIL ({ex.Message})");
            }
        }
        else
        {
            LogTest($"UI System: FAIL ({errorMessage})");
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator TestAPIIntegration()
    {
        LogTest("Testing API Integration...");
        
        if (apiClient == null)
        {
            LogTest("API Integration: FAIL (Component not found)");
            yield break;
        }
        
        var config = RAGConfiguration.Instance.Settings;
        
        if (string.IsNullOrEmpty(config.openAIAPIKey))
        {
            LogTest("API Integration: SKIP (No API key configured)");
            LogTest("  To test API integration, set your OpenAI API key in RAGConfiguration");
            yield break;
        }
        
        LogTest("API Integration: Testing with configured key...");
        LogTest("  Note: This will make an actual API call to OpenAI");
        
        // This would be a real API test, but we'll skip it for safety
        LogTest("API Integration: SKIP (Safety - avoiding real API calls in tests)");
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private void LogTest(string message)
    {
        if (enableDetailedLogging)
        {
            Debug.Log($"[RAG Test] {message}");
        }
    }
    
    [ContextMenu("Run System Tests")]
    public void RunTests()
    {
        StartCoroutine(RunSystemTests());
    }
    
    [ContextMenu("Test Document Retrieval")]
    public void TestDocumentRetrievalOnly()
    {
        StartCoroutine(TestDocumentRetrieval());
    }
    
    [ContextMenu("Test Configuration")]
    public void TestConfigurationOnly()
    {
        StartCoroutine(TestConfigurationSystem());
    }
    
    [ContextMenu("Send Test Message")]
    public void SendTestMessage()
    {
        if (companionController == null)
        {
            LogTest("Cannot send test message: RAGCompanionController not found");
            return;
        }
        
        if (currentTestIndex >= testMessages.Length)
            currentTestIndex = 0;
        
        string message = testMessages[currentTestIndex];
        LogTest($"Sending test message: {message}");
        
        companionController.SendMessage(message);
        currentTestIndex++;
    }
    
    private void Update()
    {
        // Quick test shortcuts
        if (Input.GetKeyDown(KeyCode.F10))
        {
            RunTests();
        }
        
        if (Input.GetKeyDown(KeyCode.F11))
        {
            SendTestMessage();
        }
        
        if (Input.GetKeyDown(KeyCode.F12))
        {
            TestDocumentRetrievalOnly();
        }
    }
    
    private void OnGUI()
    {
        if (!enableDetailedLogging) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("RAG System Tester", GUI.skin.box);
        
        if (GUILayout.Button("Run All Tests (F10)"))
        {
            RunTests();
        }
        
        if (GUILayout.Button("Send Test Message (F11)"))
        {
            SendTestMessage();
        }
        
        if (GUILayout.Button("Test Document Retrieval (F12)"))
        {
            TestDocumentRetrievalOnly();
        }
        
        GUILayout.Space(10);
        
        GUILayout.Label("Status:", GUI.skin.label);
        GUILayout.Label($"Tests Running: {testsRunning}");
        GUILayout.Label($"Current Test Message: {currentTestIndex + 1}/{testMessages.Length}");
        
        GUILayout.EndArea();
    }
}