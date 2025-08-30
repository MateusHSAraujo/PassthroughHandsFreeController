using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ToggleGazeInteractor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Time in seconds to wait before toggling gaze interaction")]
    public float timeToToggle = 2.0f;

    private Coroutine timeoutToActivationCorroutine;
    private Toggle m_toggle;
    private AudioSource m_AudioSource;

    void Awake()
    {
        m_toggle = GetComponent<Toggle>();
        if (m_toggle == null) DebugLogger.LogError("Toggle component not found on the GameObject.");

        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null) DebugLogger.LogError("AudioSource component not found. Toggle audio won't play.");
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
        m_AudioSource.Play();
        m_toggle.isOn = true;
    }
}
