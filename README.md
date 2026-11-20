# WinClicker 🎨🇧🇷

**Auto clicker gratuito e de código aberto para Windows 10 e 11** — fork do [WinClicker](https://github.com/PriyanshuGeTRekT/WinClicker) com tema **[Catppuccin Mocha](https://catppuccin.com)** e interface em **português (pt-BR)**.

Um único `.exe` de ~60 KB: sem instalador, sem anúncios, sem dependências. E, diferente da maioria dos auto clickers, ele consegue **clicar em segundo plano dentro de uma janela específica** (uma aba do Chrome, um jogo, um formulário) enquanto você continua usando o mouse e o teclado normalmente.

[**⬇ Baixar WinClicker.exe**](../../releases/latest)

## Recursos

- **Clique automático em segundo plano em qualquer janela.** Marque um ponto dentro de uma janela e o WinClicker envia os cliques direto para ela. O seu ponteiro real nunca se mexe, então você pode navegar, digitar e trabalhar em outros apps ao mesmo tempo. A janela pode ficar atrás das outras (só não pode ser minimizada).
- **Clica num local fixo da tela ou segue o cursor** — os modos clássicos, com um seletor na tela para escolher o pixel exato.
- **Só o seu teclado para.** Mexer o mouse nunca interrompe uma execução. Pressione o atalho de parar — ou, opcionalmente, *qualquer* tecla — e ela para na hora.
- **Atalhos globais personalizáveis** para iniciar e parar (padrão **F6** / **F7**). Clique no botão do atalho e pressione a tecla que quiser, inclusive combinações com Ctrl / Alt / Shift.
- **Intervalo ajustável** a partir de 1 ms, com uma thread de tempo dedicada — milhares de cliques por minuto se você quiser.
- **Botão esquerdo, direito ou do meio; clique simples ou duplo.**
- **Notificações de início/parada** com a contagem de cliques e o tempo de execução.
- **Tema Catppuccin Mocha** — janela sem bordas, cantos arredondados, ciente de DPI em cada monitor.
- **Portátil e minúsculo.** Um `.exe`, sem instalador, sem runtime para baixar. As configurações ficam em `%APPDATA%\WinClicker\settings.ini`.
- **Código aberto (MIT)** — o app inteiro é um arquivo C# e dois XAML.

## Como usar

| Modo | O que acontece | Para com |
|---|---|---|
| **Local fixo** | Move o ponteiro até X/Y e clica lá. Escolha o ponto com *Escolher na tela*. | Atalho de parar, ou qualquer tecla (opcional) |
| **Seguir o cursor** | Clica onde o seu ponteiro estiver no momento. | Atalho de parar, ou qualquer tecla (opcional) |
| **Numa janela** | Envia cliques direto para uma janela, sem tocar no mouse. A janela pode ficar atrás, mas não minimizada. | Só o atalho de parar — a sua digitação vai para os outros apps |

1. Escolha o modo e (no primeiro e no terceiro) clique em **Escolher na tela** para marcar o ponto exato.
2. Defina o intervalo em milissegundos, o botão do mouse e simples/duplo.
3. Pressione **F6** (ou o seu atalho) para iniciar e **F7** para parar. Um popup confirma cada um.

## Download

Baixe o `WinClicker.exe` na página de [Releases](../../releases/latest) e rode — não há nada para instalar.

O SmartScreen do Windows pode avisar sobre um editor desconhecido na primeira execução, porque o exe não é assinado digitalmente (assinatura custa dinheiro). Clique em *Mais informações → Executar assim mesmo*. Ou compile você mesmo (abaixo) se preferir não rodar um binário baixado.

## Compilar

**No Windows:** clone o repo e rode `build.bat`. Ele usa o compilador C# que já vem em todo Windows 10/11 — nada para instalar.

**No Linux (compila o exe para Windows):** `dotnet build -c Release` → o exe sai em `bin/Release/net48/WinClicker.exe`. (Requer o [.NET SDK](https://dotnet.microsoft.com/download); os assemblies de referência do .NET Framework 4.8 vêm do NuGet.)

## Segurança

- Não acessa a internet: nenhuma telemetria, nenhum download, nada é enviado.
- Só escreve um arquivo: as suas configurações em `%APPDATA%\WinClicker\settings.ini`.
- Não pede direitos de administrador.
- Usa APIs legítimas do Windows (`SendInput` e `PostMessage` para os cliques, um hook de teclado apenas para detectar os atalhos) — é a natureza de um auto clicker, e tudo está visível no código-fonte.

## Créditos e licença

- Fork de [PriyanshuGeTRekT/WinClicker](https://github.com/PriyanshuGeTRekT/WinClicker), de [PriyanshuGeTRekT](https://github.com/PriyanshuGeTRekT) — obrigado pelo app original! 🙏
- Licença [MIT](LICENSE) — livre para uso pessoal e comercial.
- Paleta: [Catppuccin](https://github.com/catppuccin/catppuccin) (Mocha), também MIT.
