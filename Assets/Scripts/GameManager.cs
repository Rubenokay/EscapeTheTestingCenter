using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameObject gameOverPanel;
    public MouseLook mouseLook;
    public Behaviour playerMovement;
    [SerializeField] private bool enableGameOverTest = true;

    public bool GameEnded { get; private set; }

    private void Start()
    {
        Time.timeScale = 1f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (enableGameOverTest && Input.GetKeyDown(KeyCode.G))
            GameOver();
    }

    public void GameOver()
    {
        if (GameEnded)
            return;

        EndGame();
        Debug.Log("YOU WERE CAUGHT!");

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    public void EndGame()
    {
        GameEnded = true;

        if (mouseLook != null)
            mouseLook.EndGame();

        if (playerMovement != null)
            playerMovement.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
