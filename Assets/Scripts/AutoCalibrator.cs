using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Grid köşe tile'larını sırayla parlak beyaz yaparak kameranın otomatik olarak
/// homografi kalibrasyonu yapmasını sağlar. Doğrulama testi ve başlangıç bandı
/// algılama özelliklerini içerir.
/// </summary>
public class AutoCalibrator : MonoBehaviour
{
    [Header("References")]
    public MasterCameraSystem cameraSystem;
    public GridManager gridManager;

    [Header("Flash Ayarları")]
    [Tooltip("Her köşe tile'ının parlak kaldığı süre (saniye)")]
    public float flashDuration = 1.5f;

    [Tooltip("Köşeler arası bekleme süresi (saniye)")]
    public float pauseBetweenFlashes = 0.5f;

    [Tooltip("Flash sırasında köşe tile renginin parlaklığı")]
    public Color flashColor = Color.white;

    [Tooltip("Parlaklık algılama eşiği (0-1)")]
    [Range(0.5f, 1f)]
    public float brightnessThreshold = 0.8f;

    [Tooltip("Doğrulama için kabul edilen grid hücre sapması")]
    public int verificationTolerance = 1;

    [Header("Kalibrasyon Durumu")]
    [SerializeField] private int lastCalibratedChapter = -1;

    // Kalibrasyon durumu
    public bool IsCalibrating { get; private set; } = false;
    public bool IsCalibrated => cameraSystem != null && cameraSystem.IsCalibrated;

    /// <summary>
    /// Bu chapter için kalibrasyon gerekli mi kontrol eder.
    /// </summary>
    public bool NeedsCalibration(int chapter)
    {
        // Grid boyutu değiştiyse veya daha önce kalibre edilmediyse kalibrasyon gerekli
        if (lastCalibratedChapter != chapter) return true;
        if (!cameraSystem.IsCalibrated) return true;
        return false;
    }

    /// <summary>
    /// Otomatik kalibrasyon sekansını başlatır.
    /// onComplete callback'i başarı/başarısızlık durumunu bildirir.
    /// </summary>
    public void StartAutoCalibration(int chapter, Action<bool> onComplete)
    {
        if (IsCalibrating)
        {
            Debug.LogWarning("AutoCalibrator: Kalibrasyon zaten devam ediyor.");
            return;
        }

        StartCoroutine(AutoCalibrationSequence(chapter, onComplete));
    }

