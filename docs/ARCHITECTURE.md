# Arquitetura

```
┌──────────────────────────── Gearbox (casco WinUI) ────────────────────────────┐
│ Program ─► App ─► Host (DI + Serilog)                                        │
│                    ├─ TrayIconService ─────────┐                             │
│                    ├─ MainWindowService ─► MainWindow ─► ModulePage          │
│                    └─ ModuleManager ◄──────────┘   (toggles e navegação)     │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │ IEnumerable<IGearboxModule>
┌──────────────────────────────▼───────────── Gearbox.Core (sem UI) ────────────┐
│ IGearboxModule · ModuleManager · ModuleFaultHub · ISettingsService<T>         │
│ IStartupService · GearboxPaths · GearboxLogging                                │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────── Gearbox.Modules.* (um por ferramenta) ─┐
│ Módulo (Start/Stop) ─► Logic/ (regras puras) + Native/ (aplica no sistema)   │
│ UI/ (página de configurações + ViewModel) · Strings/*.resw                   │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │
┌──────────────────────────────▼────────────────────────── Gearbox.Interop ─────┐
│ Windowing/: WinEventHookManager (hooks out-of-context) · NativeWindow        │
│             WindowPlacement · TransparentWindow                              │
│ Desktop/: DesktopLayer · DesktopWallpaper    Session/: SystemStateMonitor    │
│ Audio/: AudioVolumeMonitor · AudioEndpoints (Core Audio)                     │
│ Metrics/: PerformanceCounters · GraphicsAdapters · SystemMemory              │
│ Geometry/: PixelRect · FrameInsets                                           │
│ CsWin32 → user32 / dwmapi / kernel32 / wtsapi32 / IDesktopWallpaper          │
└──────────────────────────────────────────────────────────────────────────────┘
```

Cada pasta do `Gearbox.Interop` é um namespace (`Gearbox.Interop.Windowing`, `Gearbox.Interop.Desktop`, ...). Nenhuma
pasta pode se chamar `Windows`, `System` ou `Microsoft`: dentro de `Gearbox.Interop.*` ela esconderia o `Windows.Win32`
gerado pelo CsWin32 (e o `System.*`) e quebraria a resolução de nomes.

Dependências apontam só para baixo: o casco conhece Core, Interop e os módulos; um módulo conhece
Core e Interop; Core e Interop não conhecem ninguém.

## Inicialização

1. **`Program.Main`** chama `AppInstance.FindOrRegisterForKey`. Se já existe um Gearbox rodando, a
   ativação é redirecionada para ele (depois de `AllowSetForegroundWindow`) e o processo termina.
2. **`App.OnLaunched`**:
   - define `DispatcherShutdownMode = OnExplicitShutdown`, para o app continuar vivo sem janela;
   - monta o `Host` genérico: Serilog, `AddGearboxCore()`, serviços do casco e `Add{Módulo}Module()`;
   - cria o `WinEventHookManager` **na thread de UI** e liga `HandlerFaulted` ao `ModuleFaultHub`;
   - cria o ícone da bandeja e chama `ModuleManager.StartEnabledModules()`;
   - assina `AppInstance.Activated`, para mostrar a janela quando outra instância for aberta.
3. A janela principal só é criada no primeiro clique na bandeja. Fechá-la apenas a esconde.

## Módulos

- Cada módulo se registra com `services.AddXxxModule()`, que chama `AddGearboxModule<TModule, TPage>()`.
  O casco descobre os módulos por `IEnumerable<IGearboxModule>`: a navegação, o menu da bandeja e o
  `modules.json` (estado ligado/desligado) são gerados a partir dessa lista.
- A página do módulo é criada pelo DI (`ActivatorUtilities`) e embutida na `ModulePage` do casco, que
  desenha o cabeçalho, o botão "Ativar módulo" e o aviso de falha.

## Isolamento de falhas

| Onde a exceção acontece | Quem captura |
|---|---|
| `Start()` / `Stop()` | `ModuleManager` (try/catch) |
| Handler de evento de janela | `WinEventHookManager` (por assinante) → `HandlerFaulted` → `ModuleFaultHub` |
| Callback do próprio módulo (configurações, timers) | o módulo chama `IModuleFaultReporter.ReportFault` |
| Criação da página de configurações | `MainWindow` (mostra a mensagem no lugar da página) |
| Qualquer outra na UI | `App.UnhandledException` (loga e marca como tratada) |

