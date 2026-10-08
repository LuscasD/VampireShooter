using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Para a cena de demonstração: F5 recarrega a cena e coloca tudo de volta no lugar
public class ReiniciarCena : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().name
                : SceneManager.GetActiveScene().path);
    }
}
