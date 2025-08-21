using UnityEngine;
using UnityEngine.UIElements;

public class AudioScaleEffect : MonoBehaviour
{
    public Transform objectToScale; // Assign the object you want to scale
    public float minScale = 1f; // Minimum scale of the object
    public float maxScale = 3f; // Maximum scale of the object
    public float sensitivity = 2f; // How sensitive is the scale effect to the audio
    public bool useAmplitude = true; // Use amplitude instead of spectrum data

    [SerializeField]
    public AudioSource audioSource;
    private float[] audioSpectrum;
    private Vector3 startScale;
    private AudioListener audioListener;

    // Keep track of whether we're actively scaling
    private bool isScaling = false;
    private float lastIntensity = 0f;
    private float intensitySmoothing = .5f; // Lower for smoother transitions

    void Start()
    {
        // Store original scale
        if(objectToScale != null)
            startScale = objectToScale.localScale;
            
        // Get or create AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = true;
                audioSource.spatialBlend = 0f; // 2D sound
            }
        }
        
        // Make sure we have an AudioListener in the scene
        audioListener = FindObjectOfType<AudioListener>();
        if (audioListener == null)
        {
            Debug.LogWarning("No AudioListener found in scene. Adding one to this GameObject.");
            audioListener = gameObject.AddComponent<AudioListener>();
        }
        
        // Initialize audio spectrum array
        if (objectToScale != null)
        {
            audioSpectrum = new float[512]; // Increased for better resolution
        }
        
        Debug.Log("AudioScaleEffect initialized with AudioSource: " + (audioSource != null));
    }

    void Update()
    {
        if (audioSource != null && objectToScale != null)
        {
            float currentIntensity = 0f;
            
            if (useAmplitude)
            {
                // Use average amplitude from output data
                float[] outputData = new float[1024];
                audioSource.GetOutputData(outputData, 0);
                float sum = 0f;
                for (int i = 0; i < outputData.Length; i++)
                {
                    sum += Mathf.Abs(outputData[i]);
                }
                currentIntensity = (sum / outputData.Length) * sensitivity * 10f;
            }
            else
            {
                // Use spectrum data (frequency analysis)
                audioSource.GetSpectrumData(audioSpectrum, 0, FFTWindow.BlackmanHarris);
                currentIntensity = GetMaxIntensity() * sensitivity;
            }
            
            // Smooth the intensity value
            lastIntensity = Mathf.Lerp(lastIntensity, currentIntensity, intensitySmoothing);
            
            // Only scale if we have meaningful audio
            if (lastIntensity > 0.001f)
            {
                isScaling = true;
                float scale = Mathf.Lerp(minScale, maxScale, lastIntensity);
                objectToScale.localScale = new Vector3(.2f, .001f, scale);
                
                // Debug to see the values
                if (Time.frameCount % 60 == 0) // Log once per second at 60fps
                {
                    Debug.Log($"Audio intensity: {lastIntensity}, Scale: {scale}");
                }
            }
            else if (isScaling)
            {
                // Return to minimum scale when audio stops
                objectToScale.localScale = new Vector3(.2f, .001f, minScale);
                isScaling = false;
            }
        }
    }

    float GetMaxIntensity()
    {
        float maxIntensity = 0f;
        foreach (float intensity in audioSpectrum)
        {
            if (intensity > maxIntensity) maxIntensity = intensity;
        }
        return maxIntensity;
    }

    public void mouthZero()
    {
        objectToScale.localScale = new Vector3(startScale.x, startScale.y, 0);
    }
}