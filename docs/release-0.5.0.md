# 0.5.0 — Familiar-first UX

O Familiar é agora a jornada principal. Clique esquerdo abre Quick Talk; clique direito abre o painel compacto com acompanhamento, recap, runs, pesquisa web, spoilers, pasta local e Control Center.

Com **Acompanhar jogo** ativo, o Companion verifica a janela em primeiro plano a cada 1,5 s. A heurística ignora o próprio Companion e janelas comuns. Ao encontrar uma candidata, inicia a captura local, cria ou retoma o Game Chronicle, recupera a última run usada e prepara o runtime conforme a preferência. Sem Watch Mode e sem inferência contínua.

Preferências novas: acompanhamento, detecção automática, início automático de Chronicle, modo de início do modelo e última run por jogo. `settings.json` v1 é migrado sem tocar nos dados do Chronicle. Pesquisa web mantém o contrato da 0.4.0: só jogo confirmado e pergunta; nenhuma imagem ou journal sai da máquina.

O Control Center preserva captura manual, prévia, replay e diagnósticos como fallback.

## Limites

A detecção é deliberadamente conservadora; uma janela que não seja elegível deve ser escolhida pelo fallback manual. O modo de início automático prepara o runtime local e pode falhar sem encerrar o Companion. O modelo continua o candidato 2B já definido; esta entrega não altera a escolha de VLM, não adiciona Watch Mode e não troca modelos.
