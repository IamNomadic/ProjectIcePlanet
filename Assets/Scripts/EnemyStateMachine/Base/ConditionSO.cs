using UnityEngine;
public abstract class ConditionSO : ScriptableObject
{
    public abstract bool Evaluate(StateMachineContext ctx);
}
