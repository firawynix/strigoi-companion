# Entrega 0.2.0 — captura local opcional

Data: 2026-09-13. Projeto principal: `D:/Work/Strigoi-Companion`.

## O que muda

- Captura Windows.Graphics.Capture de uma janela escolhida explicitamente. Abre
  desligada, sem inferência, rede, gravação automática ou seleção de monitor.
- Painel de prévia, iniciar, pausar/retomar e encerrar. Fechar o painel libera a
  captura; fechar ou minimizar a janela capturada também encerra a sessão.
- Imagens reduzidas até 640×360, análise até 2 Hz, histórico de oito imagens
  limitado a 7.372.800 bytes. Superfícies nativas e cópias temporárias são adicionais.
  No Windows 11 24H2+ também é solicitado MinUpdateInterval de 500 ms ao sistema.
- Diferença média dos canais de cor com limiar 12% e intervalo mínimo de dois
  segundos entre eventos. Isso identifica mudança visual, não seu significado.
- Redimensionamento recria o pool; limites de resolução e erros são apresentados
  no painel. Não há fallback para captura do monitor ou reinício automático.
- Contagem de ativações renomeada para `ObservedActivations`, relatório schema 2.
  Uma ativação pode ser intencional; não implica roubo de foco. Relatórios antigos
  permanecem intactos, com seu schema original.
- Instalador detecta a versão instalada, atualiza no mesmo diretório e mantém
  perfil/relatórios. Oferece fechar o aplicativo e tentar novamente, sem encerrá-lo
  à força. Bloqueia substituir uma versão mais recente. Não é um atualizador online.

## Teste do jogador da versão 0.1.1

Original: `artifacts/player-reports/validation-20260912-205133-def484468dc84e4f952b33ff511c8bab.json`.

Foram registrados 1800,02 segundos, 361 amostras, CPU média 0,004485%, pico de
private working set 110.616.576 bytes (105,5 MiB), zero ativações e maior intervalo
entre amostras de 5,02 segundos. O jogo foi informado pelo jogador como
STAR WARS Zero Company, em borderless; o aplicativo não detectou o jogo.

Cliques, foco, arraste e atalho foram marcados como aprovados. A resposta negativa
sobre fluidez foi corrigida explicitamente pelo jogador em 2026-09-13: “Opção
marcada por engano.” A correção está em arquivo separado
`artifacts/player-reports/validation-30min-player-correction.json`, com hash do
original. Resultado efetivo: avaliação positiva do jogador nessa configuração.
Isso não é um benchmark de FPS nem homologação de outros jogos ou monitores.

## Verificações da entrega

- Build da solução sem erros ou avisos; 30 verificações do domínio.
- 11 verificações com WGC real sobre janelas sintéticas do próprio processo:
  cor da janela escolhida, mudança, resize, limites, descarte ao parar, nova sessão,
  minimizar, fechar e controles de iniciar/pausar/retomar/fechar.
- Prévia do painel renderizada e inspecionada. Nenhum jogo do usuário capturado
  durante os testes automatizados.
- Microbenchmark de captura: 15,24 segundos, janela solicitada em 1920×1080,
  CPU média 0,214%, pico de private working set 120.803.328 bytes (115,2 MiB).
  Inclui o próprio gerador de imagem; não mede FPS, GPU ou custo numa partida.
- O teste de encerramento após minimizar usa uma nova janela sintética selecionada
  para a etapa seguinte. A restauração nativa da fixture não foi validada pelo
  teste automatizado; não confundir com aprovação desse cenário em um jogo.

Atualização 0.1.1 → 0.2.0 aprovada em teste isolado: 481 arquivos conferidos,
30 verificações do aplicativo instalado e 11 verificações de captura aprovadas.
Perfil real e registro de produção permaneceram intactos, assim como o arquivo
extra criado no diretório de teste. Desinstalação exata aprovada.
Instalação nova também aprovada com as mesmas verificações.

Os resultados do instalador ficam em `artifacts/installer-qa-*/installer-result.json`.
O script testa hash de cada arquivo, versão do executável, atualização no mesmo
diretório, execução instalada, captura real, desinstalação de arquivos conhecidos
e preservação de um arquivo extra. Compara também os hashes dos JSONs do perfil
real e o registro de instalação antes/depois, sem alterar a instalação do jogador.
Atalhos de produção não são criados pelo modo de teste isolado.

## Próximas etapas

Esta entrega inicia a Fase 2; não encerra todo o roadmap. Restam replay navegável,
captura em jogos reais, comparação ligada/desligada de FPS/frametime e GPU,
monitores com DPI distintos, HDR, tela cheia exclusiva e falhas de dispositivo
induzidas. Ask Mode, Run Intent, Risk Engine, Dialogue Assist e as intervenções
visuais do Familiar ficam para etapas posteriores.

## Referências técnicas

- [Microsoft — Screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)
- [Microsoft — CreateForWindow](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow)
- [Microsoft — MinUpdateInterval](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.minupdateinterval)

As assinaturas de interop também foram verificadas no SDK Windows 10.0.26100.0
instalado. O aplicativo compila contra esse SDK, com plataforma mínima 19041.
