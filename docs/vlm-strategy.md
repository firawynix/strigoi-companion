# Estratégia VLM — decisão arquitetural de 2026-09-13

Esta decisão substitui a preferência exploratória por 4B e qualquer proposta de
usar modelos distintos para Ask/Talk e Watch. **Game performance first.**

- Um único VLM local pequeno e residente para ambos os modos.
- Primeiro avaliar 2B. 4B só após evidência de perda relevante de qualidade.
- Budgets de imagem, contexto útil e saída diferentes; mesma identidade de modelo.
- Watch futuro: entrada seletiva/reduzida, saída estruturada curta.
- Ask/Talk: captura/crop mais legível, contexto útil maior e resposta natural.
- Sem model swapping, sem dois modelos residentes e sem Watch completo antecipado.
- Fase 3 continua Ask/Talk sob demanda, cancelável e com contexto validado.
- Provider independente de família/tamanho; candidato não fica embutido nos contratos.

## Contrato de integração futura

Request: ID, sessão/janela/campanha, validade, intenção (Ask ou probe), imagens
preparadas, contexto, limite de saída, prazo/cancelamento e esquema opcional.
Response: ID de origem, texto/evento, evidências referenciadas, tempos, motivo de
término e identidade do modelo. Revalidar a sessão antes de mostrar qualquer resposta.

O runtime fica fora da UI. Um executor serial e fila limitada serão responsáveis
por não competir com o jogo. Futuro Watch cede lugar a Ask e descarta trabalho
obsoleto; nenhuma dessas políticas justifica abrir outro modelo. Esgotamento de
budget cancela/degrada trabalho, não altera automaticamente o modelo.

Manter uma capacidade de contexto alocada comum, com budgets menores de texto
útil no Watch, evita tratar mudança de modo como solicitação de recarga. Isso deve
ser verificado no runtime escolhido. Ser model-agnostic significa permitir substituir
a configuração em outra sessão/versão após avaliação, não trocar pesos por pergunta.

## Benchmark antes da escolha

Instrumento executável: `benchmarks/vlm/bench.py`; protocolo completo:
`benchmarks/vlm/README.md`. Provider de teste isolado em `provider.py`; adapter
Ollama implementado sem obrigar o produto a usar esse runtime. O antigo ADR de
runtime da Fase 0 permanece histórico; a decisão de produção virá da medição.

Mede leitura/UI, cena, classificação de eventos, Ask/Talk, latência, RAM/VRAM e
impacto no jogo. Notas humanas e dados reais ausentes ficam pendentes. A suíte
sintética valida o instrumento; não homologa um modelo para gameplay.

Estado em 2026-09-13: `qwen3.5:2b` foi executado localmente na Radeon RX 7900 XT
em 13 casos sintéticos, três repetições. Watch probe teve p95 de 0,488 s e Ask
teve p95 de 1,127 s; o pico amostrado foi 2,63 GiB de RAM do runtime e 3,58 GiB
de VRAM dedicada. JSON estruturado foi válido em 100%, mas macro-F1 de eventos
ficou em 0,734, abaixo da proposta de 0,90. O ensaio é de transporte e orçamento,
não de gameplay: nenhum 2B/4B foi escolhido definitivamente e ainda faltam corpus
real, revisão humana e impacto com jogo.

A versão 0.3.0 integra somente Ask/Talk experimental, acionado por pergunta e
usando a imagem atual em memória. Não integra Watch automático.
