# Benchmark VLM do Strigoi Companion

Objetivo: escolher o menor VLM local que atenda Ask/Talk e o futuro Watch com
budgets distintos, preservando o desempenho do jogo. Primeiro candidato de teste:
`qwen3.5:2b`. Nenhum modelo foi aprovado ou instalado por esta entrega.

Este diretório é independente do aplicativo. Não implementa Ask/Talk na UI,
Watch contínuo, captura de jogo, model swapping ou dois modelos residentes.
O adapter Ollama é uma ferramenta de avaliação; não fixa o runtime de produção.

## Executar

Python 3.12+, Pillow e psutil (dependências em requirements.txt). No ambiente atual
as duas bibliotecas já estavam disponíveis. A partir da raiz do projeto:

```powershell
python -m unittest discover -s benchmarks/vlm -p test_bench.py -v
python benchmarks/vlm/bench.py fixtures artifacts/vlm-benchmark/synthetic
python benchmarks/vlm/bench.py validate artifacts/vlm-benchmark/synthetic/manifest.json
```

O gerador produz 13 casos explicitamente sintéticos: negação, acentos, números,
objetivos, cena, morte explícita, pausa, vida zero ambígua, pergunta sem evidência,
preferência da campanha, texto não confiável e texto ilegível. A resposta esperada
nunca vai no prompt. Eles verificam o instrumento; NÃO representam qualidade em
gameplay suficiente para escolher 2B ou 4B.

Para inferência, disponibilize um runtime local exclusivo com os pesos 2B já
instalados. O runner não instala, baixa, descarrega ou troca modelos. Rejeita outro
modelo residente e candidatos remotos/cloud; confirme também a configuração local
do servidor. Informe o PID do processo principal do runtime para medir seus filhos.

```powershell
python benchmarks/vlm/bench.py run artifacts/vlm-benchmark/synthetic/manifest.json artifacts/vlm-benchmark/2b-run-01 --model qwen3.5:2b --runtime-pid 12345
```

Substitua 12345 pelo PID real. O destino deve ser novo, para preservar ensaios.
Uma execução tem aquecimento identificado e excluído da nota, três repetições,
ordem embaralhada com seed fixa e apenas uma inferência por vez. `--mode ask` ou
`--mode watch_probe` permite medir as cargas separadamente. `--interval` controla
o descanso entre solicitações (padrão 2s), não uma frequência garantida.

O modelo permanece residente após o ensaio (`keep_alive=-1`) conforme a estratégia.
O mesmo contexto alocado de 8192 tokens evita mudar a capacidade do runtime entre
perfis. O limite de texto útil é menor no Watch. A revisão de resultados registra
tempos de carga para detectar recargas inesperadas. Não confundir recomputar o
prompt com carregar pesos novamente. Timeout aborta o ensaio sem enviar outra
solicitação; o servidor ainda pode precisar terminar a solicitação cancelada.

## O que os arquivos medem

| Dimensão | Saída / avaliação |
|---|---|
| Leitura de diálogo/UI | CER normalizado, transcrição exata por caso, revisão específica de negação/números |
| Cena | Imagem + resposta + rubrica humana de fidelidade visual; sem LLM julgando automaticamente |
| Eventos | Validade estrita do JSON, classe esperada/prevista, macro-F1 incluindo falhas |
| Ask/Talk | Resposta natural, proxy lexical separado, planilha humana 0–4; nenhuma nota inventada |
| Latência | P50/P95 por perfil, tempo cliente até resposta completa, carga/avaliação/geração do runtime |
| RAM | USS/private working set agregado do runtime e filhos, com PID explícito |
| VRAM | Alocação dedicada/compartilhada por processo via contadores WDDM; também guarda `/api/ps` como métrica distinta |
| Jogo | CSVs de PresentMon: FPS médio, 1% low, p95/p99 dos intervalos; comparação de pares |

`results.json` contém respostas, hashes das imagens processadas, dimensões/crop,
configuração, falhas, runtime/modelo e métricas. `resources.json` guarda amostras
com tempo e picos amostrados; leituras ausentes ficam null. `/api/ps.size_vram` é
declaração de alocação do runtime, não uma medição independente da placa.
`human-review.csv` fica em branco para revisão humana; proxy lexical não aprova
qualidade. Imagens do corpus são lidas localmente, respostas podem reproduzir seu
texto; artefatos do benchmark precisam do mesmo cuidado com spoilers do corpus.

A coleta de recursos acontece aproximadamente a cada 2s + custo da consulta e
pode perder picos curtos. Usar a mesma instrumentação nos ensaios de referência e
de tratamento. O runner HTTP sem streaming mede resposta completa, não TTFT.
Inferência por imagem não demonstra qualidade de acompanhamento temporal contínuo.

## Corpus real necessário antes da decisão

Adicionar pelo menos 24 casos reais anotados, seis de cada grupo: diálogo/UI,
cena, eventos e Ask/Talk. Cobrir português, textos pequenos, negativas, números,
HUD denso, movimento/desfoque, menus sobrepostos, telas estáticas, incerteza,
mudança de contexto, spoilers e tentativa de instrução na imagem. Usar o jogo
do primeiro teste e pelo menos um segundo estilo de interface antes de generalizar.
Para eventos temporais, selecionar uma sequência e anotar o que NÃO pode ser
inferido de um quadro isolado. Futuro suporte a sequência exige nova versão do corpus.

