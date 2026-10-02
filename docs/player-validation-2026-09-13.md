# Validação do jogador — 2026-09-13, versão 0.2.2

Evidências: relatório validation-20260913-100542-65af64b71a814f3a887dba89f02ac8fe.json,
print enviado pelo jogador mostrando 2.896 imagens processadas e 100 mudanças
visuais, e relato explícito de que não percebeu lag durante a captura.

- [x] Avaliação manual do pet nessa sessão: cliques, foco, arraste e atalho aprovados.
- [x] Captura em jogo funcionando com prévia, comprovada pelo print.
- [x] Fluidez percebida durante a captura aprovada pelo jogador.
- [ ] Confirmação individual de replay, pausar/retomar e fechamento nessa sessão de jogo.
- [ ] Benchmark comparativo de FPS/frametime com captura ligada/desligada.
- [ ] Benchmark real do VLM (qualidade, latência, memória e impacto no jogo).

O relatório é da 0.2.2, STAR WARS Zero Company em borderless, e contém todas as
cinco observações do jogador como passed. Porém contém somente uma amostra de
medição: status insufficient-data, duração calculada zero e médias indisponíveis.
Isso não significa que a partida durou zero segundos; significa que este arquivo
não registrou uma série temporal. Não permite concluir desempenho quantitativo.
O relatório anterior de 30 minutos continua válido como evidência do pet 0.1.1,
mas não pode ser usado como medição de captura ou VLM da versão atual.

Os controles de captura/replay/fechamento já passaram nos testes automatizados
com janelas sintéticas. O formulário do jogador não possui campos específicos
para esses controles; suas cinco respostas não comprovam esse checklist completo.

A partir da versão 0.2.3, o painel oferece um relatório de sessão próprio para
registrar os metadados da captura e os controles usados, sem incluir imagens.
