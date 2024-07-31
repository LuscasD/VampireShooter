using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerControls playerInput;
    private PlayerControls.OnFootActions onFoot;

    private PlayerMovment playerMove;
    private OlharPlayer visaoDoPlayer;

    // Start is called before the first frame update
    void Awake()
    {
        playerMove = GetComponent<PlayerMovment>();
        visaoDoPlayer = GetComponent<OlharPlayer>();
        playerInput = new PlayerControls();
        onFoot = playerInput.onFoot;
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        playerMove.Movment(onFoot.MovmentAction.ReadValue<Vector2>());
    }

    private void LateUpdate()
    {
        visaoDoPlayer.FirstPerson(onFoot.OlharDoPlayer.ReadValue<Vector2>());
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }
    private void OnDisable()
    {
        onFoot.Disable();
    }
}
