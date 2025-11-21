using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "ScriptableObjects/EnemyVariant", order = 1)]
public class EnemySO : ScriptableObject
{
    [Header("Base Properties")]
    public string enemyName;
    public ItemSO[] drops;
    public Sprite[] enemyCorpse;
    public bool hasAnimation;
    public int enemyDMG;
    public int enemyHP;
    public float enemySpeed;
    public float enemyAcceleration;
    public float EnemyDrag;
    public AudioClip hurtNoise;
    public AudioClip deathNoise;
    public AudioClip attackNoise;
    public List<BiomeSO> allowedBiomes;


    protected virtual void OnEnable()
    {

    }


    protected virtual void OnDisable()
    {

    }

    protected virtual void OnDestroy()
    {

    }

}