Quando um módulo falha, o `ModuleManager` loga, chama `Stop()` (que desfaz as alterações), persiste
"desligado", guarda a exceção para a página mostrar e dispara `ModuleStateChanged`. A bandeja mostra
uma notificação.

## Threads

Tudo roda na thread de UI: o hook out-of-context entrega os eventos pelo loop de mensagens da thread
que o instalou, então os handlers dos módulos, o `ModuleManager` e os ViewModels compartilham a mesma
thread, sem locks. Eventos de outras threads (`UISettings.ColorValuesChanged`, `AppInstance.Activated`)
são reenviados com `DispatcherQueue.TryEnqueue`.

## Configurações

```
ViewModel ──Save()──► JsonSettingsService ──grava settings\{id}.json──► Changed ──► Módulo aplica na hora
```

As gravações são atômicas (arquivo temporário + `File.Move`). Um arquivo corrompido vira
`{id}.json.corrupt` e o módulo segue com os valores padrão.

## RoundedMaximize, em detalhe

```
EVENT_OBJECT_LOCATIONCHANGE (OBJID_WINDOW, CHILDID_SELF)
  └─► RoundedMaximizeEngine
        ├─ janela rastreada?
        │    ├─ maximizou de novo e o rcNormalPosition == pseudo  → restaura o original (toggle)
        │    ├─ maximizou de novo, mas foi movida antes           → maximizar novo
        │    └─ saiu do retângulo pseudo (moveu, redimensionou, snap) → para de rastrear e reverte o DWM
        └─ IsZoomed && EligibilityRules.IsEligible
             1. GetWindowPlacement (guarda o rcNormalPosition original)
             2. SetWindowPlacement(SW_SHOWNORMAL)
             3. SetWindowPos(PseudoMaximizeLayout.Compute(rcWork, margem, bordas invisíveis))
             4. guarda o retângulo pseudo resultante
             5. DwmSetWindowAttribute(CORNER_PREFERENCE = ROUND, BORDER_COLOR)
```

- `Logic/` (`EligibilityRules`, `PseudoMaximizeTracker`, `PseudoMaximizeLayout`, `ColorRef`,
  `ProcessNameCache`) não chama Win32 e é coberto por testes.
- As margens são medidas a partir da moldura **visível** (`DWMWA_EXTENDED_FRAME_BOUNDS`), descontando
  as bordas invisíveis de redimensionamento.
- Os eventos gerados pelas próprias chamadas do motor são ignorados enquanto ele está agindo (`_busy`).
- Alguns frameworks, como o WinForms, reaplicam por conta própria o último tamanho "normal" ao restaurar.
  Por isso o motor confere o resultado e, se preciso, insiste com `SetWindowPos`.
- `Stop()` restaura todas as janelas rastreadas e reverte os atributos do DWM. Isso também acontece
  ao sair pelo menu da bandeja.

## Wallpaper, em detalhe

```
WallpaperSettings + monitores (IDesktopWallpaper) + cache da galeria
  └─► WallpaperResolver ─► por monitor: galeria (cena | imagem gerada) | imagem | vídeo | manter o original
        └─► WallpaperEngine
              ├─ imagem  → IDesktopWallpaper.SetWallpaper (API do próprio Windows)
              ├─ vídeo   → wallpaper original por trás + VideoWallpaperWindow no WorkerW
              ├─ cena    → wallpaper original por trás + SceneWallpaperWindow no WorkerW
              └─ original → devolve a imagem guardada no snapshot
```

