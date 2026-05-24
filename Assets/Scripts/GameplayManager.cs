using System.Collections;
using TMPro;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text chapterLevelText;
    public TMP_Text timerText;
    public TMP_Text stateText;

    [Header("Managers")]
    public GridManager gridManager;

    private float timer = 0f;
    private bool levelFinished = false;
    private bool timerRunning = false;

    private int currentChapter;
    private int currentLevel;

    private void Start()
    {
        // Geliştirici doğrudan Gameplay sahnesini açıp test etmek isterse diye fallback
        if (GameFlowManager.Instance == null)
        {
            PrepareLevel(GameData.selectedChapter, GameData.selectedLevel);
            BeginLevel();
        }
    }

    /// <summary>
    /// Grid'i oluşturur ama oyunu başlatmaz.
    /// Kalibrasyon sekansından önce çağrılır.
    /// </summary>
    public void PrepareLevel(int chapter, int level)
    {
        currentChapter = chapter;
        currentLevel = level;
        timer = 0f;
        levelFinished = false;
        timerRunning = false;

        if (chapterLevelText != null)
            chapterLevelText.text = "CHAPTER " + currentChapter + " - LEVEL " + currentLevel;

        if (timerText != null)
            timerText.text = "TIME: 0.0";

        SetStateText("");

        // Izgara boyutunu al ve grid'i oluştur
        Vector2Int gridSize = GetGridSize(currentChapter);

        if (gridManager != null)
        {
            gridManager.GenerateGrid(gridSize.x, gridSize.y);
        }
    }

    /// <summary>
    /// Kalibrasyon ve geri sayım tamamlandıktan sonra oyunu fiilen başlatır.
    /// Path oluşturulur ve MEMORIZE/MOVE döngüsü başlar.
    /// </summary>
    public void BeginLevel()
    {
        if (gridManager != null)
        {
            gridManager.StartLevel();
        }
    }

    private void Update()
    {
        if (levelFinished || !timerRunning)
            return;

        timer += Time.deltaTime;

        if (timerText != null)
            timerText.text = "TIME: " + timer.ToString("F1");
    }

    public void SetStateText(string newState)
    {
        if (stateText != null)
            stateText.text = newState;
    }

    public void SetTimerRunning(bool isRunning)
    {
        timerRunning = isRunning;
    }

    public void OnLevelComplete()
    {
        if (levelFinished)
            return;

        levelFinished = true;
        timerRunning = false;
        SetStateText("LEVEL COMPLETE");

        GameProgress.CompleteLevel(currentChapter, currentLevel, timer);

        StartCoroutine(ReturnToChapterSelectAfterDelay());
    }

    private IEnumerator ReturnToChapterSelectAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ShowChapterSelect();
        }
    }

    public void BackToChapterSelect()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ShowChapterSelect();
        }
    }

    public void RetryLevel()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.StartGameplay(currentChapter, currentLevel);
        }
    }

    public Vector2Int GetGridSize(int chapter)
    {
        switch (chapter)
        {
            case 1: return new Vector2Int(7, 5);
            case 2: return new Vector2Int(9, 7);
            case 3: return new Vector2Int(11, 9);
            default: return new Vector2Int(7, 5);
        }
    }

    public Vector2Int GetPathLengthRange(int chapter, int level)
    {
        if (chapter == 1)
        {
            switch (level)
            {
                case 1: return new Vector2Int(10, 12);
                case 2: return new Vector2Int(15, 17);
                case 3: return new Vector2Int(20, 22);
            }
        }

        if (chapter == 2)
        {
            switch (level)
            {
                case 1: return new Vector2Int(22, 24);
                case 2: return new Vector2Int(25, 27);
                case 3: return new Vector2Int(30, 32);
            }
        }

        if (chapter == 3)
        {
            switch (level)
            {
                case 1: return new Vector2Int(33, 35);
                case 2: return new Vector2Int(37, 39);
                case 3: return new Vector2Int(42, 45);
            }
        }

        return new Vector2Int(10, 12);
    }
}