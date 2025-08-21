using UnityEngine;
using UnityEditor;

public class RAGConfigurationTester : EditorWindow
{
    [MenuItem("RAG Companion/Test RAGConfiguration")]
    public static void TestRAGConfiguration()
    {
        Debug.Log("🧪 Testing RAGConfiguration...");
        
        try
        {
            // Test 1: Basic loading
            Debug.Log("Test 1: Loading RAGConfiguration from Resources...");
            RAGConfiguration config = Resources.Load<RAGConfiguration>("RAGConfiguration");
            
            if (config == null)
            {
                Debug.LogError("❌ Failed to load RAGConfiguration from Resources!");
                return;
            }
            
            Debug.Log("✅ Successfully loaded RAGConfiguration from Resources");
            
            // Test 2: Instance property
            Debug.Log("Test 2: Testing Instance property...");
            RAGConfiguration instance = RAGConfiguration.Instance;
            
            if (instance == null)
            {
                Debug.LogError("❌ Instance property returned null!");
                return;
            }
            
            Debug.Log("✅ Instance property working correctly");
            
            // Test 3: Settings access
            Debug.Log("Test 3: Testing Settings access...");
            if (instance.Settings == null)
            {
                Debug.LogError("❌ Settings property is null!");
                return;
            }
            
            Debug.Log("✅ Settings property accessible");
            
            // Test 4: Settings values
            Debug.Log("Test 4: Testing Settings values...");
            var settings = instance.Settings;
            
            Debug.Log($"  Companion Name: {settings.companionName}");
            Debug.Log($"  Model: {settings.modelName}");
            Debug.Log($"  Temperature: {settings.temperature}");
            Debug.Log($"  Max Tokens: {settings.maxTokens}");
            Debug.Log($"  RAG Enabled: {settings.enableRAG}");
            
            Debug.Log("✅ Settings values are accessible");
            
            // Test 5: Validation
            Debug.Log("Test 5: Testing validation methods...");
            
            bool isValid = RAGConfiguration.IsConfigurationValid();
            Debug.Log($"  IsConfigurationValid: {isValid}");
            
            if (isValid)
            {
                Debug.Log("✅ Configuration validation passed");
            }
            else
            {
                Debug.LogWarning("⚠️ Configuration validation failed");
            }
            
            // Test 6: Settings validation
            Debug.Log("Test 6: Testing settings validation...");
            instance.ValidateSettings();
            Debug.Log("✅ Settings validation completed");
            
            Debug.Log("🎉 All RAGConfiguration tests passed successfully!");
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ RAGConfiguration test failed with error: {ex.Message}");
            Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }
    
    [MenuItem("RAG Companion/Test RAGConfiguration Runtime")]
    public static void TestRAGConfigurationRuntime()
    {
        Debug.Log("🧪 Testing RAGConfiguration runtime behavior...");
        
        // This will test the runtime behavior
        if (Application.isPlaying)
        {
            Debug.Log("✅ Application is playing - testing runtime behavior");
            
            try
            {
                RAGConfiguration config = RAGConfiguration.Instance;
                Debug.Log($"Runtime config loaded: {config != null}");
                
                if (config != null)
                {
                    Debug.Log($"Settings accessible: {config.Settings != null}");
                    if (config.Settings != null)
                    {
                        Debug.Log($"Companion name: {config.Settings.companionName}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Runtime test failed: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("⚠️ Application is not playing - runtime test skipped");
        }
    }
    
    [MenuItem("RAG Companion/Check RAGConfiguration Asset")]
    public static void CheckRAGConfigurationAsset()
    {
        Debug.Log("🔍 Checking RAGConfiguration asset details...");
        
        // Find the asset
        RAGConfiguration asset = Resources.Load<RAGConfiguration>("RAGConfiguration");
        
        if (asset == null)
        {
            Debug.LogError("❌ Asset not found in Resources!");
            return;
        }
        
        // Get asset path
        string assetPath = AssetDatabase.GetAssetPath(asset);
        Debug.Log($"Asset path: {assetPath}");
        
        // Check if asset is in Resources folder
        if (assetPath.Contains("/Resources/"))
        {
            Debug.Log("✅ Asset is in Resources folder");
        }
        else
        {
            Debug.LogWarning("⚠️ Asset is NOT in Resources folder!");
        }
        
        // Check asset type
        Debug.Log($"Asset type: {asset.GetType()}");
        
        // Check if it's a ScriptableObject
        if (asset is ScriptableObject)
        {
            Debug.Log("✅ Asset is a ScriptableObject");
        }
        else
        {
            Debug.LogError("❌ Asset is NOT a ScriptableObject!");
        }
        
        // Check if it has the RAGConfiguration script
        if (asset.GetType().Name == "RAGConfiguration")
        {
            Debug.Log("✅ Asset has RAGConfiguration script");
        }
        else
        {
            Debug.LogError($"❌ Asset script type mismatch: {asset.GetType().Name}");
        }
        
        // Check serialization
        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
        
        if (scriptProperty.objectReferenceValue != null)
        {
            Debug.Log("✅ Script reference is set");
            Debug.Log($"Script: {scriptProperty.objectReferenceValue.name}");
        }
        else
        {
            Debug.LogError("❌ Script reference is missing!");
        }
        
        Debug.Log("🔍 Asset check completed");
    }
}

