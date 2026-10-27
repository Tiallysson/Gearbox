# Testes das correções da revisão

Branch: `fix/review-stability-2026-10-27`.

## Preparar no Windows

Use o SDK .NET 10 e os requisitos de compilação descritos no README. Feche a instância instalada do Gearbox antes de executar a versão de teste: o aplicativo permite uma única instância.

```powershell
git fetch origin
git switch --track origin/fix/review-stability-2026-10-27
dotnet build Gearbox.sln -c Release -p:Platform=x64
dotnet test --solution Gearbox.sln -c Release --no-build --minimum-expected-tests 1
dotnet publish src/Gearbox/Gearbox.csproj -c Release -r win-x64 --self-contained true -o artifacts/Gearbox-win-x64
.\artifacts\Gearbox-win-x64\Gearbox.exe
```

Se a branch já existir localmente, use `git switch fix/review-stability-2026-10-27`. O workflow **Windows validation** executa os mesmos comandos no GitHub Actions. Quando concluir com sucesso, o artefato **Gearbox-review-win-x64** contém a pasta do aplicativo: extraia o ZIP inteiro antes de abrir `Gearbox.exe`.

A versão de teste usa `%LocalAppData%\Gearbox`, assim como a versão instalada. Com o aplicativo fechado, copie essa pasta antes do teste para poder comparar as configurações e recuperar o estado anterior. Para reportar uma falha, anote o cenário, a versão do Windows e o log correspondente em `%LocalAppData%\Gearbox\logs`.

## Cenários manuais

| Correção | Procedimento | Resultado esperado |
| --- | --- | --- |
| Eventos do Windows por instância | Ative Wallpaper e Notch juntos. Bloqueie/desbloqueie a sessão, altere a resolução e desative/reative cada módulo em ordens diferentes. | Ambos continuam recebendo os eventos aplicáveis. Desativar um módulo não interrompe o outro. Não ocorre falha de callback ao fechar o aplicativo. |
| Wallpaper de monitor conectado depois | Comece com uma imagem original no monitor A. Ative o módulo; conecte B com outra imagem original; aplique um wallpaper do Gearbox a B. Desative o módulo. | Cada monitor recupera sua própria imagem e o ajuste original é restaurado. |
| Restauração pendente | Com imagens do Gearbox em A e B, desconecte B e desative o módulo. Consulte `settings\Wallpaper.restore.json`. Reconecte B, ative e desative o módulo. | A é restaurado imediatamente; o registro de B permanece até que B possa ser restaurado. |
| Original sem imagem recuperável | Selecione cor sólida ou apresentação de slides no Windows; depois tente aplicar uma imagem pelo módulo. | O fundo original é preservado e a página informa que ele não pode ser substituído com restauração segura. Vídeos continuam sendo uma camada separada. |
| Cancelamento do monitoramento | Abra e feche rapidamente a aba Sistema do Notch; desative/reative o módulo várias vezes. | O monitoramento retoma sem exceção de token descartado e sem acumular amostradores concorrentes. |
| Slider com áudio silenciado | Com som de teste baixo, deixe o volume alto e silencie. Mova o slider diretamente para um valor baixo. | O novo nível é aplicado antes de remover o mudo. Se a escrita do nível falhar, o módulo não remove o mudo. |
| Maximização | Maximize/restaure janelas comuns; altere margem; desative o módulo. Experimente também uma janela elevada com Gearbox sem elevação. | O fluxo normal mantém seu comportamento. Falhas de aplicação tentam restaurar o placement original e ficam registradas; rollback malsucedido é tentado novamente ao parar o módulo. |
| JSON inválido | Feche o app; após guardar uma cópia, coloque `{"enabled":null}` em `settings\modules.json` (confira o nome do arquivo existente). Abra o app. | O serviço carrega os padrões e preserva o JSON inválido em `.corrupt`, sem interromper a inicialização. |

As falhas nativas de reposicionamento e de gravação/restauração são simuladas nos testes automatizados; nem sempre é possível provocá-las de forma determinística pela interface.

## Cobertura e limites

- `BackgroundLoopTests`: reativação durante limpeza, cancelamento de início pendente, recuperação após exceção e descarte.
- `JsonSettingsServiceTests`: propriedades obrigatórias nulas, enums inválidos e proteção do arquivo válido ao salvar dados inválidos.
- `QuickControlsTests`: ordem nível/mudo, falhas de escrita e valores não finitos.
- `WallpaperRestorationTests`: monitor adicionado depois, modo não recuperável, apresentação de slides, desconexão, falha por monitor, falha ao salvar e recuperação pela cópia.
- `WindowPlacementTransactionTests`: rollback em falhas intermediárias e leituras finais, exceção, nova tentativa de rollback e caminho de sucesso.

Os testes com APIs simuladas validam as decisões e a preservação de estado. A entrega real de mensagens Win32, COM, áudio e o comportamento visual do WinUI exigem os cenários manuais acima. A proteção de wallpaper não implementa restauração completa de modos como apresentação de slides: nesses casos, preserva o modo existente.
