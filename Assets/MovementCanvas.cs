using UnityEngine;

public class MovementCanvas : MonoBehaviour
{

    void Awake()
    {
        // Initialize the canvas or any other setup if needed
        Debug.Log("MovementCanvas Awake called.");
        gameObject.SetActive(false); // Initially deactivate the canvas
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowCanvas(Vector3 position, Transform lookAt)
    {
        Debug.Log("Showing Movement Canvas at position: " + position);
        transform.position = position;
        transform.LookAt(lookAt);
        transform.Rotate(0f, 180f, 0f, Space.Self);
        gameObject.SetActive(true); // Activate the canvas
    }
    
    public void HideCanvas()
    {
        Debug.Log("Hiding Movement Canvas.");
        gameObject.SetActive(false); // Deactivate the canvas
    }
}
