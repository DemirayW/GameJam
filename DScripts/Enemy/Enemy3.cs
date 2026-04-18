using UnityEngine;
using System.Collections;

public class Enemy3 : Enemy
{
    [Header("Enemy 3 Settings")]
    public GameObject energyOrbPrefab;
    public Transform firePoint;
    public float teleportDistance = 8f;
    public float safeDistance = 5f;
    
    private float nextTeleportTime = 0f;
    private bool isPreparingTeleport = false;
    private float nextFireTime = 0f;
    public float fireRate = 1.5f;

    // Unity otomatik olarak bu Update'i çalıştıracak (Base Update yerine ezmeyecek fakat çalışacak)
    // Eğer base.Update'i ezmek istemezseniz base.Update()'in içindeki mantığı kopyalamalıyız veya new kullanmalıyız.
    // Ancak temiz olması için Unity'de LateUpdate kullanalım veya new void Update()
    new void Update()
    {
        if (currentHealth <= 0 || isEating) return;

        if (isMounted)
        {
            // Kullanıcı bindiğinde zamanı dondurarak WASD ile yer seçiyor ("H" ye basarsa onaylanır)
            if (isPreparingTeleport && UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame)
            {
                ConfirmTeleport();
            }
            return;
        }

        // --- YZ (Yapay Zeka) MANTIĞI ---
        
        if (targetPlayer == null) return;

        float distToPlayer = Vector2.Distance(transform.position, targetPlayer.transform.position);

        // Duvar Arkasında mıyız? (Görüş açısı testi) Veya oyuncu bize çok mu yakın?
        RaycastHit2D hit = Physics2D.Raycast(transform.position, (targetPlayer.transform.position - transform.position).normalized, distToPlayer);
        bool hasClearLineOfSight = (hit.collider != null && hit.collider.CompareTag("Player"));

        if (!hasClearLineOfSight || distToPlayer < safeDistance)
        {
            // Işınlanıp kaç / görüş açısı bul
            if (Time.time >= nextTeleportTime) // dashTime/TelepordTime burada ışınlanma bekleme süresi olarak kullanıldı
            {
                StartCoroutine(AITeleportRoutine());
                nextTeleportTime = Time.time + 3f; // 3 saniyede bir yer değiştirir
            }
        }
        else
        {
            // Görüş açımız var ve uzaktayız! ATEŞ ET
            if (Time.time >= nextFireTime)
            {
                if (anim != null) anim.SetTrigger("Enemy3Attack");
                FireOrb((targetPlayer.transform.position - transform.position).normalized);
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    // --- YAPAY ZEKA IŞINLANMA ---
    private IEnumerator AITeleportRoutine()
    {
        if (anim != null) anim.SetTrigger("TeleportOut");
        
        // Işınlanana kadar hasar almaz/verilmez yapılabilir ama abartı olur
        yield return new WaitForSeconds(0.3f); 
        
        // Rastgele yeni bir güvenli bölge bul
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector3 targetPos = targetPlayer.transform.position + (Vector3)(randomDir * safeDistance * 1.5f);
        
        // Target pozisyonda duvar var mı?
        Collider2D wallCheck = Physics2D.OverlapCircle(targetPos, 0.5f);
        if (wallCheck == null || !wallCheck.CompareTag("Wall"))
        {
            transform.position = targetPos;
        }

        if (anim != null) anim.SetTrigger("TeleportIn");
    }

    // --- OYUNCU BİNEK KONTROLÜ ---
    public override void MoveControl(Vector2 dir)
    {
        // WASD ya basıldığı an (Eğer hazırlıkta değilsek), zaman durur ve mod açılır
        if (dir.magnitude > 0 && !isPreparingTeleport)
        {
            isPreparingTeleport = true;
            Time.timeScale = 0f; // ZAMAN DURDU!
            Time.fixedDeltaTime = 0f; // Fiziği de dondur
            
            if (anim != null) anim.SetTrigger("TeleportOut");
        }

        // Zaman dururken fizik / karakter WASD ile hareket eder
        if (isPreparingTeleport)
        {
            // Fiziğin etkileyemediği uzayda manuel yer değiştiriyoruz
            Vector3 testPos = transform.position + (Vector3)(dir * 15f * Time.unscaledDeltaTime);
            
            // Eğer yürüdüğümüz yerde duvar varsa oraya gitmemize izin verme (duvarın içinden geçemeyiz)
            Collider2D wallHit = Physics2D.OverlapCircle(testPos, 0.5f);
            if (wallHit == null || !wallHit.CompareTag("Wall")) 
            {
                transform.position = testPos; // Zaman durmuşken yürüyoruz (sanki o an ışınlanacağımız noktayı seçiyoruz)
            }
        }
    }

    private void ConfirmTeleport()
    {
        isPreparingTeleport = false;
        
        Time.timeScale = 1f; // ZAMAN DEVAM EDİYOR!
        // Sabitlenen delta time player tarafında `defaultFixedDeltaTime` var ancak Enemy'de sabit 0.02f standart olarak dönülebilir:
        Time.fixedDeltaTime = 0.02f; 
        
        if (anim != null) anim.SetTrigger("TeleportIn");
        
        // Işınlanma tamamlandı, küçük bir cooldown koyalım
        nextTeleportTime = Time.time + 0.5f; 
    }

    public override void AttackControl()
    {
        // Biz üstündeysek Sol Tıka bastığımızda mermi ateşler
        // Eğer zaman donmuş haldeysek önce onu çöz (aksi takdirde mermi durur)
        if (isPreparingTeleport) ConfirmTeleport();

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        Vector2 aimDir = (mousePos - (Vector2)transform.position).normalized;

        if (anim != null) anim.SetTrigger("Enemy3Attack");
        
        FireOrb(aimDir);
    }

    private void FireOrb(Vector2 dir)
    {
        if (energyOrbPrefab == null) return;
        
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject orb = Instantiate(energyOrbPrefab, spawnPos, Quaternion.identity);
        
        Enemy3Projectile proj = orb.GetComponent<Enemy3Projectile>();
        if (proj != null)
        {
            proj.direction = dir;
            proj.sourceEnemy = this;
        }

        // Merminin görsel dönüşü (Farenin ya da hedefin baktığı açı)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        orb.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