- **Galeria** (`Logic/Gallery`): `GalleryCatalog` tem os 10 wallpapers (5 estáticos, 5 interativos), por id estável;
  `WallpaperSettings.GalleryWallpaperId` é o ativo (nenhum por padrão). Com um ativo, ele vale para todos os
  monitores e ganha dos arquivos; escolher um arquivo na página desativa o da galeria.
  - **Estáticos**: desenhados por código em `StaticWallpaperRenderer` (ruído fractal, funções de distância,
    gradientes com dithering contra faixas) na resolução exata de cada monitor. O `GalleryImageCache` gera em segundo
    plano e grava em `%LocalAppData%\Gearbox\cache\wallpapers\{id}-{L}x{A}.png`; enquanto não existe, o monitor
    fica com o fundo atual (atribuição `Pending`, que o motor não toca) e o módulo aplica quando termina. Se a
    geração falhar, o monitor volta ao original com o problema `RenderFailed`; mudar as configurações tenta de novo.
    A imagem vai pelo mesmo caminho dos arquivos (com snapshot para restaurar), sempre em "Preencher". A cada
    aplicação o cache é podado: fica só a imagem em uso (e as que estão sendo geradas), e `.tmp` órfãos somem.
  - **Interativos**: cenas em `UI/Scenes` (`WallpaperScene` + `SceneFactory`), árvores de visuais de Composition com
    animações de quadros-chave em loop, que rodam no compositor (GPU), sem trabalho por quadro na thread de UI.
    Pausam pelos `AnimationController` (mesma `PausePolicy` dos vídeos) e, paradas, servem de pôster. As animações
    usam propriedades inteiras (`Translation`, `Offset`, `Scale`), não subcanais, para o controlador existir. Cada
    objeto de Composition criado pela cena é registrado (`Own`) e liberado na thread de UI ao reconstruir ou
    descartar, sem depender do coletor. A Aurora Flow
    usa texturas de cortina geradas por `AuroraRibbonTexture`; o Digital Clock troca o texto uma vez por minuto.
  - **Página**: cards com miniatura (estáticos gerados em 464x261) ou a cena parada, que anima com o mouse em cima
    (`ScenePreview`); clicar abre um `ContentDialog` com a prévia grande e "Ativar".
- **Janela atrás dos ícones** (`DesktopHostWindow`, usada por vídeos e cenas): `DesktopLayer` manda `0x052C` ao
  `Progman` e acha o `WorkerW` do wallpaper (filho do `Progman` no Windows 11 24H2+, ou de topo no layout clássico).
  É uma janela WinUI sem moldura. Ela precisa **aparecer antes** de virar filha do WorkerW:
  reparentar uma janela WinUI que ainda não foi exibida derruba o processo. Por isso nasce fora da tela, aparece
  sem ativar e é presa 300 ms depois.
- **Snapshot do original**: antes da primeira mudança, o wallpaper e o ajuste de cada monitor vão para
  `settings\Wallpaper.restore.json` (separado das configurações, que a página regrava), com uma cópia das imagens
  em `%LocalAppData%\Gearbox\wallpaper`. `Stop()` restaura e limpa o snapshot. Se a restauração falhar, o snapshot
  fica e é usado na próxima vez.
- **Pausa** (`PausePolicy`): monitor coberto por uma janela, na bateria, sessão bloqueada ou tela apagada.
  A `CoverageRules` considera "coberto" o monitor inteiro (tela cheia) ou a área de trabalho com folga de 72 px
  por lado, o que inclui as janelas pseudo-maximizadas pelo RoundedMaximize (que não ficam `IsZoomed`). A
  reavaliação acontece 250 ms depois de uma rajada de eventos do hook (`LocationChanged`, `ForegroundChanged`,
  `MinimizeEnded`). Overlays (`WS_EX_TRANSPARENT`), janelas de ferramenta, "cloaked" e do shell não contam.
- **Estado do sistema**: `SystemStateMonitor` é uma janela de topo oculta (não `HWND_MESSAGE`, porque
  `WM_DISPLAYCHANGE` e `TaskbarCreated` são broadcasts) que recebe bloqueio de sessão, tela apagada, mudança de
  monitores e reinício do Explorer. Energia vem do `PowerManager` (WinRT).
- **Falhas de mídia** (arquivo sumiu, formato não suportado, vídeo que não toca) não desligam o módulo: o
  monitor mantém o fundo original e a página mostra o motivo.

## Ilha dinâmica (Notch), em detalhe