Metade de cada grupo fica em `holdout`; não ajustar prompts com esses casos.
Usar `source: gameplay`, identidade de jogo/versão/resolução/cena na anotação e
gabarito conferido pelo jogador. O formato é o mesmo do manifesto sintético:
`image`, `question`, `context`, `watch_crop`, `ask_crop`, `expected`, `human_rubric`.
Imagens precisam estar dentro da pasta do corpus. Crops são previamente anotados:
o benchmark não implementa um seletor automático de regiões. Não confundir seu
resultado com o desempenho de um seletor futuro.

## Impacto no jogo — prioridade de decisão

Separar condições: A) pet+captura, sem VLM; B) mesmo cenário, VLM 2B residente
ocioso; C) mesmo cenário, solicitações Ask; D) mesmo cenário, carga offline de
watch_probe. D avalia budget e disputa de recursos, não é Watch Mode do produto.
Comparar A/B (custo de residência), B/C (Ask), B/D (probe) e A/C, A/D (impacto total).
Opcionalmente medir também jogo sozinho para custo total do Companion.

Para cada comparação: três pares de >=120s, ordem alternada AB/BA/AB, mesma cena
reproduzível/save, resolução, gráficos, driver, limitador de FPS e aquecimento.
Guardar também uso total da GPU e memória livre pelo instrumento da placa: a soma
dos processos do runtime não informa sozinha a folga de VRAM que o jogo precisa.
Não executar apenas o modelo sem o jogo e chamar isso de aprovação de coexistência.

Use a exportação CSV do PresentMon/CapFrameX com uma única métrica de intervalo
em todas as sessões (`FrameTime` ou `MsBetweenPresents`, conforme o exportador).
Não misturar processos, swapchains ou FPS de frames gerados com renderizados.
O script exige PID e, quando necessário, seleção explícita de swapchain.

```powershell
python benchmarks/vlm/bench.py monitor artifacts/vlm-benchmark/resources-baseline.json --runtime-pid 12345 --seconds 180
python benchmarks/vlm/bench.py compare-game caminho/pares.json artifacts/vlm-benchmark/game-comparison.json
```

`game-pairs.example.json` é só um molde, sem dados. FPS médio é 1000 / média dos
intervalos. 1% low é 1000 / média do 1% de intervalos mais lentos (arredondado para
cima). O CSV deve ser recortado previamente para a janela estável do ensaio;
nunca remover engasgos do tratamento. Essas métricas de CPU/presentação não
necessariamente representam todos os quadros efetivamente exibidos.

Limites iniciais propostos, herdados do roadmap: perda de FPS médio <=2% e de 1%
low <=3%; variância das referências acima de 2% torna o resultado inconclusivo.
Insuficiência de dados não passa. p95/p99 e engasgos visíveis também exigem revisão.
Interromper o ensaio se houver degradação perceptível; este harness não tem controle
de FPS em tempo real e não promete um limitador automático de impacto.

## Decidir 2B ou 4B

`config.json` registra budgets e limiares propostos antes de avaliar o holdout.
Ask: imagem/crop até 1280px, texto útil até 12000 caracteres, saída até 512 tokens.
Watch probe: até 512px, 2000 caracteres, JSON até 192 tokens. São pontos de partida
de benchmark, não requisitos comprovados de memória/velocidade do hardware.

Testar o 2B primeiro. Falha de performance exige reduzir trabalho e reavaliar,
não subir para 4B. Falha de qualidade deve ser classificada: leitura, crop ruim,
contexto ausente, esquema, alucinação ou capacidade. Ajustar uma vez no conjunto
de desenvolvimento e repetir com os budgets congelados. Só comparar 4B quando
persistir perda relevante de qualidade, usando o mesmo corpus/quantização/budgets
e uma execução separada com apenas esse modelo residente. Nenhum swapping automático.

Revisão humana: 0=errado, 1=grandes erros, 2=parcial, 3=correto com omissões menores,
4=correto e útil; avaliar fidelidade, leitura, utilidade, português e incerteza.
Qualquer afirmação crítica sem evidência bloqueia aprovação, mesmo com média alta.
Por proposta: média Ask >=3/4, macro-F1 de eventos >=0,90, JSON válido >=99%, leitura
crítica exata >=95%, p95 Watch <=2s e Ask <=8s. Resultados por categoria/holdout e
falhas individuais prevalecem sobre médias gerais. Esses limiares podem ser revistos
antes de congelar o ensaio, nunca relaxados depois apenas para declarar sucesso.

RAM/VRAM não têm um teto universal inventado: registrar pico, mínimo de memória
livre e folga durante o pior trecho do jogo. Sem essas leituras, ou sem revisão
humana/corpus real/pares de gameplay, a escolha permanece `not-decided`.

Referências verificadas: [Ollama chat](https://docs.ollama.com/api/chat),
[modelos residentes](https://docs.ollama.com/api/ps),
[saídas estruturadas](https://docs.ollama.com/capabilities/structured-outputs),
[PresentMon CSV](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md).
