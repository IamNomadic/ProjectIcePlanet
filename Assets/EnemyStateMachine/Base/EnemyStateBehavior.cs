using UnityEngine;
public abstract class EnemyStateBehaviour : ScriptableObject, IEnemyState
{
    public abstract void Enter(StateMachineContext ctx);
    public abstract void Execute(StateMachineContext ctx);
    public abstract void Exit(StateMachineContext ctx);
}
