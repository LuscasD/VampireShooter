using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Arremesso de água benta pelo Player: segure para mirar (mostra a trajetória e o raio da explosão),
// solte para jogar. Os parâmetros da explosão ficam aqui para poder mudar em tempo real.
public class ArremessarAguaBenta : MonoBehaviour
{
    public FrascoAguaBenta prefabFrasco;
    public ExplosaoAguaBenta prefabExplosao;
    public ConfigExplosao explosao = new ConfigExplosao();

    [Header("Arremesso")]
    public int quantidade = 5;
    public float forcaArremesso = 13f;
    [Tooltip("Graus para cima em relação ao centro da tela")]
    public float anguloExtra = 8f;
    public float intervalo = 0.8f;

    [Header("Mira")]
    public LineRenderer linhaTrajetoria;
    public LineRenderer anelDoRaio;
    public int passosTrajetoria = 60;
    public float passoDeTempo = 0.04f;

    [Header("UI (opcional)")]
    public Text textoQuantidade;

    private InputManager input;
    private Transform cameraPlayer;
    private Collider[] meusColliders;
    private float proximoArremesso;
    private readonly List<Vector3> pontos = new List<Vector3>();

    private void Awake()
    {
        input = GetComponent<InputManager>();
        cameraPlayer = GetComponentInChildren<Camera>().transform;
        meusColliders = GetComponentsInChildren<Collider>();
        Mostrar(false);
    }

    private void Update()
    {
        bool mirando = input.ArremessarSegurado && quantidade > 0;
        Mostrar(mirando);
        if (mirando) DesenharMira();

        if (input.ArremessarSolto && quantidade > 0 && Time.time >= proximoArremesso)
            Arremessar();

        if (textoQuantidade != null) textoQuantidade.text = "Água benta: " + quantidade;
    }

    private Vector3 PontoDeSaida => cameraPlayer.position + cameraPlayer.forward * 0.5f + cameraPlayer.right * 0.25f - Vector3.up * 0.15f;
    private Vector3 VelocidadeInicial => Quaternion.AngleAxis(-anguloExtra, cameraPlayer.right) * cameraPlayer.forward * forcaArremesso;

    private void Arremessar()
    {
        quantidade--;
        proximoArremesso = Time.time + intervalo;
        FrascoAguaBenta frasco = Instantiate(prefabFrasco, PontoDeSaida, Random.rotation);
        frasco.Lancar(VelocidadeInicial, prefabExplosao, explosao, meusColliders);
    }

    // Botão no Inspector (em Play): explode onde a mira está apontando, sem gastar frasco
    [ContextMenu("Testar explosão no ponto da mira")]
    public void TestarExplosao()
    {
        if (SimularTrajetoria(out Vector3 impacto))
            ExplosaoAguaBenta.Criar(prefabExplosao, impacto, explosao);
    }

    // Simula a parábola em passos e procura onde o frasco vai bater
    private bool SimularTrajetoria(out Vector3 impacto)
    {
        pontos.Clear();
        Vector3 posicao = PontoDeSaida;
        Vector3 velocidade = VelocidadeInicial;
        int mascara = explosao.camadasObstaculo | explosao.camadasAlvo;
        pontos.Add(posicao);

        for (int i = 0; i < passosTrajetoria; i++)
        {
            Vector3 proxima = posicao + velocidade * passoDeTempo + 0.5f * Physics.gravity * passoDeTempo * passoDeTempo;
            velocidade += Physics.gravity * passoDeTempo;
            Vector3 trecho = proxima - posicao;
            if (Physics.Raycast(posicao, trecho.normalized, out RaycastHit hit, trecho.magnitude, mascara, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                impacto = hit.point + hit.normal * 0.15f;
                pontos.Add(hit.point);
                return true;
            }
            posicao = proxima;
            pontos.Add(posicao);
        }
        impacto = posicao;
        return false;
    }

    private void DesenharMira()
    {
        bool bate = SimularTrajetoria(out Vector3 impacto);
        if (linhaTrajetoria != null)
        {
            linhaTrajetoria.positionCount = pontos.Count;
            linhaTrajetoria.SetPositions(pontos.ToArray());
        }
        if (anelDoRaio != null)
        {
            anelDoRaio.enabled = bate;
            const int segmentos = 48;
            anelDoRaio.positionCount = segmentos;
            for (int i = 0; i < segmentos; i++)
            {
                float a = i / (float)segmentos * Mathf.PI * 2f;
                anelDoRaio.SetPosition(i, impacto + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * explosao.raio);
            }
        }
    }

    private void Mostrar(bool visivel)
    {
        if (linhaTrajetoria != null) linhaTrajetoria.enabled = visivel;
        if (anelDoRaio != null && !visivel) anelDoRaio.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Prévia do raio no ponto mirado, também fora do Play (ajuda a calibrar)
        if (cameraPlayer == null) cameraPlayer = GetComponentInChildren<Camera>().transform;
        if (SimularTrajetoria(out Vector3 impacto))
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(impacto, explosao.raio);
        }
        Gizmos.color = Color.white;
        for (int i = 1; i < pontos.Count; i++) Gizmos.DrawLine(pontos[i - 1], pontos[i]);
    }
}
