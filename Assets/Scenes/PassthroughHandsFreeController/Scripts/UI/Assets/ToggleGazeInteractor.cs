using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum GazeButtonStyle
{
    Secondary = 0,
    Primary = 1,
    Danger = 2,
    Keypad = 3,
    SubtleIcon = 4,
    Custom = 5
}

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(MetaXRAudioSource))]
public class ToggleGazeInteractor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Gaze Dwell Timing")]
    [Tooltip("Time in seconds to wait before triggering the button action")]
    public float timeToToggle = 2.0f;

    [Tooltip("If true, continues triggering repeatedly while gaze remains on the button (useful for keypad keys)")]
    public bool repeatWhileHovered = false;

    [Tooltip("Cooldown in seconds between repeated activations when repeatWhileHovered is true")]
    public float repeatCooldown = 0.25f;

    [Header("Procedural Style & Feedback")]
    public GazeButtonStyle stylePreset = GazeButtonStyle.Secondary;
    public Graphic backgroundGraphic;
    public Image progressBarFill;

    public Color normalColor = new(1f, 1f, 1f, 0.10f);
    public Color dwellFillColor = new(0f, 0.39f, 0.88f, 0.88f);
    public Color progressBarColor = new(0.35f, 0.78f, 1f, 1f);
    public float hoverScale = 1.06f;
    public float returnLerpSpeed = 10f;

    public event Action OnGazeActivated;

    private Toggle _toggle;
    private Button _button;
    private AudioSource _audioSource;
    private RectTransform _progressBarRect;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseScaleInitialized = false;

    private bool _isHovered = false;
    private float _dwellElapsed = 0f;
    private float _cooldownRemaining = 0f;
    private bool _triggeredThisHover = false;

    private Color? _overrideBaseColor = null;

    private void Awake()
    {
        InitializeReferences();
        ApplyStylePresetColors();
        ResetVisualStateImmediate();
    }

    private void OnEnable()
    {
        InitializeReferences();
        ResetVisualStateImmediate();
    }

    private void OnDisable()
    {
        ResetVisualStateImmediate();
    }

    public void InitializeReferences()
    {
        if (!_baseScaleInitialized)
        {
            _baseScale = transform.localScale;
            if (_baseScale.sqrMagnitude < 0.0001f)
            {
                _baseScale = Vector3.one;
            }
            _baseScaleInitialized = true;
        }

        _toggle = GetComponent<Toggle>();
        _button = GetComponent<Button>();
        _audioSource = GetComponent<AudioSource>();

        // Disable legacy Animator so procedural dwell animation has full control
        Animator legacyAnimator = GetComponent<Animator>();
        if (legacyAnimator != null)
        {
            legacyAnimator.enabled = false;
        }

        if (_toggle != null)
        {
            _toggle.transition = Selectable.Transition.None;
        }
        if (_button != null)
        {
            _button.transition = Selectable.Transition.None;
        }

        if (backgroundGraphic == null)
        {
            Transform contentBg = transform.Find("Content/Background");
            if (contentBg != null)
            {
                backgroundGraphic = contentBg.GetComponent<Graphic>();
            }
            if (backgroundGraphic == null)
            {
                backgroundGraphic = GetComponent<Graphic>();
            }
        }

        // Ensure icon and label inside Content/Background/Elements are crisp white against dark/colored button backgrounds
        Transform iconTf = transform.Find("Content/Background/Elements/Icon");
        if (iconTf != null && iconTf.TryGetComponent<Graphic>(out var iconGraphic))
        {
            iconGraphic.color = Color.white;
        }
        Transform labelTf = transform.Find("Content/Background/Elements/Text/Label");
        if (labelTf != null && labelTf.TryGetComponent<Graphic>(out var labelGraphic))
        {
            labelGraphic.color = Color.white;
        }

        EnsureProgressBarExists();
    }

    public void ApplyStylePresetColors()
    {
        switch (stylePreset)
        {
            case GazeButtonStyle.Primary:
                normalColor = new Color(0.00f, 0.39f, 0.88f, 0.88f);
                dwellFillColor = new Color(0.18f, 0.58f, 1.00f, 1.00f);
                progressBarColor = new Color(0.65f, 0.90f, 1.00f, 1.00f);
                hoverScale = 1.06f;
                break;

            case GazeButtonStyle.Secondary:
                normalColor = new Color(1.00f, 1.00f, 1.00f, 0.10f);
                dwellFillColor = new Color(0.00f, 0.39f, 0.88f, 0.85f);
                progressBarColor = new Color(0.25f, 0.72f, 1.00f, 1.00f);
                hoverScale = 1.06f;
                break;

            case GazeButtonStyle.Danger:
                normalColor = new Color(1.00f, 0.32f, 0.32f, 0.15f);
                dwellFillColor = new Color(0.85f, 0.22f, 0.23f, 0.92f);
                progressBarColor = new Color(1.00f, 0.60f, 0.60f, 1.00f);
                hoverScale = 1.06f;
                break;

            case GazeButtonStyle.Keypad:
                normalColor = new Color(1.00f, 1.00f, 1.00f, 0.12f);
                dwellFillColor = new Color(0.14f, 0.50f, 0.98f, 0.88f);
                progressBarColor = new Color(0.40f, 0.82f, 1.00f, 1.00f);
                hoverScale = 1.05f;
                break;

            case GazeButtonStyle.SubtleIcon:
                normalColor = new Color(0.22f, 0.28f, 0.35f, 0.85f);
                dwellFillColor = new Color(0.00f, 0.39f, 0.88f, 0.95f);
                progressBarColor = new Color(0.40f, 0.82f, 1.00f, 1.00f);
                hoverScale = 1.08f;
                break;

            case GazeButtonStyle.Custom:
                break;
        }

        if (progressBarFill != null)
        {
            progressBarFill.color = progressBarColor;
        }
    }

    public void SetOverrideBaseColor(Color? overrideColor)
    {
        _overrideBaseColor = overrideColor;
        if (!_isHovered && backgroundGraphic != null)
        {
            backgroundGraphic.color = GetEffectiveBaseColor();
        }
    }

    private Color GetEffectiveBaseColor()
    {
        return _overrideBaseColor ?? normalColor;
    }

    private void EnsureProgressBarExists()
    {
        if (progressBarFill == null)
        {
            Transform existingOnRoot = transform.Find("GazeProgressBar");
            if (existingOnRoot != null)
            {
                progressBarFill = existingOnRoot.GetComponent<Image>();
            }
            else if (backgroundGraphic != null)
            {
                Transform existingOnBg = backgroundGraphic.transform.Find("GazeProgressBar");
                if (existingOnBg != null)
                {
                    progressBarFill = existingOnBg.GetComponent<Image>();
                }
            }
        }

        if (progressBarFill == null)
        {
            GameObject barObj = new("GazeProgressBar");
            barObj.layer = gameObject.layer;
            barObj.transform.SetParent(transform, false);
            progressBarFill = barObj.AddComponent<Image>();
            progressBarFill.raycastTarget = false;
        }

        LayoutElement layoutElement = progressBarFill.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = progressBarFill.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        _progressBarRect = progressBarFill.GetComponent<RectTransform>();
        _progressBarRect.anchorMin = new Vector2(0f, 0f);
        _progressBarRect.anchorMax = new Vector2(0f, 0f);
        _progressBarRect.pivot = new Vector2(0f, 0f);
        _progressBarRect.anchoredPosition = new Vector2(0f, 0f);
        _progressBarRect.sizeDelta = new Vector2(0f, 3f);

        progressBarFill.color = progressBarColor;
        progressBarFill.raycastTarget = false;
        progressBarFill.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        DebugLogger.Log("Pointer is over me", this);
        _isHovered = true;
        _dwellElapsed = 0f;
        _cooldownRemaining = 0f;
        _triggeredThisHover = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DebugLogger.Log("Pointer left me", this);
        _isHovered = false;
        _dwellElapsed = 0f;
        _cooldownRemaining = 0f;
        _triggeredThisHover = false;

        if (progressBarFill != null)
        {
            progressBarFill.enabled = false;
        }
    }

    private bool IsInteractable()
    {
        if (_toggle != null && !_toggle.interactable) return false;
        if (_button != null && !_button.interactable) return false;
        return true;
    }

    private void Update()
    {
        Color baseColor = GetEffectiveBaseColor();

        if (_isHovered && IsInteractable())
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
                SetProgressVisuals(0f, baseColor);
                return;
            }

            if (!_triggeredThisHover || repeatWhileHovered)
            {
                _dwellElapsed += Time.deltaTime;
                float duration = Mathf.Max(0.05f, timeToToggle);
                float progress = Mathf.Clamp01(_dwellElapsed / duration);

                SetProgressVisuals(progress, baseColor);

                bool controllerConfirmPressed = OVRInput.GetDown(OVRInput.RawButton.A);
                if (progress >= 1.0f || controllerConfirmPressed)
                {
                    TriggerActivation();
                }
            }
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, returnLerpSpeed * Time.deltaTime);
            if (backgroundGraphic != null)
            {
                backgroundGraphic.color = Color.Lerp(backgroundGraphic.color, baseColor, returnLerpSpeed * Time.deltaTime);
            }
            if (progressBarFill != null && progressBarFill.enabled)
            {
                progressBarFill.enabled = false;
            }
            if (_progressBarRect != null)
            {
                _progressBarRect.anchorMax = new Vector2(0f, 0f);
            }
        }
    }

    private void SetProgressVisuals(float progress, Color baseColor)
    {
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        float scaleMultiplier = Mathf.Lerp(1.0f, hoverScale, smoothProgress);
        transform.localScale = _baseScale * scaleMultiplier;

        if (backgroundGraphic != null)
        {
            backgroundGraphic.color = Color.Lerp(baseColor, dwellFillColor, smoothProgress);
        }

        if (progressBarFill != null)
        {
            progressBarFill.enabled = progress > 0.005f;
        }

        if (_progressBarRect != null)
        {
            _progressBarRect.anchorMin = new Vector2(0f, 0f);
            _progressBarRect.anchorMax = new Vector2(progress, 0f);
            _progressBarRect.anchoredPosition = new Vector2(0f, 0f);
            _progressBarRect.sizeDelta = new Vector2(0f, 3f);
        }
    }

    private void TriggerActivation()
    {
        DebugLogger.Log("Toggle activated. Calling subscribed functions.");
        _dwellElapsed = 0f;
        _triggeredThisHover = true;
        _cooldownRemaining = repeatCooldown;

        if (progressBarFill != null)
        {
            progressBarFill.enabled = false;
        }
        if (_progressBarRect != null)
        {
            _progressBarRect.anchorMax = new Vector2(0f, 0f);
        }

        if (_audioSource != null && _audioSource.clip != null)
        {
            _audioSource.Play();
        }

        if (_toggle != null)
        {
            _toggle.SetIsOnWithoutNotify(false);
            _toggle.isOn = true;
            _toggle.SetIsOnWithoutNotify(false);
        }

        if (_button != null)
        {
            _button.onClick.Invoke();
        }

        OnGazeActivated?.Invoke();
    }

    public void ResetVisualStateImmediate()
    {
        _isHovered = false;
        _dwellElapsed = 0f;
        _cooldownRemaining = 0f;
        _triggeredThisHover = false;

        if (_baseScaleInitialized)
        {
            transform.localScale = _baseScale;
        }
        if (backgroundGraphic != null)
        {
            backgroundGraphic.color = GetEffectiveBaseColor();
        }
        if (progressBarFill != null)
        {
            progressBarFill.enabled = false;
        }
        if (_progressBarRect != null)
        {
            _progressBarRect.anchorMin = new Vector2(0f, 0f);
            _progressBarRect.anchorMax = new Vector2(0f, 0f);
            _progressBarRect.anchoredPosition = new Vector2(0f, 0f);
            _progressBarRect.sizeDelta = new Vector2(0f, 3f);
        }
    }
}
