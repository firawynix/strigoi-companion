# Strigoi Companion — Fase 0

Data: 12/09/2026. Estado: análise concluída; arquitetura proposta para o primeiro protótipo. Nenhum código do Companion implementado nesta fase. Compatibilidade e desempenho em jogos ainda não medidos.

## Decisão proposta

Construir um aplicativo Windows independente em C# / WPF / .NET 10, começando pelo Familiar animado em uma janela transparente pequena. Usar interop Win32 para controle de foco e passagem de cliques. Adotar Windows.Graphics.Capture na etapa seguinte, direcionada à janela escolhida pelo jogador. Manter inferência em processo separado, inicialmente opcional.

Esta é uma escolha de engenharia para validar, não uma alegação de superioridade medida. O SDK .NET 10.0.400 está instalado nesta máquina. O primeiro gate poderá rejeitar WPF se transparência, interação ou custo de composição não atenderem aos requisitos.

## Contexto do produto

Strigoi Companion pertence ao ecossistema Strigoi, mas inicia e funciona sem a IDE. O personagem visível chama-se Familiar; o motor de contexto e decisões chama-se GameSense. O Familiar padrão é um vampirinho gótico, espirituoso, expressivo e solidário. O jogador controla sarcasmo, linguagem, frequência e voz.

Watch Mode observa e reage com moderação. Ask Mode responde quando solicitado. Dialogue Assist avalia escolhas conforme a campanha. Quest Guardian alerta sobre riscos conhecidos de tempo e progressão. Run Intent registra prioridades e spoilers por campanha; Promises registra objetivos explícitos. A personalidade apresenta a resposta, preservando fatos, incertezas e intenção do jogador.

O produto acompanha a experiência do jogador. Recomendações precisam distinguir observação, conhecimento externo, inferência e desconhecimento. Avisos são assistência de melhor esforço: ausência de alerta nunca significa que uma ação é segura.

## Evidências do projeto existente

Repositório inspecionado: `D:/Work/Strigoi`, commit `25dc770`, versão 0.1.10. O estado Git estava limpo na inspeção. `D:/Strigoi IDE` contém a instalação empacotada, não a fonte principal. Não foram encontrados AGENTS.md no repositório ou nos pais D:/ e D:/Work. Foram lidos o índice de notas e os fluxos de runtime e busca; conclusões conferidas contra os fontes relevantes.

| Área | Evidência local | Consequência para o Companion |
|---|---|---|
| Aplicativo | `package.json`; workspaces electron-app, browser-app e strigoi-core | A IDE é Electron/Theia. Não transportar a distribuição inteira para um pet. |
| Integração | `strigoi-core/package.json`: Theia 1.75.0, TypeScript e licença UNLICENSED | Reutilização interna deve manter proveniência; extração pública futura exige esclarecer licenciamento. |
| Mascote da IDE | `strigoi-core/src/browser/strigoi-core-contribution.ts`, criação do companion a partir da linha 344 | Elemento DOM dentro do painel Theia, não janela desktop independente. |
| Visual da IDE | `strigoi-core/src/browser/style/empty-editor.css`, linhas 78–85 | PNG `strigoi-mascot-full-v3.png` com flutuação CSS; não é um player de sprites. |
| Runtime | `strigoi-core/src/node/llama-cpp-runtime-provider.ts:loadModel()` | llama.cpp próprio, ROCm/Vulkan/CPU, servidor local e processo gerenciado. |
| Contrato | `strigoi-core/src/common/local-runtime-service.ts` | Referência útil para status, descoberta, carga e descarga; não equivale ao contrato completo de geração do GameSense. |
| Pesquisa | `strigoi-core/src/node/web-search-service.ts:search()/parseResults()` | DuckDuckGo HTML, timeout 12 s, consulta até 200 caracteres e até 8 resultados; retorna título, URL e snippet. Não confirma consequências. |
| Escopo ausente | Busca direcionada nos fontes por captura, overlay, GameSense, RunIntent e QuestGuardian | Não foi localizada implementação desses recursos no escopo inspecionado. Não é uma auditoria linha a linha de toda a IDE. |

Uma divergência documental importa: a nota de runtime menciona contexto 8k, mas o código atual usa 16.384 tokens, cache RAM 8.192 e todas as camadas na GPU quando acelerado. A porta é fixa em 18789. Copiar esse perfil para jogar pode disputar memória e processamento com o jogo; a nota antiga não deve orientar o novo orçamento.

## Assets do pet criado para o ChatGPT

