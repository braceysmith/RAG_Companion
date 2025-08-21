using UnityEngine;
using UnityEditor;

public class RAGConfigurationFixer : EditorWindow
{
    [MenuItem("RAG Companion/Fix RAGConfiguration Asset")]
    public static void FixRAGConfigurationAsset()
    {
        // Find existing asset
        RAGConfiguration existingAsset = Resources.Load<RAGConfiguration>("RAGConfiguration");
        
        if (existingAsset != null)
        {
            Debug.Log("RAGConfiguration asset found. Checking if it needs fixing...");
            
            // Check if the asset has the script reference
            SerializedObject serializedObject = new SerializedObject(existingAsset);
            SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
            
            if (scriptProperty.objectReferenceValue == null)
            {
                Debug.Log("Script reference is missing. Fixing...");
                
                // Find the RAGConfiguration script by searching for it
                MonoScript script = null;
                
                // Search for scripts with the name "RAGConfiguration"
                string[] guids = AssetDatabase.FindAssets("t:Script RAGConfiguration");
                if (guids.Length > 0)
                {
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    Debug.Log($"Found RAGConfiguration script: {scriptPath}");
                }
                
                // If not found by name, search by type
                if (script == null)
                {
                    guids = AssetDatabase.FindAssets("t:Script");
                    foreach (string guid in guids)
                    {
                        string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                        MonoScript testScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                        if (testScript != null && testScript.GetClass() == typeof(RAGConfiguration))
                        {
                            script = testScript;
                            Debug.Log($"Found RAGConfiguration script by type: {scriptPath}");
                            break;
                        }
                    }
                }
                
                if (script != null)
                {
                    scriptProperty.objectReferenceValue = script;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(existingAsset);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("✅ RAGConfiguration asset fixed successfully!");
                }
                else
                {
                    Debug.LogError("Could not find RAGConfiguration script! Please check if the script file exists and compiles correctly.");
                    Debug.LogError("Make sure RAGConfiguration.cs is in your project and compiles without errors.");
                }
            }
            else
            {
                Debug.Log("RAGConfiguration asset is properly configured.");
            }
        }
        else
        {
            Debug.Log("RAGConfiguration asset not found. Creating new one...");
            CreateNewRAGConfigurationAsset();
        }
    }
    
    [MenuItem("RAG Companion/Create New RAGConfiguration Asset")]
    public static void CreateNewRAGConfigurationAsset()
    {
        // Create new asset
        RAGConfiguration newAsset = ScriptableObject.CreateInstance<RAGConfiguration>();
        
        // Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        
        // Save asset
        AssetDatabase.CreateAsset(newAsset, "Assets/Resources/RAGConfiguration.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ New RAGConfiguration asset created successfully!");
        
        // Select the new asset
        Selection.activeObject = newAsset;
        EditorGUIUtility.PingObject(newAsset);
    }
    
    [MenuItem("RAG Companion/Validate RAGConfiguration")]
    public static void ValidateRAGConfiguration()
    {
        RAGConfiguration asset = Resources.Load<RAGConfiguration>("RAGConfiguration");
        
        if (asset == null)
        {
            Debug.LogError("❌ RAGConfiguration asset not found in Resources folder!");
            return;
        }
        
        if (asset.Settings == null)
        {
            Debug.LogError("❌ RAGConfiguration settings are null!");
            return;
        }
        
        // Validate settings
        var settings = asset.Settings;
        
        Debug.Log("✅ RAGConfiguration validation results:");
        Debug.Log($"  Companion Name: {settings.companionName}");
        Debug.Log($"  Model: {settings.modelName}");
        Debug.Log($"  Temperature: {settings.temperature}");
        Debug.Log($"  Max Tokens: {settings.maxTokens}");
        Debug.Log($"  RAG Enabled: {settings.enableRAG}");
        Debug.Log($"  Max Retrieval Results: {settings.maxRetrievalResults}");
        Debug.Log($"  Relevance Threshold: {settings.relevanceThreshold}");
        Debug.Log($"  Max Context Length: {settings.maxContextLength}");
        Debug.Log($"  Max Messages Displayed: {settings.maxMessagesDisplayed}");
        
        // Check for potential issues
        if (string.IsNullOrEmpty(settings.openAIAPIKey))
        {
            Debug.LogWarning("⚠️ OpenAI API Key is not set!");
        }
        
        if (settings.maxTokens <= 0)
        {
            Debug.LogWarning("⚠️ Max Tokens is invalid!");
        }
        
        if (settings.temperature < 0 || settings.temperature > 2)
        {
            Debug.LogWarning("⚠️ Temperature is out of valid range (0-2)!");
        }
        
        Debug.Log("✅ RAGConfiguration validation completed!");
    }
    
