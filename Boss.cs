using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss : MonoBehaviour
{
    //Boss fight state machine
    //Boss has four different attacks spread across two phases
    //Boss has four heads with each head getting an attack
    //When a head is destroyed, that attack is disabled from the random choice
    //When there is one head left, boss is enraged

    //Blues attack = 0
    //Reds attack = 1
    //Pinks attack = 2

    public int redsKilled { get; private set;}
    public int bluesKilled { get; private set; }
    public int pinksKilled { get; private set; }
    public int clydesKilled { get; private set; }

    public static bool bossDead { get; private set; }

    //Struct describing the attack choices that boss can choose from
    struct AttackChoice
    {
        public int attackID;
        public float weight;
        public AttackChoice(int id, float weight)
        {
            attackID = id;
            this.weight = weight;
        }
    }

    public struct BossHitInformation
    {
        public int pointWorth;
    }

    //A struct full of different settings
    //Allows for multiple difficulties with custom settings each
    [System.Serializable]
    public struct DifficultySettings
    {
        public int difficultyLevel;

        [Header("Movement Settings")]
        public float movementSpeed;
        public float dashSpeed;
        public float movementPhaseLength;
        [Range(0, 1)] public float dashChance;
        public float dashPhaseLength;
        [Header("Attack Settings")]
        public float normalAttackCooldown;
        public float enrageAttackCooldown;
        public float slamAttackCooldown;
        public float attackLeading;
        public float redsAttackDuration;
        public float redsMaxRange;
        [Range(0, 100)] public float redsRotationSmoothing;
        [Header("Slam Settings")]
        public float slamThreshold;
        public float slamStunLength;
        [Header("Damage Settings")]
        public float baseDamage;
        [Tooltip("Multiplier to the damage based on the amount of ghosts of a particular kind you kill before shooting the corresponding head")]
        public float patienceMultiplier;
    }

    //Enum for boss state machine
    public enum BossState { Initial, Chase, AttackCooldown, Dash, Slam, Attack, Enrage, Death, InvisbilityPowerup }
    public BossState currentState { get; private set; }

    PlayerController player;

    Animator animator;
    Rigidbody rb;
    BossVFX bossVFX;

    Vector3 startPosition;

    [SerializeField] BossSpawner bossSpawner; //Reference to spawner for other enemies
    [SerializeField] BossfightEndController endController; //Reference for boss fight end controller
    [SerializeField] float startDelay = 5f; //Delay to start the boss fight

    //Boss has 4 different heads, each for their own health
    [Header("Boss Heads")]
    [SerializeField] BossHead bluesHead;
    [SerializeField] BossHead redsHead;
    [SerializeField] BossHead pinksHead;
    [SerializeField] BossHead clydeHead;

    //All of the different difficulty settings
    [Header("Difficulty Settings")]
    [SerializeField] DifficultySettings[] difficultySettings = new DifficultySettings[3];

    [Header("Movement Settings")]
    [SerializeField] [Range(0,100)] float rotationSmoothing = 0.5f; //Smoothing for boss facing the player
    [SerializeField] float moveCamShakeStrength = 0.5f; //Camera shake settings for movement
    [SerializeField] float moveCamShakeDropoffDist = 10f;

    //Necessary references for all of the different attacks
    [Header("Attack Setup")]
    [SerializeField] Transform bluesProjSpawn;
    [SerializeField] Transform[] pinksProjSpawn;
    [SerializeField] Transform clydeProjSpawn;
    [SerializeField] GameObject bluesProjectile;
    [SerializeField] GameObject pinksProjectile;
    [SerializeField] GameObject clydeProjectile;
    [SerializeField] GameObject redsLaser;
    [SerializeField] LineRenderer[] redsLasers;
    [SerializeField] Transform[] redsLaserEnds;
    [SerializeField] Transform redsRaycastOrigin;

    [Header("Attack Settings")]
    [SerializeField] float pinksSpread = 0.07f;
    [SerializeField] LayerMask redsLayerMask; //Layer mask for the Reds attack

    [Header("Slam Settings")]
    [SerializeField] float slamStrength = 100;
    [SerializeField] float slamCamShakeStrength = 1f;
    [SerializeField] float slamCamShakeDropoffDist = 20f;

    //References for the score popups when damaging the boss
    [Header("Score Display Settings")]
    [SerializeField] GameObject scoreIncrementPrefab;
    [SerializeField] BoxCollider scoreIncrementSpawnArea;

    [Header("Damage Settings")]
    [SerializeField] GameObject bigBluesBloodPrefab;
    [SerializeField] GameObject smallBluesBloodPrefab;
    [SerializeField] GameObject bigRedsBloodPrefab;
    [SerializeField] GameObject smallRedsBloodPrefab;
    [SerializeField] GameObject bigPinksBloodPrefab;
    [SerializeField] GameObject smallPinksBloodPrefab;
    [SerializeField] GameObject bigClydeBloodPrefab;
    [SerializeField] GameObject smallClydeBloodPrefab;
    [SerializeField] int bloodAmountUponHeadDeath = 20; //Amount of physical blood particles to spawn when head is destroyed
    [SerializeField] int bloodAmountUponDeath = 40; //Amount of physical blood particles to spawn when boss dies
    [SerializeField] float respawnDelay = 2f;
    [SerializeField] BoxCollider bluesBloodSpawnArea;
    [SerializeField] BoxCollider pinksBloodSpawnArea;
    [SerializeField] BoxCollider redsBloodSpawnArea;
    [SerializeField] BoxCollider clydeBloodSpawnArea;
    [SerializeField] GameObject shieldPrefab;

    //Audio source references
    [Header("Audio Sources")]
    [SerializeField] AudioSource enrageSound;
    [SerializeField] AudioSource stepSound;
    [SerializeField] AudioSource stepSound2;
    [SerializeField] AudioSource redsChargeSound;
    [SerializeField] AudioSource redsReleaseSound;
    [SerializeField] AudioSource bluesChargeSound;
    [SerializeField] AudioSource bluesReleaseSound;
    [SerializeField] AudioSource pinksReleaseSound;
    [SerializeField] AudioSource clydeChargeSound;
    [SerializeField] AudioSource clydeReleaseSound;
    [SerializeField] AudioSource slamSound;
    [SerializeField] AudioSource bossHit;
    [SerializeField] AudioSource bossDeath;
    [SerializeField] AudioSource bossDeflect;
    [SerializeField] AudioSource redsDeath;
    [SerializeField] AudioSource bluesDeath;
    [SerializeField] AudioSource pinksDeath;
    [SerializeField] AudioSource clydeDeath;
    [SerializeField] AudioSource shieldBreak;

    DifficultySettings currentDifficultySettings;

    AttackChoice[] attackChoices;
    bool isAttacking = false;

    float attackCooldownTimer;

    float movementPhaseTimer = 0;
    float dashStartTimer = 0;

    bool movementPhaseStarted = false;

    bool canMove = true;

    public int damage { get 
        {
            int d = (bluesHead.dead ? 1 : 0) + ((redsHead.dead ? 1 : 0) << 1) + ((pinksHead.dead ? 1 : 0) << 2);
            return d; 
        } 
    }

    public float BossHealth {
        get
        {
            return bluesHead.Health + redsHead.Health + pinksHead.Health + clydeHead.Health;
        }
    }
    public float BossMaxHealth
    {
        get
        {
            return bluesHead.MaxHealth + redsHead.MaxHealth + pinksHead.MaxHealth + clydeHead.MaxHealth;
        }
    }

    int currentAttack = 0;

    //Initialize the boss
    private void Start()
    {
        if (Score.difficulty < difficultySettings.Length)
        {
            currentDifficultySettings = difficultySettings[Score.difficulty];
        }
        else
        {
            currentDifficultySettings = difficultySettings[0];
        }

        currentState = BossState.Initial;

        Invoke("Initial", startDelay);

        startPosition = transform.position;

        redsKilled = 0;
        bluesKilled = 0;
        pinksKilled = 0;
        clydesKilled = 0;

        bossDead = false;

        AttackChoice blues = new AttackChoice(0, 1f);
        AttackChoice reds = new AttackChoice(1, 1f);
        AttackChoice pinks = new AttackChoice(2, 1f);
        attackChoices = new AttackChoice[3];
        attackChoices[0] = blues;
        attackChoices[1] = reds;
        attackChoices[2] = pinks;

        player = FindObjectOfType<PlayerController>();

        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        bossVFX = GetComponent<BossVFX>();

        currentRotationSmoothing = rotationSmoothing;
    }

    private void Update()
    {
        Act();
        LookAtPlayer();
    }

    float currentRotationSmoothing;
    void LookAtPlayer()
    {
        Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
        dirToPlayer.y = 0;
        Quaternion rot = Quaternion.FromToRotation(transform.forward, dirToPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot * transform.rotation, currentRotationSmoothing * Time.deltaTime);
    }

    //Increment the kill count of one the grunt enemies
    //Deactivate the shield of the corresponding head
    public void IncrementKillCount(int id)
    {
        switch (id)
        {
            case 0:
                bluesKilled++;
                bluesHead.DeactivateShield();
                break;
            case 1:
                redsKilled++;
                redsHead.DeactivateShield();
                break;
            case 2:
                pinksKilled++;
                pinksHead.DeactivateShield();
                break;
            case 3:
                clydesKilled++;
                clydeHead.DeactivateShield();
                break;
        }
        if (shieldBreak) shieldBreak.Play();
    }

    void Act()
    {
        //State machine
        switch(currentState)
        {
            case BossState.Initial:
                break;
            case BossState.AttackCooldown:
                AttackCooldown();
                break;
            case BossState.Chase:
                Chase();
                break;
            case BossState.Dash:
                break;
            case BossState.Attack:
                Attack();
                break;
            case BossState.Slam:
                break;
            case BossState.Enrage:
                break;
            case BossState.Death:
                break;
            case BossState.InvisbilityPowerup:
                InvisibilityPowerup();
                break;
        }
    }

    //Start the boss fight
    void Initial()
    {
        currentState = BossState.Chase;
    }

    //Move straight towards the player for a random amount of time, randomly chosing to dash left or right at a random time
    void Chase()
    {
        if (movementPhaseStarted)
        {
            animator.SetInteger("xDir", 0);
            animator.SetInteger("yDir", 1);

            if (canMove)
            {
                animator.SetBool("isWalking", true);

                rb.velocity = transform.forward * currentDifficultySettings.movementSpeed * (damage == 7 ? 1.10f : 1f) * Mathf.Min(0.1f,Time.deltaTime);
            }

            movementPhaseTimer -= Time.deltaTime;
            dashStartTimer -= Time.deltaTime;

            //Initiate the slam because the player got too close
            float distToPlayer = (player.transform.position - transform.position).magnitude;
            if (distToPlayer < currentDifficultySettings.slamThreshold)
            {
                if (canMove)
                    rb.velocity = Vector3.zero;
                currentState = BossState.Slam;
                SlamInitiate();

                animator.SetBool("isWalking", false);
            }

            //Movement phase has ended so attack
            if(movementPhaseTimer <= 0)
            {
                if (canMove)
                    rb.velocity = Vector3.zero;

                currentState = BossState.Attack;
                movementPhaseStarted = false;

                animator.SetBool("isWalking", false);
            }

            //Initiate a dash
            if(dashStartTimer <= 0)
            {
                currentState = BossState.Dash;

                dashCoroutine = StartCoroutine(Dash());
            }
        } else
        {
            movementPhaseStarted = true;
            movementPhaseTimer = Random.Range(0, currentDifficultySettings.movementPhaseLength * (damage == 7 ? 0.5f : 1f)); //Choose a random length for the movement phase
            dashStartTimer = Random.Range(0, 1f) < currentDifficultySettings.dashChance ? Random.Range(0, movementPhaseTimer) : float.MaxValue; //Choose whether to dash and a random time to dash
        }
    }

    //Coroutine for the dash sequence
    Coroutine dashCoroutine;
    IEnumerator Dash()
    {
        float timer = 0;

        float dashLength = currentDifficultySettings.dashPhaseLength * (damage == 7 ? 0.5f : 1f);

        float turnLength = dashLength * 0.25f;
        float straightLength = dashLength * 0.5f;

        //Move diagonally
        bool rightDash = Random.Range(0, 1f) > 0.5f;
        while(timer < turnLength)
        {
            animator.SetInteger("xDir", rightDash ? 1 : -1);
            animator.SetInteger("yDir", 1);
            if (canMove)
            {
                Vector3 dir = transform.TransformDirection(new Vector3(rightDash ? 1 : -1, 0, 1).normalized);
                rb.velocity = dir * currentDifficultySettings.dashSpeed * (damage == 7 ? 1.10f : 1f) * Time.deltaTime;
            }
            yield return null;
            timer += Time.deltaTime;
        }

        //Move sideways
        timer = 0;
        while (timer < straightLength)
        {
            animator.SetInteger("xDir", rightDash ? 1 : -1);
            animator.SetInteger("yDir", 0);

            if (canMove)
            {
                Vector3 dir = transform.TransformDirection(new Vector3(rightDash ? 1 : -1, 0, 0).normalized);
                rb.velocity = dir * currentDifficultySettings.dashSpeed * (damage == 7 ? 1.10f : 1f) * Time.deltaTime;
            }
            yield return null;
            timer += Time.deltaTime;
        }

        //Move diagonally
        timer = 0;
        while (timer < turnLength)
        {
            animator.SetInteger("xDir", rightDash ? 1 : -1);
            animator.SetInteger("yDir", 1);

            if (canMove)
            {
                Vector3 dir = transform.TransformDirection(new Vector3(rightDash ? 1 : -1, 0, 1).normalized);
                rb.velocity = dir * currentDifficultySettings.dashSpeed * (damage == 7 ? 1.10f : 1f) * Time.deltaTime;
            }
            yield return null;
            timer += Time.deltaTime;
        }

        dashStartTimer = float.MaxValue;
        if(currentState != BossState.InvisbilityPowerup) currentState = BossState.Chase;
    }

    public void InvisibililtyPowerupActivated()
    {
        currentState = BossState.InvisbilityPowerup;
    }
    public void InvisibilityPowerup()
    {
        if (dashStartTimer > 0)
        {
            animator.SetInteger("xDir", 0);
            animator.SetInteger("yDir", 1);

            if (canMove)
            {
                animator.SetBool("isWalking", true);

                rb.velocity = transform.forward * currentDifficultySettings.movementSpeed * (damage == 7 ? 1.10f : 1f) * Time.deltaTime * 0.5f;
            }
        }

        if (!PlayerController.invisibilityActivated)
        {
            currentState = BossState.Chase;
        }
    }

    //Apply camera shake when walking
    //Animation Event
    public void MovementCamShake()
    {
        float distToPlayer = (player.transform.position - transform.position).magnitude;
        float multiplier = Mathf.Clamp01(1 - (distToPlayer - 1.5f) / moveCamShakeDropoffDist);
        if(multiplier > 0)
            player.CamShake.ShakeCamera(moveCamShakeStrength * multiplier, 0.5f, 0.08f);

        if (!stepSound.isPlaying)
        {
            stepSound.pitch = Random.Range(0.9f, 1.1f);
            stepSound.Play();
        }
        else if (!stepSound2.isPlaying)
        {
            stepSound2.pitch = Random.Range(0.9f, 1.1f);
            stepSound2.Play();
        }
    }

    //Attack cooldown state
    private void AttackCooldown()
    {
        if(!isAttacking)
            attackCooldownTimer -= Time.deltaTime;

        if(attackCooldownTimer <= 0)
        {
            currentState = BossState.Chase;
        }
    }

    //Choose an attack based off of a weighted system and takes into account the active heads
    private void Attack()
    {
        if (damage == 7)
        {
            isAttacking = true;
            animator.SetTrigger("ClydeAttack");

            attackCooldownTimer = currentDifficultySettings.enrageAttackCooldown;
        }
        else
        {
            currentAttack = 0;

            //Weighted random choice
            float totalWeight = 0;
            for (int i = 0; i < attackChoices.Length; i++)
            {
                totalWeight += attackChoices[i].weight;
            }
            float rand = Random.Range(0.1f, totalWeight);
            for (int i = 0; i < attackChoices.Length; i++)
            {
                if (rand <= attackChoices[i].weight)
                {
                    currentAttack = attackChoices[i].attackID;
                    break;
                }
                rand -= attackChoices[i].weight;
            }

            for (int i = 0; i < attackChoices.Length; i++) //Reset weights if head is not dead
            {
                if(attackChoices[i].weight > 0)
                    attackChoices[i].weight = 1;
            }

            if (currentAttack == 0)
            {
                isAttacking = true;
                animator.SetTrigger("BluesAttack");
            }
            else if (currentAttack == 1)
            {
                animator.SetBool("RedsAttacking", true);
                animator.SetTrigger("RedsAttack");

                isAttacking = true;

                //Change rotation smoothing of the boss to a slower amount to prevent instant death from the reds laser
                currentRotationSmoothing = currentDifficultySettings.redsRotationSmoothing;
            }
            else if (currentAttack == 2)
            {
                isAttacking = true;
                animator.SetTrigger("PinksAttack");
            }

            attackCooldownTimer = currentDifficultySettings.normalAttackCooldown;
        }

        currentState = BossState.AttackCooldown;
    }

    public void StopAttacking()
    {
        isAttacking = false;
    }

    //Helper function to update the weight of a particular attack being chosen
    void ChangeAttackChoiceWeight(int attackID, float newWeight)
    {
        for (int i = 0; i < attackChoices.Length; i++)
        {
            if (attackChoices[i].attackID == attackID)
            {
                attackChoices[i].weight = newWeight;

                if(bossSpawner) bossSpawner.ChangeGhostWeight(attackID, newWeight);
            }
        }
    }
    
    //Animation Event
    private void PinksAttack()
    {
        pinksReleaseSound.Play();
        if(attackChoices[2].weight > 0)
            attackChoices[2].weight = 0.3f; //Add weight to current attack to make it less likely to happen twice in a row

        Vector3 dirToPlayer = (player.transform.position + player.transform.up * 0.3f + player.velocity * currentDifficultySettings.attackLeading - transform.position).normalized;
        Vector3 dirToPlayer2 = (player.transform.position + player.transform.up * 0.3f + player.velocity * currentDifficultySettings.attackLeading - player.transform.right * pinksSpread - transform.position).normalized;
        Vector3 dirToPlayer3 = (player.transform.position + player.transform.up * 0.3f + player.velocity * currentDifficultySettings.attackLeading + player.transform.right * pinksSpread - transform.position).normalized;
        Quaternion rot = Quaternion.FromToRotation(transform.forward, dirToPlayer);
        Quaternion rot2 = Quaternion.FromToRotation(transform.forward, dirToPlayer2);
        Quaternion rot3 = Quaternion.FromToRotation(transform.forward, dirToPlayer3);

        GameObject proj1 = Instantiate(pinksProjectile, pinksProjSpawn[0].position, transform.rotation);
        GameObject proj2 = Instantiate(pinksProjectile, pinksProjSpawn[1].position, transform.rotation);
        GameObject proj3 = Instantiate(pinksProjectile, pinksProjSpawn[2].position, transform.rotation);
        proj1.transform.rotation = rot * proj1.transform.rotation;
        proj2.transform.rotation = rot2 * proj2.transform.rotation;
        proj3.transform.rotation = rot3 * proj3.transform.rotation;
    }

    //Animation Event
    private void RedsAttack()
    {
        if (attackChoices[1].weight > 0)
            attackChoices[1].weight = 0.3f; //Add weight to current attack to make it less likely to happen twice in a row
        StartCoroutine(RedsLaser());
    }
    void PlayRedsChargeSound()
    {
        redsChargeSound.Play();
    }
    //Activate the reds laser for a certain amount of time
    IEnumerator RedsLaser()
    {
        redsLaser.gameObject.SetActive(true);

        redsReleaseSound.Play();

        float timer = 0;
        while (timer < currentDifficultySettings.redsAttackDuration)
        {
            if (redsHead.dead) break; //Cancel attack if reds head dies

            //Find the closest distance the laser should be shooting and update all positions
            float distance = currentDifficultySettings.redsMaxRange;
            RaycastHit hitInfo = new RaycastHit();
            if(Physics.Raycast(redsRaycastOrigin.position, redsRaycastOrigin.transform.forward, out hitInfo, currentDifficultySettings.redsMaxRange, redsLayerMask))
            {
                distance = hitInfo.distance;
            }

            for (int i = 0; i < redsLasers.Length; i++)
            {
                redsLasers[i].SetPosition(1, Vector3.forward * distance * .7f);
            }
            for (int i = 0; i < redsLaserEnds.Length; i++)
            {
                redsLaserEnds[i].localPosition = Vector3.forward * distance * .7f;
            }

            yield return null;
            timer += Time.deltaTime;
        }

        animator.SetBool("RedsAttacking", false);

        redsLaser.gameObject.SetActive(false);

        isAttacking = false;

        //Change rotation smoothing of the boss back to the normal amount
        currentRotationSmoothing = rotationSmoothing;

        float volume = redsReleaseSound.volume;
        timer = 0;
        while(timer < 0.2f)
        {
            timer += Time.deltaTime;
            redsReleaseSound.volume = volume * ((0.2f - timer) / 0.2f);

            yield return null;
        }

        redsReleaseSound.Stop();
        redsReleaseSound.volume = volume;
    }

    //Animation Event
    private void BluesAttack()
    {
        if (attackChoices[0].weight > 0)
            attackChoices[0].weight = 0.3f; //Add weight to current attack to make it less likely to happen twice in a row

        Instantiate(bluesProjectile, bluesProjSpawn.position, transform.rotation);
    }

    //Animation Event
    private void ClydeAttack()
    {
        Instantiate(clydeProjectile, clydeProjSpawn.position, transform.rotation);
    }

    //Start the slam animation
    private void SlamInitiate()
    {
        animator.SetTrigger("Slam");
    }
    //Animation Event
    private void SlamAttack()
    {
        Vector3 vectorToPlayer = player.transform.position - transform.position;
        float distToPlayer = vectorToPlayer.magnitude;
        Vector3 dirToPlayer = vectorToPlayer.normalized;
        if (distToPlayer <= currentDifficultySettings.slamThreshold)
        {
            //Player is within the distance threshold so is hit by the slam attack
            player.SlamHit(dirToPlayer * slamStrength * ((currentDifficultySettings.slamThreshold - distToPlayer) / 1.5f + 1), currentDifficultySettings.slamStunLength);
        }

        //Apply camera shake based off distance to player
        float multiplier = Mathf.Clamp01(1 - (distToPlayer - 1.5f) / slamCamShakeDropoffDist);
        if(multiplier > 0) player.CamShake.ShakeCamera(slamCamShakeStrength * multiplier, 0.5f, currentDifficultySettings.slamStunLength, false);

        //Switch to cooldown phase
        attackCooldownTimer = currentDifficultySettings.slamAttackCooldown;
        currentState = BossState.AttackCooldown;
    }

    //Boss was hit with the weapon
    public BossHitInformation GotHit(Vector3 hitPosition, int headID, float damageMultiplier = 1)
    {
        BossHitInformation hit = new BossHitInformation();
        hit.pointWorth = 0;

        if (!Score.bossTimerEnded)
        {
            //Apply damage to correct head
            switch (headID)
            {
                case 0:
                    hit.pointWorth = bluesHead.TakeDamage(currentDifficultySettings.baseDamage * damageMultiplier, currentDifficultySettings.patienceMultiplier, bluesKilled);
                    break;
                case 1:
                    hit.pointWorth = redsHead.TakeDamage(currentDifficultySettings.baseDamage * damageMultiplier, currentDifficultySettings.patienceMultiplier, redsKilled);
                    break;
                case 2:
                    hit.pointWorth = pinksHead.TakeDamage(currentDifficultySettings.baseDamage * damageMultiplier, currentDifficultySettings.patienceMultiplier, pinksKilled);
                    break;
                case 3:
                    hit.pointWorth = clydeHead.TakeDamage(currentDifficultySettings.baseDamage * damageMultiplier, currentDifficultySettings.patienceMultiplier, clydesKilled);
                    break;
            }
        }

        //No points awarded means the hit heads shield was active so spawn shield prefab
        if(hit.pointWorth <= 0)
        {
            GameObject obj = Instantiate(shieldPrefab, hitPosition + transform.forward * 0.2f, transform.rotation, transform);
            obj.transform.localRotation *= Quaternion.AngleAxis(Random.Range(0, 360f), Vector3.forward);
            Destroy(obj, 2.5f);

            if (bossDeflect != null) bossDeflect.Play();
        }
        else
        {
            if (bossHit != null) bossHit.Play();

            SpawnBlood(3, headID);
            StartCoroutine(Knockback(-transform.forward * Time.deltaTime * 10000, 0.1f));
        }

        //Points awarded means the hit head took damage because the shield was inactive
        if (hit.pointWorth > 0 && scoreIncrementPrefab != null)
        {
            Vector3 spawnPoint = scoreIncrementSpawnArea.gameObject.transform.position + new Vector3(
                Random.Range(-scoreIncrementSpawnArea.bounds.extents.x, scoreIncrementSpawnArea.bounds.extents.x),
                Random.Range(-scoreIncrementSpawnArea.bounds.extents.y, scoreIncrementSpawnArea.bounds.extents.y),
                Random.Range(-scoreIncrementSpawnArea.bounds.extents.z, scoreIncrementSpawnArea.bounds.extents.z)
                );

            GameObject go = Instantiate(scoreIncrementPrefab, spawnPoint, scoreIncrementPrefab.transform.rotation);
            ScoreIncrementDisplay display = go.GetComponent<ScoreIncrementDisplay>();
            if (display) display.SetText((hit.pointWorth).ToString());
        }

        //Return the points awarded
        return hit;
    }

    //Checks if the head ID is able to be damaged, meaning no shield active
    public bool CheckHeadDamagable(int id)
    {
        switch(id)
        {
            case 0:
                return !(bluesHead.ShieldActive || bluesHead.dead);
            case 1:
                return !(redsHead.ShieldActive || redsHead.dead);
            case 2:
                return !(pinksHead.ShieldActive || pinksHead.dead);
            case 3:
                return !(clydeHead.ShieldActive || clydeHead.dead);
        }
        return false;
    }

    //Head got killed
    public void HeadKilled(int headID)
    {
        int bloodAmount = bloodAmountUponHeadDeath;
        if(headID == 3)
        {
            bloodAmount = bloodAmountUponDeath;

            if (bossSpawner) bossSpawner.ChangeGhostWeight(3, 0);
        }

        SpawnBlood(bloodAmount, headID);

        if (damage == 7 && headID < 3) //All three bottoms got killed so enrage
            StartCoroutine(EnrageSequence());
        else if (damage == 7 && headID == 3) //All heads are dead so play death sequence
            StartCoroutine(DeathSequence());
        else //One of the bottom 3 heads got killed so update damage state
            animator.SetFloat("Damage", damage);

        if(headID < 3) //Deactivate the killed heads' attack
            ChangeAttackChoiceWeight(headID, 0);

        if(currentAttack == headID) {
            isAttacking = false;
        }

        //Player the appropriate death sound
        switch(headID)
        {
            case 0:
                if (bluesDeath) bluesDeath.Play();
                break;
            case 1:
                if (redsDeath) redsDeath.Play();
                break;
            case 2:
                if (pinksDeath) pinksDeath.Play();
                break;
            case 3:
                if (clydeDeath) clydeDeath.Play();
                break;
        }
    }

    //Animation Event for Enrage animation
    public void SetMaxDamage()
    {
        animator.SetFloat("Damage", damage);
    }

    //Apply knockback of given force for given duration
    IEnumerator Knockback(Vector3 force, float duration)
    {
        canMove = false;

        rb.velocity = Vector3.zero;
        rb.AddForce(force * Time.deltaTime, ForceMode.VelocityChange);

        float timer = 0;
        while(timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        canMove = true;
    }

    //Spawns the blood effects that fall to the ground
    void SpawnBlood(int bloodAmount, int headID)
    {
        GameObject bigBlood = bigBluesBloodPrefab;
        GameObject smallBlood = smallBluesBloodPrefab;

        Collider bloodSpawnArea = bluesBloodSpawnArea;
        switch (headID)
        {
            case 0:
                bigBlood = bigBluesBloodPrefab;
                smallBlood = smallBluesBloodPrefab;
                bloodSpawnArea = inkyBloodSpawnArea;
                break;
            case 1:
                bigBlood = bigRedsBloodPrefab;
                smallBlood = smallRedsBloodPrefab;
                bloodSpawnArea = redsBloodSpawnArea;
                break;
            case 2:
                bigBlood = bigPinksBloodPrefab;
                smallBlood = smallPinksBloodPrefab;
                bloodSpawnArea = pinksBloodSpawnArea;
                break;
            case 3:
                bigBlood = bigClydeBloodPrefab;
                smallBlood = smallClydeBloodPrefab;
                bloodSpawnArea = clydeBloodSpawnArea;
                break;
        }

        for (int i = 0; i < bloodAmount; i++)
        {
            Vector3 spawnPoint = bloodSpawnArea.gameObject.transform.position + new Vector3(
                Random.Range(-bloodSpawnArea.bounds.extents.x, bloodSpawnArea.bounds.extents.x),
                Random.Range(-bloodSpawnArea.bounds.extents.y, bloodSpawnArea.bounds.extents.y),
                Random.Range(-bloodSpawnArea.bounds.extents.z, bloodSpawnArea.bounds.extents.z)
            );

            if (Random.Range(0, 5) < 1)
            {
                Instantiate(smallBlood, spawnPoint, smallBlood.transform.rotation);
            }
            else
            {
                Instantiate(bigBlood, spawnPoint, bigBlood.transform.rotation);
            }
        }
    }

    //Coroutine for the enragement process of the boss
    //Happens when there is one head left
    IEnumerator EnrageSequence()
    {
        enrageSound.Play();
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);

        if (bossSpawner) bossSpawner.ChangeGhostWeight(3, 1);

        currentState = BossState.Enrage;
        yield return new WaitForSeconds(0.1f);

        isAttacking = false;
        animator.SetTrigger("Enrage");

        yield return new WaitForSeconds(2.5f);

        movementPhaseStarted = false;
        currentState = BossState.Chase;
    }

    //Coroutine for the death process of the boss
    IEnumerator DeathSequence()
    {
        bossDead = true;

        if (dashCoroutine != null) StopCoroutine(dashCoroutine);

        rb.velocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezePosition;

        currentState = BossState.Death;
        bossDeath.Play();
        yield return new WaitForSeconds(1f);
        
        animator.SetTrigger("Death");

        yield return new WaitForSeconds(3f);

        endController.StartEndSequence();
    }

    public void ResetBoss()
    {
        transform.position = startPosition;

        rb.velocity = Vector3.zero;

        currentState = BossState.Initial;
        Invoke("Initial", respawnDelay);
    }

    //Animation event
    void PlaySlamSound()
    {
        slamSound.Play();
    }
}
