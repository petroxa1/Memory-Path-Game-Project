using System.Collections;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("UI Panelleri")]
    public GameObject mainMenuPanel;
    public GameObject chapterSelectPanel;
    public GameObject gameplayPanel;
    public GameObject calibrationPanel;

    [Header("Yöneticiler")]
    public GameplayManager gameplayManager;
    public ChapterSelectUI chapterSelectUI;
    public MasterCameraSystem cameraSystem;
    public GridManager gridManager;
    public AutoCalibrator autoCalibrator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        chapterSelectPanel.SetActive(false);
        gameplayPanel.SetActive(false);
        if (calibrationPanel != null) calibrationPanel.SetActive(false);

        if (cameraSystem != null)
        {
            cameraSystem.SetTrackingEnabled(false);
        }

        if (gridManager != null)
        {
            gridManager.StopAllCoroutines();
            gridManager.ClearGrid();
        }
    }

    public void ShowChapterSelect()
    {
        mainMenuPanel.SetActive(false);
        chapterSelectPanel.SetActive(true);
        gameplayPanel.SetActive(false);
        if (calibrationPanel != null) calibrationPanel.SetActive(false);

        if (cameraSystem != null)
        {
            cameraSystem.SetTrackingEnabled(false);
        }

        if (chapterSelectUI != null)
        {
            chapterSelectUI.InitializePanel();
        }
    }

    /// <summary>
    /// Yeni oyun akışı:
    /// 1. Grid oluşturulur (oyun başlamaz)
    /// 2. Chapter değiştiyse otomatik kalibrasyon yapılır
    /// 3. Kalibrasyon OK → Oyuncu başlangıç bandına geçer
    /// 4. 3-2-1 geri sayım → Oyun başlar
    /// </summary>
    public void StartGameplay(int chapter, int level)
    {
        mainMenuPanel.SetActive(false);
        chapterSelectPanel.SetActive(false);
        gameplayPanel.SetActive(true);
        if (calibrationPanel != null) calibrationPanel.SetActive(false);

        StartCoroutine(GameplayStartSequence(chapter, level));
    }

    private IEnumerator GameplayStartSequence(int chapter, int level)
    {
        // ===== AŞAMA 1: Grid Oluştur (oyun başlatma) =====
        if (gameplayManager != null)
        {
            gameplayManager.PrepareLevel(chapter, level);
        }

        // Kamera takibini henüz açma
        cameraSystem.SetTrackingEnabled(false);

        yield return new WaitForSeconds(0.5f);

        // ===== AŞAMA 2: Otomatik Kalibrasyon (gerekirse) =====
        if (autoCalibrator != null && autoCalibrator.NeedsCalibration(chapter))
        {
            gameplayManager.SetStateText("CALIBRATING...");

            bool calibrationDone = false;
            bool calibrationSuccess = false;
            int retryCount = 0;
            int maxRetries = 3;

            while (!calibrationSuccess && retryCount < maxRetries)
            {
                calibrationDone = false;

                autoCalibrator.StartAutoCalibration(chapter, (success) =>
                {
                    calibrationSuccess = success;
                    calibrationDone = true;
                });

                // Kalibrasyon tamamlanana kadar bekle
                while (!calibrationDone)
                {
                    yield return null;
                }

                if (!calibrationSuccess)
                {
                    retryCount++;
                    gameplayManager.SetStateText($"CALIBRATION FAILED - Retry {retryCount}/{maxRetries}");
                    yield return new WaitForSeconds(1f);
                }
            }

            if (!calibrationSuccess)
            {
                gameplayManager.SetStateText("CALIBRATION FAILED!");
                yield return new WaitForSeconds(2f);
                ShowChapterSelect();
                yield break;
            }

            // Kalibrasyon başarılı
            gameplayManager.SetStateText("CALIBRATION OK ✓");
            yield return new WaitForSeconds(1.5f);
        }

        // ===== AŞAMA 3: Oyuncu Başlangıç Bandını Bekle =====
        gameplayManager.SetStateText("STEP ON START BAND");
        cameraSystem.SetTrackingEnabled(false); // Takip henüz kapalı, sadece algılama için

        if (autoCalibrator != null && cameraSystem.IsCalibrated)
        {
            bool playerReady = false;

            autoCalibrator.WaitForPlayerOnStartBand(() =>
            {
                playerReady = true;
            });

            while (!playerReady)
            {
                yield return null;
            }
        }
        else
        {
            // Kamera yoksa veya kalibre değilse 3 saniye bekle
            yield return new WaitForSeconds(3f);
        }

        // ===== AŞAMA 4: 3-2-1 Geri Sayım =====
        gridManager.HighlightStartBand(false);

        for (int i = 3; i >= 1; i--)
        {
            gameplayManager.SetStateText(i.ToString());
            yield return new WaitForSeconds(1f);
        }

        gameplayManager.SetStateText("START!");
        yield return new WaitForSeconds(0.5f);

        // ===== AŞAMA 5: Oyunu Başlat =====
        cameraSystem.SetTrackingEnabled(true);
        gameplayManager.BeginLevel();
    }

    public void ShowCalibration()
    {
        if (calibrationPanel != null) calibrationPanel.SetActive(true);

        if (cameraSystem != null)
        {
            cameraSystem.SetTrackingEnabled(false);
        }
    }

    public void CloseCalibration()
    {
        if (calibrationPanel != null) calibrationPanel.SetActive(false);

        if (gameplayPanel.activeSelf && cameraSystem != null)
        {
            cameraSystem.SetTrackingEnabled(true);
        }
    }
}
