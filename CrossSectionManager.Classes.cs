using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;
using WatchBIM;
using WayBIM;
using WBScriptable;
using WPM;

public partial class CrossSectionManager : MonoBehaviour, IUIChecker
{
    internal class SlopeLabel
    {
        public float Station;
        public string Text;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    //도로선형 교차 데이터
    internal class AlignmentInfoIntersection
    {
        public WBAlignmentInfo AlignmentInfo;
        public List<(Vector3 position, float station)> IntersectionPairs;
        public bool IsSelectedAlignmentInfo;

        public override string ToString()
        {
            string text = string.Empty;

            if (IntersectionPairs?.Count > 0)
            {
                foreach (var pair in IntersectionPairs)
                {
                    if (text.IsValidText()) text += System.Environment.NewLine;
                    text += $"{AlignmentInfo.RoadName} : STA. {pair.station: 0+000.000}";
                }
            }

            return text;
        }
    }

    internal class Entity
    {
        public ModelTypes ModelType;
        public GameObject GO;

        public Entity(ModelTypes modelType, GameObject go)
        {
            this.ModelType = modelType;
            this.GO = go;
        }
    }

    //Dim : 각 MeshInfo에 해당하는 선 그룹
    internal class MeshLine2D
    {
        public MeshInfo MeshInfo;
        public List<Vector2[]> PointsList;
        public Color Color;
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
        public Vector2 Center;
        public bool IsValuable;
        public List<RectTransform> Rects;

        public MeshLine2D(MeshInfo meshInfo, List<Vector2[]> lines, Color color)
        {
            MeshInfo = meshInfo;
            PointsList = lines.Where(l => l?.Length > 1).ToList();
            IsValuable = PointsList?.Count > 0;
            if (IsValuable)
            {
                MinX = PointsList.Min(a => a.Min(b => b.x));
                MaxX = PointsList.Max(a => a.Max(b => b.x));
                MinY = PointsList.Min(a => a.Min(b => b.y));
                MaxY = PointsList.Max(a => a.Max(b => b.y));
                Center = new Vector2((MinX + MaxX) / 2, (MinY + MaxY) / 2);
            }
            Color = color;
        }

        public bool Contains(Vector2 point)
        {
            return point.x >= MinX && point.x <= MaxX
                && point.y >= MinY && point.y <= MaxY;
        }

        public float Distance(Vector2 point)
        {
            return Vector2.Distance(Center, point);
        }

        public void MouseEnter()
        {
            if (Rects?.Count > 0)
            {
                foreach (var line in Rects)
                {
                    line.GetComponent<Image>().color = Color.white;
                }
            }
        }

        public void MouseExit()
        {
            if (Rects?.Count > 0)
            {
                foreach (var line in Rects)
                {
                    line.GetComponent<Image>().color = Color;
                }
            }
        }

        public override string ToString()
        {
            return $"{MeshInfo.ModelType}{System.Environment.NewLine}{MeshInfo.Transform?.name}";
        }
    }

    //Dim
    internal class CrossSection2D
    {
        #region Members / Properties
        #region RectTransform, UI
        private RectTransform dimLineOffsetRect;
        private RectTransform dimLineRect;
        private RectTransform dimCurrentPointRect;
        private RectTransform dimTextRect;
        #endregion

        #region Data
        private CrossSection crossSection;
        private MeshLine2D currentMeshLine2D;
        #endregion

        #region Status
        private Transform mousePointer;
        private bool isReady;
        public bool IsReady => isReady;
        private TMP_Text infoText;

        private float minX;
        private float maxX;
        private float minY;
        private float maxY;

        private int dimCount;
        private bool dimEnabled;
        private bool snapEnabled;
        private bool multiDimEnabled;
        private bool modelInfoEnabled;

        private float snapDistance = 3.0f;
        private float line2DWidth = 0.3f;
        private float sqrSnapDistance = 9.0f;
        private float fontSize = 1;
        private float gridWidth = 1;

        private Quaternion rotation;
        private Vector2 localMousePosition;
        private Vector2 dim0;
        private Vector2 dim1;
        private Vector2 dimVector;
        private Vector2 dimPointSize;
        #endregion

        #region Temporary Generated
        private List<RectTransform> dimRects = new();
        private List<TMP_Text> dimTexts = new();
        private List<GameObject> dimGOs;
        #endregion

        #region CrossSection Generated
        private List<Entity> entities = new();
        private List<Vector2> vertices2D;
        private List<Vector2[]> pointsList;
        private List<RectTransform> normalRects;
        private List<Toggle> layerToggles = new();
        private List<(Vector2 originPosition, RectTransform rect, bool isLeft)> gridTexts = new();
        private List<RectTransform> gridRects = new();
        #endregion

        #region Define
        private readonly Vector2 gridMargin = new Vector2(5f, 15f);
        #endregion
        #endregion

        #region Init
        public CrossSection2D()
        {
            mousePointer = Instantiate(spherePrefab, goParent).transform;
            mousePointer.localScale = 0.5f * Vector3.one;
            mousePointer.name = "Pointer";
            mousePointer.GetComponent<MeshRenderer>().material.color = Color.red;

            sliderSize.onValueChanged.AddListener(SizeChanged);
            sliderLineWidth.onValueChanged.AddListener(LineWidthChanged);

            dimLineOffsetRect = GetNewLineRect(dimRect, Vector2.zero, Vector2.zero, line2DWidth, Color.white, "DimLine");
            dimLineOffsetRect.Deactivate();
            dimLineRect = dimLineOffsetRect.GetChild(0).GetComponent<RectTransform>();

            dimCurrentPointRect = Instantiate(dimPointPrefab, dimRect).GetComponent<RectTransform>();
            dimCurrentPointRect.anchoredPosition = Vector2.zero;
            dimCurrentPointRect.Deactivate();

            infoText = infoRect.GetChild(0).GetComponent<TMP_Text>();

            sqrSnapDistance = snapDistance * snapDistance;

            SizeChanged(sliderSize.value);
        }

        public void SetData(CrossSection crossSection)
        {
            this.crossSection = crossSection;
            Init();
            InitDim();
            Run2D();
            SizeChanged(sliderSize.value);

            crossSection.SetStationText();

            isReady = true;
        }

