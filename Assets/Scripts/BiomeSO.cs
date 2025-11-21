using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "biome", menuName = "ScriptableObjects/newbiome", order = 1)]
public class BiomeSO : ScriptableObject
{
    [Header("Base Properties")]
    public String biomeName;
   

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