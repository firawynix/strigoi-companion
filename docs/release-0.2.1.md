# 0.2.1 — replay das imagens recentes

Implementado antes da validação de captura pelo jogador, a pedido dele.

- [x] Rever até oito imagens recentes, com Anterior e Próxima.
- [x] Mostrar o tempo da imagem desde o início da sessão.
- [x] Manter a sequência em revisão fixa enquanto a captura continua.
- [x] Voltar imediatamente à imagem mais recente e acompanhar ao vivo.
- [x] Descartar revisão ao pausar, encerrar ou fechar a captura.
- [x] Preservar dimensões de cada imagem, inclusive após redimensionamento.
- [x] Não gravar ou enviar imagens.
- [ ] Teste de captura e replay em jogo pelo jogador.
- [ ] Benchmark comparativo de FPS/frametime com captura ligada e desligada.

A revisão é uma sequência de amostras, não um vídeo contínuo nem uma duração
garantida. O Windows pode não produzir imagens novas de uma janela estática.
O histórico e a revisão têm até 7.372.800 bytes cada; prévia e superfícies de
captura têm custo adicional. A cópia usada pela revisão não altera o histórico.

Testes: 33 verificações de domínio; suíte de captura ampliada para 15 verificações
usando somente janelas sintéticas do próprio aplicativo. Inclui navegação, retorno
ao vivo, captura avançando durante a revisão e descarte ao pausar/fechar.
O instalador mantém a atualização no mesmo diretório e a preservação do perfil.

Uso: Captura local → Iniciar captura → Rever recentes → Anterior/Próxima →
Voltar ao vivo. Pausar continua descartando todas as imagens.
Atualização isolada 0.2.0 → 0.2.1 aprovada: 481 arquivos conferidos, 30 verificações do pet instalado e 15 de captura/replay aprovadas. Perfil real e registro de produção preservados; desinstalação exata aprovada.
