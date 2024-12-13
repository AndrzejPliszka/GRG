using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(ObjectInteraction))]
public class Animations : NetworkBehaviour
{
    Animator playerAnimator;
    Animator localPlayerModelAnimator;
    Movement playerMovement;
    ObjectInteraction objectInteraction;
    float punchingAnimationDuration = 0.6f;

    void Start()
    {
        playerAnimator = GetComponent<Animator>();
        playerMovement = GetComponent<Movement>();
        objectInteraction = GetComponent<ObjectInteraction>();
        objectInteraction.OnPunch += ManagePunchingAnimation;
        
        if (playerMovement.LocalPlayerModel) localPlayerModelAnimator = playerMovement.LocalPlayerModel.GetComponent<Animator>();
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
        bool isRunning = playerMovement.IsRunning;
        ChangeAnimationPropertiesServerRpc(horizontalInput, verticalInput, IsGrounded, isRunning);
        if (localPlayerModelAnimator)
            ChangeAnimationPropertiesOnClient(horizontalInput, verticalInput, IsGrounded, isRunning);

    }

    //This changes animation locally on localPlayerModel
    void ChangeAnimationPropertiesOnClient(float horizontalInput, float verticalInput, bool IsGrounded, bool isRunning)
    {
        localPlayerModelAnimator.SetFloat("horizontalInput", horizontalInput);
        localPlayerModelAnimator.SetFloat("verticalInput", verticalInput);
        localPlayerModelAnimator.SetBool("isGrounded", IsGrounded);
        localPlayerModelAnimator.SetBool("isRunning", isRunning);
    }

    //This actually changes the animation on the server
    [Rpc(SendTo.Server)]
    void ChangeAnimationPropertiesServerRpc(float horizontalInput, float verticalInput, bool isGrounded, bool isRunning)
    {
        playerAnimator.SetFloat("horizontalInput", horizontalInput);
        playerAnimator.SetFloat("verticalInput", verticalInput);
        playerAnimator.SetBool("isGrounded", isGrounded);
        playerAnimator.SetBool("isRunning", isRunning);
    }

    void ManagePunchingAnimation(float cooldown) //cooldown is not used here yet, but it is used in PlayerUI
    {
        MakePunchingAnimationServerRpc();
        if (localPlayerModelAnimator)
            StartCoroutine(PunchingClientCourutine());
    }

    [Rpc(SendTo.Server)]
    void MakePunchingAnimationServerRpc()
    {
        StartCoroutine(PunchingServerCourutine());
    }
    IEnumerator PunchingServerCourutine()
    {
        playerAnimator.SetBool("isPunching", true);
        yield return new WaitForSeconds(punchingAnimationDuration);
        playerAnimator.SetBool("isPunching", false);
    }
    IEnumerator PunchingClientCourutine()
    {
        localPlayerModelAnimator.SetBool("isPunching", true);
        yield return new WaitForSeconds(punchingAnimationDuration);
        localPlayerModelAnimator.SetBool("isPunching", false);
    }
}
