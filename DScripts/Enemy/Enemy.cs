using UnityEngine;
using System.Collections;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    public float maxHealth = 100f;
    protected float currentHealth;
    
    [Header("UI & Health Bar")]
    [Tooltip("Düşmanın Can Barı (Image). Image Type'ı 'Filled' olmalı.")]
    public Image healthFillBar;
    
    [Header("Mount Settings")]
    [Tooltip("Oyuncunun düşmana tutunduğunda duracağı nokta. Yoksa direkt düşmanın merkezinde durur.")]
    public Transform mountPoint; 
    
    [Header("AI Logic")]
    public float visionRange = 6f; // Oyuncuyu algılama menzili
    public float attackRange = 1.5f; // Yutma saldırısına başlama menzili
    public float moveSpeed = 2f; // Yürüme hızı
    public float eatSuctionSpeed = 4f; // Sizi midesine çekerkenki vantuzlanma hızı
    
    [Header("VFX & Feedback")]
    public Color normalColor = Color.white;
    public Color mountedColor = Color.red;
    public GameObject deathVFXPrefab; 

    protected SpriteRenderer spriteRenderer;
    protected Rigidbody2D rb;
    protected Animator anim;
    
    protected Player targetPlayer;
    protected bool isTracking = false; 
    [HideInInspector] public bool isEating = false;

    [HideInInspector]
    public bool isMounted = false; 

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; 
        
        anim = GetComponentInChildren<Animator>();
        if (anim == null) anim = GetComponent<Animator>();
    }

    protected virtual void Update()
    {
        if (isMounted || currentHealth <= 0 || isEating) return;

        if (targetPlayer == null) 
        {
            targetPlayer = Object.FindAnyObjectByType<Player>();
        }

        if (targetPlayer != null && targetPlayer.currentState != Player.PlayerState.BeingEaten)
        {
            // Mükemmel Çözüm: Oyuncu bize doğru Homing Dash ya da Zıplama atıyorsa (Dashing), yutulmaz, üstümüze konar
            if (targetPlayer.currentState == Player.PlayerState.Dashing) 
            {
                return;
            }

            float dist = Vector2.Distance(transform.position, targetPlayer.transform.position);
            
            // Eğer yeterince yaklaştıysa YUT!
            if (dist <= attackRange)
            {
                StartEating(targetPlayer);
            }
            // Görüş alanına girdiyse TAKİP ET!
            else if (dist <= visionRange)
            {
                isTracking = true;
                if (anim != null) anim.SetBool("IsWalking", true);
                
                Vector2 dir = (targetPlayer.transform.position - transform.position).normalized;
                rb.linearVelocity = dir * moveSpeed;
                
                // Oyuncunun yönüne göre sağa sola dönme (yön ters çevrildi)
                if (dir.x != 0 && spriteRenderer != null)
                {
                    spriteRenderer.transform.localScale = new Vector3(-Mathf.Sign(dir.x) * Mathf.Abs(spriteRenderer.transform.localScale.x), spriteRenderer.transform.localScale.y, spriteRenderer.transform.localScale.z);
                }
            }
            // Görüş alanının dışındaysa DUR!
            else
            {
                isTracking = false;
                if (anim != null) anim.SetBool("IsWalking", false);
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    [Header("Attack Animation Delay")]
    [Tooltip("Saldırı animasyonunun başlaması için beklenecek süre (saniye). Animasyonunuzun geçiş süresine göre ayarlayın.")]
    public float attackAnimDelay = 0.3f;

    // Yutmaya başlama rutini
    void StartEating(Player p)
    {
        isEating = true;
        rb.linearVelocity = Vector2.zero;

        if (anim != null) 
        {
            anim.SetBool("IsWalking", false);
            anim.SetTrigger("AttackTrigger"); 
        }

        // Animasyona başlaması için bekleriz, sonra yutarız
        StartCoroutine(EatAfterDelay(p));
    }

    private System.Collections.IEnumerator EatAfterDelay(Player p)
    {
        // Animasyonun geçiş yapması için gerçek zamanda bekle (timeScale 1 varsayılarak)
        yield return new WaitForSecondsRealtime(attackAnimDelay);
        if (p != null) p.GetEatenBy(this);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        StartCoroutine(FlashDamage());

        if (healthFillBar != null)
        {
            healthFillBar.fillAmount = currentHealth / maxHealth;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void OnPlayerMounted()
    {
        isMounted = true;
        rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.SetBool("IsWalking", false);
        
        if (spriteRenderer != null) spriteRenderer.color = mountedColor;
    }

    public void OnPlayerDismounted()
    {
        isMounted = false;
        if (spriteRenderer != null) spriteRenderer.color = normalColor;
    }

    public void ExecuteAssassination()
    {
        Die();
    }

    public void StartSquashDeath(float duration)
    {
        StartCoroutine(SquashRoutine(duration));
    }

    private System.Collections.IEnumerator SquashRoutine(float duration)
    {
        isMounted = true; 
        isEating = true; 
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Vector3 startScale = transform.localScale;
        // X ekseninde biraz genişler, Y ekseninde (boyu) ezilir
        Vector3 endScale = new Vector3(startScale.x * 1.5f, startScale.y * 0.1f, startScale.z);

        float elapsed = 0f;
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            if (spriteRenderer != null) 
            {
                spriteRenderer.color = Color.Lerp(mountedColor, Color.red, t);
            }
            yield return null;
        }
        
        Die();
    }

    private void Die()
    {
        CreateSandShatterVFX(); // Kum gibi dağılan programatik partikülleri saç!

        if (deathVFXPrefab != null)
        {
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        }

        currentHealth = 0;
        Destroy(gameObject); // Ezilme süresi bittiği için artık anında silebiliriz (Partiküller kendi objesinde yaşıyor)
    }

    private void CreateSandShatterVFX()
    {
        // Kendi "Kum dağılımı" partikül efektimizi anlık olarak oyuncuya gerek kalmadan kodla var ediyoruz!
        GameObject vfx = new GameObject("SandShatter_VFX");
        vfx.transform.position = transform.position;
        // Büyüklüğün tam uyması için verdiğiniz Vector3 ölçeğini atıyoruz
        vfx.transform.localScale = new Vector3(2.55323625f, 2.45101857f, 2.20772696f);
        
        ParticleSystem ps = vfx.AddComponent<ParticleSystem>();
        
        // HATA ÇÖZÜMÜ: Partiküller eklendiğinde otomatik başladığı için özellikleri değişmiyordu. Önce durduruyoruz!
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var main = ps.main;
        main.duration = 1f;
        main.loop = false; // TEK SEFER PATLAMASI İÇİN (3 kere tekrarlamayı önler)
        main.startColor = Color.green; // Rengini tamamen yeşil yapıyoruz
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.05f); // Tanecikler çok daha küçük! (Kum tanesi kadar)
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 3f); // DAHA YAVAŞ SAÇILMA
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.gravityModifier = 0.5f; // Yere kum gibi düşsün
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, 20, 35) }); // PARTİKÜL SAYISI AZALTILDI
        
        // TEK YÖNE GİTME SORUNUNU ÇÖZME: 360 Derece Daire formunda her yere saçılması için
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        shape.arc = 360f;

        // Başlangıçta daha şiddetli rastgele yerlere fırlaması için Velocity (Hız) modülü
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        
        // HATA ÇÖZÜMÜ: X ve Y eksenini "MinMax (İki değer arası)" yaptıysak, 
        // Unity kuralı gereği boşta kalan Z eksenini de zorla "MinMax" tarzı belirtmek zorundayız!
        velocity.x = new ParticleSystem.MinMaxCurve(-4f, 4f); // Rastgele Sağa ve Sola fırlama
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 5f);  // Biraz yukarı fırlayıp yerçekimine kapılma
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);  // Z Ekseni kullanılmıyor ama aynı formatta yazılmak zorunda
        
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        // Standart partikül materyali - küçük kare/nokta görünümü sağlar
        renderer.material = new Material(Shader.Find("Sprites/Default")); 
        renderer.sortingOrder = 50;

        ps.Play();
        // Bu partikül gösterisi 3 saniye sonra sahneden tek başına silinecektir
        Destroy(vfx, 3f);
    }

    // --- YENİ BİNEK (MIND CONTROL) KOMUTLARI ---

    public virtual void MoveControl(Vector2 dir)
    {
        rb.linearVelocity = dir * moveSpeed;
        
        if (dir.magnitude > 0)
        {
            if (anim != null) anim.SetBool("IsWalking", true);
            if (dir.x != 0 && spriteRenderer != null)
            {
                spriteRenderer.transform.localScale = new Vector3(-Mathf.Sign(dir.x) * Mathf.Abs(spriteRenderer.transform.localScale.x), spriteRenderer.transform.localScale.y, spriteRenderer.transform.localScale.z);
            }
        }
        else
        {
            if (anim != null) anim.SetBool("IsWalking", false);
        }
    }

    public virtual void AttackControl()
    {
        if (isEating) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        Enemy targetToEat = null;
        float closestDist = float.MaxValue;

        foreach(var hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;
            
            Enemy otherE = hit.GetComponent<Enemy>();
            if (!hit.CompareTag("Enemy") && otherE == null) continue;

            if (otherE != null && !otherE.isMounted)
            {
                float d = Vector2.Distance(transform.position, otherE.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    targetToEat = otherE;
                }
            }
        }

        if (targetToEat != null)
        {
            isEating = true; 
            rb.linearVelocity = Vector2.zero;
            
            if (anim != null) 
            {
                anim.SetBool("IsWalking", false);
                anim.SetTrigger("AttackTrigger"); 
            }
            StartCoroutine(EatAfterDelayEnemy(targetToEat));
        }
    }

    private System.Collections.IEnumerator EatAfterDelayEnemy(Enemy target)
    {
        yield return new WaitForSecondsRealtime(attackAnimDelay);
        if (target != null) 
        {
            target.GetEatenByAnotherEnemy(this);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        isEating = false; 
    }

    public void GetEatenByAnotherEnemy(Enemy consumer)
    {
        isEating = true; // AI sussun
        isTracking = false;
        
        if (rb != null) rb.simulated = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        StartCoroutine(EnemyGetEatenSequence(consumer));
    }

    private System.Collections.IEnumerator EnemyGetEatenSequence(Enemy consumer)
    {
        float suctionSpeed = consumer != null ? consumer.eatSuctionSpeed * 4f : 15f; 
        while (consumer != null && Vector2.Distance(transform.position, consumer.transform.position) > 0.1f)
        {
            transform.position = Vector2.MoveTowards(transform.position, consumer.transform.position, Time.unscaledDeltaTime * suctionSpeed);
            yield return null;
        }

        if (spriteRenderer != null) spriteRenderer.enabled = false; // Rakip tamamen yutuldu!
        
        yield return new WaitForSecondsRealtime(0.1f);
        Die();
    }
    
    private IEnumerator FlashDamage()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.yellow;
            yield return new WaitForSeconds(0.1f);
            if (!isMounted) spriteRenderer.color = normalColor;
            else spriteRenderer.color = mountedColor;
        }
    }
    
    void OnDrawGizmos()
    {
        if (mountPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(mountPoint.position, 0.2f);
        }
        
        // Görüş Alanı (Mavi)
        Gizmos.color = new Color(0, 0, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, visionRange);
        
        // Yutma/Saldırı Alanı (Kırmızı)
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
