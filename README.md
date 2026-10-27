# Gearbox

Utilitários para Windows no estilo PowerToys, em .NET 10 + WinUI 3 (Windows App SDK), unpackaged.
O Gearbox vive na bandeja do sistema e cada ferramenta é um **módulo** independente que pode ser
ligado e desligado.

| Módulo | O que faz |
|---|---|
| **Maximizar arredondado** (`RoundedMaximize`) | Janelas maximizadas mantêm cantos arredondados e uma borda colorida, com uma pequena margem até a borda da tela, como no macOS. |
| **Ilha dinâmica** (`Notch`) | Uma "ilha" no topo do monitor principal, no estilo dos apps de notch do macOS: mostra o que está tocando (capa e barrinhas animadas), avisa mudanças de volume e do carregador e abre ao passar o mouse, com controles de mídia, hora, data e bateria. Some com app em tela cheia e vira só uma alça fina sobre janelas maximizadas. |
| **Wallpaper** (`Wallpaper`) | Vídeo (MP4/WebM/MKV, em loop e sem som, atrás dos ícones) ou imagem como wallpaper, um por monitor, com o wallpaper do Gearbox como padrão. Pausa o vídeo com app em tela cheia/maximizado, na bateria e com o PC bloqueado. Ao desligar, o wallpaper anterior volta. |

## Requisitos

- Windows 10 2004 (build 19041) ou superior. Os cantos arredondados e a cor da borda exigem **Windows 11**.
  No Windows 10 o módulo funciona, mas o DWM recusa os atributos (o HRESULT aparece no log).
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Visual Studio 2022/2026 com a carga de trabalho "Desenvolvimento de aplicativos WinUI" (opcional).

## Rodar

```powershell
dotnet build                       # x64 por padrão (não existe AnyCPU)
dotnet run --project src\Gearbox    # o app abre só na bandeja
```

Para abrir a janela, clique no ícone da bandeja ou execute o Gearbox de novo: a segunda instância
avisa a primeira, que mostra a janela, e encerra em seguida.

No Visual Studio, defina `Gearbox` como projeto de inicialização e escolha a plataforma `x64` ou `ARM64`.

### Testes

```powershell
dotnet test
```

Os testes usam xUnit v3 sobre o Microsoft.Testing.Platform, ativado no `global.json`.

### Onde ficam os dados

| O quê | Onde |
|---|---|
| Configurações | `%LocalAppData%\Gearbox\settings\{id}.json` (`general.json`, `modules.json` e um arquivo por módulo) |
| Logs | `%LocalAppData%\Gearbox\logs\gearbox-AAAAMMDD.log` (rolling diário, 14 dias) |
| Iniciar com o Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `Gearbox` |

## Publicar

```powershell
cd src\Gearbox
dotnet publish -c Release -r win-x64      # ou -r win-arm64
```

A saída fica em `src\Gearbox\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\publish\`. É uma pasta
autossuficiente quanto ao Windows App SDK (`WindowsAppSDKSelfContained=true`), mas usa o runtime do .NET 10
instalado na máquina. Para incluir o .NET também:

```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

> Rode o `publish` dentro de `src\Gearbox` (ou passe o `.csproj`). Na raiz, ele tentaria publicar também
> os projetos de teste.

## Instalador

