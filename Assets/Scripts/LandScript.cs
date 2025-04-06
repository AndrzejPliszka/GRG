using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LandScript : NetworkBehaviour
{
    public int townId = 0;
    public int menuXPos = 0;
    public int menuYPos = 0;
    public string menuDisplayText = "Test Land";
}
