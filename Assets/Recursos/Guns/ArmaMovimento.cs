using UnityEngine;

// Movimento procedural da arma: sway, balanço ao andar, mirar,
// pose de corrida, recuo e pose de recarga.
// Fica num objeto entre o GunHolder (animado pelo Animator do Player) e o modelo da arma.
public class ArmaMovimento : MonoBehaviour
{
    [Header("Sway")]
    public float swayRotacao = 1.2f;
    public float swayPosicao = 0.004f;
    public float swayMaximo = 6f;
    public float swaySuavidade = 8f;

    [Header("Balanço ao andar")]
    public float bobPosicao = 0.02f;
    public float bobRotacao = 1.5f;

    [Header("Mirar")]
    // Ajuste em cima da animação "Hold" para a alça de mira ficar logo abaixo do centro da tela
    public Vector3 posicaoMirando = new Vector3(0.02f, 0.17f, 0f);
    public float velocidadeMirar = 12f;

    [Header("Correr")]
    public Vector3 posicaoCorrendo = new Vector3(-0.1f, -0.08f, 0f);
    public Vector3 rotacaoCorrendo = new Vector3(12f, -18f, 8f);

    [Header("Recarregar")]
    public Vector3 posicaoRecarregando = new Vector3(0f, -0.35f, -0.1f);
    public Vector3 rotacaoRecarregando = new Vector3(35f, 0f, -25f);

    [Header("Recuo")]
    public float recuoPosicao = 0.18f;
    public float recuoRotacao = 14f;
    public float retornoRecuo = 9f;

    // Definidos pela arma (Gun)
    public bool Mirando { get; set; }
    public bool Recarregando { get; set; }

    private OlharPlayer olhar;
    private PlayerMovment movimento;

    private Vector3 swayRot;
    private Vector3 swayPos;
    private float pesoMirar;
    private float pesoCorrer;
    private float pesoRecarregar;
    private float recuo;
    private float faseBob;

    private void Awake()
    {
        olhar = GetComponentInParent<OlharPlayer>();
        movimento = GetComponentInParent<PlayerMovment>();
    }

    public void Recuar(float forca)
    {
        recuo = Mathf.Min(recuo + forca, 1.5f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;

        // Sway: a arma "atrasa" em relação ao movimento da câmera
        Vector2 delta = olhar != null ? olhar.UltimoDeltaOlhar : Vector2.zero;
        float reducao = Mirando ? 0.3f : 1f;
        Vector3 swayRotAlvo = new Vector3(
            Mathf.Clamp(delta.y * swayRotacao, -swayMaximo, swayMaximo),
            Mathf.Clamp(-delta.x * swayRotacao, -swayMaximo, swayMaximo),
            Mathf.Clamp(-delta.x * swayRotacao, -swayMaximo, swayMaximo)) * reducao;
        Vector3 swayPosAlvo = new Vector3(-delta.x, -delta.y, 0f) * swayPosicao * reducao;
        swayRot = Vector3.Lerp(swayRot, swayRotAlvo, swaySuavidade * dt);
        swayPos = Vector3.Lerp(swayPos, swayPosAlvo, swaySuavidade * dt);

        // Balanço ao andar
        float velocidade = movimento != null && movimento.NoChao ? movimento.VelocidadeAtual : 0f;
        float pesoBob = movimento != null ? Mathf.Clamp01(velocidade / movimento.velocidadeAndando) * reducao : 0f;
        faseBob += velocidade * 2f * dt;
        Vector3 bobPos = new Vector3(Mathf.Sin(faseBob) * bobPosicao, -Mathf.Abs(Mathf.Cos(faseBob)) * bobPosicao, 0f) * pesoBob;
        Vector3 bobRot = new Vector3(0f, 0f, Mathf.Sin(faseBob) * bobRotacao) * pesoBob;

        // Poses
        bool correndo = movimento != null && movimento.Correndo && !Mirando;
        pesoMirar = Mathf.MoveTowards(pesoMirar, Mirando && !Recarregando ? 1f : 0f, velocidadeMirar * dt);
        pesoCorrer = Mathf.MoveTowards(pesoCorrer, correndo ? 1f : 0f, 5f * dt);
        pesoRecarregar = Mathf.MoveTowards(pesoRecarregar, Recarregando ? 1f : 0f, 5f * dt);
        float mirar = Mathf.SmoothStep(0f, 1f, pesoMirar);
        float correr = Mathf.SmoothStep(0f, 1f, pesoCorrer);
        float recarregar = Mathf.SmoothStep(0f, 1f, pesoRecarregar);

        // Recuo
        recuo = Mathf.Lerp(recuo, 0f, retornoRecuo * dt);

        transform.localPosition = swayPos + bobPos
            + posicaoMirando * mirar
            + posicaoCorrendo * correr
            + posicaoRecarregando * recarregar
            + Vector3.back * recuo * recuoPosicao;

        transform.localRotation = Quaternion.Euler(
            swayRot + bobRot
            + rotacaoCorrendo * correr
            + rotacaoRecarregando * recarregar
            + Vector3.left * recuo * recuoRotacao);
    }
}
