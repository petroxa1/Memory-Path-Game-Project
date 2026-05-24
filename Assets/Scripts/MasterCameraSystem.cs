using UnityEngine;
using UnityEngine.UI;

public class MasterCameraSystem : MonoBehaviour
{
    [Header("Setup")]
    public RawImage displayImage;
    public int gridCols = 7;
    public int gridRows = 5;

    [Header("Tracking Settings")]
    public bool useHSV = true;
    public Color32 targetColor32 = new Color32(255, 0, 0, 255); // RGB Fallback & Editor preview
    
    [Range(0f, 1f)] public float targetHue = 0f;
    [Range(0f, 1f)] public float targetSat = 0.8f;
    [Range(0f, 1f)] public float targetVal = 0.8f;

    [Range(0f, 0.5f)] public float hueTolerance = 0.1f;
    [Range(0f, 1f)] public float minSat = 0.2f;
    [Range(0f, 1f)] public float minVal = 0.2f;

    [Range(0, 150)] public int rgbThreshold = 80;

    [Header("Performans Ayarları")]
    [Range(1, 8)] public int pixelStep = 4; // Her kaçıncı pikseli tarıyoruz
    [Range(1, 10)] public int debounceFrames = 3;

    [Header("References")]
    public GridManager gridManager;

    [Header("Calibration Points")]
    public Vector2 topLeft;
    public Vector2 topRight;
    public Vector2 bottomLeft;
    public Vector2 bottomRight;

    private WebCamTexture webcamTexture;
    private int setupStep = 5; // Varsayılan olarak kalibrasyon tamamlanmış sayılır (profil yüklenecek)
    private HomographyHelper homographyHelper = new HomographyHelper();
    private bool isTrackingEnabled = true;

    // Debounce sistemi
    private Vector2Int lastDetectedTile = new Vector2Int(-1, -1);
    private int consecutiveFrames = 0;
    private Vector2Int lastConfirmedTile = new Vector2Int(-1, -1);

    public bool IsCalibrated => homographyHelper.IsValid;
    public int SetupStep { get => setupStep; set => setupStep = value; }
    public WebCamTexture WebcamTexture => webcamTexture;

    void Start()
    {
        // PlayerPrefs veya profil sisteminden kalibrasyon noktalarını ve renk ayarlarını yükle
        LoadCalibrationSettings();

        // Kamerayı başlat
        InitializeWebcam();
    }

    public void InitializeWebcam(string deviceName = "")
    {
        if (webcamTexture != null)
        {
            if (webcamTexture.isPlaying) webcamTexture.Stop();
            Destroy(webcamTexture);
            webcamTexture = null;
        }

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("MasterCameraSystem: No webcam found or connected.");
            return;
        }

        string selectedDeviceName = devices[0].name;

