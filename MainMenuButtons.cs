using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour {

	public void PlayGame()
    {
        SceneManager.LoadScene("Race");
        Time.timeScale = 1;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
