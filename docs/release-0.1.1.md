# Rodada 0.1.1 — preparar a validação real e instalar facilmente

Projeto principal: `D:/Work/Strigoi-Companion`. Fonte, documentação, artefatos e scripts de empacotamento ficam nesse diretório.

## Alterações

- Pausar o pet agora impede que prévias manuais voltem a animá-lo.
- O painel acompanha mudanças de modo feitas pela bandeja.
- F12 foi removido dos atalhos; preferências antigas migram para F10. A documentação Win32 reserva F12 para depuradores.
- Testes de desktop usam perfil isolado e não registram atalhos globais, evitando disputar o atalho do usuário.
- A validação consulta o hit testing do Windows em 100 pontos sem mover o cursor nem injetar cliques. Isso amplia a evidência do modo bloqueado, mas não substitui entradas reais em um jogo.
- Nova tela “Testar com meu jogo”: amostragem do processo durante até 30 minutos, resultados locais e respostas do jogador inicialmente não testadas. Amostras curtas nunca viram aprovação de 30 minutos.
- Medição distingue private bytes de private working set. Valores indisponíveis permanecem nulos.
- Instalador NSIS em português, por usuário, com runtime incluído, atalhos, abertura opcional no fim e desinstalador. Não requer administrador.
- Ícone do Familiar nos atalhos e na bandeja, derivado do mesmo sprite.

## Como reproduzir

```powershell
dotnet run --project tests/Strigoi.Companion.Checks -c Release -- src/Strigoi.Companion/Assets/familiar.json
./scripts/build-installer.ps1 -MakeNsis 'caminho/para/makensis.exe'
./scripts/test-installer.ps1
```

O teste do instalador verifica os arquivos por hash, abre a aplicação instalada em modo diagnóstico e remove somente os arquivos instalados. Um arquivo adicional é colocado para comprovar que a desinstalação preserva conteúdo do usuário. O teste isolado não cria atalhos nem altera o registro de instalação de produção.

O compilador NSIS utilizado nesta máquina está no cache já existente do electron-builder. A IDE Strigoi não participa do build nem da execução do Companion. O instalador local ainda não tem assinatura digital de publicação.

## Evidência desta rodada

- 23 verificações de domínio passaram, incluindo os casos de interpretação de amostras incompletas.
- 30 verificações de desktop passaram no aplicativo publicado e novamente no aplicativo extraído pelo instalador.
- Teste isolado de instalação/desinstalação aprovado: 480 arquivos conferidos por SHA-256, abertura do aplicativo aprovada, payload removido, arquivo adicional preservado e registro de produção inalterado. Atalhos de produção não foram criados no teste.
- Ensaio de desktop de 90,03 segundos: CPU média de 0,04194% da capacidade total, pico de private working set 70.844.416 bytes (~67,6 MiB), zero ativações inesperadas. Private bytes aumentaram 11.657.216 bytes desde o início; essa amostra curta com aquecimento não permite afirmar ausência de crescimento prolongado.
- Build autocontido e compilação NSIS concluídos. Strings do instalador compiladas explicitamente em UTF-8.

Relatórios reproduzíveis ficam em artifacts/release-0.1.1-smoke, artifacts/stability-0.1.1 e artifacts/installer-qa-16bf16c145854426a6b72366054ae810.

## Próximo gate

A Fase 1 continua aguardando teste com jogo, comparação de FPS/frametime, matriz de monitores/DPI e ensaio completo de 30 minutos. O usuário informou que ainda não havia testado; o instalador e o painel foram criados para facilitar essa etapa. Nenhum resultado sintético é tratado como homologação de gameplay. A Fase 2 de captura ainda não foi iniciada.

Referências: [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), [WindowFromPoint](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-windowfrompoint), [PROCESS_MEMORY_COUNTERS_EX2](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2), [NSIS Modern UI](https://nsis.sourceforge.io/Docs/Modern%20UI%202/Readme.html).
