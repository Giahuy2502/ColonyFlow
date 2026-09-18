using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SimplePool : MonoBehaviour
{
    private static Dictionary<PoolType, Pool> poolInstance = new Dictionary<PoolType, Pool>();

    // khoi tao pool moi
    public static void PreLoad(GameUnit prefab, int amount, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogError("PREFAB IS NULL");
            return;
        }

        if (!poolInstance.ContainsKey(prefab.poolType) || poolInstance[prefab.poolType] == null)
        {
            Pool p = new Pool();
            p.PreLoad(prefab, amount, parent);
            poolInstance[prefab.poolType] = p;
        }
    }

    // lay phan tu ra
    public static T Spawn<T>(PoolType poolType, Vector3 pos, Quaternion rot) where T : GameUnit
    {
        if (!poolInstance.ContainsKey(poolType))
        {
            Debug.LogError(poolType + "IS NOT PRELOAD!!!");
            return null;
        }
        return poolInstance[poolType].Spawn(pos, rot) as T;
    }

    // tra phan tu vao
    public static void Despawn(GameUnit unit)
    {
        if (!poolInstance.ContainsKey(unit.poolType))
        {
            Debug.LogError(unit.poolType + "IS NOT PRELOAD!!!");
        }
        poolInstance[unit.poolType].Despawn(unit);
    }

    // thu thap phan tu
    public static void Collect(PoolType poolType)
    {
        if (!poolInstance.ContainsKey(poolType))
        {
            Debug.LogError(poolType + "IS NOT PRELOAD!!!");
        }
        poolInstance[poolType].Collect();
    }

    // thu thap tat ca phan tu
    public static void CollectAll()
    {
        foreach (var item in poolInstance.Values)
        {
            item.Collect();
        }
    }

    // destroy 1 pool
    public static void Release(PoolType poolType)
    {
        if (!poolInstance.ContainsKey(poolType))
        {
            Debug.LogError(poolType + "IS NOT PRELOAD!!!");
        }
        poolInstance[poolType].Release();
    }
    
    // destroy tat ca pool
    public static void ReleaseAll(PoolType poolType)
    {
        foreach (var item in poolInstance.Values)
        {
            item.Release();
        }
        poolInstance[poolType].Release();
    }
}

public class Pool
{
    private Transform parent;
    GameUnit prefab;
    // list chua cac unit dang o trong pool
    Queue<GameUnit> inActives = new Queue<GameUnit>();
    // list chua cac unit dang duoc su dung
    List<GameUnit> actives = new List<GameUnit>();

    // khoi tao pool
    public void PreLoad(GameUnit prefab, int amount, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
        for (int i = 0; i < amount; i++)
        {
            Despawn(Spawn(Vector3.zero, Quaternion.identity));
        }
    }

    // lay phan tu tu pool
    public GameUnit Spawn(Vector3 pos, Quaternion rot)
    {
        GameUnit unit;
        if (inActives.Count <= 0)
        {
            unit = GameObject.Instantiate(prefab, parent);
        }
        else
        {
            unit = inActives.Dequeue();
        }
        unit.TF.SetPositionAndRotation(pos,rot);
        unit.gameObject.SetActive(true);
        actives.Add(unit);
        return unit;
    }

    // tra phan tu vao trong pool
    public void Despawn(GameUnit unit)
    {
        if (unit!= null && unit.gameObject.activeSelf)
        {
            actives.Remove(unit);
            inActives.Enqueue(unit);
            unit.gameObject.SetActive(false);
            unit.TF.SetParent(parent);
        }
    }

    // thu thap tat ca phan tu dang dung ve pool
    public void Collect()
    {
        while (actives.Count > 0)
        {
            Despawn(actives[0]);
        }
    }

    // destroy tat ca phan tu
    public void Release()
    {
        Collect();
        while (inActives.Count > 0)
        {
            GameObject.Destroy(inActives.Dequeue().gameObject);
        }
        inActives.Clear();
    }
}
