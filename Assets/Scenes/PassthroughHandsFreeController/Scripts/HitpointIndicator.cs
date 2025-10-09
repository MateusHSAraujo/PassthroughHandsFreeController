using System;
using UnityEngine;

public class HitpointIndicator : MonoBehaviour
{
    public event Action OnIndicatorFilled; // Callback for activation events

    private float timeToTrigger; // Default time to trigger
    private float maxScale; // Default maximum scale
    private float scaleSpeed; // Speed at which the indicator scales
    private bool isScaling; // Flag to check if scaling is in progress


    // Update is called once per frame
    void Update()
    {
        if (isScaling)
        {
            DebugLogger.Log("Scaling hitpoint indicator.", this);
            // Calculate the new scale for the hitpoint indicator
            float scaleDelta = scaleSpeed * Time.deltaTime;
            DebugLogger.Log($@"Scaling hitpoint indicator.  
                     Current hitpoint indicator scale: {transform.localScale}; 
                     Hitpoint indicator scale speed: {scaleSpeed}; 
                     Time.deltaTime: {Time.deltaTime};
                     Scaling hitpoint indicator by: {scaleDelta}", this);
            // Applying new scale
            transform.localScale += new Vector3(scaleDelta, scaleDelta, scaleDelta);
            DebugLogger.Log($"New hitpoint indicator scale{transform.localScale}", this);
        
            if (transform.localScale.x >= maxScale)
            {
                DebugLogger.Log("Hitpoint indicator reached maximum scale. Triggering activation callback.", this);
                OnIndicatorFilled?.Invoke(); // Invoke the activation callback if set
                StopScaling(); // Stop scaling after reaching maximum scale
            }
        }
    }

    public void Init(float timeToTrigger, float maxScale)
    {
        DebugLogger.Log("Initializing hitpoint indicator with time to trigger: " + timeToTrigger + ", max scale: " + maxScale, this);
        gameObject.SetActive(false);
        transform.localScale = Vector3.one; // Reset scale to default when activated
        this.timeToTrigger = timeToTrigger;
        this.maxScale = maxScale;
        scaleSpeed = maxScale / timeToTrigger; // Calculate scale speed based on time to trigger
        isScaling = false; // Initially not scaling
    }

    public void StartScaling()
    {
        isScaling = true; // Set scaling flag to true
        gameObject.SetActive(true); // Activate the hitpoint indicator
        transform.localScale = Vector3.one; // Reset scale to default when activated
    }

    public void StopScaling()
    {
        isScaling = false; // Set scaling flag to false
        gameObject.SetActive(false); // Deactivate the hitpoint indicator
    }

}

