using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;

using Meta.XR.MRUtilityKit;

public class CameraGazeCursorDepthSensor : MonoBehaviour
{
    public Transform cameraTransform;
    public GameObject prefabCursor;
    public GameObject prefabHitpointIndicator;

    public float distanceThreshold = 0.05f; // Distance threshold to change cursor position
    public float hitpointIndicatorScaleFactor = 1.001f; // Scale factor for hitpoint indicator

    private Ray ray;
    private RaycastHit prevHit;
    private GameObject cursor;
    private GameObject hitpointIndicator;
    private EnvironmentRaycastManager raycastManager;

    void Awake()
    {
        // Find the EnvironmentRaycastManager in the scene
        raycastManager = FindFirstObjectByType<EnvironmentRaycastManager>();
        if (raycastManager == null)
        {
            Debug.LogError("EnvironmentRaycastManager not found in the scene.");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cursor = Instantiate(prefabCursor, Vector3.zero, Quaternion.identity);
        hitpointIndicator = Instantiate(prefabHitpointIndicator, Vector3.zero, Quaternion.identity);
        hitpointIndicator.transform.SetParent(cursor.transform); // Set hitpoint indicator as a child of the cursor
        cursor.SetActive(false); // Initially disable the cursor
        hitpointIndicator.SetActive(false); // Initially disable the hitpoint indicator
       
    }

    // Update is called once per frame
    void Update()
    {
        ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (raycastManager.Raycast(ray, out EnvironmentRaycastHit hit1))
        {
            Debug.Log("Hit: " + hit1.point);

            // Define a rotação do cursor para que ele se alinhe com a normal da superfície.
            // O primeiro argumento (hit1.normal) faz com que o "frente" do cursor aponte na direção da normal.
            // O segundo argumento (cameraTransform.up) define a orientação "para cima" do cursor.
            Quaternion cursorRotation = Quaternion.LookRotation(hit1.normal, cameraTransform.up);

            if (!cursor.activeSelf)
            {
                prevHit.point = hit1.point; // Armazena o ponto de colisão inicial
                cursor.transform.SetPositionAndRotation(hit1.point, cursorRotation);
                cursor.SetActive(true);
            }
            else if (Vector3.Distance(prevHit.point, hit1.point) > distanceThreshold)
            {
                hitpointIndicator.SetActive(false);
                prevHit.point = hit1.point; // Atualiza o ponto de colisão anterior
                cursor.transform.SetPositionAndRotation(hit1.point, cursorRotation);
            }
            else
            {
                if (!hitpointIndicator.activeSelf)
                {
                    hitpointIndicator.transform.localScale = Vector3.one;
                    hitpointIndicator.SetActive(true);
                }
                else
                {
                    hitpointIndicator.transform.localScale *= hitpointIndicatorScaleFactor;
                }
            }
        }
        /*
            // Logic to make de camera gaze cursor work with the raycast based on the scene mesh pre-loaded
            if (Physics.Raycast(ray, out RaycastHit hit))
            {

                Debug.Log("Hit: " + hit.collider.gameObject.name);
                if (hit.collider.gameObject.name == "FLOOR_EffectMesh")
                {
                    if (cursor.activeSelf == false)
                    {
                        Debug.Log("Floor hit and cursor disabled. Enabling cursor.");
                        prevHit = hit; // Store the initial hit point
                        cursor.transform.position = hit.point;
                        cursor.SetActive(true);
                    }
                    else if (Vector3.Distance(prevHit.point, hit.point) > distanceThreshold)
                    {
                        Debug.Log("Floor hit outside distance. Changing cursor position. Deactivating hitpoint indicator.");
                        hitpointIndicator.SetActive(false);
                        prevHit = hit; // Update the previous hit point
                        cursor.transform.position = hit.point;
                    }
                    else
                    {
                        Debug.Log("Floor hit inside distance. Scaling hitpoint indicator.");
                        if (hitpointIndicator.activeSelf == false)
                        {
                            hitpointIndicator.transform.localScale = Vector3.one;
                            hitpointIndicator.SetActive(true);
                        }
                        else hitpointIndicator.transform.localScale = hitpointIndicator.transform.localScale * hitpointIndicatorScaleFactor;
                    }
                }
                else
                {
                    Debug.Log("Hit on non-floor object. Deactivating cursor and hitpoint indicator.");
                    cursor.SetActive(false); // Disable cursor if not hitting the floor
                    hitpointIndicator.SetActive(false); // Disable hitpoint indicator if not hitting the floor
                }
            }
            else
            {
                Debug.Log("No hit detected");
            }
            */
        }
    
}
