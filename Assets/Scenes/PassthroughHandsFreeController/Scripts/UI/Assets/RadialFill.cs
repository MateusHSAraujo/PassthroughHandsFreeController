using UnityEngine;
using UnityEngine.UI;
using System.Collections;


public class RadialFill : MonoBehaviour
{

    [SerializeField] private Image radialFillImage;
    [SerializeField] private Image checkMarkImage;

    [Tooltip("Audio source to play the sound when filling completes")]
    [SerializeField] private AudioSource m_AudioSource;

    private float fillDuration;
    private Coroutine fillCoroutine = null;
    private bool isFilling = false;

    void Update()
    {   
        // ============================================================================
        // For testing purpose only
        /*
        if (OVRInput.Get(OVRInput.RawButton.LIndexTrigger) && !isFilling)
        {
            DebugLogger.Log("Input captured. Starting filling");
            StartFilling(5);
        }
        else if (OVRInput.Get(OVRInput.RawButton.RHandTrigger) && isFilling)
        {
            DebugLogger.Log("Input captured. Stopping filling");
            ResetFilling();
        }
        */
        // ============================================================================
    }
    

    // Update is called once per frame
    public void StartFilling(float fillDuration)
    {
        DebugLogger.Log("Start filling called");
        if (!isFilling)
        {
            this.fillDuration = fillDuration;
            isFilling = true;
            fillCoroutine = StartCoroutine(FillCoroutine());
        }
        else
        {
            DebugLogger.LogWarning("Already filling. Ignoring");
        }
    }

    public void ResetFilling()
    {
        DebugLogger.Log("Reset filling called");
        if (isFilling)
        {
            if (fillCoroutine != null) StopCoroutine(fillCoroutine);
            radialFillImage.fillAmount = 0;
            checkMarkImage.fillAmount = 0;
            isFilling = false;
        }
        else
        {
            DebugLogger.LogWarning("Not filling. Ignoring");
        }
    }

    private IEnumerator FillCoroutine()
    {
        float timePassed = 0.0f;
        DebugLogger.Log("Begin filling scan UI");
        while (timePassed < fillDuration)
        {
            float progress = timePassed / fillDuration;
            radialFillImage.color = Color.Lerp(
                progress < 0.5f ? Color.red : Color.yellow,
                progress < 0.5f ? Color.yellow : Color.green,
                progress < 0.5f ? progress * 2 : (progress - 0.5f) * 2);
            radialFillImage.fillAmount = progress;
            timePassed += Time.deltaTime;
            yield return null;
        }
        checkMarkImage.fillAmount = 1;
        radialFillImage.fillAmount = 1;
        DebugLogger.Log("Filling complete.");
        m_AudioSource.Play();
    }
}
