using UnityEngine;
using UnityEngine.SceneManagement; // Sahneleri kontrol etmek için gerekli

public class Menu : MonoBehaviour
{
    // Oyun butonu için fonksiyon
    public void PlayGame()
    {
        // "ana oyun" isimli sahneye geçiş yapar
        SceneManager.LoadScene("ana oyun");
    }

    // Ayarlar butonu (Şimdilik sadece konsola yazar)
    public void OpenSettings()
    {
        Debug.Log("Ayarlar açıldı!");
    }

    // Çıkış butonu
    public void QuitGame()
    {
        Debug.Log("Oyundan çıkılıyor...");
        Application.Quit(); // Derlenmiş oyunda çalışır
    }
}