        if (!string.IsNullOrEmpty(deviceName))
        {
            selectedDeviceName = deviceName;
        }
        else
        {
            // Öncelikli olarak iVCam veya e2esoft kamerasını seçmeye çalış (PC testleri için)
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].name.ToLower().Contains("ivcam") || devices[i].name.ToLower().Contains("e2esoft"))
                {
                    selectedDeviceName = devices[i].name;
                    break;
                }
            }
        }

        webcamTexture = new WebCamTexture(selectedDeviceName, 640, 480, 30);

        if (displayImage != null)
        {
            displayImage.color = Color.white;
            displayImage.texture = webcamTexture;
        }
        
        webcamTexture.Play();
        Debug.Log("MasterCameraSystem: Started webcam: " + selectedDeviceName);
    }

    /// <summary>
    /// Kamera görüntüsünde en parlak piksellerin ağırlık merkezini bulur.
    /// Otomatik kalibrasyon sırasında beyaz flash'ları tespit etmek için kullanılır.
    /// </summary>
    public Vector2 ScanForBrightSpot(float brightnessThreshold = 0.85f)
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
            return Vector2.zero;

        Color32[] pixels = webcamTexture.GetPixels32();
        int w = webcamTexture.width;
        int h = webcamTexture.height;

        float sumX = 0, sumY = 0;
        int count = 0;

        // Parlaklık = max(R, G, B) / 255 olarak basitleştirildi (performans için)
        byte threshold = (byte)(brightnessThreshold * 255f);

        for (int y = 0; y < h; y += pixelStep)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x += pixelStep)
            {
                Color32 c = pixels[rowOffset + x];
                // En yüksek kanalı parlaklık olarak kullan
                byte maxChannel = c.r;
                if (c.g > maxChannel) maxChannel = c.g;
                if (c.b > maxChannel) maxChannel = c.b;

                if (maxChannel >= threshold)
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }
            }
        }

        return count > 5 ? new Vector2(sumX / count, sumY / count) : Vector2.zero;
    }

    /// <summary>
    /// Otomatik kalibratör tarafından 4 köşe ayarlanarak homografi hesaplanır.
    /// </summary>
    public void SetCornersAndCalibrate(Vector2 bl, Vector2 br, Vector2 tl, Vector2 tr)
    {
        bottomLeft = bl;
        bottomRight = br;
        topLeft = tl;
        topRight = tr;
        RecalculateHomography();
        SaveCalibrationSettings();
        Debug.Log("AutoCalibration: Homography computed and saved.");
    }

    public void StartCalibration()
    {
        setupStep = 1;
        Debug.Log("Calibration Started: Click the 4 corners of your projected area in order (TL, TR, BL, BR)");
    }

    public void SetTrackingEnabled(bool enabled)
    {
        isTrackingEnabled = enabled;
        if (!enabled)
        {
            ResetLastConfirmedTile();
        }
    }

    void Update()
    {
        // 1. Manuel kalibrasyon tetikleme (kısayol tuşu)
        if (Input.GetKeyDown(KeyCode.C))
        {
            StartCalibration();
        }

        // 2. Arayüz dışı kalibrasyon tıklamaları (ekran tıklandığında)
        if (setupStep > 0 && setupStep <= 4 && Input.GetMouseButtonDown(0))
        {
            RecordCorner();
        }

        // 3. Aktif Takip
        if (isTrackingEnabled && homographyHelper.IsValid && gridManager != null)
        {
            if (webcamTexture == null || !webcamTexture.didUpdateThisFrame) return;

            Vector2 dotPos = ScanForRedDot();
            if (dotPos != Vector2.zero)
            {
                Vector2Int gridCoord = GetGridCoordinate(dotPos);

                // DEBOUNCE: Aynı tile art arda N frame görülmeli
                if (gridCoord == lastDetectedTile)
                {
                    consecutiveFrames++;
                }
                else
                {
                    lastDetectedTile = gridCoord;
                    consecutiveFrames = 1;
                }

                // Yeterli frame biriktiğinde ve daha önce bu tile tetiklenmemişse
                if (consecutiveFrames >= debounceFrames && gridCoord != lastConfirmedTile)
                {
                    lastConfirmedTile = gridCoord;
                    gridManager.OnTileStepped(gridCoord.x, gridCoord.y);
                    Debug.Log($"Tile Stepped: {gridCoord}");
                }
            }
            else
            {
                consecutiveFrames = 0;
                lastDetectedTile = new Vector2Int(-1, -1);
            }
        }
    }

    public void SetTargetColor(Color32 color)
    {
        targetColor32 = color;
        Color.RGBToHSV((Color)color, out targetHue, out targetSat, out targetVal);
        SaveCalibrationSettings();
    }

    // RGB to HSV Fast conversion
    private void ColorToHSV(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;

        float max = Mathf.Max(rf, Mathf.Max(gf, bf));
        float min = Mathf.Min(rf, Mathf.Min(gf, bf));
        float delta = max - min;

        v = max;
        s = (max > 0.001f) ? (delta / max) : 0f;

        if (delta < 0.001f)
        {
            h = 0f;
        }
        else
        {
            if (rf >= max)
                h = (gf - bf) / delta + (gf < bf ? 6f : 0f);
            else if (gf >= max)
                h = (bf - rf) / delta + 2f;
            else
                h = (rf - gf) / delta + 4f;

            h /= 6f;
        }
    }

    public Vector2 ScanForRedDot()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
            return Vector2.zero;

        Color32[] pixels = webcamTexture.GetPixels32();
        int w = webcamTexture.width;
        int h = webcamTexture.height;

        float sumX = 0, sumY = 0;
        int count = 0;

        if (useHSV)
        {
            for (int y = 0; y < h; y += pixelStep)
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x += pixelStep)
                {
                    Color32 c = pixels[rowOffset + x];
                    ColorToHSV(c.r, c.g, c.b, out float hue, out float sat, out float val);

                    // Hue dairesel fark hesabı
                    float hueDiff = Mathf.Abs(hue - targetHue);
                    if (hueDiff > 0.5f) hueDiff = 1f - hueDiff;

                    if (hueDiff <= hueTolerance && sat >= minSat && val >= minVal)
                    {
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
            }
        }
        else
        {
            // Eski RGB Mesafe Algoritması (Fallback)
            int rTarget = targetColor32.r;
            int gTarget = targetColor32.g;
            int bTarget = targetColor32.b;

            for (int y = 0; y < h; y += pixelStep)
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x += pixelStep)
                {
                    Color32 c = pixels[rowOffset + x];
                    int diff = Mathf.Abs(c.r - rTarget) + Mathf.Abs(c.g - gTarget) + Mathf.Abs(c.b - bTarget);

                    if (diff < rgbThreshold)
                    {
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
            }
        }

        return count > 0 ? new Vector2(sumX / count, sumY / count) : Vector2.zero;
    }

    public void ResetLastConfirmedTile()
    {
        lastConfirmedTile = new Vector2Int(-1, -1);
        consecutiveFrames = 0;
    }

    void RecordCorner()
    {
        if (displayImage == null || webcamTexture == null)
        {
            Debug.LogError("Calibration Failed: RawImage or WebcamTexture is missing!");
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(displayImage.rectTransform,
            Input.mousePosition, null, out Vector2 localPoint))
        {
            Vector2 pixelPos = new Vector2(
                (localPoint.x / displayImage.rectTransform.rect.width + 0.5f) * webcamTexture.width,
                (localPoint.y / displayImage.rectTransform.rect.height + 0.5f) * webcamTexture.height
            );

            if (setupStep == 1) topLeft = pixelPos;
            else if (setupStep == 2) topRight = pixelPos;
            else if (setupStep == 3) bottomLeft = pixelPos;
            else if (setupStep == 4)
            {
                bottomRight = pixelPos;
                setupStep = 5;
                RecalculateHomography();
                SaveCalibrationSettings();
                Debug.Log("Calibration finished and Homography computed successfully!");
                return;
            }

            Debug.Log($"Corner {setupStep} Set at: {pixelPos}");
            setupStep++;
        }
    }

    public void RecalculateHomography()
    {
        Vector2[] src = new Vector2[4] { topLeft, topRight, bottomLeft, bottomRight };
        // Kamera alanındaki 4 noktayı Izgara Alanına eşleştiriyoruz:
        // Top-Left -> (0, 1), Top-Right -> (1, 1), Bottom-Left -> (0, 0), Bottom-Right -> (1, 0)
        // Böylece (0,0) sol alt karo, (1,1) sağ üst karo olur ve Y ekseni tersine çevrilmeye gerek duymaz.
        Vector2[] dst = new Vector2[4] {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };

        homographyHelper.ComputeMatrix(src, dst);
    }

    public Vector2Int GetGridCoordinate(Vector2 pixelPos)
    {
        if (homographyHelper.IsValid)
        {
            Vector2 norm = homographyHelper.TransformPoint(pixelPos);

            int gridX = Mathf.FloorToInt(norm.x * gridCols);
            int gridY = Mathf.FloorToInt(norm.y * gridRows);

            gridX = Mathf.Clamp(gridX, 0, gridCols - 1);
            gridY = Mathf.Clamp(gridY, 0, gridRows - 1);

            return new Vector2Int(gridX, gridY);
        }
        else
        {
            // Eski model lineer fallback
            float normalizedX = Mathf.InverseLerp(topLeft.x, topRight.x, pixelPos.x);
            float normalizedY = Mathf.InverseLerp(bottomLeft.y, topLeft.y, pixelPos.y);

            int gridX = Mathf.FloorToInt(normalizedX * gridCols);
            int gridY = Mathf.FloorToInt(normalizedY * gridRows);

            gridY = (gridRows - 1) - gridY;

            gridX = Mathf.Clamp(gridX, 0, gridCols - 1);
            gridY = Mathf.Clamp(gridY, 0, gridRows - 1);

            return new Vector2Int(gridX, gridY);
        }
    }

    /// <summary>
    /// Kameranın algıladığı hedef rengin kalibre edilmiş alan içerisindeki 
    /// normalize koordinatını (0 ile 1 arası) döner. Algılanamazsa (-1, -1) döner.
    /// </summary>
    public Vector2 GetNormalizedTrackedPosition()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
            return -Vector2.one;

        Vector2 dotPos = ScanForRedDot();
        if (dotPos == Vector2.zero)
            return -Vector2.one;

        if (homographyHelper.IsValid)
        {
            return homographyHelper.TransformPoint(dotPos);
        }
        else
        {
            float normalizedX = Mathf.InverseLerp(topLeft.x, topRight.x, dotPos.x);
            float normalizedY = Mathf.InverseLerp(bottomLeft.y, topLeft.y, dotPos.y);
            return new Vector2(normalizedX, normalizedY);
        }
    }

    public void SaveCalibrationSettings()
    {
        PlayerPrefs.SetFloat("Calib_TL_X", topLeft.x);
        PlayerPrefs.SetFloat("Calib_TL_Y", topLeft.y);
        PlayerPrefs.SetFloat("Calib_TR_X", topRight.x);
        PlayerPrefs.SetFloat("Calib_TR_Y", topRight.y);
        PlayerPrefs.SetFloat("Calib_BL_X", bottomLeft.x);
        PlayerPrefs.SetFloat("Calib_BL_Y", bottomLeft.y);
        PlayerPrefs.SetFloat("Calib_BR_X", bottomRight.x);
        PlayerPrefs.SetFloat("Calib_BR_Y", bottomRight.y);

        PlayerPrefs.SetFloat("Track_Hue", targetHue);
        PlayerPrefs.SetFloat("Track_Sat", targetSat);
        PlayerPrefs.SetFloat("Track_Val", targetVal);
        PlayerPrefs.SetFloat("Track_HueTol", hueTolerance);
        PlayerPrefs.SetFloat("Track_MinSat", minSat);
        PlayerPrefs.SetFloat("Track_MinVal", minVal);
        PlayerPrefs.SetInt("Track_UseHSV", useHSV ? 1 : 0);

        PlayerPrefs.Save();
        Debug.Log("Calibration and Tracking settings saved successfully.");
    }

    public void LoadCalibrationSettings()
    {
        if (PlayerPrefs.HasKey("Calib_TL_X"))
        {
            topLeft = new Vector2(PlayerPrefs.GetFloat("Calib_TL_X"), PlayerPrefs.GetFloat("Calib_TL_Y"));
            topRight = new Vector2(PlayerPrefs.GetFloat("Calib_TR_X"), PlayerPrefs.GetFloat("Calib_TR_Y"));
            bottomLeft = new Vector2(PlayerPrefs.GetFloat("Calib_BL_X"), PlayerPrefs.GetFloat("Calib_BL_Y"));
            bottomRight = new Vector2(PlayerPrefs.GetFloat("Calib_BR_X"), PlayerPrefs.GetFloat("Calib_BR_Y"));

            targetHue = PlayerPrefs.GetFloat("Track_Hue", 0f);
            targetSat = PlayerPrefs.GetFloat("Track_Sat", 0.8f);
            targetVal = PlayerPrefs.GetFloat("Track_Val", 0.8f);
            hueTolerance = PlayerPrefs.GetFloat("Track_HueTol", 0.1f);
            minSat = PlayerPrefs.GetFloat("Track_MinSat", 0.2f);
            minVal = PlayerPrefs.GetFloat("Track_MinVal", 0.2f);
            useHSV = PlayerPrefs.GetInt("Track_UseHSV", 1) == 1;

            RecalculateHomography();
            Debug.Log("Calibration and Tracking settings loaded and applied.");
        }
        else
        {
            Debug.LogWarning("No calibration settings found. Please run calibration (press 'C' or open Calibration UI).");
        }
    }

    void OnDestroy()
    {
        if (webcamTexture != null)
        {
            if (webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
            }
            Destroy(webcamTexture);
            webcamTexture = null;
        }
        Debug.Log("MasterCameraSystem: Webcam texture released.");
    }
}