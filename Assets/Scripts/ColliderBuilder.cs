using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ColliderBuilder : NetworkBehaviour
{
    void Awake() {
    
        if(!IsServer) { return; }

        MeshCollider parentMeshCollider = gameObject.AddComponent<MeshCollider>();
        parentMeshCollider.convex = true; // Nie u¿ywaæ convex, jeœli nie jest potrzebne

        List<CombineInstance> combineInstances = new();

        foreach (Transform child in transform)
        {
            MeshCollider childCollider = child.GetComponent<MeshCollider>();
            if (childCollider != null)
            {
                Mesh mesh = childCollider.sharedMesh;
                if (mesh != null)
                {
                    CombineInstance combineInstance = new()
                    {
                        mesh = mesh,
                        transform = child.localToWorldMatrix
                    };
                    combineInstances.Add(combineInstance);
                }

                Destroy(childCollider); // Usuniêcie komponentu MeshCollider z dziecka
            }
        }

        if (combineInstances.Count > 0)
        {
            Mesh mergedMesh = new();
            mergedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

            parentMeshCollider.sharedMesh = mergedMesh;
        }
    }
}
