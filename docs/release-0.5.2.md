# 0.5.2 — estabilidade do Quick Control e runtime local

Corrige o encerramento ao alternar Web no Quick Control: a árvore visual é reconstruída com segurança. O provider local agora detecta um Ollama ausente, inicia `ollama serve` automaticamente quando encontra uma instalação local suportada e aguarda até oito segundos. Se não conseguir iniciar, a pergunta mostra um erro recuperável e o Companion segue aberto.