```
NotchModule (orquestra, timers)
  ├─ NotchState (Logic) ─► modo: Collapsed | Peek (volume, bateria, faixa nova) | Expanded (mouse em cima)
  ├─ OverlapRules (Logic) ─► Normal | Compact (alça) | Hidden (tela cheia)
  ├─ NotchLayout (Logic) ─► forma (tamanho em DIP + raio) e posição no topo do monitor principal
  ├─ QuickControls (Logic) ─► regras dos controles rápidos (alternar rádio, slider → volume/mudo)
  ├─ fontes: MediaSessionWatcher (WinRT) · AudioVolumeMonitor (Core Audio) · PowerManager (WinRT)
  │          ConnectivityWatcher (WinRT: Windows.Devices.Radios + NetworkInformation) · AudioEndpoints (Core Audio)
  └─► NotchWindow ─► NotchView (XAML: forma preta animada + conteúdo de cada modo; aberta: abas Mídia/Controles)
```

- **Janela**: `Window` WinUI com `OverlappedPresenter` sem borda, sempre no topo, fora do Alt+Tab. Mesmo sem borda,
  o presenter deixa um `WS_DLGFRAME` de 3 px; `NativeWindow.MakeOverlay` troca por `WS_POPUP` e adiciona
  `WS_EX_NOACTIVATE`, para que clicar nos controles de mídia não tire o foco do app atual.
- **Transparência** (cantos de baixo arredondados, topo colado na borda da tela): uma janela WinUI tem dois fundos.
  `TransparentWindow` tira o da janela Win32 (blur behind do DWM com região vazia + fundo pintado de preto, que vira
  transparente) e `TransparentBackdrop` tira o do XAML (`SystemBackdrop` com um pincel transparente do compositor
  do sistema, que exige um `Windows.System.DispatcherQueue` na thread de UI, criado por
  `TransparentWindow.EnsureSystemDispatcherQueue`).
- **Fundo** em duas camadas dentro da forma: um `SystemBackdropElement` (Windows App SDK 2.0+) com acrílico, com
  os mesmos cantos da ilha, e por cima o tom (`IslandBrush`). O usuário escolhe um **estilo**
  (`NotchSettings.BackgroundStyle`: Opaco, Pouco transparente, Transparente, Muito transparente ou Vidro) e uma
  **cor** (`BackgroundColor` em hex, de uma paleta, do seletor de cores ou, com `UseAccentColor`, o destaque do
  Windows lido por `AccentColorSource`). `NotchAppearance.Look` traduz o estilo em opacidade do tom e acrílico
  ligado ou não (só o Vidro desfoca); `NotchAppearance.Readable` escurece cores claras para o texto branco continuar
  legível. Texto, capa e ícones continuam opacos. O acrílico é o `IslandBackdrop`, um `DesktopAcrylicController` com
  configuração fixa `IsInputActive = true` e tema escuro, porque a ilha nunca é ativada. Ele **só desfoca**:
  `TintOpacity = 0` e `LuminosityOpacity = 0`; toda a cor vem do `IslandBrush`. A reserva (`FallbackColor`, sem
  efeitos de transparência ou na economia de energia) é a cor do tom opaca. Um `IslandBackdrop` por elemento vive
  enquanto a janela existir e é reconfigurado (`Configure`), e os objetos de ligação são soltos na thread de UI: soltos
  pelo coletor, na thread do finalizador, derrubam o processo (`RPC_E_WRONG_THREAD` em `Microsoft.UI.Input.dll`).
  Configurações da 0.3 (`acrylicBackground`/`backgroundOpacity`) viram o estilo mais próximo no `IJsonOnDeserialized`
  de `NotchSettings`. Não há Mica: ele só usa o papel de parede, nunca o que está atrás da ilha.
