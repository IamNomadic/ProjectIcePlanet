using UnityEngine;


    [CreateAssetMenu(menuName = "Enemy/State")]
    public class StateSO : ScriptableObject
    {
        public EnemyStateBehaviour Behaviour;
        public Transition[] Transitions;
    }
