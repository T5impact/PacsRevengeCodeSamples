using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    //Abstract class defining the blueprint for weapons
    //Makes the weapons modular and provides easy basis for developing new weapons

    //Scriptable object containing weapon data
    [SerializeField] protected WeaponInfo weaponInfo;

    public bool gunActivated { get; private set; }
    protected float gunTimer;

    protected PlayerController playerController;

    //Activating and deactivating the gun powerup after some time
    public void ActivateGun(PlayerController playerController, float gunTimeAmount)
    {
        this.playerController = playerController;
        //gunTimerCoroutine = StartCoroutine(GunTimer(gunTimeAmount));


        //gunActivated = true;
    }
    Coroutine gunTimerCoroutine;
    protected IEnumerator GunTimer(float gunTimeAmount)
    {
        gunTimer = gunTimeAmount;

        while (gunTimer >= 0)
        {
            gunTimer -= Time.deltaTime;
            yield return null;
        }

        StartCoroutine(playerController.DeactivateGun());
        gunTimer = gunTimeAmount;
    }
    public void DeactivateGun()
    {
        gunActivated = false;
    }

    public abstract void ResetWeapon();

    /// <summary>
    /// This event is called whenever the mouse button is initially pressed down
    /// </summary>
    public abstract void OnMouseDownEvent();
    /// <summary>
    /// This event is called for every frame the mouse button is pressed down
    /// </summary>
    public abstract void OnMouseEvent();
    /// <summary>
    /// This event is called for every frame the mouse button is not pressed down
    /// </summary>
    public abstract void OnNoMouseEvent();
    /// <summary>
    /// This event is called whenever the mouse button is lift up
    /// </summary>
    public abstract void OnMouseUpEvent();
    /// <summary>
    /// This event is called every frame
    /// </summary>
    public abstract void OnPassiveEvent();

    //Spawn blood particles based on the difficulty of the target area
    public virtual void SpawnBlood(GameObject bigBlood, GameObject smallBlood, Ghost.TargetAreaDifficulty difficulty, RaycastHit hit)
    {
        float spawnRadius = 0.5f;
        if (difficulty == Ghost.TargetAreaDifficulty.Easy)
        {
            spawnRadius = 0.2f;
            GameObject blood = smallBlood;
            for (int i = 0; i < 2; i++)
            {
                Instantiate(blood, hit.point +
                    hit.transform.right * Random.Range(-spawnRadius, spawnRadius) + hit.transform.up * Random.Range(-spawnRadius / 2, spawnRadius / 2), Quaternion.identity);
            }
        }
        else if (difficulty == Ghost.TargetAreaDifficulty.Medium)
        {
            GameObject blood = smallBlood;
            for (int i = 0; i < 3; i++)
            {
                Instantiate(blood, hit.point +
                    hit.transform.right * Random.Range(-spawnRadius, spawnRadius) + hit.transform.up * Random.Range(-spawnRadius / 2, spawnRadius / 2), Quaternion.identity);
            }

            blood = bigBlood;
            for (int i = 0; i < 1; i++)
            {
                Instantiate(blood, hit.point +
                    hit.transform.right * Random.Range(-spawnRadius, spawnRadius) + hit.transform.up * Random.Range(-spawnRadius / 2, spawnRadius / 2), Quaternion.identity);
            }
        }
        else
        {
            GameObject blood = smallBlood;
            for (int i = 0; i < 4; i++)
            {
                Instantiate(blood, hit.point +
                    hit.transform.right * Random.Range(-spawnRadius, spawnRadius) + hit.transform.up * Random.Range(-spawnRadius / 2, spawnRadius / 2), Quaternion.identity);
            }

            blood = bigBlood;
            for (int i = 0; i < 3; i++)
            {
                Instantiate(blood, hit.point +
                    hit.transform.right * Random.Range(-spawnRadius, spawnRadius) + hit.transform.up * Random.Range(-spawnRadius / 2, spawnRadius / 2), Quaternion.identity);
            }
        }
    }
}
