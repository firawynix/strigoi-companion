# Política de privacidade — Strigoi Companion Assistant

Última atualização: 2 de outubro de 2026.

O Strigoi Companion Assistant funciona localmente no Windows e inclui o Firaw Assistente de Trabalho. O módulo de jogos salva preferências, nomes de jogos e sessões, fatos e notas que você registrar, relatórios que você pedir para salvar e diagnósticos limitados em `%LOCALAPPDATA%\StrigoiCompanion`. O módulo de trabalho salva tarefas, checklists, anotações e preferências em `%LOCALAPPDATA%\Firawynix\WorkAssistant`. O aplicativo não exige conta. O desenvolvedor não recebe esses arquivos automaticamente.

Ao ativar a captura, o aplicativo lê a janela de jogo escolhida ou acompanhada. As imagens recentes ficam apenas na memória durante a sessão e são descartadas ao encerrar ou pausar a captura. O relatório de sessão salva metadados, sem imagens da tela. O recurso experimental de pergunta sobre imagem envia a amostra atual somente ao Ollama configurado em `localhost`, quando você aciona a pergunta; ele não baixa modelos nem envia essa imagem a um servidor do Strigoi Companion.

Se você ativar explicitamente a pesquisa na web e fizer uma pergunta, o aplicativo envia a consulta e um contexto de texto limitado ao provedor de pesquisa para obter resultados. Imagens da captura e o diário completo da sessão não são enviados nessa pesquisa. O movimento automático do cursor no módulo de trabalho é opcional e controlado pelo usuário. A conexão à Internet não é necessária para as demais funções locais.

Você pode apagar seus dados locais fechando os aplicativos e removendo as duas pastas indicadas acima. A desinstalação preserva esses dados para evitar perda acidental.

Suporte: hugo@firawynix.com.br.
