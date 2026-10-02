# 0.5.1 — correções do Familiar-first

- Mensagens contextuais agora usam somente o balão junto ao Familiar; não geram notificações do Windows.
- O balão é um popup independente, com largura máxima de 280 px e quebra de linha, sem ser recortado pela janela do sprite.
- A detecção automática exige janela grande e exclui navegadores, editores, chats e outros aplicativos comuns. Janelas menores continuam disponíveis pelo fallback manual.
- Abrir Quick Talk não encerra a captura: janelas do próprio Companion não são interpretadas como saída do jogo. O HUD não fecha durante uma pergunta em andamento.