Origem confirmada pelo usuário: usar os sprites do pet personalizado que ele criou para a feature Pets do ChatGPT, mostrado nas imagens desta conversa. Os PNGs do mascote dentro da IDE não são a base visual escolhida. A inspeção da IDE permanece relevante somente para arquitetura e experiência de runtime/pesquisa.

Pacote local encontrado: `C:/Users/jpgal/.codex/pets/strigoi/pet.json` e `spritesheet.webp`. O spritesheet instalado e a exportação abaixo são idênticos por SHA-256: `4BD1D2ACED06B773D6398898AEA250F3C00374070613CBEC63FED0A6479A08D0`. O manifesto antigo descreve o personagem como mascote da IDE; esse texto não muda a origem e o uso escolhidos pelo usuário. As imagens enviadas são visualmente consistentes com o contact sheet inspecionado. Não foi inspecionado o armazenamento interno do ChatGPT para comparar seus bytes.

Existe um pacote animado separado em `C:/Users/jpgal/Documents/Codex/2026-08-27/hatch-pet-c-users-jpgal-codex/outputs/`, com `strigoi-pet.json`, `strigoi-spritesheet.webp`, contact sheet e relatórios de validação. O manifesto identifica o vampirinho como Strigoi, sprite versão 2. O nome relativo `spritesheet.webp` do manifesto precisa ser reconciliado com o arquivo de exportação `strigoi-spritesheet.webp` ao importar.

O relatório existente registra WebP RGBA, 1536 × 2288 pixels, grade 8 × 11, células de 192 × 208, sem erros estruturais. O contact sheet foi inspecionado visualmente nesta fase. Isso não substitui validar a animação no player novo.

| Linha | Sequência disponível | Células usadas segundo o relatório | Uso proposto |
|---|---|---:|---|
| 0 | idle | 7, incluindo neutro | Idle e piscada embutida; separar neutro do loop conforme manifesto de importação |
| 1–2 | running-right / running-left | 8 cada | Movimento opcional; não necessário para arrastar |
| 3 | waving | 4 | Saudação / reação positiva |
| 4 | jumping | 5 | Celebração |
| 5 | failed | 8 | Frustração; revisar trechos para uso expressivo |
| 6 | waiting | 6 | Espera |
| 7 | running | 6 | Atividade; nome não representa a mesma corrida lateral |
| 8 | review | 6 | Pensando |
| 9–10 | olhares direcionais | 8 cada | Atenção visual opcional |

Não há sequências dedicadas comprovadas para shocked, warning, facepalm ou sleep. O protótipo deve mapear estados lógicos para poses existentes com fallback explícito. Warning pode combinar pose neutra com indicador; sleep pode suspender animação com indicador de pausa. Não apresentar esses fallbacks como animações novas. A produção dos estados faltantes pode ocorrer depois do gate técnico. Registrar duração, loop, prioridade e interrupção em um manifesto próprio; nunca animar células vazias.

## ADR-001 — Shell Windows

Status: proposto, aceitação condicionada ao gate da Fase 1.

| Opção | Adequação | Custo / incerteza | Decisão |
|---|---|---|---|
| WPF / .NET 10 | Transparência documentada, acesso a HWND e controles desktop; SDK local disponível | Exige interop para comportamento de janela; medir composição e DPI | Escolha inicial |
| WinUI 3 | Windowing moderno e integração com Windows App SDK | Benefícios de UI moderna pouco decisivos para um sprite; alpha e interação exigem prova específica | Alternativa se houver benefício demonstrado |
| Electron independente | Conhecimento existente, WebP/CSS e APIs de janela/click-through | Chromium e processos adicionais; custo real ainda não medido | Alternativa rápida se WPF falhar; nunca embutir Theia |
| Tauri 2 | Janela transparente e APIs de interação disponíveis | Rust + webview + captura nativa ampliam a superfície inicial | Não escolhido para este protótipo |

A propriedade WPF AllowsTransparency requer WindowStyle=None. Esse recurso cuida da aparência; passagem de cliques e foco precisam de implementação própria. Referência: [Microsoft — AllowsTransparency](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency).

Para layered windows, a documentação Win32 descreve passagem de mouse com WS_EX_TRANSPARENT. Usar a API apropriada à janela real, com testes entre processos. Evitar tratar IsHitTestVisible=false como solução suficiente. Referências: [Window Features](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features) e [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles).

