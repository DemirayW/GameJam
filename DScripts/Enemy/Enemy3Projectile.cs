using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy3Projectile : MonoBehaviour
{
    public float speed = 15f;
    public float lifetime = 5f;
    public Vector2 direction;
    public Enemy sourceEnemy; // Ateş edenin kendisini vurmasını engellemek için

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // Sadece kodla ilerleyecek (Yeni Unity standartlarına uyumlu)
        
        Collider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Zaman yavaşlasa da / dursa da mermilerin hızı normal kalsın istiyorsak unscaledDeltaTime kullanılabilir
        // Ancak genelde zaman durduğunda mermilerin de donması daha iyidir.
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D hit)
    {
        if (hit.gameObject == sourceEnemy.gameObject) return; // Kendi kendini vurmasın

        // Duvara çarparsa direkt yok ol
        if (hit.CompareTag("Wall"))
        {
            Destroy(gameObject);
            return;
        }

        Player player = hit.GetComponent<Player>();
        if (player != null)
        {
            player.GetBurned(); // Karakterimizi yakarak öldür!
            Destroy(gameObject);
            return;
        }

        Enemy enemy = hit.GetComponent<Enemy>();
        if (enemy != null && enemy != sourceEnemy)
        {
            StartCoroutine(BurnEnemyToDeath(enemy));
            // Mermiyi hemen yok et veya görünmez yap
            GetComponent<SpriteRenderer>().enabled = false;
            GetComponent<Collider2D>().enabled = false;
            Destroy(gameObject, 2f); // Coroutine bitene kadar bekle
        }
    }

    private IEnumerator BurnEnemyToDeath(Enemy e)
    {
        e.enabled = false; // Yapay zekasını kapat
        
        // Hareketi durdur
        Rigidbody2D eRb = e.GetComponent<Rigidbody2D>();
        if (eRb != null) eRb.linearVelocity = Vector2.zero;

        SpriteRenderer sr = e.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            // Önce turuncuya/ateş rengine dön
            sr.color = new Color(1f, 0.4f, 0f); 
        }

        // Büzüşüp yanma animasyonu (0.5 sn)
        Vector3 startScale = e.transform.localScale;
        float elapsed = 0f;
        while(elapsed < 0.8f)
        {
            if (e == null) break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / 0.8f;
            
            e.transform.localScale = Vector3.Lerp(startScale, startScale * 0.2f, t);
            
            if (sr != null)
            {
                sr.color = Color.Lerp(new Color(1f, 0.4f, 0f), Color.black, t);
            }
            
            yield return null;
        }

        if (e != null)
        {
            // Son olarak öldür ve yok et
            e.ExecuteAssassination();
        }
    }
}
