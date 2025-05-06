using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloseWindow : MonoBehaviour
{
    public void CloseParentWindow()
    {
        Destroy(gameObject.transform.parent.gameObject);
    }
}
