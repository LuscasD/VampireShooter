using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.Events;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public UnityEvent onGunShoot;

    public float cooldownDeTiro;
    public bool automatic;


    public int municao;

    private bool isHolding = false;
    private Animator anim;

    private float cooldownAtual;

    private void Start()
    {
        municao = 5;
        cooldownAtual = cooldownDeTiro;
        anim = GetComponent<Animator>();
    }

    private void Update()
    {

        anim.SetBool("isHolding", isHolding);

        if (Input.GetMouseButtonDown(1))
        {
            isHolding = true;
            
        }
        if (Input.GetMouseButtonUp(1))
        {
            
            isHolding = false;

        }



        if (automatic)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (cooldownAtual <= 0 && municao > 0)
                {
                    onGunShoot.Invoke();
                    cooldownAtual = cooldownDeTiro;
                    anim.SetTrigger("shooting");
                    municao -= 1;
                }

            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (cooldownAtual <= 0)
                {
                    onGunShoot.Invoke();
                    cooldownAtual = cooldownDeTiro;
                    anim.SetTrigger("shooting");
                }

            }
        }
    }

}