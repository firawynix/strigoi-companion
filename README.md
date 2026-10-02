# Strigoi Companion

Versão atual do código: **0.8.0**. Aplicativo gratuito para Windows. O código e os recursos visuais são distribuídos sem uma licença de reutilização pública declarada; consulte a proveniência dos sprites antes de redistribuir ou modificar os assets.

O projeto usa o nome **Strigoi Companion** reservado no Microsoft Partner Center. A versão 0.7.5 foi enviada para certificação na Microsoft Store em 2 de outubro de 2026; a disponibilidade depende da aprovação. O pacote MSIX pode ser criado com `scripts/build-store.ps1`.

## Dois aplicativos integrados

`scripts/build-suite.ps1` gera dois pacotes Windows x64 gratuitos. **Strigoi Companion Assistant** abre primeiro o visual de jogos do Strigoi e oferece acesso ao Firaw Assistente de Trabalho pelo painel e pela bandeja. **Firaw Assistente de Trabalho** abre primeiro o visual de trabalho do Firaw e oferece acesso ao Familiar de jogos pelo botão **Jogos** e pela bandeja. Cada pacote contém os dois aplicativos completos; o módulo chamado abre em sua própria janela. As preferências e os dados locais de cada aplicativo continuam em seus diretórios próprios.

Para gerar os pacotes, mantenha os repositórios `strigoi-companion` e `firaw-work-assistant` lado a lado e execute `./scripts/build-suite.ps1`. Os ZIPs resultantes ficam em `artifacts/suite/0.8.0/`.

Estratégia de IA atual: [um único VLM local residente, começando pelo 2B](docs/vlm-strategy.md).
O Ask/Talk experimental usa a imagem atual da captura apenas quando o jogador pergunta.
O [benchmark de qualidade e impacto no jogo](benchmarks/vlm/README.md) continua sendo
o gate para decidir definitivamente entre 2B e 4B. Watch Mode não está integrado.

Na captura local 0.2.3, **Salvar relatório da sessão** cria um JSON de metadados
em `%LOCALAPPDATA%/StrigoiCompanion/reports`, sem gravar imagens ou conteúdo da tela.

Aplicativo Windows do Familiar animado. Usa os sprites do pet personalizado criado para o ChatGPT. Aplicativo independente da IDE Strigoi e do ChatGPT.

## Abrir

