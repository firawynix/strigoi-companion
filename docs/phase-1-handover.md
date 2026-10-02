# Fase 1 — primeira versão executável

12/09/2026 · versão 0.1.0 · gate de gameplay ainda aberto.

## Entrega

Aplicativo WPF / .NET 10 independente em `D:/Work/Strigoi-Companion`. Usa o spritesheet original do pet personalizado do ChatGPT, convertido para PNG com igualdade dos pixels RGBA conferida. Nenhuma alteração na IDE Strigoi.

Fluxo implementado: iniciar → pet e controles da primeira execução → configurar tamanho/modo → fechar painel → animação sem ativação espontânea → recuperar por atalho ou bandeja → persistir posição/configuração ao sair.

Principais arquivos: `PetWindow.cs` controla janela, animação e posição; `ControlWindow.cs` oferece controles; `Program.cs` gerencia processo, bandeja e teste de desktop; `Native.cs` contém a ponte Win32. `Settings.cs` e `Animation.cs` formam o domínio. `Assets/familiar.json` declara frames, timings e fallbacks.

## Validação realizada

- Compilação e publicação Windows x64 autocontida concluídas sem avisos ou erros.
- 15 verificações de domínio passaram: posição em coordenadas negativas, limites, configuração inválida, persistência/backup, recuperação de JSON e versão desconhecida, animação e células usadas.
- 22 verificações no executável publicado passaram: janela layered, estilos de passagem de cliques e não ativação, ausência na barra de tarefas, troca de modo, pausa quando oculto/redução de movimento, renderização das nove expressões, persistência, posição na área atual, ausência de ativação espontânea, foreground preservado e painel renderizado.
- Imagens renderizadas do pet e painel inspecionadas. Pet íntegro e painel legível com rolagem. A renderização do teste é do próprio aplicativo, não captura do desktop.
- Conversão de assets conferida: WebP original e PNG têm pixels RGBA idênticos.

Na amostra curta do executável portátil: CPU média de aproximadamente 0,31% da capacidade total, working set total ~168 MiB e private bytes ~129 MiB. Essas são métricas distintas; private bytes não é private working set. O limite de private working set proposto na Fase 0 ainda não foi aferido. Não houve jogo em execução no ensaio e não há conclusão sobre FPS/frametime.

## Pendências do gate

1. Abrir um jogo em janela ou borderless; deixar o pet bloqueado e confirmar que mouse e teclado continuam funcionando, inclusive clicando em sua região.
2. Reposicionar sobre o jogo sem ativação inesperada, voltar a bloquear e recuperar os controles pelo atalho.
3. Rodar a matriz de dois monitores, DPI 100/125/150/200%, coordenadas negativas e desconexão física de monitor. O teste de domínio não substitui hardware.
4. Comparar desempenho sem/com pet em cenas reproduzíveis e executar soak de 30 minutos.
5. Confirmar inicialização/encerramento reais pela bandeja e restauração entre sessões no uso cotidiano.

Foco, DPI e custo de composição podem exigir ajustes. Nenhuma compatibilidade universal é prometida. Não avançar para captura/IA antes de resolver falhas materiais do gate atual.

## Nova feature anotada

Embodied Reactions / Intervention Animations está registrada como evolução após Run Intent, Risk Engine e Dialogue Assist. A imagem do Familiar pode acompanhar e agarrar visualmente o cursor; coordenadas reais e cliques permanecem livres. Ainda não implementada nem simulada como se houvesse conhecimento de diálogo.
