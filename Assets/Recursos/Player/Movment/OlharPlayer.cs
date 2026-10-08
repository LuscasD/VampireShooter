using UnityEngine;

// Câmera em primeira pessoa: olhar, inclinar (Q/E), balanço da cabeça,
// altura ao agachar, impacto ao cair, recuo e zoom ao mirar
public class OlharPlayer : MonoBehaviour
{
    public Camera cam;

    [Header("Sensibilidade")]
    public float sensibilidadeMouse = 0.12f;    // graus por pixel
    public float sensibilidadeGamepad = 160f;   // graus por segundo
    [Range(0.1f, 1f)] public float multiplicadorMirando = 0.6f;
    public bool inverterY;
    public float limiteVertical = 85f;

    [Header("Inclinar")]
    public float anguloInclinar = 14f;
    public float distanciaInclinar = 0.55f;
    public float velocidadeInclinar = 8f;

    [Header("Balanço da cabeça")]
    public float bobVertical = 0.025f;
    public float bobHorizontal = 0.012f;
    public float comprimentoDoPasso = 1.6f;

    [Header("Campo de visão")]
    public float zoomMirando = 0.8f;
    public float fovExtraCorrendo = 5f;
    public float velocidadeFov = 8f;

    [Header("Recuo")]
    public float velocidadeRecuo = 25f;
    public float retornoRecuo = 6f;

    // Definido pela arma
    public bool Mirando { get; set; }
    // Quanto a câmera girou neste frame (usado pelo sway da arma)
    public Vector2 UltimoDeltaOlhar { get; private set; }

    private PlayerMovment movimento;
    private float rotacaoX;
    private Vector3 posicaoBase;
    private float fovBase;

    private float inclinarAlvo;
    private float inclinarAtual;

    private float faseBob;
    private float pesoBob;

    private float quedaPos;
    private float quedaVel;

    private Vector2 recuoAlvo;
    private Vector2 recuoAtual;

    private void Awake()
    {
        movimento = GetComponent<PlayerMovment>();
        posicaoBase = cam.transform.localPosition;
        fovBase = cam.fieldOfView;
        rotacaoX = cam.transform.localEulerAngles.x;
        if (rotacaoX > 180f) rotacaoX -= 360f;
    }

    private void OnEnable()
    {
        if (movimento != null) movimento.Aterrissou += AoAterrissar;
    }
    private void OnDisable()
    {
        if (movimento != null) movimento.Aterrissou -= AoAterrissar;
    }

    public void FirstPerson(Vector2 input, bool gamepad)
    {
        Vector2 delta = gamepad ? input * sensibilidadeGamepad * Time.deltaTime : input * sensibilidadeMouse;
        if (Mirando) delta *= multiplicadorMirando;
        if (inverterY) delta.y = -delta.y;

        rotacaoX = Mathf.Clamp(rotacaoX - delta.y, -limiteVertical, limiteVertical);
        transform.Rotate(Vector3.up * delta.x);
        UltimoDeltaOlhar = delta;
    }

    public void Inclinar(float direcao)
    {
        inclinarAlvo = Mathf.Clamp(direcao, -1f, 1f);
    }

    public void AdicionarRecuo(float cima, float lado)
    {
        recuoAlvo += new Vector2(-cima, lado);
    }

    private void AoAterrissar(float velocidadeQueda)
    {
        quedaVel -= Mathf.Clamp(velocidadeQueda * 0.08f, 0f, 2.5f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        float escala = transform.lossyScale.x;

        // Altura (agachar)
        Vector3 cabeca = posicaoBase + Vector3.up * (movimento != null ? movimento.DiferencaDeAltura : 0f);

        // Inclinar, sem atravessar paredes
        float alvo = movimento != null && movimento.Correndo ? 0f : inclinarAlvo;
        if (alvo != 0f)
        {
            Vector3 origem = transform.TransformPoint(cabeca);
            float distancia = distanciaInclinar * escala;
            if (Physics.SphereCast(origem, 0.2f, transform.right * Mathf.Sign(alvo), out RaycastHit hit, distancia, Camadas.SemPlayer,QueryTriggerInteraction.Ignore))
                alvo *= Mathf.Clamp01(hit.distance / distancia);
        }
        inclinarAtual = Mathf.Lerp(inclinarAtual, alvo, velocidadeInclinar * dt);

        // Balanço da cabeça baseado na distância andada
        float velocidade = movimento != null ? movimento.VelocidadeAtual : 0f;
        bool andando = movimento != null && movimento.NoChao && velocidade > 0.3f;
        pesoBob = Mathf.MoveTowards(pesoBob, andando ? Mathf.Clamp(velocidade / movimento.velocidadeAndando, 0f, 1.5f) : 0f, 4f * dt);
        if (andando) faseBob += velocidade / comprimentoDoPasso * Mathf.PI * dt;
        float bobY = -Mathf.Abs(Mathf.Sin(faseBob)) * bobVertical * pesoBob;
        float bobX = Mathf.Sin(faseBob) * bobHorizontal * pesoBob;
        if (Mirando) { bobX *= 0.3f; bobY *= 0.3f; }

        // Impacto ao cair (mola)
        quedaVel += (-quedaPos * 120f - quedaVel * 14f) * dt;
        quedaPos += quedaVel * dt;

        // Recuo
        recuoAlvo = Vector2.Lerp(recuoAlvo, Vector2.zero, retornoRecuo * dt);
        recuoAtual = Vector2.Lerp(recuoAtual, recuoAlvo, velocidadeRecuo * dt);

        cam.transform.localPosition = cabeca
            + new Vector3(inclinarAtual * distanciaInclinar + bobX, bobY + quedaPos - Mathf.Abs(inclinarAtual) * 0.08f, 0f);
        cam.transform.localRotation = Quaternion.Euler(rotacaoX + recuoAtual.x, recuoAtual.y, -inclinarAtual * anguloInclinar);

        // Campo de visão
        float fovAlvo = fovBase * (Mirando ? zoomMirando : 1f) + (movimento != null && movimento.Correndo ? fovExtraCorrendo : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovAlvo, velocidadeFov * dt);
    }
}
