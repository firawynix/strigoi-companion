# Game Chronicle — 0.4.0

Dados locais: `%LOCALAPPDATA%\StrigoiCompanion\games\<game-id>`. A identidade
vem da janela confirmada pelo jogador; o VLM não escolhe o jogo.

- `game.json` / `game.md`: identidade do jogo.
- `runs/<run-id>/run.json`, `state.json`, `run.md`: campanha consolidada.
- `events.jsonl`, `promises.jsonl`: fatos e promessas estruturados.
- `sessions/*.md`: journal humano apenas de eventos significativos.
- `knowledge/knowledge.jsonl`, `sources.jsonl`: evidência externa separada.

`GameChronicleService` oferece abertura/criação/renomeação de runs, sessões,
eventos observados/declarados, promises, conhecimento externo, retrieval lexical
limitado e pesquisa controlada. `unknown` não entra no estado consolidado.
Arquivos de estado usam substituição atômica; JSONL é append-only e leitores
ignoram uma última linha inválida após encerramento anormal.

O Ask/Talk recebe somente um pacote pequeno de memórias relevantes, com origem
marcada. A camada web é `IWebResearchProvider`; a implementação inicial consulta
DuckDuckGo apenas quando o jogador habilita pesquisa e a pergunta parece factual.
Ela recebe somente nome do jogo, pergunta e política de spoiler. `blind` não
executa consulta. A resposta externa é cacheada com fonte/confiança e não vira
observação da campanha.
