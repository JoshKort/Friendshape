using UnityEngine;

public class Traps : MonoBehaviour
{
    public float restartDelay = 0f;

    private bool triggered;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (other.GetComponentInParent<PlayerShapeShift>() == null) return;
        triggered = true;
        Invoke(nameof(RestartLevel), restartDelay);
    }

    void RestartLevel()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