    [MenuItem("RAG Companion/Reset RAGConfiguration to Defaults")]
    public static void ResetRAGConfigurationToDefaults()
    {
        RAGConfiguration asset = Resources.Load<RAGConfiguration>("RAGConfiguration");
        
        if (asset == null)
        {
            Debug.LogError("RAGConfiguration asset not found!");
            return;
        }
        
        if (EditorUtility.DisplayDialog("Reset RAGConfiguration", 
            "Are you sure you want to reset the RAGConfiguration to default values? This will clear all custom settings.", 
            "Yes, Reset", "Cancel"))
        {
            asset.ResetToDefaults();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("✅ RAGConfiguration reset to defaults!");
        }
    }
    
    [MenuItem("RAG Companion/Check Script Compilation")]
    public static void CheckScriptCompilation()
    {
        Debug.Log("🔍 Checking RAGConfiguration script compilation...");
        
        // Check if the script file exists
        string[] guids = AssetDatabase.FindAssets("t:Script RAGConfiguration");
        if (guids.Length == 0)
        {
            Debug.LogError("❌ RAGConfiguration.cs script not found in project!");
            return;
        }
        
        string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        Debug.Log($"✅ Found script at: {scriptPath}");
        
        // Check if script compiles
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
        if (script == null)
        {
            Debug.LogError("❌ Could not load RAGConfiguration script!");
            return;
        }
        
        // Check if script has compilation errors
        if (EditorUtility.GetObjectEnabled(script) == 0)
        {
            Debug.LogWarning("⚠️ RAGConfiguration script is disabled!");
        }
        
        // Try to get the script class
        System.Type scriptClass = script.GetClass();
        if (scriptClass == null)
        {
            Debug.LogError("❌ RAGConfiguration script class could not be determined - compilation error!");
            return;
        }
        
        Debug.Log($"✅ Script class: {scriptClass.Name}");
        Debug.Log($"✅ Script base class: {scriptClass.BaseType?.Name}");
        
        // Check if it's a ScriptableObject
        if (scriptClass.IsSubclassOf(typeof(ScriptableObject)))
        {
            Debug.Log("✅ Script inherits from ScriptableObject (correct)");
        }
        else
        {
            Debug.LogError("❌ Script does NOT inherit from ScriptableObject!");
        }
        
        // Check if the class has the expected methods
        var instanceMethod = scriptClass.GetMethod("get_Instance", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        
        if (instanceMethod != null)
        {
            Debug.Log("✅ Instance property found");
        }
        else
        {
            Debug.LogError("❌ Instance property not found!");
        }
        
        Debug.Log("🔍 Script compilation check completed");
    }
    
    [MenuItem("RAG Companion/Force Asset Database Refresh")]
    public static void ForceAssetDatabaseRefresh()
    {
        Debug.Log("🔄 Forcing Asset Database refresh...");
        
        try
        {
            // Force a complete refresh
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("✅ Asset Database refresh completed");
            
            // Wait a moment and check again
            EditorApplication.delayCall += () =>
            {
                Debug.Log("🔍 Re-checking RAGConfiguration after refresh...");
                RAGConfiguration asset = Resources.Load<RAGConfiguration>("RAGConfiguration");
                if (asset != null)
                {
                    Debug.Log("✅ RAGConfiguration asset found after refresh");
                }
                else
                {
                    Debug.LogWarning("⚠️ RAGConfiguration asset still not found after refresh");
                }
            };
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Asset Database refresh failed: {ex.Message}");
        }
    }
    
    [MenuItem("RAG Companion/Complete RAGConfiguration Fix")]
    public static void CompleteRAGConfigurationFix()
    {
        Debug.Log("🔧 Starting complete RAGConfiguration fix process...");
        
        // Step 1: Check script compilation
        CheckScriptCompilation();
        
        // Step 2: Force asset database refresh
        ForceAssetDatabaseRefresh();
        
        // Step 3: Try to fix the asset
        EditorApplication.delayCall += () =>
        {
            FixRAGConfigurationAsset();
        };
        
        Debug.Log("🔧 Complete fix process initiated - check Console for results");
    }
}