Para usar as funções de jogos e trabalho juntas, baixe o ZIP **Strigoi Companion Assistant** da [versão 0.8.0](https://github.com/firawynix/strigoi-companion/releases/tag/v0.8.0), extraia a pasta inteira e abra `Strigoi.Companion.exe`. Para usar somente o Strigoi original, o instalador **Strigoi-Companion-Setup-0.7.5.exe** está na [versão 0.7.5](https://github.com/firawynix/strigoi-companion/releases/tag/v0.7.5). Ele inclui .NET, instala somente para seu usuário em `%LOCALAPPDATA%/Programs/Strigoi Companion` e registra a desinstalação em Aplicativos do Windows.

## Familiar-first — 0.5.0

Abra o Companion, abra o jogo e deixe **Acompanhar jogo** ligado. O Familiar identifica a janela em primeiro plano de modo conservador, inicia a captura local e cria ou retoma o Chronicle sem abrir o painel grande. Clique esquerdo no Familiar abre **Quick Talk**; clique direito abre **Quick Control** para recap, runs, pesquisa web, spoilers e pasta local. O controle 👁 abaixo dele liga ou pausa o acompanhamento.

O **Control Center** continua acessível por Ctrl + Alt + F10, bandeja ou Quick Control. Ele preserva a seleção manual de janela, prévia, replay e diagnósticos como fallback. A detecção não usa o VLM para dar nome ao jogo; a identidade vem do título da janela. A pesquisa web continua opt-in e nunca recebe imagens ou journals.

Também existe uma distribuição portátil: execute `Strigoi.Companion.exe`, mantendo os demais arquivos e a pasta Assets juntos. Nenhum dos formatos exige conta ou modelo.

Na primeira abertura aparece o painel. O pet começa bloqueado para deixar cliques passarem ao aplicativo abaixo. Abra os controles com **Ctrl + Alt + F10**, ou pelo ícone do Companion na bandeja do Windows. Se o atalho estiver ocupado, escolha outra tecla no painel.

- **Bloqueado:** deixa os cliques passarem pelo pet.
- **Interativo:** clique no pet abre os controles.
- **Reposicionar:** arraste o pet; depois volte para Bloqueado para jogar.
- **Tamanho:** 50% a 200%.
- **Reduzir movimento:** mantém poses estáticas.
- **Bandeja:** abrir, reposicionar, recuperar posição, pausar/retomar, mostrar/ocultar e sair.
- **Escape:** fecha o painel. Abrir o painel é uma ação explícita que pode pausar jogos que pausam ao perder foco.

Se o Familiar sumir, use **Trazer pet de volta** na bandeja. Preferências ficam em `%LOCALAPPDATA%/StrigoiCompanion/settings.json`; diagnósticos limitados em tamanho ficam na mesma pasta. A captura de uma janela é opcional e começa desligada.

## Testar com um jogo

1. Abra os controles com Ctrl + Alt + F10 e escolha **Testar com meu jogo**.
2. Informe o jogo e se está em janela ou borderless. Abra o jogo e inicie a medição no painel.
3. Minimize os painéis do Companion. Mantenha o Familiar no modo Bloqueado durante a partida.
4. Confira se consegue clicar normalmente, inclusive na região do pet, e se ele não rouba o foco. Pela bandeja, experimente reposicionar e voltar a bloquear.
5. Após jogar, registre as respostas no painel e clique em **Salvar avaliação local**. Você pode encerrar antes dos 30 minutos; o relatório identificará a amostra curta.

A medição registra apenas CPU, memória e ativações da janela do Companion. Não captura gameplay nem mede FPS. Seus relatos ficam identificados como observações do jogador; itens não testados permanecem pendentes. **Abrir pasta dos relatórios** mostra os arquivos para compartilhar nesta conversa.

Para desinstalar, feche o Companion pela bandeja e use Aplicativos do Windows. As preferências e os relatórios são preservados.

## Estado real

Implementados: player de sprites, janela transparente topmost, estilos nativos de não ativação e passagem de cliques, modos de interação, arraste nativo, escala, posição normalizada por monitor, persistência com substituição atômica, atalho configurável, bandeja, pausa e prévias manuais de expressão.

Os estados warning, shocked, facepalm e sleep usam fallbacks declarados no manifesto. Ask/Talk ainda é experimental: não tem memória de campanha, OCR dedicado, GameSense, conhecimento de jogos, Dialogue Assist, Quest Guardian ou intervenções visuais no cursor.

O jogador concluiu um teste de 30 minutos em borderless, com relato positivo de cliques, foco, arraste, atalho e fluidez (correção da resposta de fluidez registrada em 2026-09-13). Isso não substitui testes com outros jogos, monitores com DPI diferentes ou comparação de FPS. Fullscreen exclusivo e HDR não são compatibilidades homologadas.

## Desenvolvimento

SDK .NET 10, Windows x64. A solução contém aplicativo WPF, domínio independente e verificações executáveis sem framework de teste externo.

```powershell
dotnet build Strigoi.Companion.slnx -c Release
dotnet run --project tests/Strigoi.Companion.Checks -c Release -- src/Strigoi.Companion/Assets/familiar.json
dotnet run --project src/Strigoi.Companion -c Release
dotnet publish src/Strigoi.Companion -c Release -r win-x64 --self-contained true -o artifacts/portable
```

Teste curto de desktop, com perfil isolado e saída automática:

```powershell
./artifacts/portable/Strigoi.Companion.exe --smoke-test D:/Work/Strigoi-Companion/artifacts/smoke
```

O diretório do teste deve ser um argumento único; use aspas se houver espaços. O teste cria janelas temporárias, verifica estilos, temporizadores, persistência e renderização e fecha. A métrica de CPU representa somente uma amostra de cinco segundos no desktop.

## Referências do projeto

- [Fase 0](docs/phase-0.md)
- [Intervention Animations](docs/strigoi-companion-interventions.md)
- [Handover da primeira versão](docs/phase-1-handover.md)
- [Rodada 0.1.1 e instalador](docs/release-0.1.1.md)
- [Proveniência dos sprites](src/Strigoi.Companion/Assets/PROVENANCE.md)

Não há publicação ou licença pública automática dos assets. Projeto local iniciado a pedido do usuário.

## Atualizar para 0.2.1

Feche o pet pela bandeja e execute o novo instalador. Ele reconhece a instalação
anterior, mostra a versão encontrada e atualiza no mesmo diretório, preservando
preferências e relatórios. Não desinstale antes. Instalar uma versão mais antiga
sobre uma mais recente é bloqueado pelo instalador 0.2.1.

## Captura local opcional

Nos controles ou na bandeja, abra **Captura local · escolher janela**. Escolha
explicitamente uma janela e clique em **Iniciar captura**. O painel mostra uma
prévia reduzida e a contagem de mudanças visuais. **Pausar** libera a captura e
descarta as imagens; **Retomar** inicia uma sessão nova. **Encerrar**, fechar o
painel, fechar ou minimizar o jogo interrompem a captura e limpam o histórico.

Nenhuma imagem é enviada ou gravada automaticamente. O histórico guarda até
oito imagens de no máximo 640×360 (7,1 MiB), somente em memória. Superfícies de
captura e cópias temporárias têm custo adicional. A aplicação analisa até 2 Hz;
no Windows 11 24H2+ também solicita esse intervalo ao sistema. Em versões
anteriores, limitar a análise não garante limitar o custo nativo da aquisição.
A captura aceita janelas de até 8.847.360 pixels. A borda de captura do Windows
permanece habilitada. Não há captura automática de monitor nem troca automática
de janela. Ainda não há entendimento de diálogos, perigos ou decisões.

Esta entrega inicia a Fase 2. Validação em jogos e benchmark
comparativo com captura ligada/desligada permanecem pendentes.

Diagnóstico real de captura, exclusivamente de janelas sintéticas do próprio app:

```powershell
./artifacts/releases/Strigoi-Companion-0.2.1/Strigoi.Companion.exe --capture-test D:/Work/Strigoi-Companion/artifacts/capture-0.2.1
```

Veja [a entrega 0.2.1](docs/release-0.2.1.md) para evidências e limites.

## Replay local — 0.2.1

Com a captura ativa, clique em **Rever recentes**, navegue com **Anterior** e
**Próxima** e use **Voltar ao vivo** para acompanhar novas imagens. A sequência
em revisão fica fixa enquanto a captura continua. São até oito amostras com o
tempo desde o início da sessão; não é vídeo contínuo. Pausar, encerrar, fechar o
painel ou perder a janela capturada limpa também a revisão. Não há gravação.
A revisão utiliza até mais 7.372.800 bytes de imagens, além do histórico ao vivo.

## Ask/Talk experimental — 0.4.0

Com uma captura ativa, escreva uma pergunta em **Pergunte sobre a imagem atual**
e use **Perguntar ao Familiar**. O Companion envia somente a amostra atual da
janela escolhida ao runtime local `Ollama` em `localhost`; ela não é gravada nem
enviada à internet. A resposta é descartada se a captura encerrar ou mudar de
sessão. O runtime não baixa modelos, não troca modelos e recusa a pergunta se
outro modelo estiver residente.

O primeiro candidato é `qwen3.5:2b`, que precisa estar instalado localmente.
Não há inferência automática, Watch Mode ou análise contínua. Fechar o painel
de captura também cancela uma pergunta em andamento.

O campo **Jogo desta sessão** é preenchido pela janela escolhida. Confirme ou
edite-o antes de perguntar: esse é o nome exibido pelo Companion, e o modelo
não pode substituí-lo por uma identificação visual especulativa.

## Game Chronicle

Ao iniciar uma captura, o Companion abre o Chronicle local do jogo e uma run.
Use **Lembrar fato do jogador** e **Criar promessa** para registrar o que você
disse; cada item mantém sua origem. **Ver recap** mostra o estado consolidado,
e **Abrir pasta do jogo** abre os dados locais em `%LOCALAPPDATA%\StrigoiCompanion\games`.
Journals são Markdown para leitura; eventos, promises, estado e conhecimento usam
JSON/JSONL para busca limitada. Imagens não entram no Chronicle.

Pesquisa web é opcional e usa somente o jogo confirmado e a pergunta, nunca a
imagem ou o journal. A política de spoiler controla a consulta: `blind`, `hint`,
`light` ou `full`. Conhecimento externo fica separado de observações e fatos do
jogador, com fonte e confiança próprias.
