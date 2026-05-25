using UnityEngine;

public class ObjectReference : MonoBehaviour
{
    //Sometimes object with given tag, is parent or child of actual object holding the script we normally expect to be associated with this tag. (it happenes because children cannot have NetworkObjects on them)
    //In these situations we reference this script which has reference to object holding the script

    public GameObject objectReference;
}
