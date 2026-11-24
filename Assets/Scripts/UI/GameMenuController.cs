
using UnityEngine;

public class GameMenuController : MonoBehaviour
{
    [Header("Sistema de Ritmo")]
    [SerializeField] private RhythmManager rhythmManager;

    [Header("Paineis")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameOverPanel;

    private void OnEnable()
    {
        if (rhythmManager != null)
            rhythmManager.OnGameFinished += ShowGameOver;
    }

    private void Start()
    {
        ShowMainMenu();
    }

    private void OnDisable()
    {
        if (rhythmManager != null)
            rhythmManager.OnGameFinished -= ShowGameOver;
    }

    public void StartGame()
    {
        if (rhythmManager == null)
        {
            Debug.LogError("RhythmManager nao configurado!");
            return;
        }

        HideAllPanels();
        rhythmManager.StartGame();
    }

    public void RestartGame()
    {
        StartGame();
    }

    public void ShowMainMenu()
    {
        if (rhythmManager != null)
            rhythmManager.StopGame();

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void ShowGameOver()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    private void HideAllPanels()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}
