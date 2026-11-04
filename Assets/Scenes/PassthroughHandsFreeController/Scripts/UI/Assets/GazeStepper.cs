using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gaze-friendly numeric editor: optional preset buttons followed by [-] value [+].
/// The -/+ buttons repeat while gazed at, like the server keypad keys.
/// The hierarchy is serialized in the prefab (built once by <see cref="Create"/>); listeners are bound at runtime.
/// </summary>
public class GazeStepper : MonoBehaviour
{
    public event Action<float> ValueChanged;

    [Header("Range")]
    [SerializeField] private float min = 0f;
    [SerializeField] private float max = 100f;
    [SerializeField] private float step = 1f;
    [SerializeField] private string unit = string.Empty;

    [Header("Presets")]
    [SerializeField] private string[] presetLabels = Array.Empty<string>();
    [SerializeField] private float[] presetValues = Array.Empty<float>();
    [SerializeField] private Button[] presetButtons = Array.Empty<Button>();

    [Header("References")]
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private TextMeshProUGUI valueText;

    private float _value;
    private float? _effectiveLimit;
    private bool _bound;

    public float Value => _value;

    /// <summary>Builds the stepper hierarchy under <paramref name="parent"/>. Used by the editor layout builder.</summary>
    public static GazeStepper Create(
        Transform parent,
        string name,
        float min,
        float max,
        float step,
        string unit,
        (string label, float value)[] presets,
        float dwellTime,
        GazeUITheme theme,
        float height = 30f)
    {
        HorizontalLayoutGroup row = GazeUIFactory.CreateRow(parent, name, height, 5f);
        GazeStepper stepper = row.gameObject.AddComponent<GazeStepper>();
        stepper.min = min;
        stepper.max = max;
        stepper.step = step;
        stepper.unit = unit;
        presets ??= Array.Empty<(string, float)>();

        stepper.presetLabels = new string[presets.Length];
        stepper.presetValues = new float[presets.Length];
        stepper.presetButtons = new Button[presets.Length];
        for (int i = 0; i < presets.Length; i++)
        {
            stepper.presetLabels[i] = presets[i].label;
            stepper.presetValues[i] = presets[i].value;
            var parts = GazeUIFactory.CreateButton(row.transform, $"Preset_{presets[i].label}", presets[i].label,
                GazeButtonStyle.Secondary, dwellTime, false, theme, 11f, height);
            GazeUIFactory.SetLayoutSize(parts.Button, preferredWidth: 54f);
            stepper.presetButtons[i] = parts.Button;
        }

        var minus = GazeUIFactory.CreateButton(row.transform, "Decrease", "-", GazeButtonStyle.Keypad, dwellTime, true, theme, 16f, height);
        GazeUIFactory.SetLayoutSize(minus.Button, preferredWidth: height);
        stepper.decreaseButton = minus.Button;

        stepper.valueText = GazeUIFactory.CreateText(row.transform, "Value", string.Empty, 12f, GazeUIFactory.LabelColor,
            TextAlignmentOptions.Center, theme?.MediumFont);
        GazeUIFactory.SetLayoutSize(stepper.valueText, preferredWidth: 58f, flexibleWidth: 1f);

        var plus = GazeUIFactory.CreateButton(row.transform, "Increase", "+", GazeButtonStyle.Keypad, dwellTime, true, theme, 16f, height);
        GazeUIFactory.SetLayoutSize(plus.Button, preferredWidth: height);
        stepper.increaseButton = plus.Button;

        return stepper;
    }

    private void Awake()
    {
        Bind();
    }

    /// <summary>Hooks button listeners. Idempotent; safe to call before Awake.</summary>
    public void Bind()
    {
        if (_bound || decreaseButton == null || increaseButton == null) return;
        _bound = true;

        for (int i = 0; i < presetButtons.Length && i < presetValues.Length; i++)
        {
            float presetValue = presetValues[i];
            if (presetButtons[i] != null) presetButtons[i].onClick.AddListener(() => SetValue(presetValue, true));
        }
        decreaseButton.onClick.AddListener(() => SetValue(_value - step, true));
        increaseButton.onClick.AddListener(() => SetValue(_value + step, true));
    }

    /// <summary>Optional limit shown to the user (e.g. server max). Does not change the stored value.</summary>
    public void SetEffectiveLimit(float? limit)
    {
        _effectiveLimit = limit;
        Refresh();
    }

    public void SetValue(float value, bool notify)
    {
        Bind();
        float clamped = Mathf.Clamp(Mathf.Round(value / step) * step, min, max);
        bool changed = !Mathf.Approximately(clamped, _value);
        _value = clamped;
        Refresh();
        if (notify && changed) ValueChanged?.Invoke(_value);
    }

    private void Refresh()
    {
        if (valueText != null)
        {
            string suffix = string.IsNullOrEmpty(unit) ? string.Empty : $" {unit}";
            valueText.text = _effectiveLimit.HasValue && _effectiveLimit.Value < _value
                ? $"{_value:0} <size=80%>(max {_effectiveLimit.Value:0})</size>"
                : $"{_value:0}{suffix}";
        }

        for (int i = 0; i < presetButtons.Length && i < presetValues.Length; i++)
        {
            var parts = GazeUIFactory.GetParts(presetButtons[i]);
            if (parts?.Interactor != null) GazeUIFactory.ApplySelectionState(parts, Mathf.Approximately(presetValues[i], _value), true);
        }
    }
}
