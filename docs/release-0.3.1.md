# 0.3.1 — correção da visão no Ask/Talk

- [x] O prompt declara explicitamente que a imagem anexada está disponível para
  análise visual e exige uma descrição dos elementos visíveis.
- [x] Bloqueia a resposta-padrão incorreta de que o modelo seria somente textual
  ou não conseguiria analisar a imagem.
- [x] Inclui o título da janela como metadado local não confiável, sem tratá-lo
  como instrução ou como prova de fatos do jogo.
- [x] Teste direto com a captura enviada pelo jogador confirmou descrição visual
  da interface em vez da recusa de visão.

Conhecimento específico de jogos não foi adicionado. A imagem não é enviada à
internet; busca externa exige um provider separado e consentimento explícito.
