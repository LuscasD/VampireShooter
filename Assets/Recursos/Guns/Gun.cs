using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Gun : MonoBehaviour
{
    public UnityEvent onGunShoot;

    [Header("Tiro")]
    public float cooldownDeTiro = 1.5f;
    public bool automatic;
    [Tooltip("Se ligado, só atira com o botão direito segurado (engatilhado/mirando)")]
    public bool precisaMirarParaAtirar = true;

    [Header("Munição")]
    public int capacidade = 5;
    public int municao = 5;
    public int municaoReserva = 15;
    public float tempoDeRecarga = 2.2f;

    [Header("Precisão (graus de dispersão)")]
    public float dispersaoMirando = 0f;
    public float dispersaoSemMirar = 4f;
    public float dispersaoMovendo = 3f;

    [Header("Recuo")]
    public float recuoCameraVertical = 5f;
    public float recuoCameraHorizontal = 1.5f;
    public float forcaRecuoArma = 1f;
    [Range(0.1f, 1f)] public float velocidadeAoMirar = 0.6f;

    [Header("Barulho (raio em que os inimigos ouvem o tiro)")]
    public float raioDoBarulho = 30f;

    [Header("Sons (nome no AudioManager, vazio = sem som)")]
    public string somTiro = "tiro";
    public string somEngatilhar = "lock";
    public string somVazio = "";
    public string somRecarregar = "";

    [Header("UI (opcional)")]
    public Text textoMunicao;
    public Graphic mira;
    [Range(0f, 1f)] public float alfaDaMira = 0.8f;
    [Range(0f, 1f)] public float alfaDaMiraMirando = 0.3f;

    private bool isHolding = false;
    private bool recarregando = false;
    private float cooldownAtual;

    private Animator anim;
    private AudioManager audioManager;
    private DanoDaArma dano;
    private InputManager input;
    private PlayerMovment movimento;
    private OlharPlayer olhar;
    private ArmaMovimento armaMovimento;

    private void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
        anim = GetComponent<Animator>();
        dano = GetComponent<DanoDaArma>();
        input = GetComponentInParent<InputManager>();
        movimento = GetComponentInParent<PlayerMovment>();
        olhar = GetComponentInParent<OlharPlayer>();
        armaMovimento = GetComponentInParent<ArmaMovimento>();
    }

    private void Update()
    {
        if (input == null) return;

        // Mirar / engatilhar
        bool querMirar = input.Mirando && !recarregando;
        if (querMirar && !isHolding) TocarSom(somEngatilhar);
        isHolding = querMirar;

        if (anim != null) anim.SetBool("isHolding", isHolding);
        if (olhar != null) olhar.Mirando = isHolding;
        if (armaMovimento != null) armaMovimento.Mirando = isHolding;
        if (movimento != null)
        {
            movimento.MultiplicadorExterno = isHolding ? velocidadeAoMirar : 1f;
            movimento.PodeCorrer = !isHolding;
        }

        cooldownAtual -= Time.deltaTime;

        if (input.RecarregarPressionado) TentarRecarregar();

        bool gatilho = automatic ? input.AtirarSegurado : input.AtirarPressionado;
        if (gatilho && !recarregando && cooldownAtual <= 0f && (isHolding || !precisaMirarParaAtirar))
        {
            if (municao > 0)
                Atirar();
            else
            {
                TocarSom(somVazio);
                cooldownAtual = 0.3f;
                TentarRecarregar();
            }
        }

        if (textoMunicao != null)
            textoMunicao.text = recarregando ? "..." : municao + " / " + municaoReserva;

        // Ponto de mira some um pouco ao mirar, para não brigar com a alça da arma
        if (mira != null)
        {
            Color cor = mira.color;
            cor.a = Mathf.MoveTowards(cor.a, isHolding ? alfaDaMiraMirando : alfaDaMira, 4f * Time.deltaTime);
            mira.color = cor;
        }
    }

    private void Atirar()
    {
        municao--;
        cooldownAtual = cooldownDeTiro;

        // Menos precisão sem mirar, andando ou no ar (mirando, o movimento atrapalha bem menos)
        float dispersao = isHolding ? dispersaoMirando : dispersaoSemMirar;
        if (movimento != null)
        {
            float movendo = dispersaoMovendo * Mathf.Clamp01(movimento.VelocidadeAtual / movimento.velocidadeCorrendo);
            if (!movimento.NoChao) movendo += dispersaoMovendo;
            dispersao += isHolding ? movendo * 0.25f : movendo;
        }
        if (dano != null) dano.dispersao = dispersao;

        TocarSom(somTiro);
        Barulho.Emitir(transform.position, raioDoBarulho);
        onGunShoot.Invoke();
        if (anim != null) anim.SetTrigger("shooting");

        if (olhar != null) olhar.AdicionarRecuo(recuoCameraVertical, Random.Range(-recuoCameraHorizontal, recuoCameraHorizontal));
        if (armaMovimento != null) armaMovimento.Recuar(forcaRecuoArma);
    }

    private void TentarRecarregar()
    {
        if (recarregando || municao >= capacidade || municaoReserva <= 0) return;
        StartCoroutine(Recarregar());
    }

    private IEnumerator Recarregar()
    {
        recarregando = true;
        if (armaMovimento != null) armaMovimento.Recarregando = true;
        TocarSom(somRecarregar);

        yield return new WaitForSeconds(tempoDeRecarga);

        int quantidade = Mathf.Min(capacidade - municao, municaoReserva);
        municao += quantidade;
        municaoReserva -= quantidade;

        recarregando = false;
        if (armaMovimento != null) armaMovimento.Recarregando = false;
    }

    private void TocarSom(string nome)
    {
        if (audioManager != null && !string.IsNullOrEmpty(nome)) audioManager.Play(nome);
    }
}
