# 0.2.3 — relatório local de sessão da captura

- [x] Botão **Salvar relatório da sessão** no painel de captura.
- [x] Arquivo JSON local com instante de início/fim, janela escolhida, PID, imagens
  processadas, mudanças visuais, estado de término e uso de replay/pausa/retomada.
- [x] Nenhuma imagem, texto capturado, FPS do jogo, OCR ou inferência é salvo.
- [x] Teste automatizado confirma que o relatório não contém pixels e registra os
  controles usados; 19 verificações de captura aprovadas.
- [ ] O relatório não mede FPS, VRAM, qualidade do replay ou correção semântica das
  mudanças visuais. Esses dados continuam no benchmark específico.

O arquivo fica em `%LOCALAPPDATA%\StrigoiCompanion\reports`. Clique no botão antes
de fechar o painel; depois de iniciar a captura, ele continua disponível mesmo após
pausar ou encerrar. Salvar de novo atualiza o mesmo arquivo daquela sessão com o
estado mais recente. Atualização isolada 0.2.2 → 0.2.3 será validada antes da entrega.
