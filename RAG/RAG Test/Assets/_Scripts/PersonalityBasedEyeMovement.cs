using UnityEngine;
using UnityEngine.XR;

public class PersonalityBasedEyeMovement : MonoBehaviour
{
    public float openness = 5f; // Scale of 1 to 10

    public GameObject leftEye;
    public GameObject rightEye;

    private float nextMoveTime = 0f;
    private float moveFrequencyBase = 1f; // Time in seconds between movements
    private float moveSpeedBase = 0.01f; // Base speed, adjusted to work with smaller movements

    // Explicit min and max movement ranges
    private float minMovementRange = -0.001f;
    private float maxMovementRange = 0.001f;

    void Update()
    {
        if (Time.time >= nextMoveTime)
        {
            Vector3 newPosition = GetNewPositionBasedOnOpenness();
            StartCoroutine(MoveEye(leftEye, newPosition, moveSpeedBase));
            StartCoroutine(MoveEye(rightEye, newPosition, moveSpeedBase));
            nextMoveTime = Time.time + moveFrequencyBase;

            // Debugging output
            //Debug.Log($"New target position: {newPosition}, Time till next move: {moveFrequencyBase}s");
        }
    }

    public void ZeroEyes() 
    { 
        StopAllCoroutines();
        if (leftEye != null)
            leftEye.transform.localPosition = new Vector3(0, leftEye.transform.localPosition.y, 0);
        if (rightEye != null)
            rightEye.transform.localPosition = new Vector3(0, rightEye.transform.localPosition.y, 0);
    }

    Vector3 GetNewPositionBasedOnOpenness()
    {
        // Calculate new position based on openness, within specified min and max range
        float range = Mathf.Lerp(minMovementRange, maxMovementRange, (openness - 1) / 9f);
        float newX = Random.Range(minMovementRange, range);
        float newZ = Random.Range(minMovementRange, range);

        // Log for debugging
        //Debug.Log($"Calculated range: {range}, New X: {newX}, New Z: {newZ}");

        return new Vector3(newX, leftEye.transform.localPosition.y, newZ);
    }

    System.Collections.IEnumerator MoveEye(GameObject eye, Vector3 targetLocalPosition, float speed)
    {
        float startDistance = Vector3.Distance(eye.transform.localPosition, targetLocalPosition);
        while (Vector3.Distance(eye.transform.localPosition, targetLocalPosition) > Mathf.Epsilon)
        {
            eye.transform.localPosition = Vector3.MoveTowards(eye.transform.localPosition, targetLocalPosition, speed * Time.deltaTime);
            yield return null;
        }
        // Log final position to ensure movement has occurred
        //Debug.Log($"{eye.name} moved to {eye.transform.localPosition}. Start distance was {startDistance}");
    }

    public void EyesZero()
    {
        leftEye.transform.localPosition = Vector3.zero;
        rightEye.transform.localPosition = Vector3.zero;
       // Debug.Log($"{eye.name} moved to {eye.transform.localPosition}. Start distance was {startDistance}");
    }
}
