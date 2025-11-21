using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{

    public static bool GameIsPaused;

    public GameObject pauseMenu;
    public Button pause;
    public Button unpause;
    public AudioSource Source;
    public AudioClip ButtonHover;
    public AudioClip ButtonPressed;
    public void closeMenu()
    {
        pauseMenu.SetActive(false);
    }
    private void Start()
    {
        Time.timeScale = 1;
        
    }
    private void OnEnable()
    {
        
        pause.gameObject.SetActive(false);
        unpause.gameObject.SetActive(true);
    }
    private void OnDisable()
    {
        
        pause.gameObject.SetActive(true);
        unpause.gameObject.SetActive(false);
    }
   
    public void Pause()
    {
        if(GameIsPaused)
        {
            ResumeGame();
        }
        else
            PauseGame();
    }

    public void ResumeGame()
    {
        
        Time.timeScale = 1;
        GameIsPaused = false;
    }

    private void PauseGame()
    {
        
        Time.timeScale = 0;
        GameIsPaused = true;
    }
    public void OnHover()
    {
        Source.PlayOneShot(ButtonHover);
    }
    public void OnPlayButton()
    {
        Source.PlayOneShot(ButtonPressed);
        ResumeGame();
    }
    public void OnRestartButton()
    {
        Source.PlayOneShot(ButtonPressed);
        StartCoroutine(RestartGame());
    }

    public void OnExitButton()
    {
        SceneManager.LoadScene("Title");
        Source.PlayOneShot(ButtonPressed);
		

    }
    public void OnDeathExit()
    {
        Source.PlayOneShot(ButtonPressed);
        StartCoroutine(ReturnToTitle());
    }
    
    void ExitGame()
    {
        Time.timeScale = 1;
        
        SceneManager.LoadScene(0);

    }
    IEnumerator RestartGame()
    {
        yield return new WaitForSeconds(0f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    IEnumerator ReturnToTitle()
    {
        yield return new WaitForSeconds(0f);
        
    }
}