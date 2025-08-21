using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the different input methods and their UI components
/// </summary>
public class InputManager : MonoBehaviour
{
    // Singleton instance
    private static InputManager _instance;
    public static InputManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<InputManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("InputManager");
                    _instance = obj.AddComponent<InputManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Input GameObjects")]
    [SerializeField] private GameObject inputContainer; // Parent container for all input methods
    [SerializeField] private GameObject keyboardObject; // Keyboard input UI
    [SerializeField] private GameObject speechObject;   // Speech input UI

    //[Header("Input Buttons")]
    //[SerializeField] private Button keyboardButton;     // Button to activate keyboard
    //[SerializeField] private Button speechButton;       // Button to activate speech

    // Track current input state
    private bool isInputActive = false;
    private InputMode currentMode = InputMode.Speech;
    //public bAiEmotionDisplayManagerV0002 emotionDisplay;

    // Enum to track which input method is active
    public enum InputMode
    {
        Keyboard,
        Speech
    }

    private void Awake()
    {
        // Set up singleton
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
    }

    private void Start()
    {
        // Set up button listeners if assigned
        //if (keyboardButton != null)
         //   keyboardButton.onClick.AddListener(ActivateKeyboard);
            
        //if (speechButton != null)
        //    speechButton.onClick.AddListener(ActivateSpeech);
        
        // Initialize UI states
        UpdateInputUI();
    }

    /// <summary>
    /// Toggles the input container on/off
    /// </summary>
    public void ToggleInput()
    {
        isInputActive = !isInputActive;
        UpdateInputUI();
        
        Debug.Log($"Input toggled: {(isInputActive ? "ON" : "OFF")}, Mode: {currentMode}");
    }

    /// <summary>
    /// Activates the input and keyboard, hides speech
    /// </summary>
    public void ActivateKeyboard()
    {
        isInputActive = true;
        currentMode = InputMode.Keyboard;
        UpdateInputUI();
        
        Debug.Log("Keyboard input activated");
    }

    /// <summary>
    /// Activates the input and speech, hides keyboard
    /// </summary>
    public void ActivateSpeech()
    {
        isInputActive = true;
        currentMode = InputMode.Speech;
        UpdateInputUI();
        
        Debug.Log("Speech input activated");
    }

    /// <summary>
    /// Turns off all input methods
    /// </summary>
    public void DeactivateInput()
    {
        isInputActive = false;
        UpdateInputUI();
        
        Debug.Log("Input deactivated");
    }

    public void ActivateInput()
    {
        isInputActive = true;
        UpdateInputUI();

        Debug.Log("Input deactivated");
    }

    /// <summary>
    /// Updates the UI elements based on current state
    /// </summary>
    private void UpdateInputUI()
    {
        // First make sure input container exists
        if (inputContainer == null)
        {
            Debug.LogError("InputManager: No input container assigned!");
            return;
        }
        
        // Set main container active/inactive
        inputContainer.SetActive(isInputActive);
        //emotionDisplay.Happy();
        // If input is not active, just hide everything
        if (!isInputActive)
        {
            if (keyboardObject != null) keyboardObject.SetActive(false);
            if (speechObject != null) speechObject.SetActive(false);
            return;
        }
        
        // Handle active input method
        switch (currentMode)
        {
            case InputMode.Keyboard:
                if (keyboardObject != null) keyboardObject.SetActive(true);
                if (speechObject != null) speechObject.SetActive(false);
                break;
                
            case InputMode.Speech:
                if (keyboardObject != null) keyboardObject.SetActive(false);
                if (speechObject != null) speechObject.SetActive(true);
                break;
        }
        
        // Update button highlighting if buttons exist
        //if (keyboardButton != null && speechButton != null)
        //{
         //   keyboardButton.interactable = currentMode != InputMode.Keyboard;
        //    speechButton.interactable = currentMode != InputMode.Speech;
        //}
    }

    /// <summary>
    /// Gets the current active input mode
    /// </summary>
    public InputMode GetCurrentInputMode()
    {
        return currentMode;
    }

    /// <summary>
    /// Checks if input is currently active
    /// </summary>
    public bool IsInputActive()
    {
        return isInputActive;
    }
}