    private IEnumerator AutoCalibrationSequence(int chapter, Action<bool> onComplete)
    {
        IsCalibrating = true;

        // Kamera hazır olana kadar bekle
        yield return StartCoroutine(WaitForCamera());

        // Tüm tile'ları karart
        gridManager.DarkenAllTiles();
        // Bantları da karart
        gridManager.HighlightStartBand(false);

        yield return new WaitForSeconds(0.5f);

        // 4 köşe tile'ını al
        Tile[] corners = gridManager.GetCornerTiles();
        if (corners == null || corners.Length != 4)
        {
            Debug.LogError("AutoCalibrator: Köşe tile'ları alınamadı!");
            IsCalibrating = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // Köşe sırası: [0]=Sol-Alt, [1]=Sağ-Alt, [2]=Sol-Üst, [3]=Sağ-Üst
        string[] cornerNames = { "Sol-Alt", "Sağ-Alt", "Sol-Üst", "Sağ-Üst" };
        Vector2[] detectedPositions = new Vector2[4];

        for (int i = 0; i < 4; i++)
        {
            // Tüm tile'ları tekrar karart (önceki flash'tan kalan olmasın)
            gridManager.DarkenAllTiles();
            yield return new WaitForSeconds(pauseBetweenFlashes);

            // Bu köşeyi parlat
            corners[i].SetColor(flashColor);
            Debug.Log($"AutoCalibrator: {cornerNames[i]} köşesi yanıyor...");

            // Kameranın algılaması için bir miktar bekle, sonra taramaya başla
            yield return new WaitForSeconds(0.3f);

            // Flash süresi boyunca kamerayı tara
            Vector2 detectedPos = Vector2.zero;
            float elapsed = 0f;
            int successfulDetections = 0;
            Vector2 positionSum = Vector2.zero;

            while (elapsed < flashDuration)
            {
                if (cameraSystem.WebcamTexture != null && cameraSystem.WebcamTexture.didUpdateThisFrame)
                {
                    Vector2 spot = cameraSystem.ScanForBrightSpot(brightnessThreshold);
                    if (spot != Vector2.zero)
                    {
                        positionSum += spot;
                        successfulDetections++;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Köşeyi söndür
            corners[i].SetColor(new Color(0.1f, 0.1f, 0.1f));

            // Ortalama pozisyonu hesapla
            if (successfulDetections > 3)
            {
                detectedPos = positionSum / successfulDetections;
                detectedPositions[i] = detectedPos;
                Debug.Log($"AutoCalibrator: {cornerNames[i]} algılandı -> Piksel: ({detectedPos.x:F0}, {detectedPos.y:F0}) [{successfulDetections} frame]");
            }
            else
            {
                Debug.LogError($"AutoCalibrator: {cornerNames[i]} köşesi algılanamadı! ({successfulDetections} frame). Kalibrasyon tekrar deneniyor.");
                gridManager.RestoreAllTiles();
                IsCalibrating = false;
                onComplete?.Invoke(false);
                yield break;
            }
        }

        // Tüm köşeler algılandı - Homografiyi hesapla
        // Sıra: bottomLeft, bottomRight, topLeft, topRight
        cameraSystem.SetCornersAndCalibrate(
            detectedPositions[0],  // Sol-Alt (bottomLeft)
            detectedPositions[1],  // Sağ-Alt (bottomRight)
            detectedPositions[2],  // Sol-Üst (topLeft)
            detectedPositions[3]   // Sağ-Üst (topRight)
        );

        // Grid boyutlarını da güncelle
        cameraSystem.gridCols = gridManager.Width;
        cameraSystem.gridRows = gridManager.Height;

        // ===== DOĞRULAMA TESTİ =====
        yield return new WaitForSeconds(0.3f);

        bool verificationPassed = false;
        Tile centerTile = gridManager.GetCenterTile();

        if (centerTile != null)
        {
            // Ortadaki tile'ı parlat
            gridManager.DarkenAllTiles();
            centerTile.SetColor(flashColor);
            yield return new WaitForSeconds(0.5f);

            // Orta tile'ın konumunu kamerayla algıla
            float verifyElapsed = 0f;
            while (verifyElapsed < 1f)
            {
                if (cameraSystem.WebcamTexture != null && cameraSystem.WebcamTexture.didUpdateThisFrame)
                {
                    Vector2 spot = cameraSystem.ScanForBrightSpot(brightnessThreshold);
                    if (spot != Vector2.zero)
                    {
                        Vector2Int detectedGrid = cameraSystem.GetGridCoordinate(spot);
                        int expectedX = gridManager.Width / 2;
                        int expectedY = gridManager.Height / 2;

                        if (Mathf.Abs(detectedGrid.x - expectedX) <= verificationTolerance &&
                            Mathf.Abs(detectedGrid.y - expectedY) <= verificationTolerance)
                        {
                            verificationPassed = true;
                            Debug.Log($"AutoCalibrator: Doğrulama BAŞARILI! Beklenen: ({expectedX},{expectedY}) Algılanan: ({detectedGrid.x},{detectedGrid.y})");
                            break;
                        }
                    }
                }
                verifyElapsed += Time.deltaTime;
                yield return null;
            }

            centerTile.SetColor(new Color(0.1f, 0.1f, 0.1f));
        }

        if (!verificationPassed)
        {
            Debug.LogWarning("AutoCalibrator: Doğrulama BAŞARISIZ. Kalibrasyon tekrar denenecek.");
            gridManager.RestoreAllTiles();
            IsCalibrating = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // Kalibrasyon başarılı!
        lastCalibratedChapter = chapter;
        gridManager.RestoreAllTiles();
        IsCalibrating = false;

        Debug.Log("AutoCalibrator: Kalibrasyon tamamlandı ve doğrulandı!");
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// Oyuncunun başlangıç bandı bölgesinde olup olmadığını kontrol eder.
    /// Kameranın galoş/küp rengini alt satırlarda algılamasına bakar.
    /// </summary>
    public bool IsPlayerOnStartBand()
    {
        if (cameraSystem == null || !cameraSystem.IsCalibrated) return false;

        Vector2 dotPos = cameraSystem.ScanForRedDot();
        if (dotPos == Vector2.zero) return false;

        Vector2Int gridCoord = cameraSystem.GetGridCoordinate(dotPos);

        // Y=0 satırı (en alt sıra) veya grid dışının altı başlangıç bandıdır
        return gridCoord.y == 0;
    }

    /// <summary>
    /// Oyuncunun başlangıç bandına gelmesini bekler.
    /// onPlayerReady callback'i oyuncu banda bastığında tetiklenir.
    /// </summary>
    public void WaitForPlayerOnStartBand(Action onPlayerReady)
    {
        StartCoroutine(WaitForPlayerCoroutine(onPlayerReady));
    }

    private IEnumerator WaitForPlayerCoroutine(Action onPlayerReady)
    {
        // Başlangıç bandını vurgula
        gridManager.HighlightStartBand(true);

        // Sürekli kontrol: Oyuncunun ayağı/küpü başlangıç bandında mı?
        int consecutiveDetections = 0;
        int requiredDetections = 10; // ~10 frame boyunca kararlı algılama

        while (consecutiveDetections < requiredDetections)
        {
            if (IsPlayerOnStartBand())
            {
                consecutiveDetections++;
            }
            else
            {
                consecutiveDetections = 0;
            }

            yield return null;
        }

        gridManager.HighlightStartBand(false);
        Debug.Log("AutoCalibrator: Oyuncu başlangıç bandında algılandı!");
        onPlayerReady?.Invoke();
    }

    private IEnumerator WaitForCamera()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (cameraSystem.WebcamTexture != null &&
                cameraSystem.WebcamTexture.isPlaying &&
                cameraSystem.WebcamTexture.width > 16)
            {
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogError("AutoCalibrator: Kamera 5 saniye içinde hazır olmadı!");
    }
}
