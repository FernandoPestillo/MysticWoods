using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    public GameObject restartMenu;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !restartMenu.activeSelf)
        {
            Debug.Log("apertou ESC" + pauseMenu.activeSelf);
            if (pauseMenu.activeSelf) 
            { 
                pauseMenu.SetActive(false);
                Time.timeScale = 1;
            }
            else 
            {
                pauseMenu.SetActive(true);
                Time.timeScale = 0;
            }
        }        
    }
    public void ResumeGame()
    {
        pauseMenu.gameObject.SetActive(false);
        Time.timeScale = 1;
    }
    public void VoltarMenu()
    {
        SceneManager.LoadScene("TelaInicial");
    }

}


