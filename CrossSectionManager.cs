using Drawing;
using HMMouse;
using Michsky.MUIP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WatchBIM;
using WayBIM;
using WBScriptable;

public partial class CrossSectionManager : MonoBehaviour, IUIChecker
{
    #region Instance
    private static CrossSectionManager _instance = null;
    public static CrossSectionManager Instance
    {
        get
        {
            if (_instance.IsNull())
            {
                CrossSectionManager o = FindObjectOfType<CrossSectionManager>();
                if (o.IsNotNull()) _instance = o;
            }

            return _instance;
        }
    }
    private void Awake() { _instance = this; }
    #endregion

    #region SerializedField
    [Header("Option")]
    [SerializeField] private bool _isOn;
    [SerializeField] private bool _twoEnabled;
    [SerializeField] private bool _meshEnabled;
    [SerializeField] private bool _planeVisible;
    [SerializeField] private bool _debug;
    [SerializeField] private float _planeLength = 50.0f;
    [SerializeField] private float _progressBarLength = 140;

    [Header("Common")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _goParent;
    [SerializeField] private TMP_Text _roadNameText;
    [SerializeField] private TMP_InputField _startText;
    [SerializeField] private TMP_InputField _endText;
    [SerializeField] private Scrollbar _scrollBarHorizontal;
    [SerializeField] private Scrollbar _scrollBarVeritcal;
    [SerializeField] private Slider _sliderSize;
    [SerializeField] private Slider _sliderLineWidth;
    [SerializeField] private GameObject _dimPanel;

    [Header("UI")]
    [SerializeField] private RectTransform _innerframe;
    [SerializeField] private RectTransform _originRect;
    [SerializeField] private RectTransform _viewportRect;
    [SerializeField] private RectTransform _contentRect;
    [SerializeField] private RectTransform _gridRect;
    [SerializeField] private RectTransform _normalRect;
    [SerializeField] private RectTransform _layerRect;
    [SerializeField] private RectTransform _dimRect;
    [SerializeField] private RectTransform _infoRect;
    [SerializeField] private RectTransform _roadScrollContent;
    [SerializeField] private RectTransform _crossScrollContent;
    [SerializeField] private RectTransform _layerScrollContent;
    [SerializeField] private WBRTEWindowResizer _resizer;
    [SerializeField] private RectTransform _progressRect;
    [SerializeField] private WBSwitchManager _panelOnModeSwitch;
    [SerializeField] private WBSwitchManager _panelOffModeSwitch;
    [SerializeField] private TMP_Text _stationText;
    [SerializeField] private TMP_Text _fontSizeSliderText;

    [Header("Panel2D")]
    [SerializeField] private RectTransform _panel2D;
    [SerializeField] private RectTransform _buttonPanel2DOn;
    [SerializeField] private RectTransform _panel2DOn;
    [SerializeField] private RectTransform _panel2DOff;
    [SerializeField] private RectTransform _buttonOn;
    [SerializeField] private Toggle _allLayerToggle;

    [Header("Prefab")]
    [SerializeField] private GameObject _itemPrefab;
    [SerializeField] private GameObject _layerPrefab;
    [SerializeField] private GameObject _spherePrefab;
    [SerializeField] private GameObject _planePrefab;
    [SerializeField] private GameObject _linePrefab;
    [SerializeField] private GameObject _slopePrefab;
    [SerializeField] private GameObject _lineParentPrefab;
    [SerializeField] private GameObject _dimPointPrefab;
    [SerializeField] private GameObject _dimTextPrefab;

    [Header("Resource")]
    [SerializeField] private WBModelTypeInfo _modelTypeInfo;
    [SerializeField] private Material _material;
    [SerializeField] private Material _planeMaterial;
    [SerializeField] private SerializableDictionary<UnityEngine.Mesh, ModelTypes> _additionalMeshes;
    #endregion

    #region Members / Properties
    private HitManager _hitManager => HMMouseController.Instance.GetHitManager();
    private static HitManager hitManager;
    private static float rate = 10;
    private static WBProjectData projectData;
    private static Transform goParent;
    private static RectTransform layerScrollContent;
    private static RectTransform viewportRect;
    private static RectTransform contentRect;
    private static RectTransform gridRect;
    private static RectTransform normalRect;
    private static RectTransform dimRect;
    private static RectTransform layerRect;
    private static RectTransform infoRect;
    private static GameObject spherePrefab;
    private static GameObject layerPrefab;
    private static GameObject planePrefab;
    private static GameObject linePrefab;
    private static GameObject lineParentPrefab;
    private static GameObject dimPointPrefab;
    private static GameObject dimTextPrefab;
    private static Material material;
    private static Slider sliderSize;
    private static Slider sliderLineWidth;
    private static WBModelTypeInfo modelTypeInfo;
    private static WBAlignmentInfo currentAlignmentInfo;
    private static TMP_Text stationText;
    private static List<MeshInfo> additionalMeshInfos;
    private static bool twoEnabled;
    private static bool meshEnabled;
    private static List<SlopeLabel> slopeLabels = new();

    private CrossSection2D _crossSection2D;
    private CrossSection _currentCrossSection;
    private CrossSection _freeCrossSection;
    private List<CrossSection> _crossSections;
    private WBAlignmentInfo _currentAlignmentInfo;
    private bool _isRunning;
    private int _totalCount;
    private int _currentCount;
    private float _currentProgressBarWidth = 0f;
    private float _gap = 20;
    private float _startStation = 0;
    private float _endStation = 0;
    private float _fontSize = 0.2f;
    private bool _isFreeMode = false;
    private bool _dimensionIs3D = true;
    private bool _clipEnabled = true;
    private bool _followEnabled;
    private bool _offOthersEnabled;
    private bool _cancelled;
    #endregion

    #region Unity Messages
    private void Start()
    {
        layerPrefab = _layerPrefab;
        spherePrefab = _spherePrefab;
        planePrefab = _planePrefab;
        linePrefab = _linePrefab;
        lineParentPrefab = _lineParentPrefab;
        dimPointPrefab = _dimPointPrefab;
        dimTextPrefab = _dimTextPrefab;
        goParent = _goParent;
        layerScrollContent = _layerScrollContent;
        viewportRect = _viewportRect;
        contentRect = _contentRect;
        gridRect = _gridRect;
        normalRect = _normalRect;
        layerRect = _layerRect;
        dimRect = _dimRect;
        material = _material;
        infoRect = _infoRect;
        hitManager = _hitManager;
        sliderSize = _sliderSize;
        sliderLineWidth = _sliderLineWidth;
        stationText = _stationText;
        modelTypeInfo = _modelTypeInfo;
        twoEnabled = _twoEnabled;
        meshEnabled = _meshEnabled;

        var color = _planeMaterial.color;
        color.a = _planeVisible ? 0.2f : 0.0f;
        _planeMaterial.color = color;

        _crossSection2D = new CrossSection2D();

        HMMouseController.Instance.OnLeftClick.AddListener(OnClick);
        _allLayerToggle.onValueChanged.AddListener(_crossSection2D.OnAllLayerToggle);

        projectData = WBLoader.Instance.ProjectData;
        foreach (var road in projectData.AlignmentInfos.Values.OrderBy(o => o.RoadId))
        {
            var roadItem = Instantiate(_itemPrefab, _roadScrollContent);
            roadItem.GetComponent<TMP_Text>().text = road.RoadName;
            roadItem.GetComponent<Button>().onClick.AddListener(() => OnClickRoadItem(road));
        }

        additionalMeshInfos = new();
        foreach (var pair in _additionalMeshes)
        {
            MeshInfo m = new(pair.Key);
            m.ModelType = pair.Value;
            additionalMeshInfos.Add(m);
        }

        Init();
    }

    private void Update()
    {
        DrawSlopeLabels();

        if (Input.GetKeyDown(KeyCode.F3))
            if (_isOn) Off();
            else On();

        if (_isOn)
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0))
                _crossSection2D.ClickDim();

