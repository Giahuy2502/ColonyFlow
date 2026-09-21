using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolControl : MonoBehaviour
{
    [SerializeField] PoolAmount[] poolAmounts;
    
    private void Awake()
    {
        for (int i = 0; i < poolAmounts.Length; i++)
        {
            SimplePool.PreLoad(poolAmounts[i].prefab, poolAmounts[i].amount,poolAmounts[i].parent);
        }
    }
}

[System.Serializable]
public class PoolAmount
{
    public GameUnit prefab;
    public Transform parent;
    public int amount;
}
public enum PoolType
{
    Knife = 0,
    Hammer = 1,
    Boomerang = 2,
    Bot = 3,
    SpeedUpBooster = 4,
    AttackRangeBooster = 5,
    Axe = 6,
    Axe1 = 7,
    Candy =8,
    Candy1 = 9,
    Candy2 = 10,
    Candy3 = 11,
    Uzi = 12,
    Ant = 100,
}
