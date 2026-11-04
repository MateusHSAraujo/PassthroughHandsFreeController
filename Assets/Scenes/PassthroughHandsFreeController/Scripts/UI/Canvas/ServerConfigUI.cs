using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineOfSightPositioner))]
public class ServerConfigUI : CameraGazeControlledCanvas
{
    public static ServerConfigUI Instance;

    public enum ActiveField
    {
        IP,
        Port
    }

    [Header("Server Config UI References:")]
    [Space(5)]
    public TextMeshProUGUI StatusText;
    public TextMeshProUGUI IpValueText;
    public TextMeshProUGUI PortValueText;
    public Image IpFieldBackground;
    public Image PortFieldBackground;
    public Button IpFieldButton;
    public Button PortFieldButton;
    public Toggle ConfirmToggle;
    public Toggle CancelToggle;
    public Transform KeypadContainer;

    [Header("Scheme Selector (HTTP / HTTPS):")]
    [Tooltip("Keypad-local positions for the HTTP and HTTPS buttons (free slots of the last keypad row).")]
    public Vector2 HttpButtonPosition = new(195f, -113.5f);
    public Vector2 HttpsButtonPosition = new(273f, -113.5f);
    public Vector2 SchemeButtonSize = new(72f, 28f);

    [Header("Gaze Dwell Settings:")]
    public float KeyDwellTime = 1.15f;
    public float ActionButtonDwellTime = 1.6f;

    public event Action OnConfigurationConfirmed;
    public event Action OnConfigurationCancelled;

    private bool _isCanvasActive = false;
    private ActiveField _activeField = ActiveField.IP;
    private string _currentIp = RestThruServer.DefaultServerIP;
    private string _currentPort = RestThruServer.DefaultServerPort.ToString();
    private ServerScheme _currentScheme = ServerScheme.Http;

    private GazeUIFactory.ButtonParts _httpButton;
    private GazeUIFactory.ButtonParts _httpsButton;

    private ToggleGazeInteractor _ipFieldInteractor;
    private ToggleGazeInteractor _portFieldInteractor;

    private static readonly Color SelectedFieldColor = new(0.08f, 0.38f, 0.72f, 0.92f);
    private static readonly Color UnselectedFieldColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Color NormalStatusColor = new(0.75f, 0.86f, 0.96f, 1f);
    private static readonly Color ErrorStatusColor = new(1f, 0.42f, 0.42f, 1f);

    protected override void Awake()
    {
        DebugLogger.Log("ServerConfigUI Awake called.");
        Debug.Assert(Instance == null);
        Instance = this;
        enableSoftFollow = true;
        base.Awake();

        int overlayLayer = gameObject.layer;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = overlayLayer;
        }