            if (Input.GetKeyDown(KeyCode.Z))
                FocusStation();

            if (Input.GetKeyDown(KeyCode.Escape))
                Cancel();

            if (Input.GetKeyDown(KeyCode.C))
                OnRunButtonClick();
        }
    }

    private void LateUpdate()
    {
        UpdateProgressBar();
        _crossSection2D.Update();
    }
    #endregion

    #region Run
    private void GeneratePlanes()
    {
        if (_isFreeMode) return;
        if (_currentAlignmentInfo.IsNull()) return;

        Init();

        List<WBStationPoint> stationsPoints = new();
        float station = _startStation;
        while (station < _endStation)
        {
            stationsPoints.Add(GetStationPoint(station));
            station += _gap;
        }
        stationsPoints.Add(GetStationPoint(_endStation));

        foreach (var stationPoint in stationsPoints.Where(w => w.IsNotNull()))
        {
            CrossSection crossSection = new(stationPoint, _planeLength, AddLine);
            _crossSections.Add(crossSection);

            var stationItem = Instantiate(_itemPrefab, _crossScrollContent);
            stationItem.GetComponent<TMP_Text>().text = $"{stationPoint.Station: 0+000}";
            stationItem.GetComponent<Button>().onClick.AddListener(() => OnClickStationItem(crossSection));
        }
    }

    private void AddLine(float key, List<Vector3> points, Color color)
    {
        CrossSectionDraw.Instance.AddLine(key, points, color);
    }

    private IEnumerator RunCrossSection()
    {
        _isRunning = true;
        slopeLabels.Clear();
        _currentCount = 0;
        CrossSectionDraw.Instance.InitLines();

        yield return new WaitForEndOfFrame();

        Stopwatch stopWatch = new();
        stopWatch.Start();

        var meshInfos = hitManager.GetTotalMeshInfos();

        if (_isFreeMode)
        {
            _totalCount = 1;
            yield return StartCoroutine(_freeCrossSection.MakeDatas(meshInfos));
            UpdateProgressBarWidth();

            if (_twoEnabled) _crossSection2D.SetData(_freeCrossSection);

            SetOffOthers();
            FocusStation();
            SetClip();
        }
        else if (_crossSections?.Count > 0)
        {
            var crossSections = _crossSections.Where(w => w.IsReady);
            _totalCount = crossSections.Count();
            _currentCount++;

            foreach (var crossSection in crossSections)
            {
                yield return StartCoroutine(crossSection.MakeDatas(meshInfos));
                UpdateProgressBarWidth();
                yield return new WaitForEndOfFrame();

                // Get the elapsed time as a TimeSpan value.
                TimeSpan currentSpan = stopWatch.Elapsed;

                // Format and display the TimeSpan value.
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    currentSpan.Hours, currentSpan.Minutes, currentSpan.Seconds,
                    currentSpan.Milliseconds / 10);

                UnityEngine.Debug.Log(elapsedTime);

                if (_cancelled)
                {
                    stopWatch.Stop();
                    WBProgress.Instance.Finish();
                    _cancelled = false;
                    _isRunning = false;
                    StopAllCoroutines();
                    break;
                }
            }
        }

        // Get the elapsed time as a TimeSpan value.
        TimeSpan totalSpan = stopWatch.Elapsed;

        // Format and display the TimeSpan value.
        string totalTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            totalSpan.Hours, totalSpan.Minutes, totalSpan.Seconds,
            totalSpan.Milliseconds / 10);

        UnityEngine.Debug.Log(totalTime);

        stopWatch.Stop();
        WBProgress.Instance.Finish();

        _isRunning = false;
    }

    private void UpdateProgressBarWidth()
    {
        _currentCount++;
        float value = 0;
        if (_totalCount > 0)
        {
            if (_currentCount == _totalCount)
                value = _progressBarLength;
            else
                value = (float)_currentCount / _totalCount * _progressBarLength;
        }

        _currentProgressBarWidth = value;
    }

    private void UpdateProgressBar()
    {
        if (_isRunning)
            _progressRect.sizeDelta = new Vector2(Mathf.Lerp(_progressRect.sizeDelta.x, _currentProgressBarWidth, 5.0f * Time.deltaTime), 40);
    }

    private void Cancel()
    {
        _cancelled = true;
    }
    #endregion

    #region Private Methods
    private void DrawSlopeLabels()
    {
        if (_currentCrossSection.IsNotNull())
        {
            foreach (var slopeLabel in slopeLabels)
            {
                if (_currentCrossSection.Station == slopeLabel.Station)
                    Draw.ingame.Label3D(slopeLabel.Position, slopeLabel.Rotation, slopeLabel.Text, _fontSize, LabelAlignment.BottomCenter);
            }
        }
        else
        {
            foreach (var slopeLabel in slopeLabels)
            {
                Draw.ingame.Label3D(slopeLabel.Position, slopeLabel.Rotation, slopeLabel.Text, _fontSize, LabelAlignment.BottomCenter);
            }
        }
    }

    private void OnClick()
    {
        if (_isOn && _isFreeMode && Input.GetKey(KeyCode.LeftControl)) _freeCrossSection?.OnClick();
    }

    private void OnClickRoadItem(WBAlignmentInfo info)
    {
        _panelOnModeSwitch.SetOff(false);
        _panelOffModeSwitch.SetOff(false);

        ToggleMode(false);

        currentAlignmentInfo = info;
        _currentAlignmentInfo = info;
        _roadNameText.text = info.RoadName;
        _startText.text = Mathf.Round((float)info.StartStation).ToString();
        _endText.text = Mathf.Round((float)info.EndStation).ToString();

        float.TryParse(_startText.text, out _startStation);
        float.TryParse(_endText.text, out _endStation);
    }

    private WBStationPoint GetStationPoint(float station)
    {
        var stationPoint = _currentAlignmentInfo.StationPoints.Find(e => e.Station.Equals(station));
        if (stationPoint.IsNull()) stationPoint = _currentAlignmentInfo.StationPoints.Find(e => e.Station.Equals(Mathf.Round(station)));
        return stationPoint;
    }

    private void OnClickStationItem(CrossSection crossSection)
    {
        if (_currentCrossSection != crossSection)
        {
            _currentCrossSection = crossSection;
            _allLayerToggle.isOn = true;
            if (_twoEnabled) _crossSection2D.SetData(_currentCrossSection);
        }

        SetOffOthers();
        FocusStation();
        SetClip();
    }

    private void Init()
    {
        slopeLabels = new();

        _stationText.text = string.Empty;
        _currentProgressBarWidth = 0;
        _currentCount = 0;
        _totalCount = 0;
        CrossSectionDraw.Instance.InitLines();

        _freeCrossSection?.DestroyGOs();
        _freeCrossSection = new CrossSection(AddLine);

        if (_crossSections?.Count > 0)
        {
            foreach (var crossSection in _crossSections)
            {
                crossSection.DestroyGOs();
            }
        }
        _crossSections = new();
        _crossScrollContent.DestroyChildren();
        _currentCrossSection = null;
        _crossSection2D.Init();

        SetClip();
    }

    private void FocusStation()
    {
        if (!_followEnabled) return;
        if (_isFreeMode)
        {
            if (_freeCrossSection.IsNotNull())
            {
                HMMouseController.Instance.FocusCrossSection(_freeCrossSection.Point0, _freeCrossSection.Point1);
            }
        }
        else 
        {
            if (_currentCrossSection.IsNotNull())
            {
                HMMouseController.Instance.FocusCrossSection(_currentCrossSection.Point0, _currentCrossSection.Point1);
            }
        }
    }

    private void SetClip()
    {
        if (_clipEnabled && _isFreeMode && _freeCrossSection?.IsReady is true)
        {
            ClipManager.Instance.Toggle(true);
            ClipManager.Instance.Apply(_freeCrossSection.Point1, _freeCrossSection.Point0);
        }
        else if (_clipEnabled && !_isFreeMode && _currentCrossSection?.IsReady is true)
        {
            ClipManager.Instance.Toggle(true);
            ClipManager.Instance.Apply(_currentCrossSection.Point1, _currentCrossSection.Point0);
        }
        else
        {
            ClipManager.Instance.Toggle(false);
        }

        ClipManager.Instance.OnClickOriginalMethod(3);
    }

    private void SetDimension()
    {
        HMMouseController.Instance.CrossMode(!_dimensionIs3D);
    }

    private void SetOffOthers()
    {
        if (!_isFreeMode && _currentCrossSection?.IsReady is true && _offOthersEnabled)
        {
            CrossSectionDraw.Instance.VisibleStation(_currentCrossSection.Station);
            foreach (var crossSection in _crossSections)
            {
                crossSection.Visibie(_currentCrossSection == crossSection);
            }
        }
        else
        {
            CrossSectionDraw.Instance.VisibleStation(-1);
            if (_crossSections?.Count > 0)
            {
                foreach (var crossSection in _crossSections)
                {
                    crossSection.Visibie(true);
                }
            }
        }
    }

    private static Vector2 GetPlaneVector2(Vector3 vertex, Vector3 planeNormal, Vector3 planePosition)
    {
        var vector = planePosition - vertex;
        var x = new Vector3(vector.x, 0, vector.z).magnitude;

        Vector3 cross = Vector3.Cross(planeNormal, Vector3.up);
        if (Vector3.Dot(cross, vector) < 0) x = -x;

        return rate * new Vector2(x, -vector.y);
    }

    private static float GetSlopeEpsilon(ModelTypes modelType)
    {
        switch (modelType)
        {
            case ModelTypes.MtCutSlope:
            case ModelTypes.MtFillSlope: return 0.01f;
            case ModelTypes.MtRoadSurface: return 0.01f;
        }

        return 0.001f;
    }

    private static string SlopeTextByModelType(ModelTypes modelType, Vector2 v0, Vector2 v1)
    {
        var vector = v1 - v0;

        switch (modelType)
        {
            case ModelTypes.MtCutSlope:
            case ModelTypes.MtFillSlope:
                return $"1:{Mathf.Abs(vector.x / vector.y): 0.0}";
            case ModelTypes.MtRoadSurface:
                return $"{Mathf.Abs(vector.y / vector.x) * 100: 0.00}%";
        }

        return $"{Mathf.Abs(vector.x / vector.y): 0.0}";
    }

    private static string SlopeTextByModelType(ModelTypes modelType, Vector3 v0, Vector3 v1)
    {
        var vector = v1 - v0;
        var x = new Vector3(vector.x, 0, vector.z).magnitude;

        switch (modelType)
        {
            case ModelTypes.MtCutSlope:
            case ModelTypes.MtFillSlope:
                if (Mathf.Approximately(vector.y, 0))
                    return string.Empty;
                else
                    return $"1:{Mathf.Abs(x / vector.y): 0.0}";
            case ModelTypes.MtRoadSurface:
                if (Mathf.Approximately(x, 0))
                    return string.Empty;
                else
                    return $"{Mathf.Abs(vector.y / x) * 100: 0.00}%";
        }

        if (Mathf.Approximately(x, 0))
            return string.Empty;
        else
            return $"{Mathf.Abs(x / vector.y): 0.0}";
    }
    #endregion

    #region Public Methods
    public void On()
    {
        _isOn = true;
        Init();
        ClipManager.Instance.Toggle(true);
        HMMouseController.Instance.CrossMode(false);
        _dimPanel.Activate();
    }

    public void Off()
    {
        _isOn = false;
        Init();
        ClipManager.Instance.Toggle(false);
        HMMouseController.Instance.CrossMode(false);
        _dimPanel.Deactivate();
    }

    public void SetFreeCrossSection(WBStationPoint stationPoint)
    {
        _isFreeMode = true;
        InitFromOther();
        _freeCrossSection = new(stationPoint, _planeLength, AddLine);
        StartCoroutine(RunCrossSection());
    }

    public void InitFromOther()
    {
        slopeLabels.Clear();
        CrossSectionDraw.Instance.InitLines();

        _stationText.text = string.Empty;
        _currentProgressBarWidth = 0;
        _currentCount = 0;
        _totalCount = 0;

        if (_crossSections?.Count > 0)
        {
            foreach (var crossSection in _crossSections)
            {
                crossSection.DestroyGOs();
            }
        }
        _crossSections = new();
        _crossScrollContent.DestroyChildren();
        _currentCrossSection = null;
        _crossSection2D.Init();

        _freeCrossSection?.DestroyGOs();
    }

    public void Toggle()
    {
        _dimPanel.SetActive(!_dimPanel.activeSelf);
    }

    public void ToggleDim(bool value)
    {
        _crossSection2D.ToggleDim(value);
    }

    public void ToggleSnapping(bool value)
    {
        _crossSection2D.ToggleSnapping(value);
    }

    public void ToggleMultiDim(bool value)
    {
        _crossSection2D.ToggleMultiDim(value);
    }

    public void ToggleModelInfo(bool value)
    {
        _crossSection2D.ToggleModelInfo(value);
    }

    public void ToggleDimension(bool value)
    {
        _dimensionIs3D = value;
        SetDimension();
    }

    public void ToggleClip(bool value)
    {
        _clipEnabled = value;
        SetClip();
    }

    public void ToggleFollow(bool value)
    {
        _followEnabled = value;
        FocusStation();
    }

    public void ToggleOffOthers(bool value)
    {
        _offOthersEnabled = value;
        SetOffOthers();
    }

    public void ToggleMode(bool value)
    {
        _isFreeMode = value;
        InitFromOther();
    }

    public void ToggleDimPanel(bool value)
    {
        if (value)
        {
            _resizer.SetMinSize(new Vector2(800, 600));
            _originRect.sizeDelta = new Vector2(800, _originRect.sizeDelta.y);
            _panel2D.Activate();
            _buttonPanel2DOn.Activate();
            _panel2DOn.Activate();
            _panel2DOff.Deactivate();
            _buttonOn.Deactivate();
        }
        else
        {
            _resizer.SetMinSize(new Vector2(150, 600));
            _originRect.sizeDelta = new Vector2(150, _originRect.sizeDelta.y);
            _panel2D.Deactivate();
            _buttonPanel2DOn.Deactivate();
            _panel2DOn.Deactivate();
            _panel2DOff.Activate();
            _buttonOn.Activate();
        }
    }

    public void OnGapChanged(string value)
    {
        float.TryParse(value, out _gap);
    }

    public void OnResetButtonClick()
    {
        Init();
    }

    public void OnGeneratePlanesButtonClick()
    {
        GeneratePlanes();
    }

    public void OnRunButtonClick()
    {
        StartCoroutine(RunCrossSection());
    }

    public void OnFontSizeSliderChanged(float value)
    {
        _fontSize = value;
        _fontSizeSliderText.text = $"{value:0.0}";
    }

    public void OnStartStationChanged(string value)
    {
        if (_currentAlignmentInfo.IsNull()) return;

        float.TryParse(value, out _startStation);
        _startStation = Mathf.Max(_startStation, (float)_currentAlignmentInfo.StartStation);
        _endStation = Mathf.Max(_startStation, _endStation);

        _startText.text = _startStation.ToString();
        _endText.text = _endStation.ToString();
    }

    public void OnEndStationChanged(string value)
    {
        if (_currentAlignmentInfo.IsNull()) return;

        float.TryParse(value, out _endStation);
        _endStation = Mathf.Min(_endStation, (float)_currentAlignmentInfo.EndStation);
        _startStation = Mathf.Min(_startStation, _endStation);

        _startText.text = _startStation.ToString();
        _endText.text = _endStation.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnUIChecker.AddOnUI(GetInstanceID());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnUIChecker.RemoveOnUI(GetInstanceID());
    }
    #endregion
}