As alternativas oferecem APIs úteis, mas a escolha acima é inferência de adequação ao produto, não benchmark: [Electron — Custom Window Interactions](https://www.electronjs.org/docs/latest/tutorial/custom-window-interactions), [WinUI — Windowing overview](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview), [Tauri — Configuration](https://v2.tauri.app/reference/config/).

### Interação e compatibilidade

- **Observação bloqueada:** janela pequena, topmost e sem ativação; cliques atravessam o pet. Um atalho configurável ou menu da bandeja abre Ask Mode.
- **Pet interativo:** jogador pode optar por clicar na área visível para pedir ajuda. Área transparente deve passar cliques; caso a implementação por pixel falhe, usar uma região pequena documentada e medir a interferência.
- **Reposicionamento:** modo explícito de desbloqueio, arraste e escala; salvar posição lógica por monitor e limitar à área de trabalho disponível ao restaurar.
- **Ask Mode:** painel separado recebe foco apenas por ação explícita; Escape fecha. Um jogo pode pausar ao perder foco. Esse comportamento deve ser testado e comunicado, não ocultado com promessas de foco impossível.

Suporte inicial: Windows 11 x64, SDR, jogos em janela e borderless. Fullscreen exclusivo, HDR e combinações particulares de overlays/anti-cheat ficam na matriz de investigação. Topmost não é uma garantia universal sobre qualquer jogo. Captura bloqueada produz estado indisponível; não migrar silenciosamente para captura do monitor nem introduzir hooks/injeção no jogo.

## ADR-002 — Organização e reutilização

Atualização de estratégia em 2026-09-13: [VLM único residente e benchmark 2B primeiro](vlm-strategy.md)
substitui as preferências exploratórias de runtime/modelo desta análise. Fase 3
permanece Ask/Talk; Watch completo não é antecipado.

Propor repositório separado `Strigoi-Companion`, ainda não criado, com três unidades iniciais: aplicativo WPF, biblioteca de domínio sem dependência de UI e testes. Captura/interop podem começar em uma pasta Platform do aplicativo. Só extrair novos pacotes quando houver consumidor real. Inferência continua fora do processo da UI.

Reutilizar o pacote visual do pet personalizado do ChatGPT por cópia versionada com hash e proveniência; converter WebP para PNG durante o empacotamento se necessário para evitar exigir codec WebP na máquina do jogador. Preservar o original. Reutilizar os sprites não implica reutilizar a implementação interna da feature Pets do ChatGPT: janela, animação e interação do Companion terão implementação própria. Aproveitar os contratos e a experiência do runtime da IDE como referência, sem importar classes Theia ou carregar a IDE.

Não compartilhar automaticamente o servidor em 18789. Cada processo gerenciado terá identidade, configuração e endpoint próprios; um servidor externo explicitamente configurado não será encerrado pelo Companion. Compartilhar arquivo GGUF por referência é diferente de compartilhar a memória do modelo. Priorizar um caminho llama.cpp; Ollama e LM Studio ficam como adaptações futuras se necessárias.

## GameSense: fluxo e contratos

Fluxo planejado: captura da janela → percepção → evento normalizado → contexto da campanha → política de intervenção → consulta de conhecimento quando necessária → resposta factual → personalidade → sprite e texto.

Implementar a princípio módulos no mesmo aplicativo, com fila limitada e cancelamento; o desenho não requer microserviços. Percepção e inferência não podem bloquear o dispatcher de UI.

| Contrato | Informação mínima |
|---|---|
| Observação | ID, instante monotônico, jogo/campanha/sessão, origem, janela, validade e confiança de leitura |
| Evento | Tipo candidato ou confirmado, observações de origem, importância e chave de deduplicação |
| Evidência | Afirmação, fonte, trecho relevante, jogo/versão/condições, data de consulta, conflitos e grau de suporte |
| Decisão | Intenção da resposta, evidências, incerteza, nível de spoiler, política aplicada e prazo de validade |
| Reação | Texto curto, emoção, prioridade e expiração; sem autoridade para alterar fatos |

Trocar de jogo/campanha ou cancelar a pergunta invalida respostas pendentes. Campanhas devem poder ser escolhidas manualmente: identificar executável não identifica save. Reload/rollback pode criar um ramo de memória; não tratar decisões anteriores ao reload como fatos do save atual.

Run Intent e Promises residem em armazenamento local por campanha. SQLite é a proposta para memória estruturada; preferências simples podem usar JSON com escrita atômica. Frames ficam em memória por padrão, com prazo e teto de bytes. Histórico de evidências precisa sobreviver a reinícios; gravações de replay exigem opção explícita.

### Captura e percepção

Windows.Graphics.Capture permite obter frames de janela; testar suporte, resize, fechamento, minimização e perda do dispositivo. HDR exige tratamento específico de formato e cor. Consultar [Microsoft — Screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture) e [CreateForWindow](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow).

Separar frequência de aquisição, análise e inferência: descartar frames na aplicação não prova que o custo de captura desapareceu. Começar com análise até 2 Hz, redução de resolução e captura sob demanda para Ask Mode. Um ring buffer de frames reduzidos deve ter limite por bytes, sem guardar referências inválidas a superfícies devolvidas ao pool. Eventos rápidos de combate podem exigir maior frequência posteriormente; não prometer elogio técnico de esquivas com amostragem esparsa.

Heurísticas genéricas detectam mudança, não conhecem morte ou qualidade da jogada. Esses eventos começam como candidatos. Confirmação exige evidência visual suficiente ou adapter testado. Manter contadores de falso positivo e medir em replay.

### Conhecimento, spoilers e Quest Guardian

Uma busca com snippet não autoriza o selo “confirmado”. A próxima camada deve obter evidência da página, conferir identidade do jogo/quest/versão e condições da escolha, registrar fontes e conflitos. Dois sites que repetem o mesmo texto não são necessariamente duas confirmações independentes. Quando insuficiente, responder “não consegui confirmar”.

Páginas e OCR são dados não confiáveis. Seu conteúdo não pode alterar políticas, executar comandos ou conceder acesso. O gateway recebe consulta mínima, aplica limites de resposta/tempo/redirecionamento e bloqueia destinos locais/privados no fetch de páginas. Falhas de rede permanecem explícitas. O modelo não navega livremente.

Filtro de spoiler opera antes de gerar o texto da persona. Níveis blind/hint/light/full têm regras por campanha e confirmação para revelar além do permitido. Até nomear uma quest futura pode ser spoiler. Avisos sobre conteúdo perdível têm permissão própria.

Quest Guardian exige fonte e gatilho: tempo real, tempo do jogo, descanso, capítulo ou decisão. UNKNOWN é o padrão sem evidência; SAFE demanda escopo e justificativa. Não usar relógio real para simular prazo do jogo sem adapter. Pre-flight é melhor esforço e nunca bloqueia controles; não prometer aviso antes de um clique que ainda não foi observado.

## Sequência de execução revisada

Feature registrada após esta análise: **Embodied Reactions / Intervention Animations**, com gestos e aproximação visual do cursor, incluindo agarrar a seta sem alterar input real. Implementar após Run Intent + Risk Engine + Dialogue Assist. Especificação complementar: [Intervention Animations](strigoi-companion-interventions.md).

Os itens de desempenho, privacidade, personalidade factual e observabilidade deixam de ser etapas tardias e passam a critérios de todas as fases.

| Entrega | Escopo | Gate |
|---|---|---|
| Fase 1 — Familiar | Player, transparência, bandeja, arraste, escala, persistência, lock e atalho | Checklist abaixo aprovado; nenhum modelo necessário |
| Fase 2 — Captura | Janela escolhida, buffer limitado, análise de mudança e replay mínimo | Captura correta, pausa e falhas explícitas, custo medido |
| Fase 3 — Ask básico | Pergunta sobre contexto atual, provider opcional, cancelamento e evidências | Resposta expira ao trocar contexto; desconhecimento explícito |
| Fase 4 — Campanha e diálogo | Game Chronicle, Run Intent, Promises, memória, OCR e política de spoiler | Casos de escolhas verificáveis e separação entre saves |
| Fase 5 — Conhecimento e Guardian | Fontes verificadas, um adapter real, risco de progressão | Conjunto de quests/decisões com verdade de referência e testes negativos |
| Fase 6 — Watch e acabamento | Reações contextuais, elogio com evidência, frequência, TTS opcional e distribuição | Sessão completa com desempenho e utilidade avaliados |

O roadmap original permanece como visão de produto; suas 21 etapas numeradas de 0 a 20 não devem ser tratadas como promessa de compatibilidade universal. Escolher o primeiro jogo pelo acesso a casos reproduzíveis, fontes e sessões de teste. Dawnwalker é uma preferência do contexto, não uma integração cuja API ou leitura de save tenha sido comprovada.

## Plano concreto da Fase 1

1. Criar solução independente e manifesto do Familiar. Conferir hashes e células utilizadas; importar somente os assets necessários. Verificação: identidade visual preservada, nenhuma célula vazia na reprodução.
2. Implementar janela e ciclo de animação. Verificação: alpha correto, animações interrompíveis, relógio pausado quando oculto e CPU estável em repouso.
3. Implementar lock, modo interativo, reposicionamento, atalho e bandeja. Verificação: cliques chegam ao jogo no modo bloqueado e o atalho permite recuperar a interface.
4. Persistir monitor, posição e escala. Verificação: reinício, mudança de DPI e remoção de monitor não tornam o pet inacessível.
5. Medir com jogo em janela e borderless. Verificação: checklist, logs e métricas; qualquer falha material impede avançar para captura.

### Critérios de aceitação

- Iniciar sem IDE, servidor ou modelo instalado; encerrar sem processo órfão.
- Foco: zero ativações espontâneas durante 30 minutos de observação; abertura do painel somente por pedido explícito.
- Passagem de cliques: 100 cliques de teste em regiões cobertas no modo bloqueado alcançam o destino; nenhum clique dispara duas ações.
- Escala: 100%, 125%, 150% e 200%; dois monitores com DPI diferente e coordenadas negativas. Se faltar hardware, registrar como não testado, sem declarar aprovado.
- Estados: idle, piscada, positivo, celebração, pensando, alerta, frustração e pausa têm apresentação/fallback definido; verificar visualmente. Facepalm e shocked dedicados permanecem lacunas de arte.
- Persistência: arrastar, travar, reiniciar, desconectar monitor e recuperar posição pela bandeja.
- Estabilidade: 30 minutos sem crescimento contínuo de memória; queda do renderer/animação não bloqueia o jogo.

### Orçamento inicial proposto, ainda não medido

| Métrica | Meta de engenharia |
|---|---|
| Shell com pet, sem captura/modelo | Até 150 MiB de private working set; CPU média até 1% da capacidade total da máquina |
| Animação | 12–15 FPS conforme arte; sem render loop ativo quando oculto |
| Buffer da Fase 2 | Até 64 MiB adicionais, sem disco por padrão |
| Modelo | Nenhuma VRAM de inferência reservada na Fase 1; carga posterior explícita e com orçamento |
| Impacto no jogo | Queda de FPS médio até 2% e de 1% lows até 3%, sujeita ao ruído da medição |

Comparar Companion desligado/ligado em três pares alternados, mesma cena reproduzível, resolução, configurações e aquecimento. Registrar FPS médio, 1% lows, p95/p99 de frametime, CPU, memória e memória GPU; separar shell, captura e inferência. Se a variação da cena for maior que o limiar, a medição é inconclusiva. Não usar tok/s de um benchmark anterior como evidência de coexistência com o jogo.

Para IA posterior: começar sem modelo residente, avaliar modelo pequeno com contexto limitado e CPU/partial offload conforme medição. Cancelar ou reduzir trabalho sob pressão. Uma classificação simples pode ser determinística; não exige SLM por princípio.

## Riscos que orientam a implementação

| Risco | Tratamento e condição de revisão |
|---|---|
| WPF não atende composição/click-through | Gate inicial; isolar janela e avaliar renderização Win32/DirectComposition ou shell Electron independente |
| Overlays/fullscreen impedem visibilidade | Matriz por jogo e modo; recomendar borderless quando necessário |
| Modelo disputa GPU | Limites, descarregamento e benchmark com jogo; não herdar perfil full-offload da IDE |
| Conhecimento incorreto ou desatualizado | Evidência por condição/versão, conflitos visíveis e desconhecimento explícito |
| Capture observa contexto errado | Vincular janela selecionada; suspender quando inválida; não seguir foreground arbitrário |
| Estado do save ambíguo | Seleção manual de campanha e confirmação de reload; descartar respostas antigas |
| Arte não cobre emoções | Manifesto com fallbacks e backlog artístico explícito |

## Handover

Concluído: localização do código, inspeção dos caminhos relevantes, inventário visual, comparação de stacks com documentação oficial, ADRs propostos, limites de compatibilidade e plano verificável da Fase 1.

Não realizado: implementação, build do Companion, instalação de dependências, captura de gameplay, teste de foco em jogo ou benchmark. Nenhum desses gates está aprovado por esta análise. Não houve necessidade de reconstruir a IDE, pois os fontes não foram alterados.

Próxima ação proposta: executar a Fase 1 acima, começando pelo protótipo de janela e sprites para aceitar ou rejeitar a escolha de WPF antes de integrar IA.