        BindUIEvents();
        BuildSchemeSelector();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildSchemeSelector()
    {
        if (KeypadContainer == null) return;

        Sprite keySprite = null;
        AudioClip clickClip = null;
        TextMeshProUGUI keyLabel = null;
        Button referenceKey = KeypadContainer.GetComponentInChildren<Button>(true);
        if (referenceKey != null)
        {
            if (referenceKey.targetGraphic is Image keyImage) keySprite = keyImage.sprite;
            AudioSource keyAudio = referenceKey.GetComponent<AudioSource>();
            if (keyAudio != null) clickClip = keyAudio.clip;
            keyLabel = referenceKey.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Match the keypad keys' look (sprite, sound, font and style)
        GazeUITheme theme = new() { ClickClip = clickClip, MediumFont = keyLabel != null ? keyLabel.font : null };
        _httpButton = CreateSchemeButton("Scheme_Http", "HTTP", HttpButtonPosition, ServerScheme.Http, keySprite, theme, keyLabel);
        _httpsButton = CreateSchemeButton("Scheme_Https", "HTTPS", HttpsButtonPosition, ServerScheme.Https, keySprite, theme, keyLabel);
    }

    private GazeUIFactory.ButtonParts CreateSchemeButton(string name, string label, Vector2 position, ServerScheme scheme, Sprite sprite,
        GazeUITheme theme, TextMeshProUGUI keyLabel)
    {
        var parts = GazeUIFactory.CreateButton(KeypadContainer, name, label, GazeButtonStyle.Keypad, KeyDwellTime, false,
            theme, 12f, SchemeButtonSize.y);
        if (keyLabel != null) parts.Label.fontStyle = keyLabel.fontStyle;
        if (sprite != null)
        {
            parts.Background.sprite = sprite;
            parts.Background.type = Image.Type.Sliced;
        }
        RectTransform rt = (RectTransform)parts.Button.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = SchemeButtonSize;
        parts.Button.onClick.AddListener(() => SelectScheme(scheme));
        return parts;
    }

    public void SelectScheme(ServerScheme scheme)
    {
        _currentScheme = scheme;
        RefreshDisplay();
    }

    private void BindUIEvents()
    {
        AudioClip clickClip = null;
        if (ConfirmToggle != null)
        {
            AudioSource confirmAudio = ConfirmToggle.GetComponent<AudioSource>();
            if (confirmAudio != null) clickClip = confirmAudio.clip;
        }

        if (IpFieldButton != null)
        {
            IpFieldButton.onClick.RemoveAllListeners();
            IpFieldButton.onClick.AddListener(SelectIpField);
            _ipFieldInteractor = EnsureGazeInteractor(IpFieldButton.gameObject, GazeButtonStyle.Secondary, KeyDwellTime, false, clickClip);
        }

        if (PortFieldButton != null)
        {
            PortFieldButton.onClick.RemoveAllListeners();
            PortFieldButton.onClick.AddListener(SelectPortField);
            _portFieldInteractor = EnsureGazeInteractor(PortFieldButton.gameObject, GazeButtonStyle.Secondary, KeyDwellTime, false, clickClip);
        }

        if (ConfirmToggle != null)
        {
            ConfirmToggle.onValueChanged = new Toggle.ToggleEvent();
            ConfirmToggle.SetIsOnWithoutNotify(false);
            ConfirmToggle.onValueChanged.AddListener(OnConfirmToggleChanged);
            EnsureGazeInteractor(ConfirmToggle.gameObject, GazeButtonStyle.Primary, ActionButtonDwellTime, false, clickClip);
        }

        if (CancelToggle != null)
        {
            CancelToggle.onValueChanged = new Toggle.ToggleEvent();
            CancelToggle.SetIsOnWithoutNotify(false);
            CancelToggle.onValueChanged.AddListener(OnCancelToggleChanged);
            EnsureGazeInteractor(CancelToggle.gameObject, GazeButtonStyle.Danger, ActionButtonDwellTime, false, clickClip);
        }

        if (KeypadContainer != null)
        {
            Button[] keyButtons = KeypadContainer.GetComponentsInChildren<Button>(true);
            foreach (Button btn in keyButtons)
            {
                string keyName = btn.gameObject.name;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnKeypadButtonPressed(keyName));

                bool shouldRepeat = keyName != "Key_Clear" && keyName != "Key_Default";
                GazeButtonStyle keyStyle = keyName == "Key_Clear" ? GazeButtonStyle.Danger : GazeButtonStyle.Keypad;
                EnsureGazeInteractor(btn.gameObject, keyStyle, KeyDwellTime, shouldRepeat, clickClip);
            }
        }
    }

    private ToggleGazeInteractor EnsureGazeInteractor(
        GameObject targetObj,
        GazeButtonStyle style,
        float dwellTime,
        bool repeat,
        AudioClip clickClip)
    {
        AudioSource audioSource = targetObj.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = targetObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        if (audioSource.clip == null && clickClip != null)
        {
            audioSource.clip = clickClip;
        }

        if (targetObj.GetComponent<MetaXRAudioSource>() == null)
        {
            targetObj.AddComponent<MetaXRAudioSource>();
        }

        ToggleGazeInteractor interactor = targetObj.GetComponent<ToggleGazeInteractor>();
        if (interactor == null)
        {
            interactor = targetObj.AddComponent<ToggleGazeInteractor>();
        }

        interactor.stylePreset = style;
        interactor.timeToToggle = dwellTime;
        interactor.repeatWhileHovered = repeat;
        interactor.InitializeReferences();
        interactor.ApplyStylePresetColors();
        interactor.ResetVisualStateImmediate();
        return interactor;
    }

