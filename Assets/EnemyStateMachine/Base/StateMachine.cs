using UnityEngine;
using System.Linq;
 
public class StateMachine : MonoBehaviour
{
    [Header("FSM Setup")]
    [SerializeField] private StateSO initialState;
    [SerializeField] private EnemySO enemyData;
 

    public StateSO CurrentState { get; private set; }


    private StateMachineContext ctx;

    // runtime instance used by this enemy
    private StateSO currentStateInstance;

    private void Awake()
    {
        ctx = new StateMachineContext
        {
            rb = GetComponent<Rigidbody2D>(),
            animator = GetComponent<Animator>(),
            data = enemyData,
            transform = transform,
            player = FindObjectOfType<PlayerStats>(),
            hitCollider = GetComponentsInChildren<CircleCollider2D>().FirstOrDefault(c => c.gameObject != this.gameObject),
            selfCollider = GetComponent<Collider2D>(),
            pickup = GetComponentsInChildren<Pickup>().FirstOrDefault(c => c.gameObject != this.gameObject),
        };
        
        TransitionTo(initialState);
    }

    private void Update()
    {
        if (currentStateInstance == null) return;
  
        // evaluate outgoing transitions: all conditions in a transition must be true
        foreach (var t in currentStateInstance.Transitions)
        {
            var conditions = t.Condition;
            if (conditions == null || conditions.Length == 0) continue; // skip empty condition sets

            bool allTrue = true;
            foreach (ConditionSO condition in conditions)
            {
                if (!condition.Evaluate(ctx))
                {
                    allTrue = false;
                    break;
                }
            }

            if (allTrue)
            {
                TransitionTo(t.NextState);
                return;
            }
        }

        // execute current state logic
        currentStateInstance.Behaviour.Execute(ctx);
    }
    public void TransitionTo(StateSO next)
    {
        // call Exit on the previous instance
        if (currentStateInstance != null)
            currentStateInstance.Behaviour.Exit(ctx);

        // record transition time for conditions that rely on it
        ctx.lastTransitionTime = Time.time;

        // instantiate the StateSO template so this enemy has its own state copy
        currentStateInstance = Instantiate(next);

        // ALSO instantiate the Behaviour ScriptableObject so runtime fields on the behaviour
        // are unique per enemy (prevents shared timers, directions, etc).
        // This assumes StateSO has a public field/property 'Behaviour' that can be reassigned.
        if (next.Behaviour != null)
        {
            currentStateInstance.Behaviour = Instantiate(next.Behaviour);
        }

        // Keep a reference to the template if you still need it elsewhere
        CurrentState = next;

        // Enter the instantiated behaviour
        currentStateInstance.Behaviour.Enter(ctx);
    }

}
