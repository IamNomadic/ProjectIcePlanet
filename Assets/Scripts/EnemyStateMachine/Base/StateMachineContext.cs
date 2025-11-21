using UnityEngine;
public class StateMachineContext
{
    public Rigidbody2D rb;
    public Animator animator;
    public EnemySO data;
    public Transform transform;

    public PlayerStats player;
    public bool isAttacking;
    public float lastTransitionTime;
    public Collider2D hitCollider;
    public Collider2D selfCollider;
    public Pickup pickup;
    // Extend with fields like currentHealth, wanderTimer, etc.
}
