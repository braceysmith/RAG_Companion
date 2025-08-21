using Unity.VisualScripting;
using UnityEngine;

public class FollowYAxisRotation : MonoBehaviour
{
    public GameObject body;
    public Transform target; // The target object to follow
    public BAiComponentManagerV0002 bAiComp;
    public float rotationDelay = 0.5f; // Delay in following the rotation
    public float rotationThreshold = 5.0f; // Threshold in degrees to trigger rotation

    private float currentYRotation;
    private bool shouldRotate = false;
    private float velocity = 0.0f;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("Target not assigned. Please assign a target to follow.");
        }
        currentYRotation = body.transform.eulerAngles.y;
    }

    void Update()
    {
        if (target != null)
        {
            // Get the target's Y rotation
            float targetYRotation = target.eulerAngles.y;

            // Check if the target's rotation is outside the threshold
            if (!shouldRotate && Mathf.Abs(Mathf.DeltaAngle(currentYRotation, targetYRotation)) > rotationThreshold)
            {
                shouldRotate = true;
            }
            // && bAiComp.active
            // Rotate only if triggered
            if (shouldRotate)
            {
                // Smoothly interpolate the current Y rotation towards the target's Y rotation
                currentYRotation = Mathf.SmoothDampAngle(currentYRotation, targetYRotation, ref velocity, rotationDelay);

                // Apply the rotation to the object
                body.transform.rotation = Quaternion.Euler(0, currentYRotation, 0);

                // Check if the rotation has reached the target
                if (Mathf.Abs(Mathf.DeltaAngle(currentYRotation, targetYRotation)) < 0.1f)
                {
                    shouldRotate = false;
                }
            }
        }
    }
}