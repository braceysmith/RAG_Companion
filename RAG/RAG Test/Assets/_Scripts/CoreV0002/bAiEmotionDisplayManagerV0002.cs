using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class bAiEmotionDisplayManagerV0002 : MonoBehaviour
{
    private bool baiActive = false;
    //[SerializeField] private bAidentity m_bAidentity;
    public UI_no_weapon m_BodyAnimations;
    public BAiComponentManagerV0002 m_ComponentManager;

    public PersonalityBasedEyeMovement pEye;

    [SerializeField] private Transform m_BrowR;
    [SerializeField] private Transform m_BrowL;
    [SerializeField] private Transform m_PupilR;
    [SerializeField] private Transform m_PupilL;

    [SerializeField] private Color m_Full;
    [SerializeField] private Color m_Half;
    [SerializeField] private Color m_Empty;

    private List<Coroutine> lerpCoroutines = new List<Coroutine>();
    private Coroutine randomIntsCoroutine;

    public BAiEmotions _selectedEmotion;

    public BAiEmotions SelectedBehavior
    {
        get { return _selectedEmotion; }
        set
        {
            if(_selectedEmotion != value)
            {
                _selectedEmotion = value;
                ExecuteEmotion();
            }
        }
    }

    public ExpressionController idleFace;
    public ExpressionController happyFace;
    public ExpressionController sadFace;
    public ExpressionController surprisedFace;
    public ExpressionController angryFace;
    public ExpressionController disgustedFace;
    public ExpressionController concernedFace;
    public ExpressionController curiousFace;
    public ExpressionController confusedFace;
    public ExpressionController thinkingFace;
    public ExpressionController offFace;

    public int minValue = 0;  // Minimum integer value
    public int maxValue = 9; // Maximum integer value
    public float delay = 1.0f; // Delay in seconds


    public void BaiActive()
    {
        baiActive = true;
    }

    public void BaiDeactive()
    {
        baiActive = false;
    }

    public void FaceInteration()
    {
        m_ComponentManager.ToggleHands();
        m_BodyAnimations.FaceTouch();
    }

    public void SetFace(ExpressionController face)
    {
        SetTransform(m_BrowL, face.leftBrowPos,face.leftBrowScale,face.leftBrowRot);
        SetTransform(m_BrowR, face.rightBrowPos, face.rightBrowScale, face.rightBrowRot);
        SetTransform(m_PupilL, face.leftEyePos, face.leftEyeScale, face.leftEyeRot);
        SetTransform(m_PupilR, face.rightEyePos, face.rightEyeScale, face.rightEyeRot);
    }

    public void SetTransform(Transform trans, Vector3 pos, Vector3 scale, Vector3 rot)
    {
        trans.localPosition = pos;
        trans.localScale = scale;
        trans.localRotation = Quaternion.Euler(rot);
    }


    private IEnumerator GenerateRandomInts()
    {
        while (true && baiActive==true)
        {
            int randomValue = Random.Range(minValue, maxValue + 1); // Random.Range is inclusive for ints
            if (randomValue == 0)
                Idle();
            if (randomValue == 1)
                Happy();
            if (randomValue == 2)
                //Sad();
                Curious();
            if (randomValue == 3)
                Surprised();
            if (randomValue == 4)
                //Angry();
                Happy();
            if (randomValue == 5)
                //Disgusted();
                Confused();
            if (randomValue == 6)
                //Concerned();
                Idle();
            if (randomValue == 7)
                Curious();
            if (randomValue == 8)
                Confused();
            if (randomValue == 9)
                Thinking();
            yield return new WaitForSeconds(Random.Range(3,10));
        }
    }

    public void ActivateFace()
    {
        try
        {
            // Check if the GameObject is active before attempting to use it
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[bAiEmotionDisplayManagerV0002] Cannot activate face on inactive GameObject: {gameObject.name}");
                return;
            }
            
            baiActive = true;
            Idle();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[bAiEmotionDisplayManagerV0002] Error in ActivateFace: {e.Message}");
            // Try to set idle face as a fallback
            try { Idle(); } catch { /* Ignore errors in fallback */ }
        }
    }

    public int MapFloatToInt(float value, float minFloat = 0f, float maxFloat = 1f, int minInt = 1, int maxInt = 8)
    {
        // Clamp the input value to the float range
        value = Mathf.Clamp(value, minFloat, maxFloat);

        // Map the clamped float value to the integer range
        float t = (value - minFloat) / (maxFloat - minFloat);
        int result = Mathf.RoundToInt(Mathf.Lerp(minInt, maxInt, t));

        return result;
    }

    public float ConvertToNormalizedRange(float value, float minInput = 0f, float maxInput = 10f, float minOutput = 0f, float maxOutput = 1f)
    {
        // Clamp the input value to the input range
        value = Mathf.Clamp(value, minInput, maxInput);

        // Normalize the value to the output range
        return Mathf.Lerp(minOutput, maxOutput, (value - minInput) / (maxInput - minInput));
    }


    private void EmoDisplay(int emoValue, float emoAmount)
    {
        if (emoValue == 0)
            Idle();
        if (emoValue == 1)
            EmoLerpFace(happyFace, 1, emoAmount);//happyFace
        if (emoValue == 2)
            EmoLerpFace(curiousFace, 1, emoAmount); //curiousFace
        if (emoValue == 3)
            EmoLerpFace(confusedFace, 1, emoAmount); //confusedFace
        if (emoValue == 4)
            EmoLerpFace(surprisedFace, 1, emoAmount); //surprisedFace
        if (emoValue == 5)
            EmoLerpFace(disgustedFace, 1, emoAmount); //disgustedFace
        if (emoValue == 6)
            EmoLerpFace(concernedFace, 1, emoAmount); //concernedFace
        if (emoValue == 7)
            EmoLerpFace(sadFace, 1, emoAmount); //sadFace
        if (emoValue == 8)
            EmoLerpFace(angryFace, 1, emoAmount); //angryFace
        if (emoValue == 9)
                Thinking();
    }

    public void ReturnToRandom()
    {
        StopAllCoroutines();
        StartCoroutine(GenerateRandomInts());
    }

    public void SetVector3(Transform target, Vector3 newPosition)
    {
        target.localPosition = newPosition;
    }

    // Coroutine to lerp position
    public IEnumerator LerpPosition(Transform target, Vector3 newPosition, float duration)
    {
        if (target == null)
        {
            //Debug.LogError("Target transform is null.");
            yield break;
        }

        float time = 0;
        Vector3 startPosition = target.localPosition;

        while (time < duration)
        {
            // Calculate the fraction of the total duration that has passed
            float fraction = time / duration;

            // Lerp position based on the fraction
            target.localPosition = Vector3.Lerp(startPosition, newPosition, fraction);

            // Increment time by the time passed since last frame
            time += Time.deltaTime;

            // Wait until next frame
            yield return null;
        }

        // Ensure final position are set exactly to the new values
        target.localPosition = newPosition;
        if (baiActive == false)
        {
            SetFace(offFace);
        }
    }


    // Coroutine to lerp scale
    public IEnumerator LerpScale(Transform target, Vector3 newScale, float duration)
    {
        if (target == null)
        {
            //Debug.LogError("Target transform is null.");
            yield break;
        }

        float time = 0;
        Vector3 startScale = target.localScale;

        while (time < duration)
        {
            // Calculate the fraction of the total duration that has passed
            float fraction = time / duration;

            // Lerp scale based on the fraction
            target.localScale = Vector3.Lerp(startScale, newScale, fraction);

            // Increment time by the time passed since last frame
            time += Time.deltaTime;

            // Wait until next frame
            yield return null;
        }

        // Ensure final scale are set exactly to the new values
        target.localScale = newScale;
        if (baiActive == false)
        {
            SetFace(offFace);
        }
    }


    // Coroutine to lerp scale
    public IEnumerator LerpRotation(Transform target, Vector3 newRot, float duration)
    {
        if (target == null)
        {
            Debug.LogError("Target transform is null.");
            yield break;
        }

        float time = 0;
        Quaternion startRotation = target.localRotation;

        while (time < duration)
        {
            // Calculate the fraction of the total duration that has passed
            float fraction = time / duration;

            // Lerp scale based on the fraction
            target.localRotation = Quaternion.Lerp(startRotation, Quaternion.Euler(newRot), fraction);

            // Increment time by the time passed since last frame
            time += Time.deltaTime;

            // Wait until next frame
            yield return null;
        }

        // Ensure final scale are set exactly to the new values
        target.localRotation = Quaternion.Euler(newRot);
        if (baiActive == false)
        {
            SetFace(offFace);
        }
    }

    private void ExecuteEmotion()
    {
        switch (_selectedEmotion)
        {
            case BAiEmotions.Idle:
                Idle();
                break;

            case BAiEmotions.Happy:
                Happy();
                break;

            case BAiEmotions.Sad:
                Sad();
                break;

            case BAiEmotions.Surprised:
                Surprised();
                break;

            case BAiEmotions.Angry:
                Angry();
                break;

            case BAiEmotions.Disgusted:
                Disgusted();
                break;

            case BAiEmotions.Concerned:
                Concerned();
                break;

            case BAiEmotions.Curious:
                Curious();
                break;

            case BAiEmotions.Confused:
                Confused();
                break;

            case BAiEmotions.Thinking:
                Thinking();
                break;

            case BAiEmotions.Off:
                Off();
                break;


            default:
                // Do nothing
                break;
        }
    }


    public void DeactivateFace(int delay)
    {
        baiActive = false;
        StopAllCoroutines();
        LerpFace(offFace, delay);
    }

    public void Idle()
    {
        LerpFace(idleFace, 1);

    }

    public void Happy()
    {

        LerpFace(happyFace, 1);
    }

    public void Sad()
    {

        LerpFace(sadFace, 2);
    }

    public void Surprised()
    {

        LerpFace(surprisedFace, .25f);
    }

    public void Angry()
    {

        LerpFace(angryFace, 2);
    }

    public void Disgusted()
    {

        LerpFace(disgustedFace, 1);
    }

    public void Concerned()
    {

        LerpFace(concernedFace, 1);
    }

    public void Curious()
    {

        LerpFace(curiousFace, .5f);
    }

    public void Confused()
    {

        LerpFace(confusedFace, .5f);
    }

    public void Thinking()
    {

        LerpFace(thinkingFace, 1);
    }

    public void Off()
    {

        LerpFace(offFace, 1);
    }

    // New method to stop all lerping coroutines
    private void StopAllLerpCoroutines()
    {
        foreach (Coroutine coroutine in lerpCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        lerpCoroutines.Clear(); // Clear the list after stopping all coroutines
    }

    // Your existing LerpPosition, LerpScale, LerpRotation methods...
    // Ensure to replace StartCoroutine with StartLerpCoroutine when calling these lerp methods

    private void LerpFace(ExpressionController face, float duration)
    {
        StopAllLerpCoroutines(); // Stop all previous lerping coroutines
        StartCoroutine(LerpPosition(m_BrowL, face.leftBrowPos, duration));
        StartCoroutine(LerpPosition(m_BrowR, face.rightBrowPos, duration));
        StartCoroutine(LerpPosition(m_PupilL, face.leftEyePos, duration));
        StartCoroutine(LerpPosition(m_PupilR, face.rightEyePos, duration));

        StartCoroutine(LerpScale(m_BrowL, face.leftBrowScale, duration));
        StartCoroutine(LerpScale(m_BrowR, face.rightBrowScale, duration));
        StartCoroutine(LerpScale(m_PupilL, face.leftEyeScale, duration));
        StartCoroutine(LerpScale(m_PupilR, face.rightEyeScale, duration));

        StartCoroutine(LerpRotation(m_BrowL, face.leftBrowRot, duration));
        StartCoroutine(LerpRotation(m_BrowR, face.rightBrowRot, duration));
        StartCoroutine(LerpRotation(m_PupilL, face.leftEyeRot, duration));
        StartCoroutine(LerpRotation(m_PupilR, face.rightEyeRot, duration));
    }

    private void EmoLerpFace(ExpressionController face, float duration, float amount)
    {
        // Check if the gameObject is active before starting coroutines
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[bAiEmotionDisplayManagerV0002] Cannot start coroutines on inactive GameObject: {gameObject.name}");
            return;
        }
        
        try
        {
            StopAllLerpCoroutines(); // Stop all previous lerping coroutines
            
            // Check if facial components are available
            if (m_BrowL != null && m_BrowR != null && m_PupilL != null && m_PupilR != null)
            {
                StartCoroutine(LerpPosition(m_BrowL, InterpolateBetweenVectors(idleFace.leftBrowPos, face.leftBrowPos,amount), duration));
                StartCoroutine(LerpPosition(m_BrowR, InterpolateBetweenVectors(idleFace.rightBrowPos, face.rightBrowPos, amount), duration));
                StartCoroutine(LerpPosition(m_PupilL, InterpolateBetweenVectors(idleFace.leftEyePos, face.leftEyePos, amount), duration));
                StartCoroutine(LerpPosition(m_PupilR, InterpolateBetweenVectors(idleFace.rightBrowPos, face.rightEyePos, amount), duration));

                StartCoroutine(LerpScale(m_BrowL, InterpolateBetweenVectors(idleFace.leftBrowScale, face.leftBrowScale, amount), duration));
                StartCoroutine(LerpScale(m_BrowR, InterpolateBetweenVectors(idleFace.rightBrowScale, face.rightBrowScale, amount), duration));
                StartCoroutine(LerpScale(m_PupilL, InterpolateBetweenVectors(idleFace.leftEyeScale, face.leftEyeScale, amount), duration));
                StartCoroutine(LerpScale(m_PupilR, InterpolateBetweenVectors(idleFace.rightEyeScale, face.rightEyeScale, amount), duration));

                StartCoroutine(LerpRotation(m_BrowL, InterpolateBetweenVectors(idleFace.leftBrowRot, face.leftBrowRot, amount), duration));
                StartCoroutine(LerpRotation(m_BrowR, InterpolateBetweenVectors(idleFace.rightBrowRot, face.rightBrowRot, amount), duration));
                StartCoroutine(LerpRotation(m_PupilL, InterpolateBetweenVectors(idleFace.leftEyeRot, face.leftEyeRot, amount), duration));
                StartCoroutine(LerpRotation(m_PupilR, InterpolateBetweenVectors(idleFace.rightEyeRot, face.rightEyeRot, amount), duration));
            }
            else
            {
                Debug.LogWarning("[bAiEmotionDisplayManagerV0002] One or more facial components are null");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[bAiEmotionDisplayManagerV0002] Error in EmoLerpFace: {e.Message}");
        }
    }
    public Vector3 InterpolateBetweenVectors(Vector3 start, Vector3 end, float t)
    {
        // Clamp t to ensure it stays between 0 and 1
        t = Mathf.Clamp01(t);

        // Use Vector3.Lerp to interpolate between the two vectors
        return Vector3.Lerp(start, end, t);
    }
}

public enum BAiEmotions
{
    Idle,
    Happy,
    Sad,
    Surprised,
    Angry, 
    Disgusted,
    Concerned,
    Curious,
    Confused,
    Thinking,
    Off
}

[System.Serializable] // Make it visible and editable in the Unity Inspector
public class ExpressionController
{
    public Vector3 leftBrowPos;
    public Vector3 leftBrowRot;
    public Vector3 leftBrowScale;
    public Vector3 rightBrowPos;
    public Vector3 rightBrowRot;
    public Vector3 rightBrowScale;
    public Vector3 leftEyePos;
    public Vector3 leftEyeRot;
    public Vector3 leftEyeScale;
    public Vector3 rightEyePos;
    public Vector3 rightEyeRot;
    public Vector3 rightEyeScale;
}
