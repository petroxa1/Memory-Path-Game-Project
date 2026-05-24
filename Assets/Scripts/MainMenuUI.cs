using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public void StartGame()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ShowChapterSelect();
        }
    }

    public void OpenCalibration()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ShowCalibration();
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}