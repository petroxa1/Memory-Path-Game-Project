using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChapterSelectUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject chapterContainer;
    public GameObject levelContainer;

    [Header("Texts")]
    public TMP_Text titleText;

    [Header("Chapter Buttons")]
    public Image chapter1Image;
    public Image chapter2Image;
    public Image chapter3Image;

    [Header("Level Buttons")]
    public Image level1Image;
    public Image level2Image;
    public Image level3Image;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color lockedColor = Color.gray;
    public Color greenColor = Color.green;
    public Color orangeColor = new Color(1f, 0.5f, 0f);
    public Color purpleColor = new Color(0.6f, 0.3f, 0.8f);

    private int selectedChapter = -1;

    private void Start()
    {
        InitializePanel();
    }

    public void InitializePanel()
    {
        selectedChapter = -1;

        if (titleText != null)
            titleText.text = "SELECT CHAPTER";

        chapterContainer.SetActive(true);
        levelContainer.SetActive(false);

        ResetChapterColors();
    }

    public void SelectChapter(int chapterNumber)
    {
        selectedChapter = chapterNumber;

        ResetChapterColors();
        SetSelectedChapterColor(chapterNumber);

        if (titleText != null)
            titleText.text = "SELECT LEVEL - CHAPTER " + chapterNumber;

        chapterContainer.SetActive(true);
        levelContainer.SetActive(true);

        RefreshLevelButtons();
    }

    public void StartLevel(int levelNumber)
    {
        if (selectedChapter == -1)
            return;

        if (!GameProgress.IsUnlocked(selectedChapter, levelNumber))
            return;

        GameData.selectedChapter = selectedChapter;
        GameData.selectedLevel = levelNumber;

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.StartGameplay(selectedChapter, levelNumber);
        }
    }

    public void HandleBackButton()
    {
        if (selectedChapter != -1)
        {
            selectedChapter = -1;
            titleText.text = "SELECT CHAPTER";
            levelContainer.SetActive(false);
            ResetChapterColors();
        }
        else
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ShowMainMenu();
            }
        }
    }

    private void RefreshLevelButtons()
    {
        RefreshOneLevel(level1Image, 1);
        RefreshOneLevel(level2Image, 2);
        RefreshOneLevel(level3Image, 3);
    }

    private void RefreshOneLevel(Image levelImage, int levelNumber)
    {
        if (levelImage == null || selectedChapter == -1)
            return;

        if (!GameProgress.IsUnlocked(selectedChapter, levelNumber))
        {
            levelImage.color = lockedColor;
            return;
        }

        LevelGrade grade = GameProgress.GetGrade(selectedChapter, levelNumber);

        switch (grade)
        {
            case LevelGrade.Green:
                levelImage.color = greenColor;
                break;
            case LevelGrade.Orange:
                levelImage.color = orangeColor;
                break;
            case LevelGrade.Purple:
                levelImage.color = purpleColor;
                break;
            default:
                levelImage.color = normalColor;
                break;
        }
    }

    private void ResetChapterColors()
    {
        if (chapter1Image != null) chapter1Image.color = normalColor;
        if (chapter2Image != null) chapter2Image.color = normalColor;
        if (chapter3Image != null) chapter3Image.color = normalColor;
    }

    private void SetSelectedChapterColor(int chapterNumber)
    {
        if (chapterNumber == 1 && chapter1Image != null) chapter1Image.color = greenColor;
        if (chapterNumber == 2 && chapter2Image != null) chapter2Image.color = greenColor;
        if (chapterNumber == 3 && chapter3Image != null) chapter3Image.color = greenColor;
    }
}