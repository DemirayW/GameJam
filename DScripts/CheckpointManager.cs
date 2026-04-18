using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager instance;
    public static Vector2 lastCheckpointPos;
    public static bool hasCheckpoint = false;

    void Awake()
    {
        // Singleton yapısı, her sahnede arkaplanda çalışsın
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); return; }

        // Eğer daha önce checkpoint'e dokunulmuşsa, bölüm başında karakteri o noktaya ışınla
        if (hasCheckpoint)
        {
            Player player = Object.FindAnyObjectByType<Player>();
            if (player != null) player.transform.position = lastCheckpointPos;
        }
    }

    public static void SetCheckpoint(Vector2 pos)
    {
        lastCheckpointPos = pos;
        hasCheckpoint = true;
        Debug.Log("CHECKPOINT KAYDEDİLDİ: " + pos);
    }

    public void RestartLevel()
    {
        // Zaman ayarları (TimeScale) ve fizik ayarları donmuş kaldıysa onları resetler
        Time.timeScale = 1f;
        
        // Aynı sahneyi en baştan yükler (Böylece tüm yaratıklar yerlerine geçer) Ama CheckpointManager bizi ışınlar.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