- **Orelhas** (cantos côncavos de cima, como o notch do MacBook): dois `Grid` (um `SystemBackdropElement` quadrado
  com o mesmo material e, por cima, um `Path` com o tom: quadrado menos um quarto de círculo) nas colunas `*` do
  `Grid` raiz, encostados no corpo, que fica na coluna `Auto`. O raio vem de `NotchShape.EarRadius` (escalado por
  `NotchSettings.Roundness` via `NotchLayout.WithRoundness`) e anima junto com a forma. As formas recolhidas (`Idle`,
  `Playing`, `Handle`) têm raio 0, sem orelhas. O `SystemBackdropElement` só recorta retângulos arredondados, então
  quem tira o quarto de círculo do material é a **região da janela**: `NotchRegion` calcula o retângulo e os dois
  círculos, e `NativeWindow.TrySetRegion` subtrai os círculos com `CombineRgn`. A curva externa fica sem antialiasing.
  O traço da borda de vidro continua pela curva (cores `GlassEdgeStartColor`/`GlassEdgeEndColor`).
- **Região da janela**: a janela tem o tamanho máximo (`NotchLayout.MaxWindowSize`: a maior forma com o maior
  arredondamento, mais a folga do pico da mola) e a região recorta a forma atual, para não bloquear cliques em volta.
  Durante a animação, a `NotchView` emite `ShapeFrame` a cada quadro e a `NotchWindow` atualiza a região.
- **Animação com mola** (`NotchSettings.SpringAnimation`/`SpringIntensity`): `SpringMotion` (Logic) é um oscilador
  amortecido com solução fechada; `SpringAnimator` (UI) aplica os valores em `CompositionTarget.Rendering`. A forma
  (largura, altura e orelhas) e o painel que entra (escala 0,96→1 e deslocamento −6→0, junto do fade) usam a mola;
  desligada, a forma volta ao `Storyboard` com `CubicEase`. Uma animação interrompida continua do tamanho atual.
- **Botão de configurações**: a engrenagem no rodapé da ilha aberta recolhe a ilha e chama
  `IModuleSettingsOpener.Open` (Core), implementado pelo `MainWindowService` do casco, que mostra a janela e seleciona
  a página do módulo. O `MainWindow.ShowAndActivate` usa `NativeWindow.BringToFront` (topo por um instante e
  `SetForegroundWindow`), porque o Windows não deixa um processo em segundo plano roubar o foco.
- **Links e segurança**: `QuickLinkList.Safe` descarta, ao carregar `NotchLinks.json`, links com Id fora do formato
  GUID "N" (o Id vira nome de arquivo do ícone), links Web que não sejam http/https e alvos de app com `..` ou aspas;
  `LinkLauncher` confere de novo antes de abrir e chama o `explorer.exe` pelo caminho completo. `FaviconFetcher`
  segue no máximo 3 redirecionamentos à mão e não busca ícones em IPs internos (`NetworkAddressRules`), a menos
  que o próprio link seja interno (ex.: `localhost`).
- **Aba "Links"** (`NotchSettings.ShowLinks`): atalhos para sites e apps do Windows, numa grade com o ícone de cada um
  e um "+" no fim. A lista fica em `settings\NotchLinks.json` (`LinksSettings`, separada para o ViewModel do Notch não
  sobrescrevê-la) e pertence ao `LinkCatalog`, um singleton usado pelo módulo e pela página de configurações (lá dá
  para reordenar e remover). Regras puras em `QuickLinkList` (sem destino repetido, máximo de 30), `WebAddress` (só
  http/https, `https://` quando falta) e `FaviconParser`. A ilha não recebe teclado, então o "+" abre a
  `AddLinkWindow`, uma janela comum (via `LinkEditor`, fechada no `Stop`), com duas abas: **Site** (endereço e nome) e
  **App do Windows** (apps do Menu Iniciar, de `Gearbox.Interop.Shell.InstalledApps`, via `shell:AppsFolder`).
  Ícones ficam em `%LocalAppData%\Gearbox\cache\link-icons\{id}.img`: de sites, o `FaviconFetcher` lê a página (até
  512 KB), escolhe o maior `apple-touch-icon`/`icon` e cai para `/apple-touch-icon.png` e `/favicon.ico`, sempre no
  próprio site; de apps, o `IShellItemImageFactory` vira PNG. Sites abrem no navegador padrão (`UseShellExecute`);
  apps, com `explorer.exe shell:AppsFolder\{AUMID}`.
- **Abrir/fechar**: abre 120 ms depois de o mouse entrar e fecha 350 ms depois de sair. Com a ilha aberta, um timer
  de 500 ms confere a posição do cursor (rede de segurança para um `PointerExited` perdido) e o fechamento é
  cancelado se o cursor ainda estiver em cima.
