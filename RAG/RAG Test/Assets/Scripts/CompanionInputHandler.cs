using UnityEngine;
using UnityEngine.InputSystem;

public class CompanionInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    // [SerializeField] private float interactionRange = 5f; // Unused - commented out
    [SerializeField] private LayerMask interactableLayers = -1;
    
    private InputAction interactAction;
    private InputAction cancelAction;
    private InputAction submitAction;
    
    private RAGCompanionController companionController;
    private CompanionUI companionUI;
    private Camera playerCamera;
    
    private void Awake()
    {
        companionController = FindFirstObjectByType<RAGCompanionController>();
        companionUI = FindFirstObjectByType<CompanionUI>();
        playerCamera = Camera.main;
        
        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<Camera>();
    }
    
    private void Start()
    {
        SetupInputActions();
    }
    
    private void SetupInputActions()
    {
        if (inputActions == null)
            return;
        
        var uiActionMap = inputActions.FindActionMap("UI");
        if (uiActionMap != null)
        {
            interactAction = uiActionMap.FindAction("Interact");
            cancelAction = uiActionMap.FindAction("Cancel");
            submitAction = uiActionMap.FindAction("Submit");
        }
        
        if (interactAction != null)
        {
            interactAction.Enable();
            interactAction.performed += OnInteractPerformed;
        }
        
        if (cancelAction != null)
        {
            cancelAction.Enable();
            cancelAction.performed += OnCancelPerformed;
        }
        
        if (submitAction != null)
        {
            submitAction.Enable();
            submitAction.performed += OnSubmitPerformed;
        }
    }
    
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        HandleInteraction();
    }
    
    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        HandleCancel();
    }
    
    private void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        HandleSubmit();
    }
    
    private void HandleInteraction()
    {
        if (companionUI != null)
        {
            bool isUIActive = companionUI.gameObject.activeInHierarchy;
            companionUI.gameObject.SetActive(!isUIActive);
            
            if (!isUIActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    
    private void HandleCancel()
    {
        if (companionUI != null && companionUI.gameObject.activeInHierarchy)
        {
            companionUI.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    private void HandleSubmit()
    {
        // This is handled by the UI system
    }
    
    private void OnDestroy()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction.Disable();
        }
        
        if (cancelAction != null)
        {
            cancelAction.performed -= OnCancelPerformed;
            cancelAction.Disable();
        }
        
        if (submitAction != null)
        {
            submitAction.performed -= OnSubmitPerformed;
            submitAction.Disable();
        }
    }
    
    private void Update()
    {
        HandleQuickCommands();
    }
    
    private void HandleQuickCommands()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (companionController != null)
            {
                companionController.SendMessage("What can you help me with?");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (companionController != null)
            {
                companionController.SendMessage("Explain the current scene or environment.");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            if (companionController != null)
            {
                companionController.SendMessage("What Unity concepts should I know?");
            }
        }
    }
    
    public void SendQuickMessage(string message)
    {
        if (companionController != null)
        {
            companionController.SendMessage(message);
        }
    }
    
    public void ToggleCompanionUI()
    {
        HandleInteraction();
    }
}