O instalador é feito com o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
.\installer\build-installer.ps1              # x64
.\installer\build-installer.ps1 -Arch arm64
```

O script publica o app em Release com o .NET embutido (`--self-contained`) em `artifacts\publish\win-<arch>`
e gera `artifacts\installer\Gearbox-<versão>-<arch>-setup.exe`. A versão vem do `<Version>` do
`Directory.Build.props`.

O que o instalador faz:

- instala por usuário, sem pedir administrador, em `%LocalAppData%\Programs\Gearbox`;
- cria o atalho no Menu Iniciar e, opcionalmente, na Área de Trabalho;
- oferece "Iniciar o Gearbox com o Windows", gravando o mesmo valor `Run` que a página Geral usa;
- registra o Gearbox em "Aplicativos instalados", com desinstalador;
- **antes de atualizar ou desinstalar**, encerra o Gearbox instalado com `Gearbox.exe --exit`. É o mesmo caminho
  do "Sair" da bandeja, que devolve as janelas pseudo-maximizadas ao normal. Matar o processo deixaria as
  janelas como estavam;
- ao desinstalar, remove o valor `Run` (mesmo que tenha sido ligado pelo app) e pergunta se deve apagar as
  configurações e os logs em `%LocalAppData%\Gearbox`;
- ao atualizar uma instalação do **Darwin** (nome anterior do app, mesmo `AppId`), encerra o `Darwin.exe`, apaga os
  arquivos `Darwin*` da pasta, troca o valor `Run` "Darwin" por "Gearbox" e move `%LocalAppData%\Darwin` para
  `%LocalAppData%\Gearbox` (se este ainda não existir). A pasta de instalação continua a mesma.

Instalação silenciosa (para scripts ou implantação):

```powershell
Gearbox-0.1.0.0-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /TASKS=startup
"%LocalAppData%\Programs\Gearbox\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES   # mantém as configurações
```

> O `AppId` em `installer\Gearbox.iss` identifica a instalação para atualizações e desinstalação.
> **Nunca o altere.** Para lançar uma versão nova, mude o `<Version>` no `Directory.Build.props`.

### Janelas de apps elevados

Por causa do UIPI, um processo comum não consegue mover nem alterar janelas de processos que rodam
como administrador. Para os módulos afetarem essas janelas, rode o Gearbox como administrador ou troque
`asInvoker` por `requireAdministrator` no `src\Gearbox\app.manifest` (o arquivo explica o impacto disso
na inicialização com o Windows).

---

## Como criar um novo módulo

Exemplo: um módulo `Foo`. Use o `Gearbox.Modules.RoundedMaximize` como referência; ele segue exatamente
esta estrutura.

### 1. Criar o projeto

```powershell
dotnet new classlib -n Gearbox.Modules.Foo -o src\Gearbox.Modules.Foo
dotnet sln add src\Gearbox.Modules.Foo\Gearbox.Modules.Foo.csproj --solution-folder src
Remove-Item src\Gearbox.Modules.Foo\Class1.cs
```

Substitua o `.csproj`. O `TargetFramework`, o `Nullable` e as plataformas vêm do `Directory.Build.props`,
e as versões dos pacotes vêm do `Directory.Packages.props`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Gearbox.Modules.Foo</RootNamespace>
    <UseWinUI>true</UseWinUI>
    <DefaultLanguage>pt-BR</DefaultLanguage>
  </PropertyGroup>

  <ItemGroup>
    <!-- PrivateAssets: o casco já traz o Windows App SDK; assim o projeto de testes não o herda. -->
    <PackageReference Include="Microsoft.WindowsAppSDK" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" PrivateAssets="all" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Gearbox.Core\Gearbox.Core.csproj" />
    <ProjectReference Include="..\Gearbox.Interop\Gearbox.Interop.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Gearbox.Modules.Foo.Tests" />
  </ItemGroup>

</Project>
```

O `.sln` só tem as plataformas `x64` e `ARM64`. Se o `dotnet sln add` criar entradas `Any CPU`/`x86` para o
projeto novo, mapeie-as para `x64` (ou abra a solution no Visual Studio, que faz isso pelo Configuration Manager).

### 2. Criar os arquivos

```
src/Gearbox.Modules.Foo/
  FooModule.cs                    # implementa IGearboxModule
  FooSettings.cs                  # POCO serializado em settings\Foo.json
  ServiceCollectionExtensions.cs  # AddFooModule()
  ModuleStrings.cs                # acesso ao .resw do módulo
  Logic/                          # regras puras, sem Win32 (testáveis)
  Native/                         # chamadas ao Win32 via Gearbox.Interop
  UI/
    FooSettingsPage.xaml(.cs)
    FooSettingsViewModel.cs
  Strings/pt-BR/Resources.resw
```

**`FooModule.cs`**

