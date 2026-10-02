# 0.3.2 — identidade confirmada do jogo

- [x] O campo **Jogo desta sessão** é preenchido pela janela escolhida e pode
  ser corrigido pelo jogador antes de uma pergunta.
- [x] A resposta mostra o nome confirmado pelo Companion, fora da saída do VLM.
- [x] Frases em que o VLM tenta identificar ou nomear um jogo são removidas da
  resposta visual; isso impede títulos alucinados de substituírem a sessão.
- [x] Verificação de domínio cobre a remoção de uma identificação visual errada
  sem remover a descrição restante da cena.

Conhecimento de lore e objetivos específicos ainda exige uma fonte verificável;
ele não é inferido do nome da janela ou da imagem.
