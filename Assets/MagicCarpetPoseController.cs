using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Mediapipe;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Runs MediaPipe pose detection in the background for the magic carpet scene.
/// Only the two shoulder landmarks are forwarded to the existing movement input.
/// </summary>
public class MagicCarpetPoseController : MonoBehaviour
{
    private static MagicCarpetPoseController instance;

    private const int LeftShoulderIndex = 11;
    private const int RightShoulderIndex = 12;
    private const int MaxTrajectoryPoints = 120;
    private const int CircleClassifierPort = 5055;
    public float autoMainStartSeconds = 10f;
    private float autoMainStartAt;

    [Header("Camera")]
    public string preferredCameraNameContains = "RealSense";
    public string preferredCameraNameAlsoContains = "RGB";
    public int fallbackCameraIndex = 0;
    public int requestedCameraWidth = 640;
    public int requestedCameraHeight = 480;
    public int requestedCameraFps = 20;
    public bool showPoseCameraPreview = false;
    [Range(0.1f, 1f)]
    public float poseCameraPreviewWidthRatio = 0.5f;

    private WebCamTexture webCamTexture;
    private TextureFramePool textureFramePool;
    private PoseLandmarker poseLandmarker;
    private ShoulderInput shoulderInput;
    private ShieldController shieldController;
    private readonly List<Vector2> brightPointTrajectory = new List<Vector2>();
    private readonly List<RectTransform> brightPointPreviewDots = new List<RectTransform>();
    private BrightPointTrackingSettings brightPointSettings;
    private LineRenderer brightPointLine;
    private RectTransform brightPointPreview;
    private RectTransform brightPointMarker;
    private Canvas poseCameraPreviewCanvas;
    private RawImage poseCameraPreviewImage;
    private RectTransform poseLandmarkLayer;

    private readonly List<RectTransform> poseLandmarkDots =
        new List<RectTransform>();

    private readonly List<RectTransform> poseLandmarkLines =
        new List<RectTransform>();

    private static readonly int[,] PoseConnections =
    {
    // 顔
    { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 7 },
    { 0, 4 }, { 4, 5 }, { 5, 6 }, { 6, 8 },
    { 9, 10 },

    // 上半身
    { 11, 12 },
    { 11, 13 }, { 13, 15 },
    { 12, 14 }, { 14, 16 },

    // 手
    { 15, 17 }, { 15, 19 }, { 15, 21 },
    { 16, 18 }, { 16, 20 }, { 16, 22 },

    // 胴体
    { 11, 23 },
    { 12, 24 },
    { 23, 24 },

    // 脚
    { 23, 25 }, { 25, 27 },
    { 24, 26 }, { 26, 28 },

    // 足
    { 27, 29 }, { 29, 31 }, { 27, 31 },
    { 28, 30 }, { 30, 32 }, { 28, 32 }
};
    private Camera gameCamera;
    private int brightPointFrameCounter;
    private int missingBrightPointFrames;
    private Vector2? lastBrightPoint;
    private float lastCircleClassificationRequest;
    private volatile bool circleClassificationInFlight;
    private string pendingCircleClassification;
    private string circleResultLabel;
    private bool circleResultIsCircle;
    private float circleResultProbability;
    private float circleResultVisibleUntil;
    private float nextCirclePollTime;
    private string lastProcessedCircleResult;
    private bool initializedGlog;

