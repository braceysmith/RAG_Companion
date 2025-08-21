using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

public class BAiComponentManagerV0002 : MonoBehaviour
{
    [SerializeField]
    private GameObject baiPrefab;
    [SerializeField]
    private GameObject baiHead;
    [SerializeField]
    private GameObject baiBody;
    [SerializeField]
    private GameObject baiArmR;
    [SerializeField]
    private GameObject baiArmL;
    [SerializeField]
    private GameObject baiCupR;
    [SerializeField]
    private GameObject baiCupL;

    [SerializeField]
    private GameObject anchorHeadActive;
    [SerializeField]
    private GameObject anchorBodyActive;
    [SerializeField]
    private GameObject anchorArmRActive;
    [SerializeField]
    private GameObject anchorArmLActive;
    [SerializeField]
    private GameObject anchorCupRArm;
    [SerializeField]
    private GameObject anchorCupLArm;

    [SerializeField]
    private GameObject anchorHeadDeactive;
    [SerializeField]
    private GameObject anchorBodyDeactive;
    [SerializeField]
    private GameObject anchorArmRDeactive;
    [SerializeField]
    private GameObject anchorArmLDeactive;
    [SerializeField]
    private GameObject anchorCupRBody;
    [SerializeField]
    private GameObject anchorCupLBody;

    private Dictionary<GameObject, Coroutine> gameObjectCoroutines = new Dictionary<GameObject, Coroutine>();
    public bool active = false;

    public bAiEmotionDisplayManagerV0002 baiEmo;
    [SerializeField]
    private PersonalityBasedEyeMovement eyeMover;


    [SerializeField]
    private AudioScaleEffect mouthMover;

    [SerializeField]
    private bool activateOnStart = false;

    [SerializeField]
    private UI_no_weapon bodyAni;

    //private float emoType = 0;
    //private float emoAmount = 0;

    public AudioSource m_audiosource;
    public AudioClip activateSound;
    public AudioClip deactivateSound;

    private bool handsAttached;

    //[SerializeField]
    //private bAiHeadRotator rotateManager;
    /*
    // Update is called once per frame
    public void Update()
    {

        //Debug.Log("updating");
        if (Input.GetKeyDown(KeyCode.A)) //attach to active body
        {
            ActivateBAi();
            //Debug.Log("activated");
        }

        if (Input.GetKeyDown(KeyCode.R)) //attach to deactive body
        {
            DeactivateBAI();
            //Debug.Log("DE-activated");
        }

        if (Input.GetKeyDown(KeyCode.H)) //attach cups to hands
        {
            AttachHands();
        }

        if (Input.GetKeyDown(KeyCode.B)) //attach cups to body
        {
            ReturnHands();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) 
        {
            bodyAni.ListenFriendlyOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            bodyAni.ListenNegativeOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            bodyAni.ListenNeutralOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            bodyAni.HelloFriendlyOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            bodyAni.HelloNegativeOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            bodyAni.HelloNeutralOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            bodyAni.TalkAgressiveOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            bodyAni.TalkFriendlyOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            bodyAni.TalkNegativeOnClick();
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            bodyAni.TalkNeutralOnClick();
        }
    }
    */
    public void RandomAni()
    {
        if (active)
        {
            int rando = Random.Range(0, 9);
            if (rando == 0)
            {
                bodyAni.ListenFriendlyOnClick();
            }
            if (rando == 1)
            {
                bodyAni.ListenNegativeOnClick();
            }
            if (rando == 2)
            {
                bodyAni.ListenNeutralOnClick();
            }
            if (rando == 3)
            {
                bodyAni.HelloFriendlyOnClick();
            }
            if (rando == 4)
            {
                bodyAni.HelloNegativeOnClick();
            }
            if (rando == 5)
            {
                bodyAni.HelloNeutralOnClick();
            }
            if (rando == 6)
            {
                bodyAni.TalkAgressiveOnClick();
            }
            if (rando == 7)
            {
                bodyAni.TalkFriendlyOnClick();
            }
            if (rando == 8)
            {
                bodyAni.TalkNegativeOnClick();
            }
            if (rando == 9)
            {
                bodyAni.TalkNeutralOnClick();
            }
        }
    }

    public void RandomHitAni()
    {
        if (active)
        {
            int rando = Random.Range(0, 2);
            if (rando == 0)
            {
                bodyAni.Hit01OnClick();
            }
            if (rando == 1)
            {
                bodyAni.Hit02OnClick();
            }
            if (rando == 2)
            {
                bodyAni.Hit02OnClick();
            }
        }
        
    }

    private void Start()
    {
        //if (baiPrefab.GetComponent<bAidentity>() != null)
         //   emoType = baiPrefab.GetComponent<bAidentity>().EmoPitch();
        //if (baiPrefab.GetComponent<bAidentity>() != null)
           // emoAmount = baiPrefab.GetComponent<bAidentity>().Emotionality();
        //if (activateOnStart)
        //{
        //    ActivateBAi();
        //}
        //else
        //{
        //    DeactivateBAI();
        //}
    }

