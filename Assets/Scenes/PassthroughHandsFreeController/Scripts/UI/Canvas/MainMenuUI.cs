using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(LineOfSightPositioner))]
public class MainMenuUI : CameraGazeControlledCanvas
{
    public static MainMenuUI Instance;

    [Header("Main Menu UI Buttons:")]
    [Space(5)]
    public Toggle MoveButton;
    public Toggle RotateButton;
    public Toggle ConfigButton;

    [Header("Status & Subtitle References:")]
    public TextMeshProUGUI ServerStatusBadgeText;
    public Image ServerStatusDot;
    public TextMeshProUGUI ConfigButtonSubtitle;

    public event Action OnMoveRequested;
    public event Action OnRotateRequested;
    public event Action OnServerConfigRequested;

    private static readonly Color OnlineColor = new(0.18f, 0.80f, 0.44f, 1f);
    private static readonly Color CheckingColor = new(0.95f, 0.61f, 0.07f, 1f);
    private static readonly Color OfflineColor = new(1.00f, 0.42f, 0.42f, 1f);

    private int _statusCheckVersion = 0;
    private Coroutine _periodicStatusCoroutine;

    protected override void Awake()
    {
        DebugLogger.Log("MainMenuUI Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        enableSoftFollow = true;
        base.Awake();

        ApplyRichCardLayout();

        int overlayLayer = gameObject.layer;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = overlayLayer;
        }

        BindButtonEvents();
    }

    private void ApplyRichCardLayout()
    {
        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.sizeDelta = new Vector2(320f, 275f);
        }

        Transform bg = transform.Find("Backgroung");
        if (bg == null) return;

        if (bg.TryGetComponent<LayoutGroup>(out var bgLg))
        {
            bgLg.enabled = false;
        }

        Transform spaceTf = bg.Find("space");
        if (spaceTf != null)
        {
            spaceTf.gameObject.SetActive(false);
        }

        Transform titleTf = bg.Find("Text (TMP)");
        if (titleTf != null && titleTf.TryGetComponent<RectTransform>(out var titleRt))
        {
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -22f);
            titleRt.sizeDelta = new Vector2(280f, 26f);