    private IEnumerator Start()
    {
        instance = this;

        brightPointSettings = Resources.Load<BrightPointTrackingSettings>("BrightPointTrackingSettings");
        if (brightPointSettings == null)
        {
            brightPointSettings = ScriptableObject.CreateInstance<BrightPointTrackingSettings>();
        }

        var carpet = FindFirstObjectByType<CarpetMove>();
        if (carpet == null)
        {
            yield break;
        }

        shoulderInput = carpet.shoulderInput;
        if (shoulderInput == null)
        {
            shoulderInput = carpet.GetComponent<ShoulderInput>();
            if (shoulderInput == null)
            {
                shoulderInput = carpet.gameObject.AddComponent<ShoulderInput>();
            }

            carpet.shoulderInput = shoulderInput;
        }

        shieldController = carpet.GetComponent<ShieldController>();
        if (shieldController == null)
        {
            shieldController = carpet.gameObject.AddComponent<ShieldController>();
        }

        carpet.shieldController = shieldController;

        // React directly to the current shoulder angle, with a small dead zone for natural resting motion.
        shoulderInput.deadZone = 0.01f;
        shoulderInput.sensitivity = 8f;
        shoulderInput.smooth = 1000f;
        shoulderInput.invert = false;

        Glog.Initialize(nameof(MagicCarpetPoseController));
        initializedGlog = true;
        Protobuf.SetLogHandler(Protobuf.DefaultLogHandler);
#if UNITY_EDITOR
        AssetLoader.Provide(new LocalResourceManager());
#else
        AssetLoader.Provide(new StreamingAssetsResourceManager());
#endif

        var config = new PoseLandmarkDetectionConfig
        {
            Model = ModelType.BlazePoseLite,
            RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO,
            ImageReadMode = ImageReadMode.CPUAsync,
        };

        yield return AssetLoader.PrepareAssetAsync(config.ModelPath, true);
        poseLandmarker = PoseLandmarker.CreateFromOptions(config.GetPoseLandmarkerOptions());

        var selectedCameraName = SelectCameraName();
        webCamTexture = string.IsNullOrEmpty(selectedCameraName)
            ? new WebCamTexture(null, requestedCameraWidth, requestedCameraHeight, requestedCameraFps)
            : new WebCamTexture(selectedCameraName, requestedCameraWidth, requestedCameraHeight, requestedCameraFps);
        webCamTexture.Play();

        yield return new WaitUntil(() => webCamTexture != null && webCamTexture.width > 16);
        CreatePoseCameraPreview();

        textureFramePool = new TextureFramePool(
            webCamTexture.width,
            webCamTexture.height,
            TextureFormat.RGBA32,
            4);

        var result = PoseLandmarkerResult.Alloc(1);
        var processingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

        while (enabled && webCamTexture != null && webCamTexture.isPlaying)
        {
            if (!webCamTexture.didUpdateThisFrame)
            {
                yield return null;
                continue;
            }

            if (!textureFramePool.TryGetTextureFrame(out var textureFrame))
            {
                yield return null;
                continue;
            }

            var request = textureFrame.ReadTextureAsync(webCamTexture, false, false);
            yield return new WaitUntil(() => request.done);

            if (request.hasError)
            {
                textureFrame.Release();
                yield return null;
                continue;
            }

            var image = textureFrame.BuildCPUImage();
            textureFrame.Release();

            if (poseLandmarker.TryDetectForVideo(
    image,
    Time.frameCount,
    processingOptions,
    ref result))
            {
                ForwardShoulders(result);
                UpdatePoseLandmarkPreview(result);
            }
            else
            {
                shoulderInput.LostBody();
                HidePoseLandmarkPreview();
            }

            DisposeMasks(result);
        }
    }


