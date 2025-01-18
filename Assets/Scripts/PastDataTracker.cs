using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public struct TickData
{
    public Quaternion rotation;
    public Vector3 position;
}

//this component is used for lag compensation as it holds previous positions and rotations of object
public class PastDataTracker : NetworkBehaviour
{

    //server side dictionary (will not be sent to clients)
    readonly Dictionary<float,  TickData> tickDictionary = new();

    private void FixedUpdate()
    {
        if(!IsServer) return;
            AddRecordToTickDictionary();
        //Debug.Log("CurrentTick: " + NetworkManager.Singleton.ServerTime.Time);
    }

    void AddRecordToTickDictionary()
    {
        if(!IsServer) { throw new System.Exception("tickDictionary is only used Server Side!"); }
        float currentTick = NetworkManager.Singleton.ServerTime.Tick;
        if (tickDictionary.ContainsKey(currentTick)) { return; }

        transform.GetPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation);
        if(tickDictionary.Count >= 100) 
        {
            float oldestKey = tickDictionary.OrderBy(pair => pair.Key).First().Key;
            tickDictionary.Remove(oldestKey);
        }
        tickDictionary.Add(currentTick, new TickData() { rotation = currentRotation, position = currentPosition });   
    }

    public TickData GetPastData(int tick)
    {
        if (!IsServer) { throw new System.Exception("SingleTickData is only used Server Side!"); }
        //Get value corresponding to smallest key larger than time in function
        TickData result = tickDictionary
            .Where(entry => entry.Key >= tick)
            .OrderBy(entry => entry.Key) 
            .FirstOrDefault().Value;

        return result;
    }
}