    public override void ShowCanvas()
    {
        LoadCurrentServerValues();
        base.ShowCanvas();
    }

    public override void ShowCanvasAtDirectionOf(Vector3 targetPosition)
    {
        LoadCurrentServerValues();
        base.ShowCanvasAtDirectionOf(targetPosition);
    }

    public override void HideCanvas()
    {
        _isCanvasActive = false;
        base.HideCanvas();
    }

    private void LoadCurrentServerValues()
    {
        _currentIp = RestThruServer.ServerIP;
        _currentPort = RestThruServer.ServerPort.ToString();
        _currentScheme = RestThruServer.Scheme;
        _activeField = ActiveField.IP;

        if (ConfirmToggle != null) ConfirmToggle.SetIsOnWithoutNotify(false);
        if (CancelToggle != null) CancelToggle.SetIsOnWithoutNotify(false);

        SetStatus("Gaze at keys to edit IP or Port, then Connect", false);
        RefreshDisplay();
        _isCanvasActive = true;
    }

    public override void Update()
    {
        base.Update();

        if (!_isCanvasActive) return;

        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_activeField == ActiveField.IP) SelectPortField();
            else SelectIpField();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelConfiguration();
            return;
        }

        foreach (char c in Input.inputString)
        {
            if (c >= '0' && c <= '9')
            {
                AppendCharacter(c.ToString());
            }
            else if (c == '.' && _activeField == ActiveField.IP)
            {
                AppendCharacter(".");
            }
            else if (c == '\b')
            {
                Backspace();
            }
            else if (c == '\n' || c == '\r')
            {
                ConfirmConfiguration();
            }
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!_isCanvasActive) return;

        GUI.skin.box.fontSize = 14;
        GUILayout.BeginArea(new Rect(16, 16, 330, 185), "RestThru Server Setup (PC Override)", GUI.skin.window);
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Server IP:", GUILayout.Width(80));
        string newIp = GUILayout.TextField(_currentIp, 15);
        if (newIp != _currentIp)
        {
            _currentIp = newIp;
            RefreshDisplay();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Server Port:", GUILayout.Width(80));
        string newPort = GUILayout.TextField(_currentPort, 5);
        if (newPort != _currentPort)
        {
            _currentPort = newPort;
            RefreshDisplay();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Back", GUILayout.Height(30)))
        {
            CancelConfiguration();
        }
        if (GUILayout.Button(_currentScheme == ServerScheme.Https ? "HTTPS" : "HTTP", GUILayout.Height(30)))
        {
            SelectScheme(_currentScheme == ServerScheme.Https ? ServerScheme.Http : ServerScheme.Https);
        }
        if (GUILayout.Button("Default", GUILayout.Height(30)))
        {
            RestoreDefaultValues();
        }
        if (GUILayout.Button("Save & Connect", GUILayout.Height(30)))
        {
            ConfirmConfiguration();
        }
        GUILayout.EndHorizontal();

        if (StatusText != null)
        {
            GUILayout.Label(StatusText.text);
        }
        GUILayout.EndArea();
    }
#endif

    public void SelectIpField()
    {
        _activeField = ActiveField.IP;
        RefreshDisplay();
    }

    public void SelectPortField()
    {
        _activeField = ActiveField.Port;
        RefreshDisplay();
    }

    public void RestoreDefaultValues()
    {
        _currentIp = RestThruServer.DefaultServerIP;
        _currentPort = RestThruServer.DefaultServerPort.ToString();
        _currentScheme = ServerScheme.Http;
        SetStatus($"Restored default ({_currentIp}:{_currentPort})", false);
        RefreshDisplay();
    }

    private void OnKeypadButtonPressed(string keyName)
    {
        switch (keyName)
        {
            case "Key_Back":
                Backspace();
                break;
            case "Key_Clear":
                ClearActiveField();
                break;
            case "Key_Default":
                RestoreDefaultValues();
                break;
            case "Key_Dot":
                AppendCharacter(".");
                break;
            default:
                if (keyName.StartsWith("Key_"))
                {
                    string digit = keyName.Substring(4);
                    AppendCharacter(digit);
                }
                break;
        }
    }

    public void AppendCharacter(string ch)
    {
        if (_activeField == ActiveField.IP)
        {
            if (ch == "." && (_currentIp.Length == 0 || _currentIp.EndsWith(".")))
            {
                return;
            }
            if (_currentIp.Length < 15)
            {
                _currentIp += ch;
            }
        }
        else
        {
            if (ch == ".") return;
            if (_currentPort.Length < 5)
            {
                _currentPort += ch;
            }
        }

        RefreshDisplay();
    }

    public void Backspace()
    {
        if (_activeField == ActiveField.IP)
        {
            if (_currentIp.Length > 0)
            {
                _currentIp = _currentIp.Substring(0, _currentIp.Length - 1);
            }
        }
        else
        {
            if (_currentPort.Length > 0)
            {
                _currentPort = _currentPort.Substring(0, _currentPort.Length - 1);
            }
        }

        RefreshDisplay();
    }

    public void ClearActiveField()
    {
        if (_activeField == ActiveField.IP)
        {
            _currentIp = string.Empty;
        }
        else
        {
            _currentPort = string.Empty;
        }

        RefreshDisplay();
    }

    private void OnConfirmToggleChanged(bool isOn)
    {
        if (!isOn) return;
        if (ConfirmToggle != null)
        {
            ConfirmToggle.SetIsOnWithoutNotify(false);
        }
        ConfirmConfiguration();
    }

    private void OnCancelToggleChanged(bool isOn)
    {
        if (!isOn) return;
        if (CancelToggle != null)
        {
            CancelToggle.SetIsOnWithoutNotify(false);
        }
        CancelConfiguration();
    }

    public void ConfirmConfiguration()
    {
        if (RestThruServer.TrySetServerAddress(_currentScheme, _currentIp, _currentPort, out string errorMessage))
        {
            SetStatus($"Saved: {RestThruServer.BaseUrl}", false);
            OnConfigurationConfirmed?.Invoke();
        }
        else
        {
            DebugLogger.LogWarning($"Invalid server configuration ({_currentIp}:{_currentPort}): {errorMessage}");
            SetStatus(errorMessage, true);
            if (ConfirmToggle != null)
            {
                ConfirmToggle.SetIsOnWithoutNotify(false);
            }
        }
    }

    public void CancelConfiguration()
    {
        DebugLogger.Log("Server configuration cancelled. Returning to MainMenuState.");
        OnConfigurationCancelled?.Invoke();
    }

    private void SetStatus(string message, bool isError)
    {
        if (StatusText != null)
        {
            StatusText.text = message;
            StatusText.color = isError ? ErrorStatusColor : NormalStatusColor;
        }
    }

    private void RefreshDisplay()
    {
        if (IpValueText != null)
        {
            IpValueText.text = string.IsNullOrEmpty(_currentIp) ? "_" : _currentIp;
        }

        if (PortValueText != null)
        {
            PortValueText.text = string.IsNullOrEmpty(_currentPort) ? "_" : _currentPort;
        }

        if (_httpButton != null) GazeUIFactory.ApplySelectionState(_httpButton, _currentScheme == ServerScheme.Http, true);
        if (_httpsButton != null) GazeUIFactory.ApplySelectionState(_httpsButton, _currentScheme == ServerScheme.Https, true);

        Color ipColor = (_activeField == ActiveField.IP) ? SelectedFieldColor : UnselectedFieldColor;
        Color portColor = (_activeField == ActiveField.Port) ? SelectedFieldColor : UnselectedFieldColor;

        if (_ipFieldInteractor != null)
        {
            _ipFieldInteractor.SetOverrideBaseColor(ipColor);
        }
        else if (IpFieldBackground != null)
        {
            IpFieldBackground.color = ipColor;
        }

        if (_portFieldInteractor != null)
        {
            _portFieldInteractor.SetOverrideBaseColor(portColor);
        }
        else if (PortFieldBackground != null)
        {
            PortFieldBackground.color = portColor;
        }
    }
}
