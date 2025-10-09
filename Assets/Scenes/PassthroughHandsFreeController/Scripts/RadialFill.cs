using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RadialFill : MonoBehaviour
{

    [SerializeField] private Image radialFillImage;
    [SerializeField] private Image checkMarkImage;

    [Tooltip("Audio source to play the sound when filling completes")]
    [SerializeField] private AudioSource m_AudioSource;

    [Tooltip("Factor to scale scan marker proportionaly to trackable boundry box")]
    [SerializeField] private float scaleMultiplier = 0.6f;

    [SerializeField] CanvasGroup m_canvasGroup;

    private RectTransform m_rectTransform;
    private float fillDuration;
    private Coroutine fillCoroutine = null;
    private bool isFilling = false;

    void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(Rect parentRect)
    {
        transform.Rotate(0f, 180f, 0f, Space.Self);

        Vector2 parentSize = parentRect.size;
        Vector2 canvasSize = m_rectTransform.sizeDelta;

        float scaleX = parentSize.x / canvasSize.x;
        float scaleY = parentSize.y / canvasSize.y;

        transform.localScale = new Vector3(scaleX * scaleMultiplier, scaleY * scaleMultiplier, 1f);
        transform.localPosition = new Vector3(0, 0, 0.01f); // Position canvas a litle offset from the trackable

        // Make it invisible
        m_canvasGroup.alpha = 0;
        m_canvasGroup.interactable = false;
        m_canvasGroup.blocksRaycasts = false;
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
        DebugLogger.Log("Stop filling called");
        if (isFilling)
        {
            if (fillCoroutine != null) StopCoroutine(fillCoroutine);
            radialFillImage.fillAmount = 0;
            checkMarkImage.fillAmount = 0;
            isFilling = false;
        }
        else
        {
            DebugLogger.LogWarning("Currently not filling. Ignoring");
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

    public void ShowCanvas()
    {
        m_canvasGroup.alpha = 1;
        m_canvasGroup.interactable = true;
        m_canvasGroup.blocksRaycasts = true;
    }

    public void HideCanvas()
    {
        m_canvasGroup.alpha = 0;
        m_canvasGroup.interactable = false;
        m_canvasGroup.blocksRaycasts = false;
    }
}
