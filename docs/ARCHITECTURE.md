# Arquitetura

```
┌──────────────────────────── Darwin (casco WinUI) ────────────────────────────┐
│ Program ─► App ─► Host (DI + Serilog)                                        │
│                    ├─ TrayIconService ─────────┐                             │
│                    ├─ MainWindowService ─► MainWindow ─► ModulePage          │
│                    └─ ModuleManager ◄──────────┘   (toggles e navegação)     │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │ IEnumerable<IDarwinModule>
┌──────────────────────────────▼───────────── Darwin.Core (sem UI) ────────────┐
│ IDarwinModule · ModuleManager · ModuleFaultHub · ISettingsService<T>         │
│ IStartupService · DarwinPaths · DarwinLogging                                │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────── Darwin.Modules.* (um por ferramenta) ─┐
│ Módulo (Start/Stop) ─► Logic/ (regras puras) + Native/ (aplica no sistema)   │
│ UI/ (página de configurações + ViewModel) · Strings/*.resw                   │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │
┌──────────────────────────────▼────────────────────────── Darwin.Interop ─────┐
│ WinEventHookManager (1 hook out-of-context) · NativeWindow (wrappers)        │
│ CsWin32 → user32 / dwmapi / kernel32                                         │
└──────────────────────────────────────────────────────────────────────────────┘
```

Dependências apontam só para baixo: o casco conhece Core, Interop e os módulos; um módulo conhece
Core e Interop; Core e Interop não conhecem ninguém.

## Inicialização

1. **`Program.Main`** chama `AppInstance.FindOrRegisterForKey`. Se já existe um Darwin rodando, a
   ativação é redirecionada para ele (depois de `AllowSetForegroundWindow`) e o processo termina.
2. **`App.OnLaunched`**:
   - define `DispatcherShutdownMode = OnExplicitShutdown`, para o app continuar vivo sem janela;
   - monta o `Host` genérico: Serilog, `AddDarwinCore()`, serviços do casco e `Add{Módulo}Module()`;
   - cria o `WinEventHookManager` **na thread de UI** e liga `HandlerFaulted` ao `ModuleFaultHub`;
   - cria o ícone da bandeja e chama `ModuleManager.StartEnabledModules()`;
   - assina `AppInstance.Activated`, para mostrar a janela quando outra instância for aberta.
3. A janela principal só é criada no primeiro clique na bandeja. Fechá-la apenas a esconde.

## Módulos

- Cada módulo se registra com `services.AddXxxModule()`, que chama `AddDarwinModule<TModule, TPage>()`.
  O casco descobre os módulos por `IEnumerable<IDarwinModule>`: a navegação, o menu da bandeja e o
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
