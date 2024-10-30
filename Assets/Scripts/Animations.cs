using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent (typeof(Movement))]
public class Animations : NetworkBehaviour
{
    Animator playerAnimator;
    Animator localPlayerModelAnimator;
    Movement playerMovement;
    void Start()
    {
        playerAnimator = GetComponent<Animator>();
        playerMovement = GetComponent<Movement>();
        if (GameObject.Find("LocalPlayerModel")) localPlayerModelAnimator = GameObject.Find("LocalPlayerModel").GetComponent<Animator>();
        //Workaround for some weird problem (animation would start playing, but stop immediately if I didn't include these two lines) 
        playerAnimator.Rebind();
        playerAnimator.Update(0f);
    }
    //update animations
    private void Update()
    {
        if (!IsOwner) { return; }
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        bool IsGrounded = playerMovement.IsGrounded;
        ChangeAnimationPropertiesServerRpc(horizontalInput, verticalInput, IsGrounded);
        if (localPlayerModelAnimator)
            ChangeAnimationPropertiesOnClient(horizontalInput, verticalInput, IsGrounded);
    }

    //This changes animation locally on localPlayerModel
    void ChangeAnimationPropertiesOnClient(float horizontalInput, float verticalInput, bool IsGrounded)
    {
        localPlayerModelAnimator.SetFloat("horizontalInput", horizontalInput);
        localPlayerModelAnimator.SetFloat("verticalInput", verticalInput);
        localPlayerModelAnimator.SetBool("isGrounded", IsGrounded);
    }

    //This actually changes the animation on the server
    [Rpc(SendTo.Server)]
    void ChangeAnimationPropertiesServerRpc(float horizontalInput, float verticalInput, bool IsGrounded)
    {
        playerAnimator.SetFloat("horizontalInput", horizontalInput);
        playerAnimator.SetFloat("verticalInput", verticalInput);
        playerAnimator.SetBool("isGrounded", IsGrounded);
    }
}
