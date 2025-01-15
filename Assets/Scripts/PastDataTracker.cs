using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public struct TimeData
{
    public Quaternion rotation;
    public Vector3 position;
}

//this component is used for lag compensation as it holds previous positions and rotations of object
public class PastDataTracker : NetworkBehaviour
{

    //server side dictionary (will not be sent to clients)
    readonly Dictionary<float,  TimeData> timeDictionary = new();

    private void FixedUpdate()
    {
        if(!IsServer) return;
            AddRecordToTickDictionary();
        //Debug.Log("CurrentTick: " + NetworkManager.Singleton.ServerTime.Time);
    }

    void AddRecordToTickDictionary()
    {
        if(!IsServer) { throw new System.Exception("timeDictionary is only used Server Side!"); }
        float currentTime = NetworkManager.Singleton.ServerTime.Tick;
        if (timeDictionary.ContainsKey(currentTime)) { return; }

        transform.GetPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation);
        if(timeDictionary.Count >= 100) 
        {
            float oldestKey = timeDictionary.OrderBy(pair => pair.Key).First().Key;
            timeDictionary.Remove(oldestKey);
        }
        timeDictionary.Add(currentTime, new TimeData() { rotation = currentRotation, position = currentPosition });   
    }

    public TimeData GetPastData(int time)
    {
        if (!IsServer) { throw new System.Exception("SingleTickData is only used Server Side!"); }
        //Get value corresponding to smallest key larger than time in function
        TimeData result = timeDictionary
            .Where(entry => entry.Key >= time)
            .OrderBy(entry => entry.Key) 
            .FirstOrDefault().Value;

        return result;
    }
}
