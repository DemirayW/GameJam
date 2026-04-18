using UnityEngine;
using System.Collections;

// Enemy2, Enemy'nin tüm özelliklerini (can barı, zıplama hedefi olma, hasar alma) otomatik devralır
public class Enemy2 : Enemy
{
    [Header("Enemy 2 - Dash Movement Settings")]
    public float dashForce = 8f; // Kısa kısa gidiş hızı
    public float dashCooldown = 1f; // YZ'nin bekleme süresi
    private float nextDashTime = 0f;

    [Header("Enemy 2 - Big Attack Settings")]
    public float chargeTime = 2f; 
    public float bigDashForce = 25f; 
    public float bigDashDuration = 0.5f;

    private bool isChargingAttack = false;
    private bool isDoingBigDash = false;
    private Vector2 lastAimDirection;

    protected override void Start()
    {
        base.Start(); // Enemy1'in tüm bar dolum, rb ayarlarını yapar
    }

    // STANDART YÜRÜME YERİNE DASH ATARAK İLERLEME YAPAY ZEKASI
    protected override void Update()
    {
        if (isMounted || currentHealth <= 0 || isEating) return;

        if (targetPlayer == null) 
        {
            targetPlayer = Object.FindAnyObjectByType<Player>();
        }

        if (isChargingAttack || isDoingBigDash) return; // Saldırı ortasında düşünmeyi kes

        if (targetPlayer != null && targetPlayer.currentState != Player.PlayerState.BeingEaten)
        {
            if (targetPlayer.currentState == Player.PlayerState.Dashing) return;

            float dist = Vector2.Distance(transform.position, targetPlayer.transform.position);

            // 1- Karakter çok yakınsa Büyük Saldırı için hazırlan! (2 saniye)
            if (dist <= attackRange * 1.5f) // Saldırı menzilini hafif geniş tuttum ki kaçamasın
            {
                StartCoroutine(Enemy2AttackRoutine());
            }
            // 2- Sadece Görüş alanındaysa, üzerine küçük Dash atarak gel!
            else if (dist <= visionRange && Time.time >= nextDashTime)
            {
                Vector2 dir = (targetPlayer.transform.position - transform.position).normalized;
                
                // Oyuncuya Dönme
                if (dir.x != 0 && spriteRenderer != null)
                {
                    spriteRenderer.transform.localScale = new Vector3(-Mathf.Sign(dir.x) * Mathf.Abs(spriteRenderer.transform.localScale.x), spriteRenderer.transform.localScale.y, spriteRenderer.transform.localScale.z);
                }

                // Kısa bir sekme
                rb.linearVelocity = dir * dashForce;
                nextDashTime = Time.time + dashCooldown;
                
                // İsterseniz Animator'da Kısa Zıplama eklenebilir
                if (anim != null) anim.SetTrigger("AttackTrigger"); 
            }
            else 
            {
                // Bekleme anlarında dur
                if (Time.time >= nextDashTime - (dashCooldown * 0.5f))
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }
    }

    // OYUNCU BİNDİĞİNDE YÖNETİM - WASD
    public override void MoveControl(Vector2 dir)
    {
        if (isChargingAttack || isDoingBigDash) return;

        // Devamlı kaymak yerine, oyuncu bastıkça seri dashler atar
        if (dir.magnitude > 0 && Time.time >= nextDashTime)
        {
            if (anim != null) anim.SetBool("IsWalking", true); // Binek animasyonunu çalıştır

            rb.linearVelocity = dir.normalized * dashForce;
            // Oyuncu kullanırken daha akıcı olması için cooldown düşük verilir
            nextDashTime = Time.time + 0.35f; 

            if (dir.x != 0 && spriteRenderer != null)
            {
                spriteRenderer.transform.localScale = new Vector3(-Mathf.Sign(dir.x) * Mathf.Abs(spriteRenderer.transform.localScale.x), spriteRenderer.transform.localScale.y, spriteRenderer.transform.localScale.z);
            }
        }
        else if (dir.magnitude == 0 && Time.time >= nextDashTime)
        {
            if (anim != null) anim.SetBool("IsWalking", false);
            rb.linearVelocity = Vector2.zero;
        }
    }

    // OYUNCU BİNDİĞİNDE SALDIRI - SOL TIK
    public override void AttackControl()
    {
        if (isChargingAttack || isDoingBigDash) return;
        
        // Mouse yönünü bulur
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        Vector2 aimDir = (mousePos - (Vector2)transform.position).normalized;

        if (anim != null) anim.SetTrigger("Enemy2Attack"); // Saldırı kararı verildi, animasyon hemen başlar

        // Hemen büyük dash'i başlat (Hazırlanma süresi yok, çünkü oyuncu beklemesin)
        StartCoroutine(BigDashRoutine(aimDir));
    }

    // YZ (AI) Kendi Büyük Saldırısı (2 sn Beklemeli)
    private IEnumerator Enemy2AttackRoutine()
    {
        isChargingAttack = true;
        rb.linearVelocity = Vector2.zero;
        
        // Şarj rengi / hazırlık
        if (spriteRenderer != null) spriteRenderer.color = Color.yellow; 

        // UYARI: YZ Karakter saldırı fikrine girdiği an animasyonu tetikler (2 Saniye öncesinden)
        if (anim != null) anim.SetTrigger("Enemy2Attack");

        // 2 Saniye Hazırlan (Bu esnada oyuncu onu vurabilir veya kaçabilir)
        yield return new WaitForSeconds(chargeTime);
        
        if (spriteRenderer != null) spriteRenderer.color = normalColor;

        // Süre sonunda oyuncu üstüne binmediyse ve yaşıyorsa BAM!
        if (!isMounted && currentHealth > 0)
        {
            if (targetPlayer != null)
            {
                lastAimDirection = (targetPlayer.transform.position - transform.position).normalized;
                StartCoroutine(BigDashRoutine(lastAimDirection));
            }
        }

        isChargingAttack = false;
    }

    // EZİP GEÇME (PASS-THROUGH CRUSH) MEKANİĞİ
    private IEnumerator BigDashRoutine(Vector2 dir)
    {
        isDoingBigDash = true;

        // Çarpışmaları kapatıyoruz ki kimseye takılmasın, içlerinden mermi gibi geçsin
        Collider2D col = GetComponent<Collider2D>();
        bool originalTrigger = col.isTrigger;
        col.isTrigger = true; 

        rb.linearVelocity = dir * bigDashForce;

        float elapsed = 0f;
        float nextGhostTime = 0f;

        while(elapsed < bigDashDuration)
        {
            // İZ BIRAKMA (Kırmızı Saydam Hayalet Efekti)
            if (elapsed >= nextGhostTime)
            {
                SpawnGhostTrail();
                nextGhostTime = elapsed + 0.05f; // Saniyede 20 kere iz bırakır
            }

            // ÇARPIŞMALARI HASSASLAŞTIR (Tag yerine direkt component kontrolü)
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2.5f); // Daha geniş alan
            
            foreach (var h in hits)
            {
                if (h.gameObject == this.gameObject) continue;

                // Eğer AI kendi başına bize veya başka bir objeye çarparsa
                if (!isMounted)
                {
                    // Ya direkt olarak havada gezen/fizikli oyuncuya çarpar
                    Player p = h.GetComponent<Player>();
                    if (p != null) p.GetEatenBy(this); 

                    // Ya da oyuncunun üstünde binek olarak bindiği "diğer düşmana" çarpar
                    Enemy e = h.GetComponent<Enemy>();
                    if (e != null && e.enabled && e.gameObject != this.gameObject)
                    {
                        // Çarptığı şey düşmansa ve oyuncu onun kafasına kamp kurmuşsa (isMounted = true ise)
                        if (e.isMounted && targetPlayer != null)
                        {
                            targetPlayer.GetEatenBy(this); // Seni de öldürür
                            SquashEnemyPermanently(e); // Altındaki bineği de ezer!
                        }
                    }
                }
                
                // Biz üstündeysek düşmanların içinden geçiyorsak düşmanları ezeriz
                if (isMounted)
                {
                    Enemy e = h.GetComponent<Enemy>();
                    
                    if (e != null && e.gameObject != this.gameObject && !e.isEating && e.enabled) 
                    {
                        SquashEnemyPermanently(e);
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        col.isTrigger = originalTrigger; // Fizikleri normale döndür
        isDoingBigDash = false;
    }

    // DÜŞMANI ALT KATMANA ATIP CESET BIRAKAN EZME SİSTEMİ
    private void SquashEnemyPermanently(Enemy targetObj)
    {
        // Onu işlemsiz hale getir (ölü kabul et)
        targetObj.enabled = false; 

        SpriteRenderer sr = targetObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) 
        {
            // Katman sırasını arka plana al (Böylece diğer tüm düşmanların altında kalır)
            sr.sortingOrder = -10; 
            sr.color = new Color(0.6f, 0.1f, 0.1f); // Kanlı ezilmiş siyah-kırmızı bir renk
        }

        // Objeyi y-ekseninde ez!
        targetObj.transform.localScale = new Vector3(
            targetObj.transform.localScale.x * 1.5f, // Yana yayılma
            0.15f, // Boydan ezilme
            targetObj.transform.localScale.z
        );

        // Bir daha dokunulamasın diye fiziklerini sil
        Collider2D[] cols = targetObj.GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;
        
        Rigidbody2D targetRb = targetObj.GetComponent<Rigidbody2D>();
        if (targetRb != null) targetRb.simulated = false;

        // Ezilen düşman cesetlerini sonsuza dek yerde tutmak yerine 2 saniye sonra tamamen oyundan/sahnede sil!
        Destroy(targetObj.gameObject, 2f);
    }

    private void SpawnGhostTrail()
    {
        if (spriteRenderer == null) return;
        
        GameObject ghost = new GameObject("DashGhost");
        ghost.transform.position = transform.position;
        ghost.transform.localScale = transform.localScale;
        
        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = spriteRenderer.sprite;
        sr.color = new Color(1f, 0f, 0f, 0.4f); // Kırmızı ama saydam
        sr.sortingOrder = spriteRenderer.sortingOrder - 2;
        
        Destroy(ghost, 0.4f); // Kısa süre sonra silinir
    }
}
