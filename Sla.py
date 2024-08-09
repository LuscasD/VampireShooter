ano_atual = 2024
nascimento = int(input("Digite seu ano de nascimento"))
idade = ano_atual - nascimento
resp = input("Você ja fez aniversario esse ano (S ou N)")
if resp == "N":
    idade -= 1    
print("Sua idade é: ", idade)