            if (titleTf.TryGetComponent<TextMeshProUGUI>(out var titleTmp))
            {
                titleTmp.text = "Wheelchair Control";
                titleTmp.fontSize = 19f;
                titleTmp.color = Color.white;
                titleTmp.alignment = TextAlignmentOptions.Center;
            }
        }

        Transform statusPillTf = bg.Find("ServerStatusPill");
        if (statusPillTf != null && statusPillTf.TryGetComponent<RectTransform>(out var pillRt))
        {
            if (statusPillTf.TryGetComponent<LayoutElement>(out var pillLe))
            {
                pillLe.ignoreLayout = true;
            }
            pillRt.anchorMin = new Vector2(0.5f, 1f);
            pillRt.anchorMax = new Vector2(0.5f, 1f);
            pillRt.pivot = new Vector2(0.5f, 0.5f);
            pillRt.anchoredPosition = new Vector2(0f, -48f);
            pillRt.sizeDelta = new Vector2(276f, 20f);

            Transform dotTf = statusPillTf.Find("StatusDot");
            if (dotTf != null)
            {
                ServerStatusDot = dotTf.GetComponent<Image>();
            }

            Transform statusTxtTf = statusPillTf.Find("StatusText");
            if (statusTxtTf != null)
            {
                ServerStatusBadgeText = statusTxtTf.GetComponent<TextMeshProUGUI>();
            }
        }

        Transform buttonsTf = bg.Find("Buttons");
        if (buttonsTf != null && buttonsTf.TryGetComponent<RectTransform>(out var buttonsRt))
        {
            if (buttonsTf.TryGetComponent<LayoutGroup>(out var buttonsLg))
            {
                buttonsLg.enabled = false;
            }

            buttonsRt.anchorMin = new Vector2(0.5f, 1f);
            buttonsRt.anchorMax = new Vector2(0.5f, 1f);
            buttonsRt.pivot = new Vector2(0.5f, 1f);
            buttonsRt.anchoredPosition = new Vector2(0f, -68f);
            buttonsRt.sizeDelta = new Vector2(276f, 192f);
        }

        if (MoveButton != null)
        {
            ConfigureCardButton(
                MoveButton.transform,
                new Vector2(0f, -27f),
                new Vector2(276f, 52f),
                "Move Wheelchair",
                "Gaze at floor to choose destination",
                GazeButtonStyle.Primary,
                out _);
        }

        if (RotateButton != null)
        {
            ConfigureCardButton(
                RotateButton.transform,
                new Vector2(0f, -90f),
                new Vector2(276f, 52f),
                "Rotate in Place",
                "Align wheelchair with head gaze",
                GazeButtonStyle.Secondary,
                out _);
        }

        if (ConfigButton != null)
        {
            ConfigureCardButton(
                ConfigButton.transform,
                new Vector2(0f, -153f),
                new Vector2(276f, 52f),
                "RestThru Server",
                RestThruServer.AddressDisplay,
                GazeButtonStyle.Secondary,
                out TextMeshProUGUI serverSub);
            ConfigButtonSubtitle = serverSub;
        }
    }

    private void ConfigureCardButton(
        Transform btnTf,
        Vector2 anchoredPos,
        Vector2 size,
        string labelText,
        string subtitleText,
        GazeButtonStyle style,
        out TextMeshProUGUI subtitleTmp)
    {
        subtitleTmp = null;
        if (btnTf == null) return;

        if (btnTf.TryGetComponent<LayoutGroup>(out var rootLg))
        {
            rootLg.enabled = false;
        }

        if (btnTf.TryGetComponent<RectTransform>(out var rt))
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        Transform contentTf = btnTf.Find("Content");
        if (contentTf != null)
        {
            if (contentTf.TryGetComponent<LayoutGroup>(out var cLg))
            {
                cLg.enabled = false;
            }
            if (contentTf.TryGetComponent<RectTransform>(out var cRt))
            {
                cRt.anchorMin = Vector2.zero;
                cRt.anchorMax = Vector2.one;
                cRt.pivot = new Vector2(0.5f, 0.5f);
                cRt.offsetMin = Vector2.zero;
                cRt.offsetMax = Vector2.zero;
            }
        }

        Transform bgTf = btnTf.Find("Content/Background");
        if (bgTf != null)
        {
            if (bgTf.TryGetComponent<LayoutElement>(out var bgLe))
            {
                bgLe.ignoreLayout = true;
            }
            if (bgTf.TryGetComponent<RectTransform>(out var bgRt))
            {
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.pivot = new Vector2(0.5f, 0.5f);
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
            }
        }

        Transform elementsTf = btnTf.Find("Content/Background/Elements");
        if (elementsTf != null)
        {
            if (elementsTf.TryGetComponent<RectTransform>(out var eRt))
            {
                eRt.anchorMin = Vector2.zero;
                eRt.anchorMax = Vector2.one;
                eRt.offsetMin = new Vector2(14f, 6f);
                eRt.offsetMax = new Vector2(-14f, -6f);
            }
            if (elementsTf.TryGetComponent<HorizontalLayoutGroup>(out var hlg))
            {
                hlg.enabled = true;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.padding = new RectOffset(0, 0, 0, 0);
            }
        }

        Transform iconTf = btnTf.Find("Content/Background/Elements/Icon");
        if (iconTf != null && iconTf.TryGetComponent<Image>(out var iconImg))
        {
            iconImg.color = Color.white;
        }

        Transform textTf = btnTf.Find("Content/Background/Elements/Text");
        if (textTf != null)
        {
            if (textTf.TryGetComponent<RectTransform>(out var textRt))
            {
                textRt.sizeDelta = new Vector2(size.x - 68f, 34f);
            }

            Transform labelTf = textTf.Find("Label");
            if (labelTf != null && labelTf.TryGetComponent<TextMeshProUGUI>(out var lTmp))
            {
                lTmp.text = labelText;
                lTmp.fontSize = 14f;
                lTmp.color = Color.white;
                lTmp.overflowMode = TextOverflowModes.Overflow;
                lTmp.textWrappingMode = TextWrappingModes.NoWrap;
            }

            Transform subTf = textTf.Find("Subtitle");
            if (subTf != null && subTf.TryGetComponent<TextMeshProUGUI>(out subtitleTmp))
            {
                subTf.gameObject.SetActive(true);
                subtitleTmp.text = subtitleText;
                subtitleTmp.fontSize = 10.5f;
                subtitleTmp.color = new Color(0.75f, 0.86f, 0.96f, 0.9f);
                subtitleTmp.overflowMode = TextOverflowModes.Overflow;
                subtitleTmp.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        if (btnTf.TryGetComponent<ToggleGazeInteractor>(out var interactor))
        {
            interactor.stylePreset = style;
            interactor.InitializeReferences();
            interactor.ApplyStylePresetColors();
            interactor.ResetVisualStateImmediate();
        }
    }

    private void BindButtonEvents()
    {
        if (MoveButton != null)
        {
            MoveButton.onValueChanged = new Toggle.ToggleEvent();
            MoveButton.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                MoveButton.SetIsOnWithoutNotify(false);
                OnMoveRequested?.Invoke();
            });
        }

        if (RotateButton != null)
        {
            RotateButton.onValueChanged = new Toggle.ToggleEvent();
            RotateButton.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                RotateButton.SetIsOnWithoutNotify(false);
                OnRotateRequested?.Invoke();
            });
        }

        if (ConfigButton != null)
        {
            ConfigButton.onValueChanged = new Toggle.ToggleEvent();
            ConfigButton.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                ConfigButton.SetIsOnWithoutNotify(false);
                OnServerConfigRequested?.Invoke();
            });
        }
    }

    private void ResetButtons()
    {
        if (MoveButton != null) MoveButton.SetIsOnWithoutNotify(false);
        if (RotateButton != null) RotateButton.SetIsOnWithoutNotify(false);
        if (ConfigButton != null) ConfigButton.SetIsOnWithoutNotify(false);
    }

    public override void ShowCanvas()
    {
        ResetButtons();
        StartPeriodicStatusCheck();
        base.ShowCanvas();
    }

    public override void ShowCanvasAtDirectionOf(Vector3 targetPosition)
    {
        ResetButtons();
        StartPeriodicStatusCheck();
        base.ShowCanvasAtDirectionOf(targetPosition);
    }

    public override void HideCanvas()
    {
        if (_periodicStatusCoroutine != null)
        {
            StopCoroutine(_periodicStatusCoroutine);
            _periodicStatusCoroutine = null;
        }
        base.HideCanvas();
    }

    private void StartPeriodicStatusCheck()
    {
        if (_periodicStatusCoroutine != null)
        {
            StopCoroutine(_periodicStatusCoroutine);
        }
        _periodicStatusCoroutine = StartCoroutine(PeriodicStatusCheckRoutine());
    }

    private IEnumerator PeriodicStatusCheckRoutine()
    {
        while (true)
        {
            RefreshServerStatusAsync();
            yield return new WaitForSeconds(5f);
        }
    }

    private void EnsureStatusReferences()
    {
        if (ServerStatusBadgeText == null || ServerStatusDot == null)
        {
            Transform pill = transform.Find("Backgroung/ServerStatusPill");
            if (pill != null)
            {
                if (ServerStatusDot == null)
                {
                    Transform dot = pill.Find("StatusDot");
                    if (dot != null) ServerStatusDot = dot.GetComponent<Image>();
                }
                if (ServerStatusBadgeText == null)
                {
                    Transform txt = pill.Find("StatusText");
                    if (txt != null) ServerStatusBadgeText = txt.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        if (ConfigButtonSubtitle == null && ConfigButton != null)
        {
            Transform sub = ConfigButton.transform.Find("Content/Background/Elements/Text/Subtitle");
            if (sub != null) ConfigButtonSubtitle = sub.GetComponent<TextMeshProUGUI>();
        }
    }

    public async void RefreshServerStatusAsync()
    {
        EnsureStatusReferences();

        string address = RestThruServer.AddressDisplay;
        if (ConfigButtonSubtitle != null)
        {
            ConfigButtonSubtitle.text = address;
        }

        if (ServerStatusBadgeText != null)
        {
            ServerStatusBadgeText.text = $"Server: {address} (Checking...)";
        }
        if (ServerStatusDot != null)
        {
            ServerStatusDot.color = CheckingColor;
        }

        int currentCheck = ++_statusCheckVersion;
        bool reachable = await RestThruServer.CheckServerReachabilityAsync(2);

        if (currentCheck != _statusCheckVersion || this == null) return;

        if (ServerStatusDot != null)
        {
            ServerStatusDot.color = reachable ? OnlineColor : OfflineColor;
        }
        if (ServerStatusBadgeText != null)
        {
            string stateLabel = reachable ? "Online" : "Offline";
            ServerStatusBadgeText.text = $"Server: {address} ({stateLabel})";
        }
    }
}