- **Avisos** duram 2 s e são substituídos pelo mais recente; abrir a ilha descarta o aviso. Faixa nova só avisa se
  já havia uma faixa tocando (não na primeira leitura).
- **Sobreposição** (`OverlapRules`), reavaliada 150 ms depois de uma rajada de eventos do hook: some se o shell diz
  que há app em tela cheia/apresentação (`SHQueryUserNotificationState`) ou se a janela em foco cobre o monitor
  inteiro; vira alça (120×6 DIP) se a janela em foco cruza a ilha recolhida. Área de trabalho, barra de tarefas,
  janelas minimizadas/"cloaked" e o próprio Gearbox não contam.
- **Mídia**: `GlobalSystemMediaTransportControlsSessionManager` funciona sem identidade de pacote. As leituras são
  assíncronas e podem terminar fora de ordem: só a mais recente publica, e a troca de faixa não se perde se uma
  leitura for descartada. Falhas do app que está tocando só são logadas.
- **Volume**: `AudioVolumeMonitor` registra `IAudioEndpointVolumeCallback` no dispositivo de saída padrão e
  `IMMNotificationClient` para trocar de dispositivo. Os avisos chegam numa thread do Core Audio e são levados
  para a thread de UI. O slider da aba "Controles" escreve com `SetLevel`/`SetMuted`; a volta pelo callback não
  realimenta o slider (`_updatingVolume` na view) e não gera aviso (avisos são ignorados com a ilha aberta).
- **Controles rápidos** (aba "Controles", só com `ShowConnectivity` ou `ShowAudioControls` ligados; a ilha aberta
  passa a 420×204 DIP):
  - Wi‑Fi/Bluetooth via `Windows.Devices.Radios` (a API das Configurações Rápidas, funciona sem pacote). Clique
    alterna o rádio; botão direito abre `ms-availablenetworks:` / `ms-settings:bluetooth`. A lista de rádios é relida
    a cada abertura da ilha (adaptadores USB). Modo avião/chave física: `SetStateAsync` recusa e só é logado.
  - Saída de áudio: `AudioEndpoints` lista as saídas ativas e troca a padrão via `IPolicyConfig`, interface COM
    **não documentada** (a mesma do painel de som), declarada à mão no Interop por não estar nos metadados do Win32.
    Falhas viram `false` + log; a lista volta a mostrar a saída real.
  - A escolha da saída é uma página dentro da ilha, não um `MenuFlyout`: popups ficam fora da janela e o
    `IsCursorInside()` do fechamento fecharia a ilha com o menu aberto. Pelo mesmo motivo o slider não tem a dica
    do polegar.
  - O que esses controles mudam é ação do usuário, como nas Configurações Rápidas: `Stop()` não desfaz.
  - Rede (`NotchPage.Network`, botão `>` ao lado do botão de rede; forma alta): gráfico dos últimos 30 s
    (`NetworkHistory`, `NetworkChart`) com download e upload somados dos adaptadores ativos (sem loopback/túnel;
    `NetworkInterface.GetIPStatistics`) e ping ICMP a `1.1.1.1` a cada 2 s. `NetworkMonitor` usa um `BackgroundLoop`
    e só roda com a página na tela; o histórico recomeça cada vez que ela abre.
  - Teste de velocidade (`SpeedTester`, só ao clicar): `speed.cloudflare.com` — ping = mediana do tempo até os
    cabeçalhos de `__down?bytes=0`, download por `__down` e upload por `__up` com 4 conexões por ~7 s cada, com
    teto de 300 MB / 100 MB. Os bytes contados vão para a UI via `Progress<T>`. Cancela pelo botão, ao desligar
    `ShowConnectivity` e no `Stop()`; falhas de rede só são logadas e mostradas na página (não são falha do módulo).
    Em conexão limitada (`ConnectionCost`), o primeiro clique só avisa; o segundo testa (`SpeedTestGate`). O teste
    continua se a página fechar.
