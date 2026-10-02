# 0.2.2 — correção do fechamento da captura

Corrige a queda do Companion ao fechar o painel com a captura desligada ou pausada.
O registro do usuário confirmou uma segunda chamada de fechamento dentro do evento
Closing do WPF. A parada sem captura ativa terminava imediatamente e tornava a
chamada reentrante. O fechamento agora aguarda o evento inicial terminar e compartilha
uma única operação com a saída do aplicativo.

A suíte de captura passou em 18 verificações, incluindo fechamento sem iniciar,
fechamento pausado, fechamento ativo e fechamento simultâneo à saída do aplicativo.
As imagens continuam sendo descartadas. Nenhuma alteração na instalação do usuário
é feita pelos testes isolados.
Atualização isolada 0.2.1 → 0.2.2 aprovada, com 481 arquivos conferidos, 30 verificações do pet e 18 de captura/fechamento aprovadas. Perfil e registro de produção preservados.
