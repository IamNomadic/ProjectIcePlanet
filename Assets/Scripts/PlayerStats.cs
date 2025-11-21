using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour
{
    
    

    public int CurrentHunger;
    public int MaxHunger;
    public int HungerTickTimeGate;
    [SerializeField]float HungerTickTime;
    public int MaxHealth;
    public int CurrentHealth;
    public float invincibilityTime;
    float _invincibilityTime;
    public string currentRitual;
   
    public bool dead;
    public bool RitualsDone;
    public bool targetable;
    bool Starving;
    bool OutOfHunger= false;

    public int RitualsCompleted;
    public int RitualsToComplete;
    public static event Action OnPlayerDamaged;
    private void Start()
    {
        OutOfHunger = false;
        RitualsDone = false;
        _invincibilityTime = invincibilityTime;

    }
    
    // Update is called once per frame
    public void FixedUpdate()
    {
        if (OutOfHunger == false || Starving == true)
        {
            HungerTickTime += Time.deltaTime;

        }
        
        if (HungerTickTime >=HungerTickTimeGate)
        {
            HungerTickTime = 0;
            CurrentHunger--;
            if (Starving)
            {
                TakeDamage(1);
            }
            OnPlayerDamaged?.Invoke();

            Debug.Log("lost 1 hunger");

        }
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }
        if (CurrentHunger > MaxHunger)
        {
            CurrentHunger = MaxHunger;
        }
        if (CurrentHunger <=0)
        {
            OutOfHunger = true;
            Starving = true;
            
        }
        else if (CurrentHunger>0)
        {
            OutOfHunger = false;
            Starving = false;


        }
        if (RitualsCompleted>=RitualsToComplete)
        {
            RitualsDone = true;
        }

        if(_invincibilityTime>0)
        {
            _invincibilityTime = _invincibilityTime - Time.deltaTime;
        }
       




    }

    public void RefreshHud()
    {
        OnPlayerDamaged?.Invoke();

    }

    private IEnumerator LevelReset()
    {

        yield return new WaitForSeconds(6.3f);
        dead = false;
        SceneManager.LoadScene("Title");
    }

    public void TakeDamage(int damage)
    {
        if(_invincibilityTime<=0)
        {
            CurrentHealth -= damage;
            Debug.Log("takedDmag");

            OnPlayerDamaged?.Invoke();
            
            _invincibilityTime = invincibilityTime;
            if (CurrentHealth <= 0)
            {
                StartCoroutine("LevelReset");
                Debug.Log("you are dead");


                dead = true;
            }

        }
    }
    public void HealDamage(int Health)
    {
        
            CurrentHealth += Health;
       
        OnPlayerDamaged?.Invoke();
        if (CurrentHealth <= 0)
        {
            StartCoroutine("LevelReset");
            Debug.Log("you are dead");


            dead = true;
        }
    }
    public void GainHunger(int Hunger)
    {
       
        CurrentHunger += Hunger; 

        OnPlayerDamaged?.Invoke();
    }
    public void OnCollisionEnter2D (Collision2D DeathBox)
    {
       
     
    }
}