using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    // Serialized fields use _camelCase; public properties use PascalCase
    [SerializeField] public EnemySO enemyData;
    [SerializeField] private GameObject _corpsePrefab;
    [SerializeField] StateSO DeathState;
    [SerializeField] StateMachine sm;

    // Backing field for current health
    private int _health;

    // Expose health as a read‐only property
    public int Health => _health;

    // Events let other systems (like your FSM) know when damage or death occurs
    public event Action<EnemyHealth, int> Damaged; // (self, damageAmount)
    public event Action<EnemyHealth> Died;

    private void Awake()
    {
        // Initialize from the ScriptableObject
        _health = enemyData.enemyHP;
    }

    // IDamageable interface implementation
    public void TakeDamage(int damage, IDamager damager)
    {
        if (_health <= 0 || damage <= 0)
            return;

        _health -= damage;
        Damaged?.Invoke(this, damage);

        if (_health <= 0)
            Die();
    }

    void Die()
    {
        Died?.Invoke(this);
        sm.TransitionTo(DeathState);
        // Spawn corpse if assigned
       
    }
    public void ToDeathState()
    {
        sm.TransitionTo(DeathState);
    }
}
