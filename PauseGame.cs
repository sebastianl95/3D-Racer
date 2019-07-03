using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class PauseGame : MonoBehaviour
{
    public Transform canvas;

    //use this for initialization
    void Start()
    {
        canvas.gameObject.SetActive(false);
    }
    // Update is called once per frame
    void Update()
    {
		if(Input.GetKeyDown(KeyCode.Escape))
        {
            Pause();
        }
	}

    public void Pause()
    {
        if (canvas.gameObject.activeInHierarchy == false)
        {
            canvas.gameObject.SetActive(true);
            Time.timeScale = 0;
            
        }
        else
        {
            canvas.gameObject.SetActive(false);
            Time.timeScale = 1;
        }
    }

    public void Quit()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
