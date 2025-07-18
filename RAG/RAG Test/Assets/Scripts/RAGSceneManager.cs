using UnityEngine;
using UnityEngine.SceneManagement;

public class RAGSceneManager : MonoBehaviour
{
    [Header("RAG System Components")]
    [SerializeField] private GameObject ragCompanionPrefab;
    [SerializeField] private GameObject ragUIPrefab;
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool persistBetweenScenes = true;
    
    [Header("Scene Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Transform companionSpawnPoint;
    
    private RAGCompanionController companionController;
    private CompanionUI companionUI;
    private CompanionInputHandler inputHandler;
    
    private static RAGSceneManager instance;
    public static RAGSceneManager Instance => instance;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Initialize();
    }
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupRAGSystem();
        }
    }
    
    private void Initialize()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (uiCanvas == null)
        {
            uiCanvas = FindFirstObjectByType<Canvas>();
        }
        
        RAGConfiguration.Instance.LoadSettings();
        RAGConfiguration.Instance.ValidateSettings();
    }
    
    public void SetupRAGSystem()
    {
        SetupCompanionController();
        SetupCompanionUI();
        SetupInputHandler();
        CreateStreamingAssetsFolder();
        
        Debug.Log("RAG System setup complete");
    }
    
    private void SetupCompanionController()
    {
        companionController = FindFirstObjectByType<RAGCompanionController>();
        
        if (companionController == null)
        {
            if (ragCompanionPrefab != null)
            {
                Vector3 spawnPosition = companionSpawnPoint != null 
                    ? companionSpawnPoint.position 
                    : Vector3.zero;
                
                GameObject companionObj = Instantiate(ragCompanionPrefab, spawnPosition, Quaternion.identity);
                companionController = companionObj.GetComponent<RAGCompanionController>();
            }
            else
            {
                GameObject companionObj = new GameObject("RAG Companion Controller");
                companionController = companionObj.AddComponent<RAGCompanionController>();
                companionObj.AddComponent<OpenAIAPIClient>();
                companionObj.AddComponent<RAGDocumentRetrieval>();
                
                if (persistBetweenScenes)
                {
                    DontDestroyOnLoad(companionObj);
                }
            }
        }
        
        if (companionController != null)
        {
            companionController.SetAPIKey(RAGConfiguration.Instance.Settings.openAIAPIKey);
            companionController.ToggleRAG(RAGConfiguration.Instance.Settings.enableRAG);
        }
    }
    
    private void SetupCompanionUI()
    {
        companionUI = FindFirstObjectByType<CompanionUI>();
        
        if (companionUI == null)
        {
            if (ragUIPrefab != null && uiCanvas != null)
            {
                GameObject uiObj = Instantiate(ragUIPrefab, uiCanvas.transform);
                companionUI = uiObj.GetComponent<CompanionUI>();
            }
            else
            {
                Debug.LogWarning("CompanionUI prefab or Canvas not found. Creating basic UI setup.");
                CreateBasicUI();
            }
        }
        
        if (companionUI != null && persistBetweenScenes)
        {
            DontDestroyOnLoad(companionUI.gameObject);
        }
    }
    
    private void SetupInputHandler()
    {
        inputHandler = FindFirstObjectByType<CompanionInputHandler>();
        
        if (inputHandler == null)
        {
            if (mainCamera != null)
            {
                inputHandler = mainCamera.gameObject.AddComponent<CompanionInputHandler>();
            }
            else
            {
                GameObject inputObj = new GameObject("Companion Input Handler");
                inputHandler = inputObj.AddComponent<CompanionInputHandler>();
                
                if (persistBetweenScenes)
                {
                    DontDestroyOnLoad(inputObj);
                }
            }
        }
    }
    
    private void CreateBasicUI()
    {
        if (uiCanvas == null)
        {
            GameObject canvasObj = new GameObject("RAG UI Canvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        GameObject uiObj = new GameObject("Companion UI");
        uiObj.transform.SetParent(uiCanvas.transform, false);
        companionUI = uiObj.AddComponent<CompanionUI>();
        
        // Create basic UI structure
        CreateBasicChatInterface(uiObj);
    }
    
    private void CreateBasicChatInterface(GameObject parent)
    {
        // This would create a basic chat interface programmatically
        // For now, we'll leave it as a placeholder
        Debug.Log("Basic chat interface created. Please set up UI prefabs for better experience.");
    }
    
    private void CreateStreamingAssetsFolder()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        if (!System.IO.Directory.Exists(streamingAssetsPath))
        {
            System.IO.Directory.CreateDirectory(streamingAssetsPath);
        }
        
        string documentsPath = System.IO.Path.Combine(streamingAssetsPath, 
            RAGConfiguration.Instance.Settings.documentsPath);
        if (!System.IO.Directory.Exists(documentsPath))
        {
            System.IO.Directory.CreateDirectory(documentsPath);
        }
    }
    
    public void SaveCurrentConfiguration()
    {
        RAGConfiguration.Instance.SaveSettings();
    }
    
    public void ReloadConfiguration()
    {
        RAGConfiguration.Instance.LoadSettings();
        
        if (companionController != null)
        {
            companionController.SetAPIKey(RAGConfiguration.Instance.Settings.openAIAPIKey);
            companionController.ToggleRAG(RAGConfiguration.Instance.Settings.enableRAG);
        }
    }
    
    public void ResetToDefaults()
    {
        RAGConfiguration.Instance.ResetToDefaults();
        ReloadConfiguration();
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            SaveCurrentConfiguration();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveCurrentConfiguration();
        }
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            SaveCurrentConfiguration();
        }
    }
    
    public RAGCompanionController GetCompanionController()
    {
        return companionController;
    }
    
    public CompanionUI GetCompanionUI()
    {
        return companionUI;
    }
    
    public CompanionInputHandler GetInputHandler()
    {
        return inputHandler;
    }
}