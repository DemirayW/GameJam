using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    public enum PlayerState { Ground, Dashing, OnWall, OnEnemy, BeingEaten } 

    [Header("Current State")]
    public PlayerState currentState = PlayerState.Ground;
    private Animator anim;
    private Rigidbody2D rb;
    private Vector2 movement;
    private Vector2 dashDirection;

    [Header("Mount Settings")]
    [Tooltip("Düşmanın kafasında durduğunuzda yüksekliği hassas ayarlamak için (0.1, 0.2 vs. yapabilirsiniz)")]
    public float mountYOffset = 0.25f;

    [Header("Movement Settings (Ground)")]
    public float moveSpeed = 5f;
    
    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private float dashCooldownTimer;
    private float currentDashTime = 0f; 

    [Header("Wall Mechanics")]
    public LayerMask wallLayer;
    public float wallCheckDistance = 0.6f;
    public float wallMoveSpeed = 8f;

    [Header("Enemy Mount & Execution")]
    public LayerMask enemyLayer; 
    public float executionPopUpForce = 15f; 
    [Tooltip("Öldürme animasyonu kaç saniye sürüp bitiyor? O an karakter serbest kalır.")]
    public float executionLockTime = 0.5f; 
    private Enemy mountedEnemy; 
    private GameObject lastAttachedObject; 

    [Header("Slow-Mo (Zaman Yavaşlatma)")]
    [Range(0.01f, 1f)]
    public float slowMotionTimeScale = 0.25f; // Biraz hızlandırıldı
    private float defaultFixedDeltaTime;
    private bool isAiming = false; 

    [Header("Big Dash (V Tuşu İnfaz Dash)")]
    public float bigDashSpeed = 35f;
    public float bigDashAimMaxTime = 2f;
    private bool isBigDashAiming = false; // "V" tuşuna basılı tutmayı algılar
    private float bigDashAimTimer = 0f;
    [HideInInspector] public bool isBigDashing = false;
    private LineRenderer aimLine;

    [Header("Combat Settings - Pointers")]
    public Transform attackPoint; 

    [Header("Combat Settings - Ground Combos")]
    public float comboWindowTime = 5f;
    public float attack1Damage = 20f;
    public float attack1Radius = 1.8f;
    public float attack2Damage = 20f;
    public float attack2Radius = 2.2f;
    public float attack3Damage = 60f;
    public float attack3Radius = 3f;
    private int comboStep = 0;
    private float comboTimer = 0f;

    [Header("Combat Settings - Ground Attack (Space)")]
    public float groundAttackDamage = 30f;
    public float groundAttackRadius = 2.5f;

    [Header("Combat Settings - Homing Dash Attack")]
    public float dashAttackDamage = 40f;  
    public float dashAttackRadius = 3f;
    public float dashAttackForce = 25f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        anim = GetComponentInChildren<Animator>(); 
        if (anim == null) anim = GetComponent<Animator>();

        rb.gravityScale = 0f; 

        defaultFixedDeltaTime = Time.fixedDeltaTime; 

        SetupSlowMoOverlay();

        // Görsel Hedef Ok'u (LineRenderer) hazırlığı
        aimLine = GetComponent<LineRenderer>();
        if (aimLine == null)
        {
            aimLine = gameObject.AddComponent<LineRenderer>();
        }
        aimLine.startWidth = 0.15f;
        aimLine.endWidth = 0.02f; // Oka benzesin diye ucu sivri
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor = Color.red;
        aimLine.endColor = Color.yellow;
        aimLine.positionCount = 2;
        aimLine.enabled = false;
    }

    private UnityEngine.UI.Image slowMoOverlay;
    void SetupSlowMoOverlay()
    {
        // Ekranı kaplayacak bir UI Canvas ve Gri Resim oluşturur
        GameObject canvasObj = new GameObject("SlowMoCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        GameObject imageObj = new GameObject("OverlayImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        slowMoOverlay = imageObj.AddComponent<UnityEngine.UI.Image>();
        slowMoOverlay.color = new Color(0.1f, 0.1f, 0.1f, 0f); // Başlangıçta Görünmez
        
        RectTransform rt = slowMoOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (currentState == PlayerState.BeingEaten) return; // Yutulurken tuşlar engelleniyor!

        UpdateTimers();
        CheckStateTransitions(keyboard); 

        switch (currentState)
        {
            case PlayerState.Ground:
                HandleGroundMovement(keyboard);
                HandleGroundCombat(keyboard, mouse);
                HandleBigDashAiming(keyboard, mouse); // Yeni V TUŞU Mekaniği!
                HandleDash(keyboard);
                break;

            case PlayerState.Dashing:
                break;

            case PlayerState.OnWall:
                HandleWallMovement(keyboard);
                HandleSlowMoAiming(keyboard, mouse); 
                HandleBigDashAiming(keyboard, mouse); // Duvardan da BigDash atılabilir
                break;

            case PlayerState.OnEnemy:
                HandleMountedMovement(keyboard);
                HandleMountedCombat(mouse);
                HandleSlowMoAiming(keyboard, mouse); 
                HandleEnemyExecution(mouse); // Düşmandayken Sağ tık var olan eziş sisteminde devam eder
                break;
        }

        UpdateSlowMoVisual();
        UpdateAnimator(); 
    }

    void UpdateSlowMoVisual()
    {
        if (slowMoOverlay != null)
        {
            // Zaman yavaşlamışsa alfa değerini 0.6 yapıp ekranı grileştirir, değilse 0'a çekip şeffaflaştırır
            float targetAlpha = Time.timeScale < 0.99f ? 0.6f : 0f; 
            Color c = slowMoOverlay.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * 10f); // Smooth geçiş
            slowMoOverlay.color = c;
        }
    }

    void FixedUpdate()
    {
        switch (currentState)
        {
            case PlayerState.Ground:
                rb.linearVelocity = movement * moveSpeed;
                break;

            case PlayerState.Dashing:
                rb.linearVelocity = dashDirection * dashSpeed;
                break;

            case PlayerState.OnWall:
                rb.linearVelocity = movement * wallMoveSpeed; 
                break;
                
            case PlayerState.OnEnemy:
                rb.linearVelocity = Vector2.zero; 
                break;
                
            case PlayerState.BeingEaten:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    void UpdateTimers()
    {
        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;
        
        if (comboTimer > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
            {
                comboStep = 0; 
            }
        }
    }

    // ---------------- HAREKET: GROUND & DASH ----------------

    void HandleGroundMovement(Keyboard keyboard)
    {
        // Hedef alma esnasında karakter durur
        if (isAiming || isBigDashAiming) 
        {
            movement = Vector2.zero;
            return;
        }

        float moveX = 0f;
        float moveY = 0f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY -= 1f;

        movement = new Vector2(moveX, moveY).normalized;

        if (movement.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(movement.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void HandleDash(Keyboard keyboard)
    {
        if (keyboard.shiftKey.wasPressedThisFrame && dashCooldownTimer <= 0 && movement.magnitude > 0)
        {
            currentState = PlayerState.Dashing;
            isBigDashing = false;
            currentDashTime = 0f;
            dashDirection = movement; 
            
            float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
            if (transform.localScale.x < 0) angle += 180f;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (anim != null) 
            {
                anim.SetTrigger("Dashing"); 
                anim.SetBool("IsDashingBool", true); 
            }
            Invoke(nameof(StopDash), dashDuration);
        }
    }

    void StopDash()
    {
        if (currentState == PlayerState.Dashing)
        {
            currentState = PlayerState.Ground;
            isBigDashing = false; // Reset
            lastAttachedObject = null; // Yere indiğinde hafızayı sıfırla ki duvarı/düşmanı unutmasın
            dashCooldownTimer = dashCooldown;
            transform.rotation = Quaternion.identity;
            if (anim != null) anim.SetBool("IsDashingBool", false);
            
            if (!isAiming && !isBigDashAiming) StopAimingTimeOnly();
        }
    }

    void HandleWallMovement(Keyboard keyboard)
    {
        if (isAiming || isBigDashAiming) 
        {
            movement = Vector2.zero;
            return;
        }

        float moveX = 0f;
        float moveY = 0f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY -= 1f;

        movement = new Vector2(moveX, moveY).normalized;
    }

    // ---------------- YENİ: DÜŞMAN KONTROLÜ (MIND CONTROL) ----------------

    [HideInInspector] public bool isExecutingMounting = false;

    void HandleMountedMovement(Keyboard keyboard)
    {
        if (isAiming || isBigDashAiming || isExecutingMounting) return; 
        if (mountedEnemy != null && mountedEnemy.isEating) return; 

        float moveX = 0f;
        float moveY = 0f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveY += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveY -= 1f;

        Vector2 moveDir = new Vector2(moveX, moveY).normalized;
        if (mountedEnemy != null)
        {
            mountedEnemy.MoveControl(moveDir);
        }

        // Karakterin de yönünü döndür (Görsel düzeltme)
        if (moveX != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(moveX) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void HandleMountedCombat(Mouse mouse)
    {
        if (isExecutingMounting) return;

        if (mouse.leftButton.wasPressedThisFrame && !isAiming && !isBigDashAiming)
        {
            if (mountedEnemy != null) mountedEnemy.AttackControl();
        }
    }

    // ---------------- YENİ: BIG DASH (V TUŞU SİLAHI) ----------------

    void HandleBigDashAiming(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.vKey.wasPressedThisFrame && !isAiming && !isBigDashAiming)
        {
            isBigDashAiming = true;
            bigDashAimTimer = 0f;
            
            // Zaman Yavaşlar
            Time.timeScale = slowMotionTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
            
            if (aimLine != null) aimLine.enabled = true;
        }

        if (isBigDashAiming)
        {
            bigDashAimTimer += Time.unscaledDeltaTime; // Unscaled: Yavaşlamadan etkilenmeden gerçek 2 saniye!
            
            Camera mainCam = Camera.main;
            if (mainCam != null && aimLine != null)
            {
                Vector2 mousePos = mainCam.ScreenToWorldPoint(mouse.position.ReadValue());
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, mousePos);
            }

            // Eğer "V" Tuşunu bırakırsa VEYA 2 saniye dolarsa ZORLA atıl!
            if (keyboard.vKey.wasReleasedThisFrame || bigDashAimTimer >= bigDashAimMaxTime)
            {
                ExecuteBigDash(mouse);
            }
        }
    }

    void ExecuteBigDash(Mouse mouse)
    {
        isBigDashAiming = false;
        if (aimLine != null) aimLine.enabled = false;
        
        // Zamanı Normalleştir
        StopAimingTimeOnly();

        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        
        // Eğer binek üzerindeysek önce ondan inelim ve fiziği açalım
        if (currentState == PlayerState.OnEnemy && mountedEnemy != null)
        {
            mountedEnemy.OnPlayerDismounted();
            mountedEnemy = null;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        Vector2 mousePos = mainCam.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 attackDir = (mousePos - (Vector2)transform.position).normalized;

        currentState = PlayerState.Dashing;
        isBigDashing = true; // SONSUZA KADAR DUVAR DELEN UÇUŞ AKTİF!
        currentDashTime = 0f;
        dashDirection = attackDir;
        dashSpeed = bigDashSpeed; 
        
        float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
        if (transform.localScale.x < 0) angle += 180f;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (anim != null) 
        {
            anim.SetTrigger("Dashing"); 
            anim.SetBool("IsDashingBool", true);
        }

        // ÖNEMLİ: Dash'i durduran Invoke çağırmıyoruz! Duvar bulana kadar sadece uçar!
        // Failsafe: Haritadan dışarı düşerse diye max 7 saniye süre konulur gizli
        Invoke(nameof(StopDash), 7f); 
    }

    // ---------------- COMBAT: KOMBO VE SAĞ TIK ----------------

    void HandleGroundCombat(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (anim != null) anim.SetTrigger("AttackingGround");
            PerformDamage(groundAttackRadius, groundAttackDamage);
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && !isBigDashAiming)
        {
            comboTimer = comboWindowTime;
            comboStep++;

            if (comboStep == 1)
            {
                if (anim != null) anim.SetTrigger("Attacking");
                PerformDamage(attack1Radius, attack1Damage);
            }
            else if (comboStep == 2)
            {
                if (anim != null) anim.SetTrigger("Attacking2");
                PerformDamage(attack2Radius, attack2Damage);
            }
            else if (comboStep >= 3)
            {
                if (anim != null) anim.SetTrigger("Attacking3");
                PerformDamage(attack3Radius, attack3Damage);
                comboStep = 0; 
            }
        }
    }

    void HandleEnemyExecution(Mouse mouse)
    {
        if (isExecutingMounting) return;

        if (mouse.rightButton.wasPressedThisFrame && mountedEnemy != null)
        {
            if (isAiming) StopAimingTimeOnly(); 

            isExecutingMounting = true; // Karakteri kilitle 
            
            // Düşmanın kendi içindeki 2 saniyelik bükülme/ezilme ölüm dizisine sokuyoruz
            mountedEnemy.StartSquashDeath(2f);

            // Oyuncuyu tam olarak düşmanın animasyonu bitene (2 saniye) kadar kilitleriz ki üstünden düşmesin
            Invoke(nameof(FreeExecutionLock), 2f); 

            if (anim != null) 
            {
                anim.SetTrigger("ExecutionKill"); 
            }
        }
    }

    void FreeExecutionLock()
    {
        isExecutingMounting = false; // Karakter tekrar hareket edebilir
        
        if (currentState == PlayerState.OnEnemy && mountedEnemy != null)
        {
            mountedEnemy.OnPlayerDismounted(); // Erken bırak (düşman ezilmeye devam edecek)
            mountedEnemy = null;
            
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true; // Yere inebilmesi için fiziği açıyoruz
        }
    }

    // ---------------- Z TUŞU SLOW-MO (HOMING) ----------------

    void HandleSlowMoAiming(Keyboard keyboard, Mouse mouse)
    {
        if (isExecutingMounting) return;

        if (keyboard.zKey.wasPressedThisFrame && !isBigDashAiming)
        {
            isAiming = true;
            Time.timeScale = slowMotionTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale; 
            
            if (aimLine != null) aimLine.enabled = true; // Z tuşunda da oku göster
        }

        if (isAiming)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null && aimLine != null)
            {
                Vector2 mousePos = mainCam.ScreenToWorldPoint(mouse.position.ReadValue());
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, mousePos);
            }
        }

        if (isAiming && keyboard.zKey.wasReleasedThisFrame)
        {
            if (aimLine != null) aimLine.enabled = false; // Oku gizle
            
            ExecuteHomingDash(mouse);
            
            isAiming = false;
            StopAimingTimeOnly();
        }
    }

    void StopAimingTimeOnly()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    void ExecuteHomingDash(Mouse mouse)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        
        Vector2 mousePos = mainCam.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 attackDir = (mousePos - (Vector2)transform.position).normalized;
        
        if (currentState == PlayerState.OnEnemy && mountedEnemy != null)
        {
            mountedEnemy.OnPlayerDismounted();
            mountedEnemy = null;

            // FİZİKLERİ GERİ AÇ (Homing dash atarak binek terkedildi)
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        currentState = PlayerState.Dashing;
        isBigDashing = false;
        currentDashTime = 0f;
        dashDirection = attackDir;
        dashSpeed = dashAttackForce; 
        
        float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
        if (transform.localScale.x < 0) angle += 180f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        
        if (anim != null) 
        {
            anim.SetTrigger("WallAttackingBig"); 
            anim.SetBool("IsDashingBool", true);
        }
        
        Invoke(nameof(StopDash), dashDuration * 1.5f); 
        
        PerformDamage(dashAttackRadius, dashAttackDamage);
    }

    // ---------------- DURUM YÖNETİMİ & TETİKLEYİCİLER ----------------

    void CheckStateTransitions(Keyboard keyboard)
    {
        if (currentState == PlayerState.Dashing)
        {
            currentDashTime += Time.deltaTime;

            // A - DÜŞMAN KONTROLÜ (Dash atarken radar genişletildi, çünkü karakter ışınlanacak kadar hızlı uçuyor!)
            Collider2D[] enemyHits = Physics2D.OverlapCircleAll(transform.position, wallCheckDistance * 2.5f); 
            foreach(var eHit in enemyHits)
            {
                if (eHit.gameObject == this.gameObject) continue;
                
                // Unity Tag'i "Enemy" mi VEYA üzerinde Enemy scripti var mı?
                Enemy detectedEnemy = eHit.GetComponent<Enemy>();
                if (!eHit.CompareTag("Enemy") && detectedEnemy == null) continue;

                // Üstünden yeni atladığımız düşmana bu uçuş boyunca bir daha YAPILAMAZ (Sürekli aynı düşmana sekmeyi önler)
                if (eHit.gameObject == lastAttachedObject) continue;

                if (detectedEnemy != null && !detectedEnemy.isMounted)
                {
                    // Artık her dash (BigDash de dahil) düşmana çarpınca durur ve binek (mind control) moduna geçer!
                    isBigDashing = false; // Duvara kadar beklemeden dash'i iptal et
                    MountToEnemy(detectedEnemy);
                    return; // Uçuş biter, kafasına konulur
                }
            }

            // B - DUVAR KONTROLÜ
            Collider2D[] wallHits = Physics2D.OverlapCircleAll(transform.position, wallCheckDistance); // Layer sınırı kaldırıldı
            bool foundRealWall = false;
            foreach (var wHit in wallHits)
            {
                if (wHit.gameObject == this.gameObject) continue;
                
                // Mükemmel bir şekilde "Wall" Tag kontrolü
                if (!wHit.CompareTag("Wall")) continue; 

                if (wHit.gameObject == lastAttachedObject && currentDashTime < 0.1f) continue; 
                
                foundRealWall = true;
                lastAttachedObject = wHit.gameObject; 
                break;
            }

            if (foundRealWall)
            {
                // Duvara Çaptığımızda:
                isBigDashing = false; // Sonsuz uçuşu devreden çıkar
                currentState = PlayerState.OnWall;
                transform.rotation = Quaternion.identity;
                CancelInvoke(nameof(StopDash)); 
                if (anim != null)
                {
                    anim.SetTrigger("WallTrans");
                    anim.SetBool("IsDashingBool", false);
                }
            }
        }
        
        else if (currentState == PlayerState.OnWall)
        {
            if (keyboard != null && keyboard.shiftKey.wasPressedThisFrame && dashCooldownTimer <= 0)
            {
                currentState = PlayerState.Ground;
                lastAttachedObject = null; 
                dashCooldownTimer = dashCooldown; 
            }
            
            Collider2D[] wallChecks = Physics2D.OverlapCircleAll(transform.position, wallCheckDistance * 1.5f);
            bool isStillOnWall = false;
            foreach (var w in wallChecks)
            {
                if (w.gameObject != this.gameObject && w.CompareTag("Wall"))
                {
                    isStillOnWall = true;
                    lastAttachedObject = w.gameObject; 
                    break;
                }
            }

            if (!isStillOnWall) 
            {
                currentState = PlayerState.Ground;
                lastAttachedObject = null; 
            }
        }

        else if (currentState == PlayerState.OnEnemy)
        {
            if (mountedEnemy == null)
            {
                currentState = PlayerState.Ground;
                lastAttachedObject = null; 
                
                // Binek ölmüş veya kaybolmuşsa FİZİĞİ GERİ AÇ
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = true;

                isExecutingMounting = false; // Bükülme bittiği için kilidi kaldır
                StopAimingTimeOnly(); 
                return;
            }

            // Pozisyon takibi LateUpdate'e taşındı (Jitter glitch bug fix)
        }
    }

    void MountToEnemy(Enemy enemy)
    {
        currentState = PlayerState.OnEnemy;
        
        // Atladığı AN HIZI ANINDA KESİYORUZ
        if (rb != null) rb.linearVelocity = Vector2.zero;
        
        // FİZİKLERİ KAPAT: Player ve Enemy üst üste binerse fizik motoru ikisini beraber aşağı sürükler/iter
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        transform.rotation = Quaternion.identity;
        CancelInvoke(nameof(StopDash)); 

        mountedEnemy = enemy;
        lastAttachedObject = enemy.gameObject; 

        // Atladığı an beklemeden kafasına yapıştırıyoruz
        if (mountedEnemy.mountPoint != null) 
        {
            transform.position = mountedEnemy.mountPoint.position;
        }
        else 
        {
            SpriteRenderer esr = mountedEnemy.GetComponentInChildren<SpriteRenderer>();

            if (esr != null)
            {
                transform.position = new Vector3(mountedEnemy.transform.position.x, esr.bounds.max.y + mountYOffset, transform.position.z);
            }
            else
            {
                transform.position = mountedEnemy.transform.position;
            }
        }

        mountedEnemy.OnPlayerMounted(); 

        if (anim != null)
        {
            anim.SetTrigger("WallTrans"); 
            anim.SetBool("IsDashingBool", false);
        }
    }

    void UpdateAnimator()
    {
        if (anim == null) return;

        bool isMoving = movement.magnitude > 0;

        if (currentState == PlayerState.Ground)
        {
            anim.SetBool("Walking", isMoving);
        }
        else if (currentState == PlayerState.Dashing)
        {
            anim.SetBool("Walking", false);
        }
        else if (currentState == PlayerState.OnWall)
        {
            anim.SetBool("Walking", false);
        }
        else if (currentState == PlayerState.OnEnemy)
        {
            anim.SetBool("Walking", false);
        }
    }

    private void PerformDamage(float radius, float damageAmount)
    {
        Vector2 damagePos = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(damagePos, radius);
        foreach (var enemy in hitEnemies)
        {
            Enemy eScript = enemy.GetComponent<Enemy>();
            // Tag (Etiket) kontrolü olmadan sadece "Enemy" kodunu taşıyorsa hasar alır, böylece unutkanlık hatalarının önüne geçilir.
            if (eScript != null) 
            {
                eScript.TakeDamage(damageAmount);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 pos = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, attack1Radius); 

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, groundAttackRadius); 

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, wallCheckDistance); 
    }

    // ---------------- YENİ: DÜŞMAN TARAFINDAN YUTULMA (ÖLÜM) ----------------

    public void GetEatenBy(Enemy enemy)
    {
        // Eğer o an bir canavara biniyorsak, o canavardan güvenle koparız 
        if (mountedEnemy != null)
        {
            mountedEnemy.OnPlayerDismounted();
            mountedEnemy = null;
        }

        currentState = PlayerState.BeingEaten;
        movement = Vector2.zero;
        dashDirection = Vector2.zero;
        isAiming = false;
        isBigDashAiming = false;
        StopAimingTimeOnly();

        // FİZİKLERİ VE ÇARPIŞMAYI KAPATIYORUZ 
        // Yoksa düşmanın içine çekilirken 2 adet fiziksel obje (Player ve Enemy) üst üste biner
        // Bu yüzden fizik motoru Player'ı mermi gibi uzağa fırlatıp hata verir!
        if (rb != null) rb.simulated = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        StartCoroutine(EatenSequence(enemy));
    }

    private System.Collections.IEnumerator EatenSequence(Enemy enemy)
    {
        // 1- Karakteri düşmanın içine doğru HIZLICA çek.
        // Hızınızı isterseniz düşman objesi içinden ayarlayabilirsiniz. Fakat animasyona yetişmesi için hızı x4 yapıyorum.
        float suctionSpeed = enemy != null ? enemy.eatSuctionSpeed * 4f : 15f; 
        while (enemy != null && Vector2.Distance(transform.position, enemy.transform.position) > 0.1f)
        {
            transform.position = Vector2.MoveTowards(transform.position, enemy.transform.position, Time.unscaledDeltaTime * suctionSpeed);
            yield return null;
        }

        // 2- DÜŞMANIN MİDESİNE ULAŞTIK: Karakteri GÖRÜNMEZ yap! (Gerçekten Yutuldu)
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false; 

        // 3- Zamanın Bozulması Efekti (Glitch Death)
        Time.timeScale = 0.05f; 
        Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
        if (anim != null) anim.speed = 0f; 

        // 4- Bütün bu çarpık izlenimi 1 Saniye (Gerçek saatte) yaşat
        yield return new WaitForSecondsRealtime(1f);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        
        CheckpointManager cpManager = Object.FindAnyObjectByType<CheckpointManager>();
        if (cpManager != null) 
        {
            cpManager.RestartLevel();
        }
        else 
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void GetBurned()
    {
        if (currentState == PlayerState.BeingEaten) return;

        if (mountedEnemy != null)
        {
            mountedEnemy.OnPlayerDismounted();
            mountedEnemy = null;
        }

        currentState = PlayerState.BeingEaten; // Oyunu dondurur/komutları keser
        movement = Vector2.zero;
        dashDirection = Vector2.zero;
        isAiming = false;
        isBigDashAiming = false;
        StopAimingTimeOnly();

        if (rb != null) rb.simulated = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        StartCoroutine(BurnedSequence());
    }

    private System.Collections.IEnumerator BurnedSequence()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        if (sr != null) sr.color = new Color(1f, 0.4f, 0f);

        Time.timeScale = 0.2f; 
        Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;

        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while(elapsed < 0.5f) // Gerçek zamanda yaklaşık 2-3 saniye sürer timeScale 0.2 olduğu için
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / 0.5f;

            transform.localScale = Vector3.Lerp(startScale, startScale * 0.1f, t);
            if (sr != null) sr.color = Color.Lerp(new Color(1f, 0.4f, 0f), Color.black, t);
            
            yield return null;
        }

        if (sr != null) sr.enabled = false;

        yield return new WaitForSecondsRealtime(0.5f);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        
        CheckpointManager cpManager = Object.FindAnyObjectByType<CheckpointManager>();
        if (cpManager != null) 
        {
            cpManager.RestartLevel();
        }
        else 
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }

    void LateUpdate()
    {
        // GÖRSEL TİTREŞİMİ ENGELLEME: Sadece Enemy fizik hesabını tamamladıktan SONRA kameraya ve karaktere işlenmesini sağlar
        if (currentState == PlayerState.OnEnemy && mountedEnemy != null)
        {
            if (mountedEnemy.mountPoint != null) 
            {
                transform.position = mountedEnemy.mountPoint.position;
            }
            else 
            {
                SpriteRenderer esr = mountedEnemy.GetComponentInChildren<SpriteRenderer>();
                if (esr != null)
                {
                    transform.position = new Vector3(mountedEnemy.transform.position.x, esr.bounds.max.y + mountYOffset, transform.position.z);
                }
                else
                {
                    transform.position = mountedEnemy.transform.position; 
                }
            }
        }
    }
}