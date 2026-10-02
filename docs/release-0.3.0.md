# 0.3.0 — Ask/Talk local experimental

- [x] Pergunta explícita sobre a última imagem em memória da janela escolhida.
- [x] Transporte HTTP aceito somente para Ollama em `localhost`.
- [x] Uma inferência por vez, com cancelamento e descarte da resposta se a sessão
  de captura mudar ou encerrar.
- [x] Sem gravação de imagens, prompts ou respostas; sem download, troca de
  modelos ou Watch automático.
- [x] `qwen3.5:2b` avaliado no corpus sintético local: GPU integral, p95 de
  1,127 s para Ask e 0,488 s para a sonda estruturada.
- [ ] Escolha definitiva do modelo: F1 sintético de eventos de 0,734 está abaixo
  da meta de 0,90; faltam corpus real, revisão humana e coexistência com o jogo.

O modelo fica instalado localmente, mas o benchmark o descarregou ao terminar.
Ao usar Ask/Talk, Ollama pode mantê-lo residente para reduzir a próxima latência.
O Companion não cria Watch Mode e não faz chamadas sem uma pergunta do jogador.
