# Embodied Reactions / Intervention Animations

Registrado em 12/09/2026 a pedido do usuário. Feature de produto planejada após Run Intent, Risk Engine e Dialogue Assist; não faz parte da Fase 1.

O Familiar usa o corpo para expressar uma recomendação: olha e aponta para uma escolha, corre até o cursor, agarra visualmente a seta e é arrastado junto quando o jogador insiste. Pode também incentivar uma boa escolha ou apontar loot. O tom é vampírico, dramático e configurável.

## Controle do jogador

A intervenção padrão é exclusivamente visual: não move o cursor real, não injeta entradas, não bloqueia cliques nem captura o mouse. O overlay da intervenção permanece click-through. Ainda pode distrair ou encobrir texto: limitar área, duração e frequência, evitar o rótulo da opção e oferecer redução de movimento.

Modos: Off, Subtle, Playful e Dramatic. Subtle como proposta inicial; Dramatic depende de opção explícita. Intervenções críticas que exigem segundo clique ficam fora do MVP e exigiriam decisão de produto separada. Não implementar bloqueio de input como efeito colateral de animação.

## Comportamento pretendido

| Risco | Expressão |
|---|---|
| Baixo | Olhar desconfiado ou apontar |
| Médio | Aproximar-se e balançar a cabeça |
| Alto | Agarrar visualmente o cursor e acompanhar seu movimento |
| Crítico | Aviso curto sobre conflito confirmado com objetivo da campanha; clique permanece livre |

Exemplos de fala: “NÃO.”; “Você disse que queria salvar ela.”; “…eu vou fingir que não vi isso.” Linguagem forte depende da preferência do jogador. Resultado após clicar deve ser observado; não inferir a escolha apenas pelo botão do mouse.

## Contrato futuro

Intervenção recebe targetId, região em coordenadas de tela, identidade de janela/campanha, severity, reason, evidenceIds, animation, validade e spoiler policy. Animação não calcula risco nem inventa consequências. UNKNOWN não dispara advertência factual crítica.

Animation Controller aproxima o Familiar do cursor e aplica gripOffset; inclinação e balanço respondem à velocidade com limites. Soltar por timeout, mudança de contexto, movimento rápido, cancelamento ou Escape. Restaurar posição original ao terminar. Dados antigos de OCR/posição invalidam a intervenção; não perseguir cursor fora da janela do jogo.

## Dependências e gate

- Run Intent e Promises por campanha; identificação de opções com confiança e validade.
- Risk Engine com evidências; política de spoiler antes do texto.
- Arte dedicada grab, pull, dragged, release/fall/recover; sprites atuais podem prototipar deslocamento, mas não representam essas animações completas.
- Testar mesmo trajeto e cliques com intervenção ligada/desligada: posições do cursor e entradas recebidas pelo jogo devem permanecer iguais.
- Testar DPI misto, controller sem cursor, troca de janela, OCR obsoleto, redução de movimento e cancelamento.

Objetivo: “ele tenta te impedir, mas você continua sendo o jogador”.