```csharp
using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Gearbox.Modules.Foo.UI;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Foo;

public sealed class FooModule(
    ISettingsService<FooSettings> settings,
    IModuleFaultReporter faults,
    ILogger<FooModule> logger) : IGearboxModule
{
    public const string ModuleId = "Foo";        // estável: vira o nome do arquivo de configurações

    public string Id => ModuleId;
    public string DisplayName => ModuleStrings.Get("ModuleDisplayName");
    public string Description => ModuleStrings.Get("ModuleDescription");
    public string IconGlyph => "";         // Segoe Fluent Icons
    public bool IsEnabled { get; private set; }
    public Type SettingsPageType => typeof(FooSettingsPage);

    public void Start()                          // sempre na thread de UI
    {
        if (IsEnabled) return;
        try
        {
            settings.Changed += OnSettingsChanged;
            // ... ligar hooks, timers etc.
            IsEnabled = true;
        }
        catch
        {
            Cleanup();                           // não deixar o módulo "meio ligado"
            throw;                               // o casco loga e desabilita
        }
    }

    public void Stop()
    {
        if (!IsEnabled) return;
        IsEnabled = false;
        Cleanup();
    }

    private void Cleanup()
    {
        settings.Changed -= OnSettingsChanged;
        // ... desfazer TUDO o que o módulo alterou no sistema
    }

    private void OnSettingsChanged(object? sender, FooSettings e)
    {
        try { /* aplicar na hora */ }
        catch (Exception ex) { faults.ReportFault(Id, ex); }   // o casco desliga o módulo
    }
}
```

**`ServiceCollectionExtensions.cs`**

```csharp
using Gearbox.Core;
using Gearbox.Modules.Foo.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gearbox.Modules.Foo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFooModule(this IServiceCollection services)
    {
        services.AddGearboxSettings<FooSettings>(FooModule.ModuleId);
        services.TryAddTransient<FooSettingsViewModel>();
        services.AddGearboxModule<FooModule, FooSettingsPage>();   // registra como IGearboxModule + página
        return services;
    }
}
```

**`ModuleStrings.cs`**: copie o do RoundedMaximize e troque o mapa de recursos para
`"Gearbox.Modules.Foo/Resources"` (em bibliotecas, os `.resw` ficam num mapa com o nome do assembly).

**`UI/FooSettingsPage.xaml.cs`**: a página recebe o ViewModel pelo construtor, porque o casco a cria pelo DI:

```csharp
public sealed partial class FooSettingsPage : Page
{
    public FooSettingsPage(FooSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public FooSettingsViewModel ViewModel { get; }
}
```

A página traz só as configurações do módulo. O título, a descrição, o ícone e o botão "Ativar módulo"
são desenhados pelo casco (`Views/ModulePage.xaml`).

**`UI/FooSettingsViewModel.cs`**: um `ObservableObject` (CommunityToolkit.Mvvm) que lê com
`settings.Load()` e grava com `settings.Save(...)`. O `Save` dispara `Changed` e o módulo aplica na
hora, sem reiniciar. Use propriedades `[ObservableProperty] public partial ...`.

**`Strings/pt-BR/Resources.resw`**: no mínimo `ModuleDisplayName` e `ModuleDescription`, mais as chaves
`x:Uid` da página (por exemplo `MeuTitulo.Text`).

### 3. Registrar no casco

Em `src\Gearbox\Gearbox.csproj`:

```xml
<ProjectReference Include="..\Gearbox.Modules.Foo\Gearbox.Modules.Foo.csproj" />
```

Em `src\Gearbox\App.xaml.cs`, no método `BuildHost()`:

```csharp
builder.Services.AddRoundedMaximizeModule();
builder.Services.AddFooModule();   // ← novo
```

Pronto. O item de navegação, a página com o botão "Ativar módulo", o toggle no menu da bandeja, a
persistência ligado/desligado e o isolamento de falhas vêm automaticamente do casco.

### 4. Testes (recomendado)

```powershell
dotnet new classlib -n Gearbox.Modules.Foo.Tests -o tests\Gearbox.Modules.Foo.Tests
dotnet sln add tests\Gearbox.Modules.Foo.Tests\Gearbox.Modules.Foo.Tests.csproj --solution-folder tests
```

Use o `.csproj` de `tests\Gearbox.Modules.RoundedMaximize.Tests` como modelo (`OutputType=Exe`, `xunit.v3`,
referência ao módulo) e teste as classes de `Logic/`.

### Regras para módulos

- `Start()` e `Stop()` rodam na thread de UI. Não bloqueie: nada de I/O longo nem `.Wait()`.
- `Stop()` precisa desfazer **tudo** o que o módulo mudou no sistema. Ele é chamado ao desligar o
  módulo, ao sair do app e quando o módulo falha.
