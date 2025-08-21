using UnityEngine;
using UnityEngine.UI;

public class KeyboardManager : MonoBehaviour
{
    public RectTransform inputFieldContainer; // Assign the input field container
    public RectTransform scrollView; // Assign the ScrollView (stretchy RectTransform)
    public Canvas canvas; // Assign your UI Canvas (Needed for scaling)

    private Vector2 originalPosition;
    private float originalScrollViewBottomOffset; // Stores the original bottom offset
    private bool isKeyboardVisible = false;

    void Start()
    {
#if UNITY_IOS || UNITY_ANDROID
        // Store original positions
        originalPosition = inputFieldContainer.anchoredPosition;
        originalScrollViewBottomOffset = scrollView.offsetMin.y; // Store only the bottom offset
#endif
    }

    void Update()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (TouchScreenKeyboard.visible && !isKeyboardVisible)
        {
            MoveUIUp();
            isKeyboardVisible = true;
        }
        else if (!TouchScreenKeyboard.visible && isKeyboardVisible)
        {
            ResetUI();
            isKeyboardVisible = false;
        }
#endif
    }

    public void OpenKeyboard(InputField inputField)
    {
#if UNITY_IOS || UNITY_ANDROID
        TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
#endif
    }

    void MoveUIUp()
    {
#if UNITY_IOS || UNITY_ANDROID
        float keyboardHeight = GetKeyboardHeight();

        if (keyboardHeight > 0)
        {
            // Move input field container up
            inputFieldContainer.anchoredPosition = new Vector2(originalPosition.x, originalPosition.y + keyboardHeight);

            // Adjust only the bottom of the ScrollView without affecting its top
            scrollView.offsetMin = new Vector2(scrollView.offsetMin.x, originalScrollViewBottomOffset + keyboardHeight);
            if(scrollView.gameObject.GetComponent<ScrollRect>()!=null)
                scrollView.gameObject.GetComponent<ScrollRect>().verticalNormalizedPosition = 0f;
        }
#endif
    }

    void ResetUI()
    {
#if UNITY_IOS || UNITY_ANDROID
        inputFieldContainer.anchoredPosition = originalPosition;
        scrollView.offsetMin = new Vector2(scrollView.offsetMin.x, originalScrollViewBottomOffset); // Restore original bottom offset
#endif
    }

    float GetKeyboardHeight()
    {
        float keyboardHeight = TouchScreenKeyboard.area.height;

        if (keyboardHeight <= 0)
        {
            // Estimate keyboard height as 30-40% of screen height
            keyboardHeight = Screen.height * 0.35f;
        }

        // Convert world space to UI space based on canvas scaling
        if (canvas != null)
        {
            keyboardHeight /= canvas.scaleFactor;
        }

        return keyboardHeight;
    }
}