- **Mixer de volume** (`NotchSettings.ShowMixer`, dentro da aba "Controles"): a linha "Mixer de volume", abaixo da
  saída de áudio, abre a página `NotchPage.Mixer` (subpágina, como `Devices`; a aba "Mídia" não muda), entrando da direita com fade (a mesma mola da ilha, ou easing sem mola). Toda subpágina que abre "para dentro"
  usa essa entrada (`PageTransitions.IsDrillIn`): Controles → Dispositivos, Controles → Mixer e Mixer → saída do app.
  Com as quatro
  linhas da aba "Controles" visíveis, a ilha passa a 420×240 (`ExpandedRoomyShape`); com o mixer na tela, usa a forma
  alta (420×260) e a lista rola.
  - `AudioSessionTable` (Interop) lista as sessões do Core Audio (`IAudioSessionManager2` em cada saída ativa) e
    guarda os objetos COM para ler o pico (`IAudioMeterInformation`) e mudar volume e mudo (`ISimpleAudioVolume`).
    Tudo roda na thread de UI: varredura a cada 1 s (`AudioMixer`, Native) e medidor a 20 Hz só com a página do
    mixer na tela. Notificações de sessão nova exigiriam uma thread MTA, por isso a varredura.
  - `MixerApps.Open` (Logic) mostra o mesmo que o mixer de volume do Windows: toda sessão não expirada, tocando ou
    não, uma linha por executável (chave = nome do `.exe` em minúsculas). "Sons do sistema" (`#system`) vem
    sempre primeiro; o volume mostrado é o da sessão que está tocando, se houver.
  - Saída por app: `AppAudioRouting` usa `IAudioPolicyConfigFactory` (`Windows.Media.Internal.AudioPolicyConfig`),
    interface **não documentada** com dois IIDs conforme a versão do Windows, declarada à mão como o `IPolicyConfig`.
    Como o método é chamado por posição na vtable, só é usada nas faixas de build conhecidas
    (`AppAudioRouting.FactoryInterfaceFor`; acima de `FirstUnknownBuild` a saída por app some da UI até alguém
    conferir o layout e subir a constante). A tela avisa que a escolha fica gravada no Windows.
    A escolha é uma página dentro da ilha (`MixerDevices`), aberta com o botão direito na linha.
  - As escolhas ficam em `settings\NotchMixer.json` (`MixerSettings`) e são reaplicadas a toda sessão que o módulo
    ainda não viu (inclusive as que já existiam ao ligar). Diferente dos controles rápidos, isso é estado do módulo:
    ao parar, os apps que o Gearbox mudou nesta execução voltam a 100%, sem mudo e à saída padrão
    (`MixerMemory.Reset`). Um app que já fechou não pode ser desfeito (a saída por app é por processo vivo).
- **Visual** no estilo shadcn/ui (tema "neutral" escuro): `UI/NotchTheme.xaml` (um `ResourceDictionary` com
  `x:Class`, mesclado na `NotchView` por instância, pelo mesmo motivo de caminho da fonte abaixo) tem a paleta, com
  `muted`/`accent`/`input` como brancos translúcidos para o acrílico aparecer, e templates próprios de
  `Button`/`ToggleButton` (ghost, outline, toggle e aba), sem a borda de elevação nem o acento do Windows. Slider e
  itens da lista usam lightweight styling (chaves conferidas no `generic.xaml` do WinUI). A borda de 1 px
  (`GlassEdge`, pincel `GlassEdgeBrush`) fica por cima do fundo, sem o lado de cima, e acompanha os cantos animados
  em `NotchView.SetShape`; `NotchSettings.GlassBorder` só a esconde (`NotchView.SetGlassBorder`).
