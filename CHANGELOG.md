# Notas da versão

Todas as mudanças relevantes do Gearbox ficam registradas aqui. O formato segue o
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e as versões seguem o
[Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Não lançado]

## [0.3.0] - 2026-10-26

### Novo: módulo Ilha dinâmica

- Uma "ilha" preta no topo do monitor principal, no estilo dos apps de notch do macOS (NotchNook, boring.notch).
- Mostra o que está tocando no Spotify, no navegador ou em qualquer app integrado aos controles de mídia do
  Windows: capa e barrinhas animadas na ilha recolhida e um aviso rápido ao trocar de faixa.
- Ao passar o mouse, abre com capa, título, artista, botões anterior/reproduzir/próxima, hora, data e bateria.
  Clicar nos controles não tira o foco do app que você está usando.
- Avisos rápidos ao mudar o volume ou o mudo e ao ligar/desligar o carregador.
- Some enquanto um jogo, vídeo ou apresentação ocupa a tela inteira e vira só uma alça fina quando a janela em
  foco passa por baixo dela (maximizada, por exemplo), para não cobrir abas e botões.
- Fundo acrílico (ligado por padrão): desfoca o que está atrás da ilha, como os menus do Windows 11. Com os
  efeitos de transparência do Windows desligados ou no modo economia de energia, o fundo fica sólido.
- Opacidade do tom preto ajustável (20–100%, padrão 60%) na página do módulo: com o acrílico, quanto menor, mais
  o desfoque aparece; sem ele, o fundo fica só translúcido. Texto, capa e ícones continuam opacos.
- Aba "Controles" na ilha aberta, ao lado de "Mídia":
  - Botões de Wi‑Fi (com o nome da rede conectada) e Bluetooth: clique liga/desliga; botão direito abre a lista de
    redes ou os dispositivos Bluetooth do Windows.
  - Slider de volume e botão de mudo. Arrastar até 0 ativa o mudo; mover acima de 0 tira o mudo.
  - Troca da saída de áudio (fone, caixa, HDMI) direto na ilha. Usa um recurso interno do Windows e pode deixar de
    funcionar após uma atualização do sistema; nesse caso, a troca só não acontece.