    [ContextMenu("ActivateBai")]
    public void ActivateBAi()
    {
        try
        {
            // Check if gameObject is active before trying to use it
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[BAiComponentManagerV0002] Cannot activate bAi on inactive GameObject: {gameObject.name}");
                return;
            }
            
            // Check for null references before using them
            if (baiHead != null && anchorHeadActive != null)
                StartLerp(baiHead, anchorHeadActive, .5f);
            else
                Debug.LogWarning("[BAiComponentManagerV0002] Cannot activate head - references are null");
                
            if (baiBody != null && anchorBodyActive != null)
                StartLerp(baiBody, anchorBodyActive, 1);
            else
                Debug.LogWarning("[BAiComponentManagerV0002] Cannot activate body - references are null");
                
            if (baiArmL != null && anchorArmLActive != null)
                StartLerp(baiArmL, anchorArmLActive, 1f);
            else
                Debug.LogWarning("[BAiComponentManagerV0002] Cannot activate left arm - references are null");
                
            if (baiArmR != null && anchorArmRActive != null)
                StartLerp(baiArmR, anchorArmRActive, 1f);
            else
                Debug.LogWarning("[BAiComponentManagerV0002] Cannot activate right arm - references are null");
            
            // Activate facial expression management if available
            if (baiEmo != null && baiEmo.gameObject.activeInHierarchy)
                baiEmo.ActivateFace();
            else
                Debug.LogWarning("[BAiComponentManagerV0002] Cannot activate face - emotion manager is null or inactive");
            
            // Enable mouth and eye movement if available
            if (mouthMover != null)
                mouthMover.enabled = true;
            if (eyeMover != null)
                eyeMover.enabled = true;
                
            active = true;
            
            // Play activation sound if available
            if (m_audiosource != null && activateSound != null && m_audiosource.gameObject.activeInHierarchy)
            {
                m_audiosource.clip = activateSound;
                m_audiosource.Play();
            }

            baiEmo.Happy();

        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BAiComponentManagerV0002] Error activating bAi: {e.Message}");
        }
    }

    public void ToggleActive()
    {
        if(active) {
            DeactivateBAI();
        }
        else
        {
            ActivateBAi();
        }
    }

    [ContextMenu("DeactivateBAI")]
    public void DeactivateBAI()
    {
        eyeMover.enabled = false;
        eyeMover.ZeroEyes();
        mouthMover.enabled = false;
        mouthMover.mouthZero();
        StartLerp(baiHead, anchorHeadDeactive, .5f);
        StartLerp(baiBody, anchorBodyDeactive, 1);
        StartLerp(baiArmL, anchorArmLDeactive, 1f);
        StartLerp(baiArmR, anchorArmRDeactive, 1f);
        StartLerp(baiCupL, anchorCupLBody, .25f);
        StartLerp(baiCupR, anchorCupRBody, .25f);
        //baiEmo.DeactivateFace(1);
        active = false;
        handsAttached = false;
        //rotateManager.ShutDownAttention();
        if (m_audiosource != null && deactivateSound != null)
        {
            //m_audiosource.clip = deactivateSound;
            //m_audiosource.Play();
        }
    }

    public void AttachHands()
    {
        if (active)
        {
            StartLerp(baiCupL, anchorCupLArm, .25f);
            StartLerp(baiCupR, anchorCupRArm, .25f);
        }
    }

    public void ReturnHands()
    {
        if (active)
        {
            StartLerp(baiCupL, anchorCupLBody, .25f);
            StartLerp(baiCupR, anchorCupRBody, .25f);
        }
    }

    // Coroutine to parent childObject to parentObject and lerp transformations
    public IEnumerator ParentAndLerpTransform(GameObject childObject, GameObject parentObject, float duration)
    {
        if (childObject == null || parentObject == null)
        {
            Debug.LogError("One or both GameObjects are null.");
            yield break; // Exit the coroutine if objects are null
        }

        // Set childObject's parent to parentObject
        childObject.transform.SetParent(parentObject.transform);

        float time = 0; // Initialize time variable to track lerp progress

        // Store initial values for lerp
        Vector3 initialPosition = childObject.transform.localPosition;
        Quaternion initialRotation = childObject.transform.localRotation;
        Vector3 initialScale = childObject.transform.localScale;

        while (time < duration)
        {
            // Calculate the fraction of the duration that has passed
            float fraction = time / duration;

            // Lerp transformations
            childObject.transform.localPosition = Vector3.Lerp(initialPosition, Vector3.zero, fraction);
            childObject.transform.localRotation = Quaternion.Lerp(initialRotation, Quaternion.identity, fraction);
            childObject.transform.localScale = Vector3.Lerp(initialScale, Vector3.one, fraction);

            // Increment time by the time passed since last frame
            time += Time.deltaTime;

            // Wait until next frame to continue
            yield return null;
        }
        AlignChild(childObject);
    }

    public void AttachPrefabToAnchor(GameObject anchor)
    {
        baiPrefab.transform.SetParent(anchor.transform);
        AlignChild(baiPrefab);
    }

    private void AlignChild(GameObject child)
    {
        // Ensure final transformations are exactly as intended
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
    }

    // Example usage
    // Method to start the coroutine with specific GameObjects
    public void StartLerp(GameObject child, GameObject parent, float duration)
    {
        // Generate a unique key for the child-parent pair
        GameObject key = child; // For simplicity, we use child as the key. Adjust based on needs.

        // If a coroutine for these GameObjects is already running, stop it
        if (gameObjectCoroutines.TryGetValue(key, out Coroutine runningCoroutine))
        {
            StopCoroutine(runningCoroutine);
            gameObjectCoroutines.Remove(key);
        }

        // Start the new coroutine and store it in the dictionary
        Coroutine newCoroutine = StartCoroutine(ParentAndLerpTransform(child, parent, duration));
        gameObjectCoroutines[key] = newCoroutine;
    }

    // Optional: Method to stop all coroutines if needed
    public void StopAllManagedCoroutines()
    {
        foreach (var coroutine in gameObjectCoroutines.Values)
        {
            StopCoroutine(coroutine);
        }
        gameObjectCoroutines.Clear();
    }

    public void ToggleHands()
    {
        if (handsAttached)
        {
            ReturnHands();
            handsAttached = false;
        }
        else
        {
            AttachHands();
            handsAttached = true;
        }
    }
}
