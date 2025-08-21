using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class RotateTowardsWithDelay : MonoBehaviour
{
    public string targetName = "";   // Optionally find the object by name
    public float delayTime = 2f;     // Delay before rotating (in seconds)
    public float rotationSpeed = 5f; // Speed at which the object rotates
    public float noMovementDuration = 3f; // Time to wait after no movement to return to original rotation

    public float minXRotation = -25f; // Minimum X rotation angle
    public float maxXRotation = 25f;  // Maximum X rotation angle

    private GameObject targetObject;  // The object to rotate towards
    private bool isRotating = false;  // To prevent multiple rotations at the same time
    private bool returningToOriginal = false; // Track if it's returning to the original rotation
    private Quaternion originalRotation;      // Original rotation to return to
    private Vector3 lastTargetPosition;       // Last known position of the target object
    private float timeSinceLastMove = 0f;     // Time since the target last moved

    public GameObject rotator;

    private bool close = false;
    //public Slider rotationSlider; // Reference to the slider in the UI
    public float threshold = 0.5f; // The value at which the function changes the target

    [SerializeField] private BAiComponentManagerV0002 m_baiCompMan;

    void Start()
    {

        if (rotator != null)
        {
            originalRotation = rotator.transform.rotation;
        }

        if (targetObject == null && !string.IsNullOrEmpty(targetName))
        {
            targetObject = GameObject.Find(targetName);
        }

        if (targetObject != null)
        {
            lastTargetPosition = targetObject.transform.position;
            StartCoroutine(RotateTowardsTargetWithDelay());
        }
        else
        {
            Debug.LogError("Target object not found. Please check the tag or name.");
        }
    }

    IEnumerator RotateTowardsTargetWithDelay()
    {
        yield return new WaitForSeconds(delayTime);
        isRotating = true;
    }

    public void NewTarget(string target)
    {
        targetObject = GameObject.Find(target);
    }

    void Update()
    {
        if (targetObject != null && rotator != null && m_baiCompMan.active)
        {
            if (targetObject.transform.position != lastTargetPosition)
            {
                timeSinceLastMove = 0f;
                lastTargetPosition = targetObject.transform.position;

                if (returningToOriginal)
                {
                    returningToOriginal = false;
                    isRotating = true;
                }
            }
            else
            {
                timeSinceLastMove += Time.deltaTime;
            }

            if (timeSinceLastMove >= noMovementDuration && !returningToOriginal)
            {
                isRotating = false;
                returningToOriginal = true;
            }

            if (returningToOriginal)
            {
                //rotator.transform.rotation = Quaternion.Slerp(rotator.transform.rotation, originalRotation, Time.deltaTime * rotationSpeed);
                Vector3 direction = (Camera.main.gameObject.transform.position - rotator.transform.position).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(direction);

                // Interpolate and clamp X rotation only
                Quaternion targetRotation = Quaternion.Slerp(rotator.transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
                Vector3 eulerRotation = targetRotation.eulerAngles;

                // Ensure the X rotation respects the min and max limits
                float xRotation = eulerRotation.x;
                if (xRotation > 180) xRotation -= 360; // Normalize to -180 to 180 for consistent clamping
                xRotation = Mathf.Clamp(xRotation, minXRotation, maxXRotation);
                eulerRotation.x = xRotation;

                rotator.transform.rotation = Quaternion.Euler(eulerRotation);
            }
            else if (isRotating)
            {
                Vector3 direction = (targetObject.transform.position - rotator.transform.position).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(direction);

                // Interpolate and clamp X rotation only
                Quaternion targetRotation = Quaternion.Slerp(rotator.transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
                Vector3 eulerRotation = targetRotation.eulerAngles;

                // Ensure the X rotation respects the min and max limits
                float xRotation = eulerRotation.x;
                if (xRotation > 180) xRotation -= 360; // Normalize to -180 to 180 for consistent clamping
                xRotation = Mathf.Clamp(xRotation, minXRotation, maxXRotation);
                eulerRotation.x = xRotation;

                rotator.transform.rotation = Quaternion.Euler(eulerRotation);
            }
        }
    }

    // Call this function whenever the slider value changes
    public void CheckSliderAndSetTarget()
    {
        if (close)
        {
            NewTarget("ScreenPosAttention");
        }
        else
        {
            NewTarget("WorldPosAttention");
        }
    }
}