    private void CreatePoseCameraPreview()
    {
        if (!showPoseCameraPreview ||
            webCamTexture == null ||
            poseCameraPreviewCanvas != null)
        {
            return;
        }

        var canvasObject = new GameObject(
            "Pose Camera Preview",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        DontDestroyOnLoad(canvasObject);

        poseCameraPreviewCanvas = canvasObject.GetComponent<Canvas>();
        poseCameraPreviewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // ゲーム画面より確実に手前へ表示
        poseCameraPreviewCanvas.sortingOrder = 1000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // RealSense映像
        var previewObject = new GameObject(
            "Pose Camera Image",
            typeof(RectTransform),
            typeof(RawImage));

        previewObject.transform.SetParent(canvasObject.transform, false);

        var previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero;

        poseCameraPreviewImage = previewObject.GetComponent<RawImage>();
        poseCameraPreviewImage.texture = webCamTexture;
        poseCameraPreviewImage.raycastTarget = false;

        // 骨格描画用レイヤー
        var landmarkLayerObject = new GameObject(
            "Pose Landmark Layer",
            typeof(RectTransform));

        landmarkLayerObject.transform.SetParent(canvasObject.transform, false);

        poseLandmarkLayer =
            landmarkLayerObject.GetComponent<RectTransform>();

        poseLandmarkLayer.anchorMin = Vector2.zero;
        poseLandmarkLayer.anchorMax = Vector2.one;
        poseLandmarkLayer.offsetMin = Vector2.zero;
        poseLandmarkLayer.offsetMax = Vector2.zero;

        CreatePoseLandmarkObjects();
    }

    private void CreatePoseLandmarkObjects()
    {
        if (poseLandmarkLayer == null)
        {
            return;
        }

        // MediaPipe Poseの33点
        for (var i = 0; i < 33; i++)
        {
            var dotObject = new GameObject(
                $"Pose Point {i}",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image));

            dotObject.transform.SetParent(poseLandmarkLayer, false);

            var rect = dotObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(14f, 14f);

            var image =
                dotObject.GetComponent<UnityEngine.UI.Image>();

            image.color = UnityEngine.Color.green;
            image.raycastTarget = false;

            dotObject.SetActive(false);
            poseLandmarkDots.Add(rect);
        }

        // 骨格を結ぶ線
        var connectionCount = PoseConnections.GetLength(0);

        for (var i = 0; i < connectionCount; i++)
        {
            var lineObject = new GameObject(
                $"Pose Line {i}",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image));

            lineObject.transform.SetParent(poseLandmarkLayer, false);

            var rect = lineObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);

            var image =
                lineObject.GetComponent<UnityEngine.UI.Image>();

            image.color = UnityEngine.Color.green;
            image.raycastTarget = false;

