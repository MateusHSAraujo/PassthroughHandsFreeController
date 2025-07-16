using UnityEngine;


public class CursorController : MonoBehaviour
{
    public float distanceThreshold; // Distance threshold to change cursor position
    public float hitpointTimeToTrigger; // Time spent scaling the hitpoint indicator before it is triggered
    public float maxHitpointIndicatorScale; // Maximum scale for hitpoint indicator

    //[TODO]: Add a scale factor to make the cursor bigger the further the hitpoint. Also scale the distanceThreshold to
    // make selecting the point easier.

    private Vector3 prevPosition; // Previous position of the cursor
    private HitpointIndicator hitpointIndicator;

    void Awake()
    {
        DebugLogger.Log("CursorController Awake method called.", this);
        // Ensure the cursor is initially inactive
        gameObject.SetActive(false);
        hitpointIndicator = GetComponentInChildren<HitpointIndicator>();
        if (hitpointIndicator == null) DebugLogger.LogError("HitpointIndicator component not found on this GameObject.");
            
        // Initiate the hitpoint indicator with the specified parameters
        hitpointIndicator.Init(hitpointTimeToTrigger, maxHitpointIndicatorScale);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DebugLogger.Log("CursorController Start method called.", this);
        prevPosition = Vector3.zero; // Initialize previous position to zero
    }

    private void OnEnable()
    {
        DebugLogger.Log("CursorController OnEnable method called.", this);
        // Subscribe to the OnIndicatorFilled event
        if (hitpointIndicator != null) hitpointIndicator.OnIndicatorFilled += DeactivateCursor; 
    }

    private void OnDisable()
    {
        DebugLogger.Log("CursorController OnDisable method called.", this);
        // Unsubscribe from the OnIndicatorFilled event
        if (hitpointIndicator != null) hitpointIndicator.OnIndicatorFilled -= DeactivateCursor;
    }


    public void UpdateCursorPosition(Vector3 newPosition, Quaternion cursorRotation)
    {
        DebugLogger.Log($"Updating cursor position. New position: {newPosition}");
        DebugLogger.Log($"Distance calculation: prevPosition = {prevPosition}, newPosition = {newPosition}, distance = {Vector3.Distance(prevPosition, newPosition)}");
        if (gameObject.activeSelf == false)
        {
            DebugLogger.Log("Floor hit and cursor disabled. Enabling cursor.");
            prevPosition = newPosition; // Store the initial hit point
            transform.SetPositionAndRotation(newPosition, cursorRotation); // Update cursor position and rotation
            gameObject.SetActive(true); // Activate cursor
        }
        else if (Vector3.Distance(prevPosition, newPosition) > distanceThreshold)
        {
            DebugLogger.Log("Floor hit outside threshold distance. Changing cursor position. Reseting hitpoint indicator.");
            prevPosition = newPosition; // Update the previous hit point
            hitpointIndicator.StartScaling(); // Reset the hitpoint indicator
            transform.SetPositionAndRotation(newPosition, cursorRotation); // Update cursor position and rotation
        }
        else
        {
            DebugLogger.Log("Floor hit inside distance. Keep scaling hitpoint indicator.");
        }
    }

    public void DeactivateCursor()
    {
        DebugLogger.Log("Deactivating cursor and hitpoint indicator.");
        hitpointIndicator.StopScaling(); // Stop scaling the hitpoint indicator
    }
}
