// Assets/WellBound/Scripts/EnemyStateMachine/Transition.cs
using System;
using UnityEngine;

[Serializable]
public struct Transition
{
    // When this ConditionSO.Evaluate(...) returns true, we switch to NextState
    public ConditionSO[] Condition;

    // The StateSO asset to enter
    public StateSO NextState;
}
