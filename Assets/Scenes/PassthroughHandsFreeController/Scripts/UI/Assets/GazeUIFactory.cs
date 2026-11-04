using Oculus.Interaction.Samples;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared look for runtime-built gaze UI (rounded material, click sound, corner radii).</summary>
public class GazeUITheme
{
    /// <summary>Meta Interaction SDK RoundedBoxUI material; null renders plain rectangles.</summary>
    public Material RoundedMaterial;
    public AudioClip ClickClip;
    public float PanelRadius = 24f;
    /// <summary>Button corner radius as a fraction of the button height (existing 40px buttons use 22).</summary>
    public float ButtonRadiusFactor = 0.55f;
    public Color PanelColor = new(0.086f, 0.133f, 0.169f, 0.98f);

    // Roboto SDF fonts used by the other canvases (Meta Interaction SDK samples). Null = TMP default font.
    public TMP_FontAsset RegularFont;
    public TMP_FontAsset MediumFont;
    public TMP_FontAsset BoldFont;
}

/// <summary>
/// Builds gaze-dwell UI elements at runtime (buttons with <see cref="ToggleGazeInteractor"/>, labels, rows),
/// so new menus do not depend on hand-laid prefab hierarchies or transform.Find string paths.
/// </summary>
public static class GazeUIFactory
{
    public static readonly Color LabelColor = Color.white;
    public static readonly Color SubtleTextColor = new(0.75f, 0.86f, 0.96f, 0.9f);
    public static readonly Color SelectedColor = new(0.08f, 0.38f, 0.72f, 0.92f);
    public static readonly Color DisabledColor = new(1f, 1f, 1f, 0.04f);

    public class ButtonParts
    {
        public Button Button;
        public ToggleGazeInteractor Interactor;
        public TextMeshProUGUI Label;
        public Image Background;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static LayoutElement SetLayoutSize(Component target, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f)
    {
        LayoutElement le = target.GetComponent<LayoutElement>();
        if (le == null) le = target.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.preferredHeight = preferredHeight;
        le.flexibleWidth = flexibleWidth;
        if (preferredHeight > 0f) le.minHeight = preferredHeight;
        return le;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, TMP_FontAsset font = null)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    public static HorizontalLayoutGroup CreateRow(Transform parent, string name, float height, float spacing = 6f)
    {
        RectTransform rt = CreateRect(name, parent);
        HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        SetLayoutSize(rt, preferredHeight: height);
        return hlg;
    }

    /// <summary>Rebuilds the parts of a button created by <see cref="CreateButton"/> (e.g. after loading a prefab).</summary>
    public static ButtonParts GetParts(Button button)
    {
        if (button == null) return null;
        return new ButtonParts
        {
            Button = button,
            Interactor = button.GetComponent<ToggleGazeInteractor>(),
            Label = button.GetComponentInChildren<TextMeshProUGUI>(true),
            Background = button.targetGraphic as Image
        };
    }

    public static ButtonParts CreateButton(
        Transform parent,
        string name,
        string label,
        GazeButtonStyle style,
        float dwellTime,
        bool repeatWhileHovered = false,
        GazeUITheme theme = null,
        float fontSize = 12f,
        float height = 30f)
    {
        RectTransform rt = CreateRect(name, parent);

        Image bg = rt.gameObject.AddComponent<Image>();
        ApplyRounded(bg, theme, height * (theme?.ButtonRadiusFactor ?? 0f));
        AudioClip clickClip = theme?.ClickClip;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        TextMeshProUGUI tmp = CreateText(rt, "Label", label, fontSize, LabelColor, TextAlignmentOptions.Center, theme?.MediumFont);
        RectTransform labelRt = tmp.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(4f, 2f);
        labelRt.offsetMax = new Vector2(-4f, -2f);

        // ToggleGazeInteractor requires AudioSource + MetaXRAudioSource (added automatically by RequireComponent).
        ToggleGazeInteractor interactor = rt.gameObject.AddComponent<ToggleGazeInteractor>();
        AudioSource audio = rt.GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.playOnAwake = false;
            if (clickClip != null) audio.clip = clickClip;
        }

        interactor.backgroundGraphic = bg;
        interactor.stylePreset = style;
        interactor.timeToToggle = dwellTime;
        interactor.repeatWhileHovered = repeatWhileHovered;
        interactor.InitializeReferences();
        interactor.ApplyStylePresetColors();
        interactor.ResetVisualStateImmediate();

        return new ButtonParts { Button = button, Interactor = interactor, Label = tmp, Background = bg };
    }

    /// <summary>
    /// Gives an Image rounded corners using the same technique as the existing canvases:
    /// the RoundedBoxUI material plus a RoundedBoxUIProperties mesh modifier.
    /// </summary>
    public static void ApplyRounded(Image image, GazeUITheme theme, float radius)
    {
        if (image == null || theme == null || theme.RoundedMaterial == null || radius <= 0f) return;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.material = theme.RoundedMaterial;
        RoundedBoxUIProperties props = image.GetComponent<RoundedBoxUIProperties>();
        if (props == null) props = image.gameObject.AddComponent<RoundedBoxUIProperties>();
        props.borderRadius = Vector4.one * radius;
        image.SetAllDirty();
    }

    /// <summary>Visually marks a button as selected / unselected / unavailable.</summary>
    public static void ApplySelectionState(ButtonParts parts, bool selected, bool available)
    {
        parts.Button.interactable = available;
        parts.Label.color = available ? LabelColor : new Color(1f, 1f, 1f, 0.35f);
        Color? baseColor = !available ? DisabledColor : (selected ? SelectedColor : (Color?)null);
        parts.Interactor.SetOverrideBaseColor(baseColor);
    }
}
