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
│ WinEventHookManager (hooks out-of-context) · NativeWindow (wrappers)         │
│ DesktopLayer · DesktopWallpaper · SystemStateMonitor                         │
│ TransparentWindow · AudioVolumeMonitor · AudioEndpoints (Core Audio)         │
│ CsWin32 → user32 / dwmapi / kernel32 / wtsapi32 / IDesktopWallpaper          │
└──────────────────────────────────────────────────────────────────────────────┘
```

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
WallpaperSettings + monitores (IDesktopWallpaper) + wallpaper embutido
  └─► WallpaperResolver ─► por monitor: imagem | vídeo | manter o original (+ problema, se a escolha caiu)
        └─► WallpaperEngine
              ├─ imagem  → IDesktopWallpaper.SetWallpaper (API do próprio Windows)
              ├─ vídeo   → wallpaper original por trás + VideoWallpaperWindow presa no WorkerW
              └─ original → devolve a imagem guardada no snapshot
```

- **Vídeo atrás dos ícones**: `DesktopLayer` manda `0x052C` ao `Progman` e acha o `WorkerW` do wallpaper (filho do
  `Progman` no Windows 11 24H2+, ou de topo no layout clássico). A `VideoWallpaperWindow` é uma janela WinUI sem
  moldura com `MediaPlayerElement` (loop, sem som). Ela precisa **aparecer antes** de virar filha do WorkerW:
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
  monitor cai no wallpaper do Gearbox (ou no original) e a página mostra o motivo.
- **Wallpaper embutido**: `Assets\Wallpapers\Gearbox.jpg` (e `Gearbox.mp4`, opcional) do projeto do módulo. No build
  ele vai para `Gearbox.Modules.Wallpaper\Assets\...` e no publish para `Assets\...`; o módulo procura nos dois.

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
- **Fundo** em duas camadas dentro da forma: um `SystemBackdropElement` (Windows App SDK 2.0+) com acrílico, com os
  mesmos cantos da ilha, e por cima o tom preto (`IslandBrush`), com a opacidade de `NotchSettings.BackgroundOpacity`
  (20–100%, limitada por `NotchAppearance`). Texto, capa e ícones continuam opacos. O acrílico é o
  `AcrylicBackdrop`, um `DesktopAcrylicController` com configuração fixa `IsInputActive = true` e tema escuro: o
  `DesktopAcrylicBackdrop` do XAML acompanha a ativação da janela e, como a ilha nunca é ativada, ficaria sempre na
  cor sólida de reserva. O acrílico **só desfoca**: `TintOpacity = 0`, `LuminosityOpacity = 0` e `FallbackColor`
  transparente. Toda a escuridão vem do `IslandBrush`, que também pinta as orelhas. Sem isso, a tinta escura do próprio
  acrílico (e, na reserva, uma cor sólida opaca) deixaria o corpo bem mais escuro que as orelhas.
- **Orelhas** (cantos côncavos de cima, como o notch do MacBook): dois `Path` (quadrado menos um quarto de círculo)
  nas colunas `*` do `Grid` raiz, encostados no corpo, que fica na coluna `Auto`. O raio vem de
  `NotchShape.EarRadius` e anima junto com a forma. As formas recolhidas (`Idle`, `Playing`, `Handle`) têm raio 0,
  sem orelhas, e a janela fica do tamanho exato do corpo, porque a ilha recolhida fica no topo o tempo todo. As
  orelhas só aparecem no aviso e na ilha aberta, e crescem e somem com a animação. Elas têm só o tom (`IslandBrush`),
  sem o acrílico, porque o `SystemBackdropElement` só recorta retângulos arredondados. A cor é a mesma do corpo
  (veja "Fundo"); com o acrílico ligado, a única diferença é que atrás delas o desktop aparece nítido, e não
  desfocado. O traço da borda de vidro continua pela curva (cores
  `GlassEdgeStartColor`/`GlassEdgeEndColor`, as pontas do gradiente).
- **Tamanho da janela = tamanho da forma** (corpo + `2 × EarRadius`, `NotchLayout.WindowSize`), para não bloquear cliques em volta. Para crescer, a janela aumenta antes
  e a forma anima; para encolher, a forma anima e a janela diminui no fim da animação.
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
- **Visual** no estilo shadcn/ui (tema "neutral" escuro): `UI/NotchTheme.xaml` (um `ResourceDictionary` com
  `x:Class`, mesclado na `NotchView` por instância, pelo mesmo motivo de caminho da fonte abaixo) tem a paleta, com
  `muted`/`accent`/`input` como brancos translúcidos para o acrílico aparecer, e templates próprios de
  `Button`/`ToggleButton` (ghost, outline, toggle e aba), sem a borda de elevação nem o acento do Windows. Slider e
  itens da lista usam lightweight styling (chaves conferidas no `generic.xaml` do WinUI). A borda de 1 px
  (`GlassEdge`, pincel `GlassEdgeBrush`) fica por cima do fundo, sem o lado de cima, e acompanha os cantos animados
  em `NotchView.SetShape`; `NotchSettings.GlassBorder` só a esconde (`NotchView.SetGlassBorder`).
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
    relido cada vez que a aba aparece. Anéis em `UsageRing` (geometria em `RingGeometry`, vermelho a partir de 90%).
    O clique abre o disco com o caminho completo do `explorer.exe` e o disco como argumento separado, sem shell.
    Antes, confere se é mesmo a raiz de uma unidade.
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
