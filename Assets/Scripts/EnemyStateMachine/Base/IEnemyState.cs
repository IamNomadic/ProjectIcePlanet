public interface IEnemyState
{
    void Enter(StateMachineContext ctx);
    void Execute(StateMachineContext ctx);
    void Exit(StateMachineContext ctx);
}