- **Contraste do texto** (`NotchSettings.AdaptTextContrast`): com fundo não opaco, o módulo captura (GDI,
  `Interop/Desktop/ScreenPixels`) faixas da tela logo ao lado e abaixo da ilha — fora da região recortada da janela,
  então é o que está atrás dela —, a cada 1 s e depois de cada reavaliação de sobreposição. `Logic/TextContrast` mistura
  a média com a cor da ilha pela opacidade do estilo, calcula a luminância relativa (WCAG) e escolhe o tom com
  histerese (escuro acima de 0,24, volta ao claro abaixo de 0,13). `NotchView.SetTextTone` troca o `RequestedTheme`
  (texto padrão dos controles) e `NotchTheme.ApplyTone` muda a `Color` dos próprios pincéis da paleta: como todo o XAML
  os usa por `StaticResource` (a mesma instância), tudo se atualiza sem recriar nada. Por isso nenhuma cor da ilha pode
  ficar fixa no XAML nem num `NotchTheme` separado (o `UsageRing` recebe os pincéis por propriedade). Trocar o
  `RequestedTheme` dispara `OnDefaultSystemBackdropConfigurationChanged` nos backdrops da janela; a implementação base
  lança `ArgumentException` para alvos que ela não gerencia, então `IslandBackdrop` e `TransparentBackdrop` a
  sobrescrevem sem fazer nada (a configuração deles é fixa).
- **Ícones**: fonte Phosphor Icons (regular, MIT) em `Assets\Fonts`, referenciada como
  `ms-appx:///Gearbox.Modules.Notch/Assets/Fonts/Phosphor.ttf#Phosphor` (recurso `PhosphorFont` da `NotchView`).
  Os codepoints que mudam com o estado ficam em `NotchGlyphs`; os fixos, no XAML com o nome do ícone num
  comentário. O `Content` tem `TargetPath` explícito: sem ele, o `dotnet build` copia o conteúdo de uma biblioteca
  WinUI para `<Assembly>\...` e o `dotnet publish` para a raiz, e uma URI `ms-appx` fixa só funcionaria num dos dois.
  O ícone do módulo na navegação do casco continua Segoe (é o casco que desenha).
- **Rede por cabo**: `ConnectivityWatcher.Link` classifica o perfil de internet pelo adaptador (`NetworkLinks.Classify`,
  IANA ifType 6 = Ethernet). Com cabo, o botão do Wi‑Fi mostra o ícone `network`, o nome do perfil e a velocidade do
  link (`InboundMaxBitsPerSecond`); o clique abre `ms-settings:network-ethernet`.
- **Abas e páginas**: a view expõe `CurrentPage` (`NotchPage`) e `PageChanged`; `NotchModule.UpdatePageSources` só lê
  discos e sensores com a ilha aberta **e** a página deles na tela.
  - *Armazenamento*: `DriveScanner` (`DriveInfo`, numa thread do pool; só discos fixos e removíveis, sem rede/CD) é
    relido cada vez que a aba aparece. Anéis em `UsageRing` (controle em `UI/Controls`, geometria em `RingGeometry`,
    vermelho a partir de 90%). O clique abre o disco com o caminho completo do `explorer.exe` e o disco como argumento
    separado, sem shell. Antes, confere se é mesmo a raiz de uma unidade.
  - *Sistema*: `SystemMonitor` amostra a cada 1 s numa thread do pool enquanto a aba está visível (`SetActive`).
    Modelos da CPU e da placa-mãe vêm do registro (`HARDWARE\DESCRIPTION\System`), a GPU principal (a com mais memória
    dedicada) vem do DXGI (`GraphicsAdapters`). Uso via PDH (`PerformanceCounters`, caminhos em inglês):
    `Processor Information(_Total)\% Processor Utility`, `GPU Engine(*)\Utilization Percentage` (agregado por LUID em
    `GpuCounters.UsageByAdapter`) e `GPU Adapter Memory(*)\Dedicated Usage`. RAM por `GlobalMemoryStatusEx`.
  - *Temperaturas sem driver*: GPU via `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)`, a fonte do Gerenciador
    de Tarefas, em décimos de °C (0 = o driver não informa). A da "CPU" é a zona térmica ACPI mais quente
    (`Thermal Zone Information(*)\Temperature`, em Kelvin), que nem todo desktop tem. Temperatura real dos núcleos
    exigiria um driver de kernel (ex.: WinRing0, bloqueado pelo Defender como driver vulnerável): fora de escopo
    por segurança.
- **Notificações de outros apps** não são suportadas: `UserNotificationListener` exige identidade de pacote.
- `Stop()` fecha a janela e cancela todas as assinaturas; o módulo não altera nada no sistema por conta própria.