        public void Init()
        {
            isReady = false;

            normalRects = new();
            gridRects = new();
            gridTexts = new();

            for (int i = 0; i < gridRect.childCount; i++)
            {
                Destroy(gridRect.GetChild(i).gameObject);
            }
            for (int i = 0; i < layerRect.childCount; i++)
            {
                Destroy(layerRect.GetChild(i).gameObject);
            }

            if (entities?.Count > 0)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    Destroy(entities[i].GO);
                }
                entities.Clear();
            }
            else entities = new();

            ToggleDim(dimEnabled);
            ToggleMultiDim(multiDimEnabled);
            ToggleSnapping(snapEnabled);
            ToggleModelInfo(modelInfoEnabled);
        }

        public void InitDim()
        {
            currentMeshLine2D = null;
            infoText.text = string.Empty;
            dimRects = new();
            dimTexts = new();
            if (dimGOs?.Count > 0)
            {
                for (int i = 0; i < dimGOs.Count; i++)
                {
                    Destroy(dimGOs[i]);
                }
                dimGOs.Clear();
            }
            else dimGOs = new();
        }
        #endregion

        #region Update
        public void Update()
        {
            if (!isReady) return;

            UpdateGrid();
            UpdateMirror();
            UpdateDim();
            UpdateModelInfo();
        }

        private void UpdateGrid()
        {
            //grid text
            foreach (var gridPair in gridTexts)
            {
                var viewportPosition = viewportRect.InverseTransformPoint(gridPair.rect.transform.position);
                if (gridPair.isLeft)
                {
                    viewportPosition.x = 3;
                    viewportPosition = viewportRect.TransformPoint(viewportPosition);
                    viewportPosition.z = 0;
                    gridPair.rect.position = viewportPosition;
                }
                else if (!gridPair.isLeft)
                {
                    viewportPosition.x = viewportRect.rect.size.x - 3;
                    viewportPosition = viewportRect.TransformPoint(viewportPosition);
                    viewportPosition.z = 0;
                    gridPair.rect.transform.position = viewportPosition;
                }
            }
        }

