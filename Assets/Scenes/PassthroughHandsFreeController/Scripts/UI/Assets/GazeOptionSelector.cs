using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gaze-friendly single-choice selector: a row of dwell buttons where exactly one is highlighted.
/// Options can be marked unavailable (shown dimmed and not interactable).
/// The hierarchy is serialized in the prefab (built once by <see cref="Create"/>); listeners are bound at runtime.
/// </summary>
public class GazeOptionSelector : MonoBehaviour
{
    public event Action<int> OptionSelected;

    [SerializeField] private Button[] optionButtons = Array.Empty<Button>();

    private bool[] _available;
    private int _selectedIndex = -1;
    private bool _bound;

    public int SelectedIndex => _selectedIndex;
    public int OptionCount => optionButtons.Length;

    /// <summary>Builds the selector hierarchy under <paramref name="parent"/>. Used by the editor layout builder.</summary>
    public static GazeOptionSelector Create(
        Transform parent,
        string name,
        string[] labels,
        float dwellTime,
        GazeUITheme theme,
        float height = 30f,
        float fontSize = 11f)
    {
        HorizontalLayoutGroup row = GazeUIFactory.CreateRow(parent, name, height, 5f);
        row.childForceExpandWidth = true;
        GazeOptionSelector selector = row.gameObject.AddComponent<GazeOptionSelector>();
        selector.optionButtons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var parts = GazeUIFactory.CreateButton(row.transform, $"Option_{i}", labels[i],
                GazeButtonStyle.Secondary, dwellTime, false, theme, fontSize, height);
            GazeUIFactory.SetLayoutSize(parts.Button, flexibleWidth: 1f);
            selector.optionButtons[i] = parts.Button;
        }
        return selector;
    }

    private void Awake()
    {
        Bind();
    }

    /// <summary>Hooks button listeners. Idempotent; safe to call before Awake.</summary>
    public void Bind()
    {
        if (_bound) return;
        _bound = true;
        _available = new bool[optionButtons.Length];
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            _available[i] = true;
            if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => Select(index, true));
        }
    }

    public void SetAvailable(int index, bool available)
    {
        Bind();
        if (index < 0 || index >= _available.Length) return;
        _available[index] = available;
        Refresh();
    }

    public void Select(int index, bool notify)
    {
        Bind();
        if (index < 0 || index >= optionButtons.Length || !_available[index]) return;
        bool changed = index != _selectedIndex;
        _selectedIndex = index;
        Refresh();
        if (notify && changed) OptionSelected?.Invoke(index);
    }

    private void Refresh()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            var parts = GazeUIFactory.GetParts(optionButtons[i]);
            if (parts?.Interactor != null) GazeUIFactory.ApplySelectionState(parts, i == _selectedIndex, _available[i]);
        }
    }
}