- Visual no estilo [shadcn/ui](https://ui.shadcn.com): cinzas neutros, botões e abas sem o visual padrão do Windows,
  slider e barra de volume finos e arredondados, e uma borda de 1 px quase transparente em volta da ilha (efeito de
  vidro), que pode ser desligada em "Aparência".
- Cantos de cima côncavos, que se fundem com a borda da tela como o notch do MacBook, nos avisos e na ilha aberta.
  Recolhida, a ilha mantém o tamanho e o formato de antes, para não cobrir nada no topo da tela.
- Ícones da ilha com o [Phosphor Icons](https://phosphoricons.com) (licença MIT). As abas "Mídia" e "Controles"
  mostram só o ícone (o nome aparece na dica). Wi‑Fi e Bluetooth mudam de ícone quando desligados. Na tomada, a
  bateria mostra o ícone de carregando e a porcentagem ao lado.
- Conectado por cabo, o botão do Wi‑Fi vira o da rede cabeada: ícone de cabo, nome da rede e velocidade do link
  (ex.: "Rede · 1 Gbps"). O clique abre as configurações de Ethernet.
- Aba "Armazenamento": o uso de cada disco em um anel (vermelho a partir de 90%), com o espaço livre. Clicar num disco
  o abre no Explorer.
- Aba "Sistema": processador, placa de vídeo e memória com uso em tempo real, e o modelo da placa-mãe. Mostra a
  temperatura da placa de vídeo (quando o driver informa) e a do sistema (zona térmica, quando o PC tem), sem
  instalar drivers. Só é atualizada enquanto a aba está aberta.
- Cada conteúdo e comportamento pode ser desligado na página do módulo (inclusive os dois grupos de controles
  rápidos, em "Controles rápidos").

> Notificações de outros apps não aparecem na ilha: a API do Windows para lê-las exige um app empacotado (MSIX).

### Corrigido

- Rodar o app pelo Rider ou pelo Visual Studio falhava com
  `Appx recipe file [...] Gearbox.build.appxrecipe does not exist`. Agora existe o perfil
  `Gearbox (Unpackaged)` em `src/Gearbox/Properties/launchSettings.json`, que roda o executável direto, sem
  empacotar em MSIX.

## [0.2.0] - 2026-10-18

### Novo: módulo Wallpaper

- Coloca um vídeo (MP4, WebM ou MKV, em loop e sem som, atrás dos ícones da área de trabalho) ou uma imagem
  (JPG, PNG, BMP ou WebP) como wallpaper.
- Mesmo wallpaper em todos os monitores, ou um arquivo diferente para cada monitor.
- O wallpaper do Gearbox é o padrão para monitores sem arquivo escolhido. Dá para desligar essa opção e manter
  o wallpaper do Windows.
- Ajuste da imagem ou do vídeo na tela: Preencher, Ajustar, Esticar ou Centralizar.
- Economia de recursos: o vídeo pausa quando um app em tela cheia ou maximizado cobre o monitor, na bateria
  e com o PC bloqueado ou a tela apagada. Cada uma dessas pausas pode ser desligada.
- Ao desligar o módulo, o wallpaper que o Windows tinha antes volta. O estado original fica salvo em
  `settings\Wallpaper.restore.json`, junto com uma cópia da imagem, então a restauração funciona mesmo depois
  de um crash ou se o arquivo original tiver sido apagado.
- Se o arquivo escolhido sumir, tiver formato não suportado ou o vídeo não puder ser reproduzido, a página
  avisa e o monitor usa o wallpaper de reserva.

> Vídeos WebM precisam da extensão **Extensões de Vídeo VP9** da Microsoft Store.

### Mudou

- **O projeto se chamava Darwin e agora se chama Gearbox.** Mudaram o executável (`Gearbox.exe`), a pasta de
  dados (`%LocalAppData%\Gearbox`), a pasta de instalação e o valor `Gearbox` em
  `HKCU\...\Run`.
- Instalações antigas são migradas pelo instalador: as configurações e os logs passam de
  `%LocalAppData%\Darwin` para `%LocalAppData%\Gearbox`, e "Iniciar com o Windows" continua ligado se já
  estava. O `AppId` não mudou, então o Gearbox atualiza o Darwin no lugar em vez de ser instalado ao lado dele.
- O instalador ficou menor: os componentes do Windows App SDK que o Gearbox não usa (IA, ML, busca semântica
  e Widgets) não vão mais junto.

### Corrigido

- Abrir a janela pelo ícone da bandeja podia derrubar o app se acontecesse um erro nesse momento. Agora o
  erro vai para o log e o Gearbox continua rodando.

## [0.1.0] - 2026-10-17

Primeira versão, ainda com o nome Darwin.

### Novo

- App na bandeja do sistema, com uma instância só (rodar de novo mostra a janela que já está aberta), página
  Geral (tema, iniciar com o Windows) e página Sobre.
- Módulos independentes que podem ser ligados e desligados. Um módulo que falha é desligado sozinho sem
  derrubar o app, e o motivo aparece na página dele.
- **Módulo Maximizar arredondado:** janelas maximizadas mantêm os cantos arredondados e uma borda colorida,
  com uma pequena margem até a borda da tela, como no macOS. Os cantos e a cor da borda exigem Windows 11.
- Instalador por usuário (sem pedir administrador) para x64 e ARM64. Ele encerra o app pelo caminho normal
  para que as janelas sejam restauradas antes de atualizar ou desinstalar.

[Não lançado]: https://github.com/Tiallysson/Gearbox/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/Tiallysson/Gearbox/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/Tiallysson/Gearbox/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Tiallysson/Gearbox/releases/tag/v0.1.0