        private void UpdateMirror()
        {
            var mousePosition = Input.mousePosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(viewportRect, mousePosition))
            {
                localMousePosition = normalRect.InverseTransformPoint(mousePosition);

                mousePointer.Activate();
                mousePointer.position = crossSection.GetMousePointerPosition(localMousePosition);
            }
            else mousePointer.Deactivate();
        }

        private void UpdateDim()
        {
            if (dimEnabled)
            {
                dimCurrentPointRect.anchoredPosition = GetCurrentVertex();

                if (dimCount == 1)
                {
                    dim1 = dimCurrentPointRect.anchoredPosition;

                    var distance = Vector2.Distance(dim0, dim1);
                    var angle = Mathf.Atan2(dim1.y - dim0.y, dim1.x - dim0.x) * Mathf.Rad2Deg;

                    dimLineOffsetRect.localRotation = Quaternion.Euler(0, 0, angle);
                    dimLineRect.sizeDelta = new Vector2(dimLineRect.sizeDelta.x, distance);

                    rotation = GetTextRotation(dim0, dim1);

                    dimTextRect.anchoredPosition = (dim0 + dim1) / 2;
                    dimTextRect.localRotation = rotation;
                    dimTextRect.GetComponent<TMP_Text>().text = $"{Vector2.Distance(dim0, dim1) / rate: 0.000}m";
                }
            }
        }

        private void UpdateModelInfo()
        {
            if (modelInfoEnabled)
            {
                infoRect.anchoredPosition = localMousePosition;

                if (pointsList?.Count > 0 is false) return;

                var valuableMeshLines = crossSection.MeshLines2D
                    .Where(w => w.Contains(localMousePosition))
                    .OrderBy(o => o.Distance(localMousePosition))
                    .ToList();

                if (valuableMeshLines?.Count > 0)
                {
                    var selectedMeshLine = valuableMeshLines.First();
                    if (currentMeshLine2D.IsNull() || currentMeshLine2D != selectedMeshLine)
                    {
                        currentMeshLine2D?.MouseExit();
                        currentMeshLine2D = selectedMeshLine;
                        infoText.text = currentMeshLine2D.ToString();
                        selectedMeshLine.MouseEnter();
                    }
                    infoRect.Activate();
                }
                else
                {
                    currentMeshLine2D?.MouseExit();
                    currentMeshLine2D = null;
                    infoRect.Deactivate();
                }
            }
        }

        public void ClickDim()
        {
            if (!dimEnabled || !isReady) return;

            var currentPoint = GetCurrentVertex();

            dimCount++;
            if (dimCount == 1)
            {
                InitDim();

                dim0 = currentPoint;

                var dimPoint = Instantiate(dimPointPrefab, dimRect);
                dimGOs.Add(dimPoint);

                var dimPointRect = dimPoint.GetComponent<RectTransform>();
                dimPointRect.anchoredPosition = dim0;
                dimRects.Add(dimPointRect);

                var dimText = Instantiate(dimTextPrefab, dimRect);
                dimGOs.Add(dimText);

                dimTextRect = dimText.GetComponent<RectTransform>();
                dimTextRect.anchoredPosition = dim0;

                dimLineOffsetRect.anchoredPosition = dim0;
                dimLineOffsetRect.Activate();
            }
            else if (dimCount == 2)
            {
                dimCount = 0;

                dimVector = dim1 - dim0;

                MakeDimPoints();
            }

            SizeChanged(sliderSize.value);
        }
        #endregion

        #region Generate
        private void Run2D()
        {
            if (crossSection.MeshLines2D?.Count > 0 is false) return;

            //유효한 2D 선만 추림
            var crossSectionLines = crossSection.MeshLines2D.Where(w => w.IsValuable).ToList();
            minX = crossSectionLines.Min(m => m.MinX);
            maxX = crossSectionLines.Max(m => m.MaxX);
            minY = crossSectionLines.Min(m => m.MinY);
            maxY = crossSectionLines.Max(m => m.MaxY);

            //선
            pointsList = new();
            foreach (var lines in crossSectionLines.Select(s => s.PointsList))
            {
                pointsList.AddRange(lines);
            }

            //점
            vertices2D = new();
            foreach (var line in pointsList)
            {
                vertices2D.AddRange(line);
            }
            vertices2D = vertices2D.Distinct().ToList();

            //2D 전체 크기에 맞게 rect 조절
            contentRect.sizeDelta = rate * 0.5f * new Vector2(maxX - minX, maxY - minY) + new Vector2(minX + maxX, minY + maxY) / 2;

            for (int i = 0; i < crossSectionLines.Count; i++)
            {
                var data = crossSectionLines[i];
                var lineParentGO = Instantiate(lineParentPrefab, normalRect);
                lineParentGO.name = $"{data.MeshInfo.ModelType}.{data.MeshInfo.Transform?.name}{i}";
                entities.Add(new(data.MeshInfo.ModelType, lineParentGO));

                var lineParentRect = lineParentGO.GetComponent<RectTransform>();
                lineParentRect.localPosition = Vector3.zero;
                lineParentRect.SetTop(0);
                lineParentRect.SetBottom(0);

                List<RectTransform> currLine2DRects = new();

                for (int j = 0; j < data.PointsList.Count; j++)
                {
                    var line2D = data.PointsList[j];

                    for (int k = 0; k < line2D.Length - 1; k++)
                    {
                        var lineRect = GetNewLineRect(lineParentRect, line2D[k], line2D[k + 1], line2DWidth, data.Color, $"line{j}{k}")
                            .GetChild(0).GetComponent<RectTransform>();

                        currLine2DRects.Add(lineRect);
                    }

                    AddSlopeText(data.MeshInfo.ModelType, line2D);
                }

                data.Rects = currLine2DRects;
                normalRects.AddRange(currLine2DRects);
            }

            //grid
            var selectedIntersectionPairs = crossSection.AlignmentInfoIntersections.Find(w => w.IsSelectedAlignmentInfo)?.IntersectionPairs;
            if (selectedIntersectionPairs.IsNotNull())
            {
                var mainIntersectionPair = selectedIntersectionPairs.OrderBy(o => Vector3.Distance(o.position, crossSection.Center)).First();
                var mainPoint = GetPlaneVector2(mainIntersectionPair.position, crossSection.PlaneNormal, crossSection.PlanePosition);

                List<float> gridHeights = new();
                gridHeights.Add(mainPoint.y);

                float currentUpY = mainPoint.y;
                while (currentUpY <= maxY + gridMargin.y * rate)
                {
                    currentUpY += 5.0f * rate;
                    gridHeights.Add(currentUpY);
                }

                float currentDownY = mainPoint.y;
                while (currentDownY >= minY - gridMargin.y * rate)
                {
                    currentDownY -= 5.0f * rate;
                    gridHeights.Add(currentDownY);
                }

                var lineMinX = minX - gridMargin.x * rate;
                var lineMaxX = maxX + gridMargin.x * rate;

                foreach (var height in gridHeights)
                {
                    var position0 = new Vector2(lineMinX, height);
                    var position1 = new Vector2(lineMaxX, height);

                    var lineRect = GetNewLineRect(gridRect, position0, position1, gridWidth, Color.yellow).GetChild(0).GetComponent<RectTransform>();
                    var color = Color.yellow;
                    color.a = 0.2f;
                    lineRect.GetComponent<Image>().color = color;
                    gridRects.Add(lineRect);

                    var textRect = GetNewTextRect(gridRect, position0, $"{(height - mainPoint.y) / rate: 0}");
                    textRect.sizeDelta = new Vector2(0, 10);
                    var text = textRect.GetComponent<TMP_Text>();
                    text.alignment = TextAlignmentOptions.BottomLeft;
                    text.margin = new Vector4(2, 0, 0, 0);
                    text.color = Color.yellow;
                    gridTexts.Add((position0, textRect, true));
                }

                var absHeights = gridHeights.Select(s => (int)(s / rate + WBHelper.Offset.y));
                var absHeight = absHeights.Min() - (absHeights.Min() % 5) - 10;
                while (absHeight <= absHeights.Max() + 10)
                {
                    bool isLong = absHeight % 5 == 0;

                    var position = new Vector2(lineMaxX, absHeight * rate);
                    var textRect = GetNewTextRect(gridRect, position, isLong ? $"{absHeight: 0}" : string.Empty);
                    textRect.sizeDelta = Vector2.zero;

                    var text = textRect.GetComponent<TMP_Text>();
                    text.alignment = TextAlignmentOptions.BottomRight;
                    text.margin = new Vector4(0, 0, 2, 0);
                    text.color = Color.white;
                    gridTexts.Add((position, textRect, false));

                    var lineRect = GetNewLineRect(textRect, Vector2.zero, new Vector2(isLong ? -15 : -5, 0), gridWidth, Color.white).GetChild(0).GetComponent<RectTransform>();
                    gridRects.Add(lineRect);

                    absHeight++;
                }
            }

            //선형 교차점 데이터
            foreach (var alignmentInfoIntersection in crossSection.AlignmentInfoIntersections)
            {
                foreach (var pair in alignmentInfoIntersection.IntersectionPairs)
                {
                    var planePosition = GetPlaneVector2(pair.position, crossSection.PlaneNormal, crossSection.PlanePosition);
                    var newText = GetNewTextRect(normalRect, planePosition + 4 * Vector2.up, $"{alignmentInfoIntersection.AlignmentInfo.RoadName}{System.Environment.NewLine}{pair.station: 0+000.000}");

                    entities.Add(new(ModelTypes.MtRoadLines, newText.gameObject));
                }
            }

            //해당되는 레이어 리스트업
            layerToggles = new();
            var modelTypes = entities?.Select(s => s.ModelType)?.Distinct().ToList();
            if (modelTypes?.Count > 0)
            {
                foreach (var modelType in modelTypes)
                {
                    var layerItem = Instantiate(layerPrefab, layerScrollContent);

                    string layerName = modelType.ToString();
                    if (modelTypeInfo.Dic.TryGetValue(modelType, out var data))
                        layerName = data.KoreanName;
                    layerItem.GetComponent<TMP_Text>().text = layerName;
                    layerItem.name = layerName;

                    var toggle = layerItem.GetComponent<Toggle>();
                    toggle.onValueChanged.AddListener((value) => OnLayerToggle(modelType, value));
                    layerToggles.Add(toggle);
                }
            }
        }

        private void OnLayerToggle(ModelTypes modelType, bool value)
        {
            foreach (var entity in entities.Where(w => w.ModelType == modelType))
            {
                entity.GO.SetActive(value);
            }
        }

        public void OnAllLayerToggle(bool value)
        {
            foreach (var entity in entities)
            {
                entity.GO.SetActive(value);
            }

            foreach (var toggle in layerToggles)
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }

        /// <summary>
        /// ModelType에 따른 경사도 텍스트 추가
        /// </summary>
        /// <param name="modelType"></param>
        /// <param name="vertices"></param>
        private void AddSlopeText(ModelTypes modelType, Vector2[] vertices)
        {
            bool isValid = false;
            switch (modelType)
            {
                case ModelTypes.MtCutSlope:
                case ModelTypes.MtFillSlope:
                case ModelTypes.MtRoadSurface:
                    isValid = true;
                    break;
            }

            if (!isValid) return;

            Vector2 firstVertex = Vector2.zero;
            Vector2 lastVertex = Vector2.zero;
            float slope = -1;
            float epsilon = GetSlopeEpsilon(modelType);

            for (int i = 0; i < vertices.Length - 1; i++)
            {
                var v0 = vertices[i];
                var v1 = vertices[i + 1];
                var vector = v1 - v0;

                var currSlope = Mathf.Abs(vector.x / vector.y);

                if (slope < 0)
                {
                    slope = currSlope;
                    firstVertex = v0;
                    lastVertex = v1;

                    if (vertices.Length == 2)
                    {
                        var textGO = GetNewTextRect(normalRect, (firstVertex + lastVertex) / 2, SlopeTextByModelType(modelType, firstVertex, lastVertex));
                        textGO.transform.localRotation = GetTextRotation(firstVertex, lastVertex);
                        entities.Add(new(modelType, textGO.gameObject));
                    }
                }
                else
                {
                    if (Mathf.Abs(slope - currSlope) < epsilon)
                    {
                        lastVertex = v1;
                    }
                    else
                    {
                        var textGO = GetNewTextRect(normalRect, (firstVertex + lastVertex) / 2, SlopeTextByModelType(modelType, firstVertex, lastVertex));
                        textGO.transform.localRotation = GetTextRotation(firstVertex, lastVertex);
                        entities.Add(new(modelType, textGO.gameObject));
                        slope = currSlope;
                        firstVertex = v0;
                        lastVertex = v1;
                    }

                    if (i == vertices.Length - 2)
                    {
                        var textGO = GetNewTextRect(normalRect, (firstVertex + lastVertex) / 2, SlopeTextByModelType(modelType, firstVertex, lastVertex));
                        textGO.transform.localRotation = GetTextRotation(firstVertex, lastVertex);
                        entities.Add(new(modelType, textGO.gameObject));
                    }
                }
            }
        }

        private RectTransform GetNewTextRect(RectTransform parent, Vector2 position, string text = "")
        {
            var textRect = Instantiate(dimTextPrefab, parent).GetComponent<RectTransform>();
            textRect.anchoredPosition = position;

            var tmpText = textRect.GetComponent<TMP_Text>();
            tmpText.text = text;
            tmpText.fontSize = 1;
            //tmpText.alignment = textAlignmentOptions;
            tmpText.Rebuild(CanvasUpdate.PostLayout);

            return textRect;
        }

        private RectTransform GetNewLineRect(Transform lineParent, Vector2 p0, Vector2 p1, float width, Color color, string name = "")
        {
            float angle = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(p0, p1);

            var lineOffset = Instantiate(linePrefab, lineParent.transform);
            lineOffset.name = $"{name}:{length}";

            var lineOffsetRect = lineOffset.GetComponent<RectTransform>();
            lineOffsetRect.anchoredPosition = p0;
            lineOffsetRect.localRotation = Quaternion.Euler(0, 0, angle);

            var line = lineOffset.transform.GetChild(0).gameObject;
            var lineRect = line.GetComponent<RectTransform>();
            lineRect.sizeDelta = new Vector2(width, length);
            line.GetComponent<Image>().color = color;

            return lineOffsetRect;
        }
        #endregion

        #region Dim
        private void MakeDimPoints()
        {
            if (multiDimEnabled)
            {
                dimGOs.Remove(dimTextRect.gameObject);
                Destroy(dimTextRect.gameObject);

                List<Vector2> points = new();
                points.Add(dim0);
                foreach (var line in pointsList)
                {
                    for (int i = 0; i < line.Length - 1; i++)
                    {
                        if (FindLineIntersectionPoint(line[i], line[i + 1], out var point)) points.Add(point);
                    }
                }
                points = points.OrderBy(o => (dim0 - o).sqrMagnitude).ToList();
                points.Add(dim1);

                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p0 = points[i];
                    var p1 = points[i + 1];

                    //point
                    var dimPointGO = Instantiate(dimPointPrefab, dimRect);
                    dimGOs.Add(dimPointGO);

                    var dimPointRect = dimPointGO.GetComponent<RectTransform>();
                    dimPointRect.anchoredPosition = p0;

                    dimRects.Add(dimPointRect);

                    //text
                    var dimTextGO = Instantiate(dimTextPrefab, dimRect);
                    dimGOs.Add(dimTextGO);

                    var dimText = dimTextGO.GetComponent<TMP_Text>();
                    dimText.text = $"{Vector2.Distance(p0, p1) / rate: 0.0}";
                    dimText.fontSize = 1;

                    var dimTextRect = dimTextGO.GetComponent<RectTransform>();
                    dimTextRect.anchoredPosition = (p0 + p1) / 2;
                    dimTextRect.localRotation = rotation;

                    dimTexts.Add(dimText);
                }

                //final point
                var finalDimPointGO = Instantiate(dimPointPrefab, dimRect);
                dimGOs.Add(finalDimPointGO);
                var finalDimPointRect = finalDimPointGO.GetComponent<RectTransform>();
                finalDimPointRect.anchoredPosition = points.Last();

                dimRects.Add(finalDimPointRect);
            }
        }

        private bool FindLineIntersectionPoint(Vector2 p0, Vector2 p1, out Vector2 point)
        {
            Vector2 dirLine2 = p1 - p0;

            float denominator = dimVector.x * dirLine2.y - dimVector.y * dirLine2.x;

            if (Mathf.Approximately(denominator, 0))
            {
                point = Vector2.zero;
                return false;
            }

            Vector2 line1ToPoint = p0 - dim0;
            float t = (line1ToPoint.x * dirLine2.y - line1ToPoint.y * dirLine2.x) / denominator;
            float u = (line1ToPoint.x * dimVector.y - line1ToPoint.y * dimVector.x) / denominator;

            point = dim0 + t * dimVector;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }
        #endregion

        #region Functions
        private Vector2 GetCurrentVertex()
        {
            var currentPoint2D = new Vector2(localMousePosition.x, localMousePosition.y);

            if (Input.GetKey(KeyCode.LeftControl) && dimCount == 1)
            {
                var currentVector = dim0 - currentPoint2D;
                if (Mathf.Abs(currentVector.x) > Mathf.Abs(currentVector.y)) currentPoint2D.y = dim0.y;
                else currentPoint2D.x = dim0.x;
            }
            else if (snapEnabled && vertices2D?.Count > 0)
            {
                var valuables = vertices2D
                    .Select(v => (v, (v - currentPoint2D).sqrMagnitude))
                    .Where(w => w.sqrMagnitude < sqrSnapDistance)
                    .ToList();

                if (valuables?.Count > 0) return valuables.OrderBy(o => o.sqrMagnitude).First().v;
            }

            return currentPoint2D;
        }

        private Quaternion GetTextRotation(Vector2 v0, Vector2 v1)
        {
            var angle = Mathf.Atan2(v1.y - v0.y, v1.x - v0.x) * Mathf.Rad2Deg;
            if (angle > 90 || angle < -90) angle += 180;
            return Quaternion.Euler(0, 0, angle);
        }
        #endregion

        #region Public Methods
        public void SizeChanged(float value)
        {
            var size = value / rate;
            gridWidth = 1 / size;
            fontSize = 30 / size;
            dimPointSize = 20 / size * Vector2.one;

            //base rect
            var moveVector = (contentRect.localScale.x - size) * contentRect.InverseTransformPoint(Input.mousePosition);
            moveVector.z = 0;
            contentRect.localScale = new Vector3(size, size, 1);
            contentRect.localPosition += moveVector;

            //grid line
            foreach (var rect in gridRects)
            {
                rect.sizeDelta = new Vector2(gridWidth, rect.sizeDelta.y);
            }

            //grid text
            foreach (var gridPair in gridTexts)
            {
                gridPair.rect.GetComponent<TMP_Text>().fontSize = fontSize;
            }

            //dim text
            if (dimTextRect.IsNotNull()) dimTextRect.GetComponent<TMP_Text>().fontSize = fontSize;
            foreach (var text in dimTexts)
            {
                text.fontSize = fontSize;
            }

            //dim point
            dimCurrentPointRect.sizeDelta = dimPointSize;
            foreach (var dimRect in dimRects)
            {
                dimRect.sizeDelta = dimPointSize;
            }

            //dim line
            dimLineRect.sizeDelta = new Vector2(dimPointSize.x * 0.2f, dimLineRect.sizeDelta.y);

            //info
            infoRect.localScale = 2 / size * Vector3.one;
        }

        public void LineWidthChanged(float value)
        {
            line2DWidth = value;
            foreach (var line in normalRects)
            {
                var size = line.sizeDelta;
                size.x = value;
                line.sizeDelta = size;
            }
        }

        public void ToggleDim(bool value)
        {
            InitDim();

            dimEnabled = value;

            if (dimEnabled)
            {
                dimCurrentPointRect.Activate();
            }
            else
            {
                dimCurrentPointRect.Deactivate();
                dimLineOffsetRect.Deactivate();
                dimTextRect.Deactivate();
            }
        }

        public void ToggleSnapping(bool value)
        {
            snapEnabled = value;
        }

        public void ToggleMultiDim(bool value)
        {
            InitDim();

            multiDimEnabled = value;
        }

        public void ToggleModelInfo(bool value)
        {
            modelInfoEnabled = value;
            if (!value)
            {
                infoRect.Deactivate();
                currentMeshLine2D?.MouseExit();
            }
        }
        #endregion
    }

    //CrossSection
    internal class CrossSection
    {
        #region Members / Properties
        private bool isReady = false;
        public bool IsReady => isReady;
        private float station;
        public float Station => station;
        private Vector3 point0;
        public Vector3 Point0 => point0;
        private Vector3 point1;
        public Vector3 Point1 => point1;
        public Vector3 Center => (point0 + point1) / 2;
        private Vector3 planeForward;
        private Vector3 planeNormal;
        public Vector3 PlaneNormal => planeNormal;
        private Vector3 planePosition;
        public Vector3 PlanePosition => planePosition;

        private Transform crossSectionPlane;
        private Transform crossSectionSphere0;
        private Transform crossSectionSphere1;

        private List<GameObject> gos = new();
        private List<AlignmentInfoIntersection> alignmentInfoIntersections;
        public List<AlignmentInfoIntersection> AlignmentInfoIntersections => alignmentInfoIntersections;
        private List<MeshLine2D> meshLines2D = new();
        public List<MeshLine2D> MeshLines2D => meshLines2D;
        private Action<float, List<Vector3>, Color> addLineAction;

        private int clickCount;
        #endregion

        #region Free
        public CrossSection(Action<float, List<Vector3>, Color> addLineAction = null)
        {
            this.addLineAction = addLineAction;
        }

        public void DestroyGOs()
        {
            for (int i = 0; i < gos.Count; i++)
            {
                Destroy(gos[i]);
            }
            gos = new();
        }

        public void OnClick()
        {
            if (clickCount == 0)
            {
                DestroyGOs();
                point0 = hitManager.CurrentHitInfo.Point;
                crossSectionSphere0 = Instantiate(spherePrefab, goParent).transform;
                gos.Add(crossSectionSphere0.gameObject);
                crossSectionSphere0.Activate();
                crossSectionSphere0.position = point0;
            }
            else
            {
                point1 = hitManager.CurrentHitInfo.Point;
                crossSectionSphere1 = Instantiate(spherePrefab, goParent).transform;
                gos.Add(crossSectionSphere1.gameObject);
                crossSectionSphere1.Activate();
                crossSectionSphere1.position = point1;
                crossSectionPlane = Instantiate(planePrefab, goParent).transform;
                gos.Add(crossSectionPlane.gameObject);
                SetPlane();
                SetStationText();
            }

            clickCount++;
            if (clickCount >= 2) clickCount = 0;
        }
        #endregion

        #region Normal
        public CrossSection(WBStationPoint stationPoint, float planeLength, Action<float, List<Vector3>, Color> addLineAction = null)
        {
            this.addLineAction = addLineAction;
            this.station = stationPoint.Station;

            var right = planeLength * 0.5f * new Vector3(-stationPoint.Direction.z, 0, stationPoint.Direction.x).normalized;
            this.point0 = stationPoint.Position - right;
            this.point1 = stationPoint.Position + right;

            crossSectionPlane = Instantiate(planePrefab, goParent).transform;
            crossSectionSphere0 = Instantiate(spherePrefab, goParent).transform;
            crossSectionSphere1 = Instantiate(spherePrefab, goParent).transform;

            crossSectionSphere0.position = point0;
            crossSectionSphere1.position = point1;

            gos.Add(crossSectionPlane.gameObject);
            gos.Add(crossSectionSphere0.gameObject);
            gos.Add(crossSectionSphere1.gameObject);

            SetPlane();
        }
        #endregion

        #region Functions
        public Vector3 GetMousePointerPosition(Vector2 mousePosition)
        {
            mousePosition /= rate;
            return planePosition - mousePosition.x * planeForward + mousePosition.y * Vector3.up;
        }

        public void Visibie(bool enable)
        {
            foreach (var go in gos)
            {
                go.SetActive(enable);
            }
        }

        private bool DoesBoundsIntersectPlane(Vector3[] vertices)
        {
            foreach (Vector3 vertex in vertices)
            {
                if (Vector3.Dot(vertex - planePosition, planeNormal) > 0f)
                    return true;
            }

            return false;
        }
        #endregion

        #region Generate
        /// <summary>
        /// 유효한 MeshInfo와의 교선 탐색후 ALine 추가
        /// </summary>
        /// <param name="meshInfos"></param>
        /// <returns></returns>
        public IEnumerator MakeDatas(List<MeshInfo> meshInfos)
        {
            gos.Remove(crossSectionPlane.gameObject);
            gos.Remove(crossSectionSphere0.gameObject);
            gos.Remove(crossSectionSphere1.gameObject);

            Destroy(crossSectionPlane.gameObject);
            Destroy(crossSectionSphere0.gameObject);
            Destroy(crossSectionSphere1.gameObject);

            //평면과 간섭되는 모든 MeshInfo의 교선 계산
            foreach (var meshInfo in meshInfos.Where(w => DoesBoundsIntersectPlane(w.BoundsVertices)))
            {
                Color color = Color.white;
                if (modelTypeInfo.Dic.TryGetValue(meshInfo.ModelType, out var data))
                    color = data.Color;

                MakeData(meshInfo, color);
            }

            //추가 MeshInfo의 교선 계산
            if (additionalMeshInfos?.Count > 0)
            {
                Dictionary<ModelTypes, List<Vector3[]>> verticesDic = new();
                foreach (var meshInfo in additionalMeshInfos)
                {
                    Color color = Color.white;
                    if (modelTypeInfo.Dic.TryGetValue(meshInfo.ModelType, out var data))
                        color = data.Color;

                    var verticesList = MakeData(meshInfo, color);
                    verticesDic.Add(meshInfo.ModelType, verticesList);
                }

                ////토사 리핑 사이 mesh
                //{
                //    if (verticesDic.TryGetValue(ModelTypes.MtTerrain, out var soilVerticesList) &&
                //        verticesDic.TryGetValue(ModelTypes.MtRippingSurface, out var rippingVerticesList))
                //    {
                //        var leftPoint = point0 + 10 * (point1 - point0).normalized;
                //        var rightPoint = point1 + 10 * (point0 - point1).normalized;

                //        var soilVertices = soilVerticesList.First().OrderBy(o => Vector3.Distance(leftPoint, o));
                //        var rippingVertices = rippingVerticesList.First().OrderBy(o => Vector3.Distance(rightPoint, o));

                //        var vertices = soilVertices.Union(rippingVertices).ToArray();
                //        if (vertices.Length > 2)
                //        {
                //            var triangles = Triangulator.GetPoints(vertices);
                //            if (triangles.Length > 2)
                //            {
                //                gos.Add(MakeGO("Soil_Ripping", goParent, vertices, triangles, Color.white));
                //            }
                //        }
                //    }
                //}

                ////리핑 발파 사이 mesh
                //{
                //    if (verticesDic.TryGetValue(ModelTypes.MtRippingSurface, out var rippingVerticesList) &&
                //        verticesDic.TryGetValue(ModelTypes.MtBlastingSurface, out var blastingVerticesList))
                //    {
                //        var leftPoint = point0 + 10 * (point1 - point0).normalized;
                //        var rightPoint = point1 + 10 * (point0 - point1).normalized;

                //        var rippingVertices = rippingVerticesList.First().OrderBy(o => Vector3.Distance(leftPoint, o));
                //        var blastingVertices = blastingVerticesList.First().OrderBy(o => Vector3.Distance(rightPoint, o));

                //        var vertices = rippingVertices.Union(blastingVertices).ToArray();
                //        if (vertices.Length > 2)
                //        {
                //            var triangles = Triangulator.GetPoints(vertices);
                //            if (triangles.Length > 2)
                //            {
                //                gos.Add(MakeGO("Ripping_Blasting", goParent, vertices, triangles, Color.gray));
                //            }
                //        }
                //    }
                //}
            }

            yield return new WaitForEndOfFrame();
        }

        private void SetPlane()
        {
            isReady = true;

            meshLines2D = new();

            crossSectionPlane.Activate();

            //position
            planePosition = (point0 + point1) / 2;
            crossSectionPlane.position = new(planePosition.x, hitManager.TotalBounds.center.y, planePosition.z);

            //direction
            var vector = point0 - point1;
            var direction = vector;
            direction.y = 0;
            crossSectionPlane.forward = direction.normalized;
            planeForward = direction.normalized;
            planeNormal = crossSectionPlane.right;

            //plane scale
            crossSectionPlane.localScale = new(1, hitManager.TotalBounds.size.y, vector.magnitude);

            //AlignmentInfo
            alignmentInfoIntersections = new();
            foreach (var alignmentInfo in projectData.AlignmentInfos.Values)
            {
                List<(Vector3 point, float station)> intersectionPairs = new();
                for (int i = 0; i < alignmentInfo.StationPoints.Count - 1; i++)
                {
                    var currStationPoint = alignmentInfo.StationPoints[i];
                    var nextStationPoint = alignmentInfo.StationPoints[i + 1];

                    Vector3 lineDirection = (nextStationPoint.Position - currStationPoint.Position).normalized;
                    float denominator = Vector3.Dot(planeNormal, lineDirection);

                    // Check if the line and plane are not parallel
                    if (Mathf.Abs(denominator) > 0.0001f)
                    {
                        float t = -Vector3.Dot(currStationPoint.Position - planePosition, planeNormal) / denominator;
                        if (t >= 0 && t <= 1)
                        {
                            var point = currStationPoint.Position + t * lineDirection;
                            if (!intersectionPairs.Exists(e => Vector3.Distance(e.point, point) < 1f))
                            {
                                var vector0 = point0 - point;
                                vector0.y = 0;
                                var vector1 = point1 - point;
                                vector1.y = 0;
                                if (vector0.normalized != vector1.normalized)
                                {
                                    intersectionPairs.Add((point, currStationPoint.Station + t * (nextStationPoint.Station - currStationPoint.Station)));
                                }
                            }
                        }
                    }
                }

                if (intersectionPairs.Count > 0) alignmentInfoIntersections.Add(new AlignmentInfoIntersection()
                {
                    AlignmentInfo = alignmentInfo,
                    IntersectionPairs = intersectionPairs,
                    IsSelectedAlignmentInfo = currentAlignmentInfo.IsNull() ? true : alignmentInfo.RoadId == currentAlignmentInfo.RoadId,
                });
            }
        }

        public void SetStationText()
        {
            string intersectionText = string.Empty;
            if (alignmentInfoIntersections?.Count > 0)
            {
                foreach (var alignmentInfoIntersection in alignmentInfoIntersections)
                {
                    if (intersectionText.IsValidText()) intersectionText += System.Environment.NewLine;
                    intersectionText += alignmentInfoIntersection.ToString();
                }
            }

            stationText.text = intersectionText;
        }

        /// <summary>
        /// 해당 MeshInfo와 Plane의 모든 교선을 이어진 것들끼리 Grouping 후 데이터 생성
        /// </summary>
        /// <param name="meshInfo"></param>
        /// <param name="color"></param>
        [BurstCompatible]
        private List<Vector3[]> MakeData(MeshInfo meshInfo, Color color)
        {
            #region Intersections
            NativeArray<(Vector3 p0, Vector3 p1, Vector3 p2)> trianglesNArray = new(meshInfo.TriangleVertices, Allocator.TempJob);
            NativeArray<(Vector3 c0, Vector3 c1, Vector3 c2)> crossedPointsNArray = new(meshInfo.TriangleVertices.Length, Allocator.TempJob);
            NativeArray<(bool b0, bool b1, bool b2)> crossedNArray = new(meshInfo.TriangleVertices.Length, Allocator.TempJob);

            new MeshPlaneIntersectionJob
            {
                TriangleVertices = trianglesNArray,
                PlaneNormal = planeNormal,
                PlanePosition = planePosition,
                Point0 = point0,
                Point1 = point1,
                CrossedPoints = crossedPointsNArray,
                Crossed = crossedNArray,
            }
            .Schedule(meshInfo.TriangleVertices.Length, 10)
            .Complete();

            var crossedPointsList = crossedPointsNArray.ToList();
            var crossedList = crossedNArray.ToList();

            trianglesNArray.Dispose();
            crossedPointsNArray.Dispose();
            crossedNArray.Dispose();

            List<List<Vector3>> intersections = new();
            for (int i = 0; i < crossedList.Count; i++)
            {
                List<Vector3> foundsPoints = new();

                if (crossedList[i].b0) foundsPoints.Add(crossedPointsList[i].c0);
                if (crossedList[i].b1) foundsPoints.Add(crossedPointsList[i].c1);
                if (crossedList[i].b2) foundsPoints.Add(crossedPointsList[i].c2);

                if (foundsPoints.Count == 2)
                {
                    intersections.Add(new List<Vector3>()
                    {
                        foundsPoints[0],
                        foundsPoints[1]
                    });
                }
            }
            #endregion

            #region Grouping
            //이어진 교선끼리 그루핑
            List<List<List<Vector3>>> groups = new();
            List<List<Vector3>> currentGroup = new();
            while (intersections.Count > 0)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(intersections[0]);
                    intersections.RemoveAt(0);
                }
                else
                {
                    bool addedToGroup = false;

                    for (int i = 0; i < intersections.Count; i++)
                    {
                        ///해당 그룹에 현재의 교선과 접하는 부분이 있는지 확인
                        ///그룹의 처음이나 끝이 교선의 처음이나 끝과 같은지 확인하기 위해 4개의 경우의 수가 필요

                        //그룹의 끝 <=> 교선의 시작 : 교선 그대로
                        if (VertexEqual(currentGroup.Last().Last(), intersections[i].First()))
                        {
                            currentGroup.Add(intersections[i]);
                            intersections.RemoveAt(i);
                            addedToGroup = true;
                            break;
                        }
                        //그룹의 끝 <=> 교선의 끝 : 교선 뒤집기
                        else if (VertexEqual(currentGroup.Last().Last(), intersections[i].Last()))
                        {
                            var temp = intersections[i].ToList();
                            temp.Reverse();
                            currentGroup.Add(temp);
                            intersections.RemoveAt(i);
                            addedToGroup = true;
                            break;
                        }
                        //그룹의 시작 <=> 교선의 끝 : 교선 그대로
                        else if (VertexEqual(currentGroup.First().First(), intersections[i].Last()))
                        {
                            currentGroup.Insert(0, intersections[i]);
                            intersections.RemoveAt(i);
                            addedToGroup = true;
                            break;
                        }
                        //그룹의 시작 <=> 교선의 시작 : 교선 뒤집기
                        else if (VertexEqual(currentGroup.First().First(), intersections[i].First()))
                        {
                            var temp = intersections[i].ToList();
                            temp.Reverse();
                            currentGroup.Insert(0, temp);
                            intersections.RemoveAt(i);
                            addedToGroup = true;
                            break;
                        }
                    }

                    if (!addedToGroup)
                    {
                        groups.Add(currentGroup);
                        currentGroup = new List<List<Vector3>>();
                    }
                }
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }
            #endregion

            #region ReArrange
            ///데이터 정리
            List<Vector3[]> verticesList = new();
            foreach (var group in groups)
            {
                //점집합
                List<Vector3> vertexList = new();
                foreach (var points in group)
                {
                    vertexList.AddRange(points);
                }

                //연속된 점중 겹치는 것 삭제
                for (int i = vertexList.Count - 1; i > 0; i--)
                {
                    if (VertexEqual(vertexList[i], vertexList[i - 1])) vertexList.RemoveAt(i - 1);
                }

                if (vertexList.Count >= 2) verticesList.Add(vertexList.ToArray());
            }
            #endregion

            #region MakeData
            //각 그룹의 2D, 3D
            GameObject go = new(meshInfo.ModelType.ToString());
            go.transform.SetParent(goParent);
            gos.Add(go);

            ///slope
            AddSlopeText(meshInfo.ModelType, verticesList);

            List<Vector2[]> lines2D = new();
            int index = 0;
            foreach (var currVertices in verticesList)
            {
                ///ALine
                addLineAction?.Invoke(Station, currVertices.ToList(), color);

                ///2D
                if (twoEnabled) lines2D.Add(currVertices.Select(s => GetPlaneVector2(s, planeNormal, planePosition)).ToArray());

                ///Mesh
                if (meshEnabled)
                {
                    //mesh 가능한지 체크
                    if (currVertices.Length < 3 ||
                        (currVertices.Length == 3 && !AreaValuable(currVertices[0], currVertices[1], currVertices[2])) ||
                        !VertexEqual(currVertices[0], currVertices[currVertices.Length - 1])) continue;

                    var triangles = Triangulator.GetPoints(currVertices);
                    if (triangles.Length < 3) continue;

                    MakeGO($"{meshInfo.ModelType}_{meshInfo.Transform?.name}_{index++}", go.transform, currVertices, triangles, color);
                }
            }

            meshLines2D.Add(new MeshLine2D(meshInfo, lines2D, color));
            #endregion

            return verticesList;
        }

        /// <summary>
        /// ModelType에 따른 경사도 텍스트 추가
        /// </summary>
        /// <param name="modelType"></param>
        /// <param name="vertices"></param>
        private void AddSlopeText(ModelTypes modelType, List<Vector3[]> verticesList)
        {
            bool isValid = false;
            switch (modelType)
            {
                case ModelTypes.MtCutSlope:
                case ModelTypes.MtFillSlope:
                case ModelTypes.MtRoadSurface:
                    isValid = true;
                    break;
            }

            if (!isValid) return;

            float epsilon = GetSlopeEpsilon(modelType);

            List<(float slope, Vector3 firstVertex, Vector3 lastVertex)> slopes = new();
            foreach (var vertices in verticesList)
            {
                for (int i = 0; i < vertices.Length - 1; i++)
                {
                    var v0 = vertices[i];
                    var v1 = vertices[i + 1];

                    var vector = v1 - v0;
                    var currSlope = Mathf.Abs(vector.y / new Vector3(vector.x, 0, vector.z).magnitude);

                    if (slopes.Count == 0 || Mathf.Abs(slopes.Last().slope - currSlope) >= epsilon)
                    {
                        slopes.Add((currSlope, v0, v1));
                    }
                    else
                    {
                        var pair = slopes.Last();
                        pair.lastVertex = v1;
                        slopes[slopes.Count - 1] = pair;
                    }
                }
            }

            foreach (var pair in slopes)
            {
                var vector = (pair.firstVertex - pair.lastVertex).normalized;
                if (Vector3.Dot(planeForward, vector) < 0) vector = -vector;
                
                slopeLabels.Add(new SlopeLabel()
                {
                    Station = station,
                    Position = (pair.firstVertex + pair.lastVertex) * 0.5f,
                    Rotation = Quaternion.LookRotation(PlaneNormal, Vector3.Cross(vector, PlaneNormal)),
                    Text = SlopeTextByModelType(modelType, pair.firstVertex, pair.lastVertex),
                });
            }
        }

        /// <summary>
        /// Mesh 를 갖는 GameObject 생성
        /// </summary>
        /// <param name="name"></param>
        /// <param name="transform"></param>
        /// <param name="vertices"></param>
        /// <param name="triangles"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        private GameObject MakeGO(string name, Transform transform, Vector3[] vertices, int[] triangles, Color color)
        {
            GameObject go = new(name);
            go.transform.SetParent(transform);
            go.AddComponent<MeshFilter>().mesh = new()
            {
                vertices = vertices,
                triangles = triangles,
            };
            go.AddComponent<MeshRenderer>().material = new Material(material) { color = color };

            return go;
        }

        private bool AreaValuable(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            return Vector3.Cross(p1 - p0, p2 - p0).sqrMagnitude > 0.0001f;
        }

        private bool VertexEqual(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.Distance(lhs, rhs) < 0.001f;
        }
        #endregion
    }
}