- Exceções em `Start`/`Stop` são capturadas pelo casco. Exceções em callbacks do próprio módulo
  (eventos, timers) devem ir para o `IModuleFaultReporter`. Exceções em handlers do
  `WinEventHookManager` são capturadas e reportadas automaticamente.
- Eventos de janela: `hooks.Subscribe(Id, handler)` no `WinEventHookManager` (um único hook compartilhado).
  Para precisar de outro evento Win32, adicione-o ao `WinEventKind` e à faixa do hook em `Gearbox.Interop`.
- Win32: adicione o nome da API ao `src\Gearbox.Interop\NativeMethods.txt` e exponha um wrapper tipado em
  `NativeWindow`. Nada de `DllImport` escrito à mão e nenhum tipo do CsWin32 fora do `Gearbox.Interop`.
- Textos da UI ficam no `.resw`, nunca fixos no código ou no XAML.

## Estrutura

```
Gearbox.sln
Directory.Build.props       # TFM, Nullable, plataformas, TreatWarningsAsErrors
Directory.Packages.props    # versões dos pacotes (Central Package Management)
global.json                 # dotnet test com Microsoft.Testing.Platform
src/
  Gearbox/                   # casco WinUI: bandeja, janela, navegação, DI
  Gearbox.Core/              # contratos e serviços sem UI
  Gearbox.Interop/           # Win32/DWM via CsWin32 + WinEventHookManager
  Gearbox.Modules.Notch/
  Gearbox.Modules.RoundedMaximize/
  Gearbox.Modules.Wallpaper/
tests/
  Gearbox.Core.Tests/
  Gearbox.Modules.Notch.Tests/
  Gearbox.Modules.RoundedMaximize.Tests/
  Gearbox.Modules.Wallpaper.Tests/
installer/
  Gearbox.iss                # script do Inno Setup
  build-installer.ps1       # publica + gera o setup em artifacts\installer
docs/
  ARCHITECTURE.md
```

Detalhes do fluxo em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Pontos em aberto

- **Tamanho da publicação (~170 MB):** o metapacote `Microsoft.WindowsAppSDK` 2.x traz componentes de IA
  (`onnxruntime.dll`, `DirectML.dll`, ~38 MB) que o Gearbox não usa. Dá para reduzir referenciando só os
  componentes necessários, mas o `H.NotifyIcon.WinUI` depende do metapacote.
- Sem instalador, MSIX ou Store por enquanto.


### Dock

O módulo **Dock** adiciona uma barra flutuante inspirada nas docks do macOS e Windows 11.
Ative-o na navegação do Gearbox ou pelo menu da bandeja. Inclui atalhos para Explorador,
navegador padrão, Prompt de Comando, Bloco de Notas, Calculadora, Configurações, som e Lixeira.
Os ícones usam a fonte do Windows, com cores próprias e animação ao passar o mouse.

Nas configurações do módulo, escolha tema claro ou escuro, tamanho dos ícones, distância
inferior e exibição de relógio e foco. As alterações são salvas e aplicadas imediatamente.
A dock aparece no monitor principal, acima da barra de tarefas, acompanha mudanças de DPI
ou área útil e permite rolagem horizontal se a tela for estreita. Não substitui a barra de tarefas.

Clique no foco para iniciar ou pausar os 25 minutos; use o botão direito para reiniciar.
Ao terminar, o cartão mostra “Foco concluído”. O temporizador é encerrado ao desativar o módulo.
O cartão de data e hora abre o Relógio do Windows. A Lixeira é aberta sem apagar arquivos.
Esta versão usa atalhos fixos; não inclui lista de janelas abertas, previsão do tempo nem player de mídia.

Validação no Windows com .NET 10 e ferramentas WinUI instaladas:

```powershell
dotnet build Gearbox.sln -p:Platform=x64
dotnet test
dotnet run --project src\Gearbox
```

Confira ativar/desativar pela bandeja, troca de tema e tamanho com o módulo ativo, todos os
atalhos, pausa/reinício do foco, mudança de resolução/DPI e fechamento do Gearbox.
A dock deve desaparecer ao desativar o módulo ou sair do aplicativo.
