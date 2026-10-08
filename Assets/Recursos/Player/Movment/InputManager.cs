using UnityEngine;
using UnityEngine.InputSystem;

// Lê o Input System e repassa para os scripts do player e da arma
public class InputManager : MonoBehaviour
{
    private PlayerControls playerInput;
    private PlayerControls.OnFootActions onFoot;

    private PlayerMovment playerMove;
    private OlharPlayer visaoDoPlayer;

    // Lidos pela arma (Gun)
    public bool Mirando => onFoot.Aim.IsPressed();
    public bool AtirarPressionado => onFoot.Fire.WasPressedThisFrame();
    public bool AtirarSegurado => onFoot.Fire.IsPressed();
    public bool RecarregarPressionado => onFoot.Reload.WasPressedThisFrame();

    // Lidos pelo arremesso de água benta (segura para mirar, solta para jogar)
    public bool ArremessarSegurado => onFoot.Arremessar.IsPressed();
    public bool ArremessarSolto => onFoot.Arremessar.WasReleasedThisFrame();

    void Awake()
    {
        playerMove = GetComponent<PlayerMovment>();
        visaoDoPlayer = GetComponent<OlharPlayer>();
        playerInput = new PlayerControls();
        onFoot = playerInput.onFoot;
    }

    private void Update()
    {
        // Olhar primeiro, para o movimento já usar a rotação nova
        InputAction olhar = onFoot.OlharDoPlayer;
        bool gamepad = olhar.activeControl != null && olhar.activeControl.device is Gamepad;
        visaoDoPlayer.FirstPerson(olhar.ReadValue<Vector2>(), gamepad);

        float inclinar = 0f;
        if (onFoot.LeanLeft.IsPressed()) inclinar -= 1f;
        if (onFoot.LeanRight.IsPressed()) inclinar += 1f;
        visaoDoPlayer.Inclinar(inclinar);

        if (onFoot.Crouch.WasPressedThisFrame()) playerMove.AlternarAgachar();
        if (onFoot.Jump.WasPressedThisFrame()) playerMove.Pular();

        playerMove.Movment(onFoot.MovmentAction.ReadValue<Vector2>(), onFoot.Sprint.IsPressed(), onFoot.Jump.IsPressed());
    }

    private void OnEnable()
    {
        onFoot.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    private void OnDisable()
    {
        onFoot.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
