using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(MetaXRAudioSource))]
public class ToggleGazeInteractor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Time in seconds to wait before toggling gaze interaction")]
    public float timeToToggle = 2.0f;

    private Coroutine timeoutToActivationCorroutine;
    private Toggle toggle;
    private AudioSource AudioSource;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        if (toggle == null) DebugLogger.LogError("Toggle component not found on the GameObject.");

        AudioSource = GetComponent<AudioSource>();
        if (AudioSource == null) DebugLogger.LogError("AudioSource component not found. Toggle audio won't play.");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        DebugLogger.Log("Pointer is over me", this);
        timeoutToActivationCorroutine = StartCoroutine(TimeOutToActivation());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DebugLogger.Log("Pointer left me", this);
        if (timeoutToActivationCorroutine != null) StopCoroutine(timeoutToActivationCorroutine);
    }

    private IEnumerator TimeOutToActivation()
    {
        DebugLogger.Log("Starting my timeout", this);
        float timePassed = 0.0f;

        while (timePassed < timeToToggle)
        {
            timePassed += Time.deltaTime;
            yield return null;
        }

        DebugLogger.Log("Toggle activated. Calling subscribed functions.");
        AudioSource.Play();
        toggle.isOn = true;
    }
}