            lineObject.SetActive(false);
            poseLandmarkLines.Add(rect);
        }

        // 点を線より手前に表示
        foreach (var dot in poseLandmarkDots)
        {
            dot.SetAsLastSibling();
        }
    }

    private void UpdatePoseLandmarkPreview(
    PoseLandmarkerResult result)
    {
        if (!showPoseCameraPreview ||
            poseLandmarkLayer == null ||
            result.poseLandmarks == null ||
            result.poseLandmarks.Count == 0)
        {
            HidePoseLandmarkPreview();
            return;
        }

        var landmarks = result.poseLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 33)
        {
            HidePoseLandmarkPreview();
            return;
        }

        var layerSize = poseLandmarkLayer.rect.size;

        // 点を更新
        for (var i = 0; i < poseLandmarkDots.Count; i++)
        {
            var landmark = landmarks[i];
            var dot = poseLandmarkDots[i];

            /*
             * MediaPipe:
             * 左上が(0,0)、右下が(1,1)
             *
             * Unity UI:
             * 左下が(0,0)
             */
            var position = new Vector2(
                landmark.x * layerSize.x,
                landmark.y * layerSize.y);

            dot.anchoredPosition = position;
            dot.gameObject.SetActive(true);
        }
        // 線を更新
        var connectionCount = PoseConnections.GetLength(0);

        for (var i = 0; i < connectionCount; i++)
        {
            var startIndex = PoseConnections[i, 0];
            var endIndex = PoseConnections[i, 1];

            var startPosition =
                poseLandmarkDots[startIndex].anchoredPosition;

            var endPosition =
                poseLandmarkDots[endIndex].anchoredPosition;

            UpdatePoseLine(
                poseLandmarkLines[i],
                startPosition,
                endPosition);
        }
    }

    private void UpdatePoseLine(
    RectTransform line,
    Vector2 start,
    Vector2 end)
    {
        var difference = end - start;
        var distance = difference.magnitude;
        var angle = Mathf.Atan2(
            difference.y,
            difference.x) * Mathf.Rad2Deg;

        line.anchoredPosition = start;
        line.sizeDelta = new Vector2(distance, 5f);
        line.localRotation = Quaternion.Euler(0f, 0f, angle);
        line.gameObject.SetActive(true);
    }

    private void HidePoseLandmarkPreview()
    {
        foreach (var dot in poseLandmarkDots)
        {
            if (dot != null)
            {
                dot.gameObject.SetActive(false);
            }
        }

        foreach (var line in poseLandmarkLines)
        {
            if (line != null)
            {
                line.gameObject.SetActive(false);
            }
        }
    }

    private void ForwardShoulders(PoseLandmarkerResult result)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            shoulderInput.LostBody();
            return;
        }

        var landmarks = result.poseLandmarks[0].landmarks;
        if (landmarks == null || landmarks.Count <= RightShoulderIndex)
        {
            shoulderInput.LostBody();
            return;
        }

        // MediaPipe image coordinates grow downward, while the movement input uses upward-positive Y.
        var left = landmarks[LeftShoulderIndex];
        var right = landmarks[RightShoulderIndex];
        shoulderInput.SetShoulders(
            new Vector2(left.x, 1f - left.y),
            new Vector2(right.x, 1f - right.y));
    }

    private string SelectCameraName()
    {
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("No Unity webcam devices were found.");
            return null;
        }

        for (var i = 0; i < devices.Length; i++)
        {
            Debug.Log($"Unity camera [{i}]: {devices[i].name}");
        }

        if (!string.IsNullOrWhiteSpace(preferredCameraNameContains))
        {
            var preferred = preferredCameraNameContains.ToLowerInvariant();
            var preferredAlso = string.IsNullOrWhiteSpace(preferredCameraNameAlsoContains)
                ? null
                : preferredCameraNameAlsoContains.ToLowerInvariant();

            if (preferredAlso != null)
            {
                for (var i = 0; i < devices.Length; i++)
                {
                    var cameraName = devices[i].name.ToLowerInvariant();
                    if (cameraName.Contains(preferred) && cameraName.Contains(preferredAlso))
                    {
                        Debug.Log($"Selected Unity camera by name: {devices[i].name}");
                        return devices[i].name;
                    }
                }
            }

            for (var i = 0; i < devices.Length; i++)
            {
                if (devices[i].name.ToLowerInvariant().Contains(preferred))
                {
                    Debug.Log($"Selected Unity camera by name: {devices[i].name}");
                    return devices[i].name;
                }
            }

            Debug.LogWarning($"No Unity camera matched name: {preferredCameraNameContains}");
        }

        var index = Mathf.Clamp(fallbackCameraIndex, 0, devices.Length - 1);
        Debug.Log($"Selected Unity camera by index: [{index}] {devices[index].name}");
        return devices[index].name;
    }

    private void Update()
    {
        FlushCircleClassification();

        if (MagicCarpetGameFlow.IsCircleChallengeActive && !MagicCarpetGameFlow.IsCircleChallengeAcceptingResults)
        {
            nextCirclePollTime = Time.unscaledTime + 0.05f;
            return;
        }

        if (MagicCarpetGameFlow.IsCircleChallengeAcceptingResults && Time.unscaledTime >= nextCirclePollTime)
        {
            nextCirclePollTime = Time.unscaledTime + 0.2f;
            RequestCircleClassification();
        }
    }

    private void OnGUI()
    {
        /*if (string.IsNullOrEmpty(circleResultLabel) || Time.unscaledTime > circleResultVisibleUntil)
        {
            return;
        }

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = circleResultIsCircle
            ? new UnityEngine.Color(0.1f, 1f, 0.45f, 1f)
            : new UnityEngine.Color(1f, 0.55f, 0.2f, 1f);

        var rect = new UnityEngine.Rect(24f, 88f, 360f, 72f);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(rect, $"{circleResultLabel}  {circleResultProbability * 100f:0}%", style);*/
    }

    private void CreateBrightPointPreview()
    {
        var canvasObject = new GameObject(
            "Brightness Detection Preview",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        DontDestroyOnLoad(canvasObject);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var previewObject = new GameObject("Camera", typeof(RectTransform), typeof(RawImage));
        previewObject.transform.SetParent(canvasObject.transform, false);
        brightPointPreview = previewObject.GetComponent<RectTransform>();
        brightPointPreview.anchorMin = Vector2.one;
        brightPointPreview.anchorMax = Vector2.one;
        brightPointPreview.pivot = Vector2.one;
        brightPointPreview.anchoredPosition = new Vector2(-24f, -24f);
        brightPointPreview.sizeDelta = new Vector2(320f, 240f);

        var preview = previewObject.GetComponent<RawImage>();
        preview.texture = webCamTexture;
        preview.raycastTarget = false;

        var markerObject = new GameObject("Bright Point", typeof(RectTransform), typeof(RawImage));
        markerObject.transform.SetParent(previewObject.transform, false);
        brightPointMarker = markerObject.GetComponent<RectTransform>();
        brightPointMarker.pivot = new Vector2(0.5f, 0.5f);
        brightPointMarker.sizeDelta = new Vector2(18f, 18f);

        var marker = markerObject.GetComponent<RawImage>();
        marker.color = new UnityEngine.Color(0.1f, 1f, 1f, 0.9f);
        marker.raycastTarget = false;
        markerObject.SetActive(false);
    }

    private void UpdateBrightPointMarker(Vector2 point)
    {
        if (brightPointMarker == null)
        {
            return;
        }

        brightPointMarker.gameObject.SetActive(true);
        brightPointMarker.anchorMin = point;
        brightPointMarker.anchorMax = point;
        brightPointMarker.anchoredPosition = Vector2.zero;
    }

    private void HidePreviewTrajectory()
    {
        foreach (var dot in brightPointPreviewDots)
        {
            dot.gameObject.SetActive(false);
        }
    }

    private void ClearBrightPointTrajectory()
    {
        brightPointTrajectory.Clear();
        if (brightPointLine != null)
        {
            brightPointLine.positionCount = 0;
        }

        foreach (var dot in brightPointPreviewDots)
        {
            dot.gameObject.SetActive(false);
        }
    }

    private bool TryFindBrightPoint(out Vector2 point)
    {
        point = default;
        var pixels = webCamTexture.GetPixels32();
        var width = webCamTexture.width;
        var height = webCamTexture.height;

        if (pixels == null || width <= 0 || height <= 0)
        {
            return false;
        }

        var sampleStep = brightPointSettings.sampleStep;
        var gridWidth = (width + sampleStep - 1) / sampleStep;
        var gridHeight = (height + sampleStep - 1) / sampleStep;
        var values = new byte[gridWidth * gridHeight];
        var maxValue = 0;

        for (var gridY = 0; gridY < gridHeight; gridY++)
        {
            var y = gridY * sampleStep;
            for (var gridX = 0; gridX < gridWidth; gridX++)
            {
                var x = gridX * sampleStep;
                var color = pixels[y * width + x];
                var value = (byte)Mathf.Max(color.r, color.g, color.b);
                values[gridY * gridWidth + gridX] = value;
                maxValue = Mathf.Max(maxValue, value);
            }
        }

        var threshold = brightPointSettings.useAutomaticThreshold
            ? Mathf.Max(brightPointSettings.minimumAutomaticValue, maxValue - brightPointSettings.automaticValueMargin)
            : brightPointSettings.valueThreshold;
        var visited = new bool[values.Length];
        var queue = new int[values.Length];
        var bestPoint = default(Vector2);
        var bestScore = float.NegativeInfinity;

        for (var startIndex = 0; startIndex < values.Length; startIndex++)
        {
            if (visited[startIndex] || values[startIndex] < threshold)
            {
                continue;
            }

            var head = 0;
            var tail = 0;
            queue[tail++] = startIndex;
            visited[startIndex] = true;
            var count = 0;
            var totalX = 0f;
            var totalY = 0f;
            var peakValue = 0;
            var peakX = 0f;
            var peakY = 0f;

            while (head < tail)
            {
                var index = queue[head++];
                var gridX = index % gridWidth;
                var gridY = index / gridWidth;
                var value = values[index];
                count++;
                totalX += gridX * sampleStep;
                totalY += gridY * sampleStep;
                if (value > peakValue)
                {
                    peakValue = value;
                    peakX = gridX * sampleStep;
                    peakY = gridY * sampleStep;
                }

                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        var neighborX = gridX + offsetX;
                        var neighborY = gridY + offsetY;
                        if (neighborX < 0 || neighborX >= gridWidth || neighborY < 0 || neighborY >= gridHeight)
                        {
                            continue;
                        }

                        var neighborIndex = neighborY * gridWidth + neighborX;
                        if (visited[neighborIndex] || values[neighborIndex] < threshold)
                        {
                            continue;
                        }

                        visited[neighborIndex] = true;
                        queue[tail++] = neighborIndex;
                    }
                }
            }

            if (count < brightPointSettings.minimumRegionPixels || count > values.Length * brightPointSettings.maximumRegionFraction)
            {
                continue;
            }

            var candidate = new Vector2(peakX / width, peakY / height);
            var distance = lastBrightPoint.HasValue ? Vector2.Distance(lastBrightPoint.Value, candidate) : 0f;
            if (lastBrightPoint.HasValue && distance > brightPointSettings.maximumTrackingJump)
            {
                continue;
            }

            var score = peakValue * 10000f + count - distance;
            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
            }
        }

        if (float.IsNegativeInfinity(bestScore))
        {
            return false;
        }

        point = bestPoint;
        return true;
    }

    private void HideBrightPointLine()
    {
        if (brightPointLine != null)
        {
            brightPointLine.positionCount = 0;
        }
    }

    private void RequestCircleClassification()
    {
        if (circleClassificationInFlight)
        {
            return;
        }

        var request = "{}";
        lastCircleClassificationRequest = Time.unscaledTime;
        circleClassificationInFlight = true;

        _ = Task.Run(() => SendCircleClassificationRequest(request));
    }

    public static void StartCircleDetection()
    {
        if (instance == null)
        {
            return;
        }

        instance.lastProcessedCircleResult = null;
        instance.pendingCircleClassification = null;
        instance.nextCirclePollTime = 0f;

        // start通信が完了するまでstatus通信を止める
        instance.circleClassificationInFlight = true;

        _ = Task.Run(() =>
            instance.SendCircleDetectionStartRequest()
        );
    }

    private void SendCircleDetectionStartRequest()
    {
        try
        {
            using var client = new TcpClient();

            var connectTask =
                client.ConnectAsync(
                    "127.0.0.1",
                    CircleClassifierPort
                );

            if (!connectTask.Wait(500))
            {
                return;
            }

            using var stream = client.GetStream();
            stream.ReadTimeout = 500;

            var request =
                Encoding.UTF8.GetBytes(
                    "{\"command\":\"start\"}\n"
                );

            stream.Write(
                request,
                0,
                request.Length
            );

            // Pythonがstartを処理した後の応答を受け取る。
            // この応答自体は判定結果として使用しない。
            var response = new byte[1024];
            stream.Read(
                response,
                0,
                response.Length
            );
        }
        catch
        {
            // Python未起動時は無視
        }
        finally
        {
            circleClassificationInFlight = false;
            nextCirclePollTime = 0f;
        }
    }
        public static void CancelCircleDetection()
    {
        if (instance == null)
        {
            return;
        }

        instance.pendingCircleClassification = null;
        instance.lastProcessedCircleResult = null;
        instance.circleResultLabel = null;
        instance.nextCirclePollTime = 0f;
    }

    private void SendCircleClassifierCommand(string command)
    {
        _ = Task.Run(() => SendCircleClassifierCommandRequest($"{{\"command\":\"{command}\"}}"));
    }

    private void SendCircleClassifierCommandRequest(string request)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", CircleClassifierPort);
            if (!connectTask.Wait(150))
            {
                return;
            }

            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(request + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // The Python bridge is optional while it is not running.
        }
    }

    private void SendCircleClassificationRequest(string request)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", CircleClassifierPort);
            if (!connectTask.Wait(150))
            {
                return;
            }

            using var stream = client.GetStream();
            stream.ReadTimeout = 500;
            var bytes = Encoding.UTF8.GetBytes(request + "\n");
            stream.Write(bytes, 0, bytes.Length);

            var response = new byte[1024];
            var length = stream.Read(response, 0, response.Length);
            pendingCircleClassification = Encoding.UTF8.GetString(response, 0, length).Trim();
        }
        catch
        {
            // The Python bridge is optional while it is not running.
        }
        finally
        {
            circleClassificationInFlight = false;
        }
    }

    private void FlushCircleClassification()
    {

        if (!MagicCarpetGameFlow.IsCircleChallengeActive)
        {
            pendingCircleClassification = null;
            return;
        }
        if (string.IsNullOrEmpty(pendingCircleClassification))
        {
            return;
        }

        if (MagicCarpetGameFlow.IsCircleChallengeActive && !MagicCarpetGameFlow.IsCircleChallengeAcceptingResults)
        {
            pendingCircleClassification = null;
            lastProcessedCircleResult = null;
            circleResultLabel = null;
            return;
        }

        Debug.Log($"Circle ML result: {pendingCircleClassification}");
        var normalized = pendingCircleClassification.ToLowerInvariant();

        bool isComplete =
            normalized.Contains("\"status\":\"complete\"")
            || normalized.Contains("\"status\": \"complete\"");

        if (isComplete)
        {
            if (pendingCircleClassification != lastProcessedCircleResult)
            {
                lastProcessedCircleResult = pendingCircleClassification;

                if (MagicCarpetGameFlow.IsCircleChallengeActive)
                {
                    MagicCarpetGameFlow.ReportCircleChallengeComplete();
                }
            }

            pendingCircleClassification = null;
            return;
        }

        bool isResult =
            normalized.Contains("\"status\":\"result\"")
            || normalized.Contains("\"status\": \"result\"");

        if (!isResult)
        {
            pendingCircleClassification = null;
            return;
        }

        if (pendingCircleClassification == lastProcessedCircleResult)
        {
            pendingCircleClassification = null;
            return;
        }

        lastProcessedCircleResult = pendingCircleClassification;

        circleResultIsCircle = normalized.Contains("\"label\":\"circle\"") || normalized.Contains("\"label\": \"circle\"");
        circleResultProbability = ExtractCircleProbability(normalized);
        circleResultLabel = circleResultIsCircle ? "CIRCLE" : "OTHER";
        circleResultVisibleUntil = Time.unscaledTime + 2f;
        var acceptedCircle = circleResultIsCircle;
        if (MagicCarpetGameFlow.IsCircleChallengeActive)
        {
            if (!acceptedCircle)
            {
                nextCirclePollTime = Time.unscaledTime + 0.2f;
            }

            MagicCarpetGameFlow.ReportCircleChallengeResult(acceptedCircle);

            if (acceptedCircle && shieldController != null)
            {
                shieldController.ActivateShield();
            }

            pendingCircleClassification = null;
            return;
        }

        pendingCircleClassification = null;
    }

    private static float ExtractCircleProbability(string response)
    {
        const string key = "circleprobability";
        var keyIndex = response.IndexOf(key);
        if (keyIndex < 0)
        {
            return 0f;
        }

        var colonIndex = response.IndexOf(':', keyIndex);
        if (colonIndex < 0)
        {
            return 0f;
        }

        var startIndex = colonIndex + 1;
        while (startIndex < response.Length && char.IsWhiteSpace(response[startIndex]))
        {
            startIndex++;
        }

        var endIndex = startIndex;
        while (endIndex < response.Length && "0123456789.eE+-".IndexOf(response[endIndex]) >= 0)
        {
            endIndex++;
        }

        var raw = response.Substring(startIndex, endIndex - startIndex);
        return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? Mathf.Clamp01(value)
            : 0f;
    }

    [System.Serializable]
    private class CirclePoint
    {
        public float x;
        public float y;
    }

    [System.Serializable]
    private class CircleRequest
    {
        public CirclePoint[] points;
    }

    private static void DisposeMasks(PoseLandmarkerResult result)
    {
        if (result.segmentationMasks == null)
        {
            return;
        }

        foreach (var mask in result.segmentationMasks)
        {
            mask.Dispose();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        textureFramePool?.Dispose();
        poseLandmarker?.Close();

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }

        if (poseCameraPreviewCanvas != null)
        {
            Destroy(poseCameraPreviewCanvas.gameObject);
        }

        if (initializedGlog)
        {
            Glog.Shutdown();
            Protobuf.ResetLogHandler();
        }
    }
}
