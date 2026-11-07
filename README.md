# Gearbox

**Pequenas ferramentas para deixar o Windows do seu jeito.**

O Gearbox fica quietinho perto do relógio, na bandeja do sistema, e reúne algumas melhorias visuais e de
conforto para o Windows. Cada ferramenta pode ser ligada ou desligada quando você quiser, e nada muda no seu
computador sem você pedir.

## O que ele faz

### Ilha dinâmica

Uma "ilha" discreta no topo da tela, inspirada no notch do Mac.

- Mostra o que está tocando agora, no Spotify, no navegador ou em qualquer app com controles de mídia, com a
  capa do álbum e uma animação que acompanha a música.
- Passe o mouse para abrir: controles de música, hora, data e bateria.
- Na aba **Controles**, liga e desliga Wi‑Fi e Bluetooth, ajusta o volume e troca a saída de áudio (fone,
  caixa de som, monitor) sem abrir as configurações do Windows.
- Nas abas **Sistema** e **Armazenamento**, acompanha processador, placa de vídeo, memória e o espaço livre
  de cada disco.
- Avisa rapidinho quando o volume muda ou quando o carregador é ligado ou desligado.
- Some sozinha durante jogos, vídeos e apresentações em tela cheia, e vira só uma alça fininha quando uma
  janela maximizada fica embaixo dela.

### Wallpaper

Coloque um vídeo ou uma imagem como papel de parede.

- Vídeos em MP4, WebM ou MKV ficam em loop, sem som, atrás dos ícones da área de trabalho.
- Use o mesmo wallpaper em todas as telas ou um diferente em cada monitor.
- Escolha como ele ocupa a tela: preencher, ajustar, esticar ou centralizar.
- Para não pesar no computador, o vídeo pausa quando um jogo ou janela cobre a tela, quando o notebook está na
  bateria e quando o PC está bloqueado.
- Desligou? O wallpaper que você tinha antes volta do jeito que estava.

### Maximizar arredondado

Janelas maximizadas ganham cantos arredondados, uma borda colorida e um pequeno respiro até a borda da tela,
como no macOS. Precisa do Windows 11 para os cantos e a cor da borda.

## Como instalar

1. Abra a página de [versões do Gearbox](https://github.com/Tiallysson/Gearbox/releases).
2. Na versão mais recente, baixe o instalador do seu computador:
   - `Gearbox-...-x64-setup.exe` para a grande maioria dos PCs;
   - `Gearbox-...-arm64-setup.exe` para PCs com processador ARM (como Surface com Snapdragon).
3. Abra o arquivo e siga o assistente. Não precisa ser administrador.

> **O Windows mostrou "O Windows protegeu o computador"?** Isso acontece com apps novos que ainda não têm
> assinatura digital. Clique em **Mais informações** e depois em **Executar assim mesmo**.

Depois de instalado, o Gearbox abre na bandeja do sistema, perto do relógio (às vezes escondido na setinha
`^`). Clique no ícone para abrir a janela e escolher quais ferramentas usar. Clique com o botão direito para
ligar e desligar as ferramentas rapidamente ou para sair.

### Requisitos

- Windows 10 (versão 2004 ou mais recente) ou Windows 11.
- Os cantos arredondados do "Maximizar arredondado" só funcionam no Windows 11.
- Vídeos WebM precisam das **Extensões de Vídeo VP9**, gratuitas na Microsoft Store.

## Atualizações

O Gearbox verifica sozinho, uma vez por dia, se existe versão nova. Quando encontra, avisa na bandeja e mostra
um botão **Instalar** na página **Geral**. Nada é instalado sem o seu clique.

Ao instalar, ele baixa a versão nova, confere se o arquivo é exatamente o publicado aqui no GitHub, fecha com
cuidado (devolvendo wallpaper e janelas ao normal) e abre de novo já atualizado. Suas configurações são mantidas.

Na página **Geral** você também pode:

- clicar em **Verificar agora** a qualquer momento;
- desligar a verificação automática;
- ligar **Receber versões de teste** para experimentar novidades antes de todo mundo. Elas podem ter falhas.

## Dicas

- **Iniciar com o Windows:** ligue ou desligue na página **Geral**. Atualizações respeitam a sua escolha.
- **Algo estranho?** Desligue a ferramenta pela bandeja: tudo que ela mudou volta ao normal na hora. Se uma
  ferramenta der erro, o Gearbox a desliga sozinho e avisa.
- **Janelas de programas abertos como administrador** não são afetadas, por uma proteção do próprio Windows.
  Para incluí-las, abra o Gearbox como administrador também.
- **Sempre saia pela bandeja** (botão direito → **Sair**). Encerrar pelo Gerenciador de Tarefas pode deixar
  janelas ou wallpaper do jeito que o Gearbox deixou.

## Seus dados

O Gearbox não tem conta, não coleta dados e não envia nada sobre você. A única conexão com a internet é a
verificação de atualizações aqui no GitHub.

Configurações e registros de erro ficam no seu computador, em `%LocalAppData%\Gearbox`. Na página **Sobre**
há um botão para abrir essa pasta, e na página **Geral**, um para abrir os registros de erro, úteis para
reportar problemas.

## Como desinstalar

Vá em **Configurações do Windows → Aplicativos → Aplicativos instalados**, procure **Gearbox** e clique em
**Desinstalar**. O Gearbox fecha devolvendo tudo ao normal e pergunta se você também quer apagar suas
configurações.

## Encontrou um problema ou tem uma ideia?

Abra uma [issue](https://github.com/Tiallysson/Gearbox/issues) contando o que aconteceu. Se for um erro,
anexe o arquivo de registro do dia (página **Geral** → **Abrir pasta de logs**) e diga sua versão do Windows.

O histórico de novidades de cada versão está no [CHANGELOG](CHANGELOG.md).

## Para desenvolvedores

Quer compilar, contribuir ou criar uma ferramenta nova? Veja o [guia de desenvolvimento](docs/DEVELOPMENT.md) e a
[arquitetura](docs/ARCHITECTURE.md).

## Licença

O Gearbox é software livre, sob a [licença MIT](LICENSE): você pode usar, copiar, modificar e distribuir, desde
que mantenha o aviso de copyright. Os ícones da Ilha dinâmica vêm do [Phosphor Icons](https://phosphoricons.com),
também sob licença MIT.
