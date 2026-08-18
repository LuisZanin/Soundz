# CLAUDE.md — Soundz

Nome do app: **Soundz** — S maiúsculo, resto minúsculo (título da janela,
wordmark e README). O **assembly foi renomeado para `Soundz`** em 18/08, a
pedido do usuário: o executável agora é `Soundz.exe` e o Windows mostra
"Soundz" na barra de tarefas, no Gerenciador de Tarefas e nas propriedades do
arquivo. O **namespace do código continua `Soundboard`** de propósito — é
interno, invisível para o usuário, e trocar em todo arquivo seria só
retrabalho.

Contexto do projeto para o Claude. Manter atualizado a cada mudança relevante.

## Sobre o usuário

- Fala português (PT-BR). **Não conhece C#** — explicar conceitos da linguagem
  e do WPF conforme aparecem, de forma curta e prática.
- Especificação original do projeto: `plano.md` (fonte da verdade do escopo).
  ⚠️ Esse arquivo **não está no repositório** — é material de trabalho, sem
  utilidade para quem só usa o app, e o usuário optou por não publicá-lo
  (`.gitignore`). Ele continua na máquina. As menções a ele adiante são
  contexto histórico: explicam por que certas decisões contrariam o plano.

## O que é

App Windows desktop (C#/.NET 8, WPF) tipo Soundpad: captura o mic físico,
mistura com efeitos sonoros (wav/mp3) em tempo real via NAudio e manda o
resultado para o "CABLE Input" do VB-Audio Virtual Cable. Discord/jogos usam
o "CABLE Output" como microfone e ouvem voz + efeitos.

## Estado atual (2026-08-18)

- ✅ Projeto **compilando sem erros/avisos** (`dotnet build`).
- ✅ Áudio testado com hardware real pelo usuário ("deu perfeitamente certo").
- ✅ **Redesign completo da UI**: identidade Obsidian & Ruby (ver seção abaixo).
- ✅ **Fluxo de reprodução verificado com áudio real**, dirigindo a interface
  por UI Automation: auto-start do mixer, carregar pelo card, tocar pelo FAB,
  play/pausa/retomar, anterior/próximo (tocando × parado), parar todos, fim de
  som voltando para "Pronto para tocar", e o fone alternando no meio do som sem
  religar o mixer. Handles: 20 sons tocados = +3 (sem vazamento).
- ✅ **Configuração persiste** (`AppConfig`, decisão 15): dispositivos e
  volumes voltam como estavam. Testado de ponta a ponta por UI Automation —
  mexer, fechar, reabrir, conferir.
- ✅ **Medidor de nível da voz** no rail (decisão 16). O movimento foi
  verificado forçando um nível falso e comparando quadros: enche pela esquerda
  em esmeralda e vira âmbar perto do talo.
- ✅ **Aviso de saída não-virtual** (decisão 17), verificado escolhendo o fone
  Corsair do usuário: chip âmbar + status explicando que o Discord não ouve.
- ⬜ **Falta a conferência de ouvido do usuário**: se a voz chega no Discord,
  se o efeito sai no fone quando o toggle liga, e se a pausa não estala.
- ✅ **Medidor confirmado com voz real**: visto aceso com sinal do mic
  Corsair do usuário, durante o uso normal dele.
- ✅ **Assembly renomeado para `Soundz`** (decisão 19): `Soundz.exe`, com
  `FileDescription`/`ProductName` conferidos no binário.
- ✅ **Biblioteca movida para `%APPDATA%\Soundz\audios`** (decisão 13). A
  migração foi verificada nos arquivos reais do usuário: 13 de 13 movidos,
  md5 idêntico antes e depois, pasta antiga esvaziada.
- ✅ **Publish self-contained testado**: `Soundz.exe` de `publish/` abre e
  funciona sem SDK/runtime instalado no caminho. 162MB de pasta, 263 arquivos.
- ✅ **Repositório preparado para ser público sob MIT** (decisão 20), com
  varredura de segredos feita.
- ✅ **Instalador gerado e instalado com sucesso** na release `v1.0.1`
  (relato do usuário: "deu certo, ficou ótimo"). Ou seja: a sintaxe do `.iss`
  compila, o workflow completo funciona e a instalação conclui. **Ainda sem
  medida**: o tamanho final do `Setup.exe` — perguntar ao usuário ou olhar a
  aba Releases.
- ⬜ **O app é bloqueado pelo Controle Inteligente de Aplicativos** por não ser
  assinado (decisão 23). Documentado no README; a correção de verdade depende
  de certificado.
- ✅ **`Soundz.ico` existe** (decisão 22): 9 tamanhos verificados no arquivo
  (16→256, todos com canal alfa), gerado do próprio tema e embutido no
  executável. O ícone genérico do Windows sumiu.
- ⬜ **Não existe ícone de bandeja nem início com o Windows.** Foi conversado
  em 18/08 e explicitamente adiado — não há nada disso no código. Fechar no X
  fecha o app de verdade e corta a voz que vai para o cabo; minimizar mantém
  tudo rodando. O `.ico`, que era o pré-requisito, já está pronto.
- .NET 8 SDK (8.0.424) foi instalado via winget nesta máquina (não havia SDK).
  `dotnet` pode não estar no PATH de shells já abertos — se falhar, recarregar
  o PATH: `$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine')`.

## Arquivos

| Arquivo | Papel |
|---|---|
| `Soundz.csproj` | Projeto .NET 8 Windows, WPF, NuGet NAudio 2.2.1 |
| `App.xaml/.cs` | Entrada do app; carrega o tema via MergedDictionaries |
| `Themes/Obsidian.xaml` | Cores, fontes, a marca e TODOS os ControlTemplate |
| `MainWindow.xaml` | Layout (barra de título, rail, grade, tocando agora) |
| `MainWindow.xaml.cs` | Event handlers; liga UI ao AudioEngine |
| `SoundItem.cs` | Um som da grade (nome, duração, arte, IsPlaying) |
| `AudioEngine.cs` | TODA a lógica de áudio (captura, mixagem, saídas) |
| `AppConfig.cs` | Preferências em JSON (`%APPDATA%\Soundz\config.json`) |
| `README.md` | Instruções de uso/instalação do VB-Cable |
| `LICENSE` | MIT |
| `Soundz.ico` | Ícone do executável — **gerado**, não editar na mão |
| `tools/gerar-icone.ps1` | Rasteriza o `Soundz.ico` a partir do tema |
| `installer/soundz.iss` | Script do Inno Setup que gera o `Setup.exe` |
| `.github/workflows/release.yml` | CI que compila e publica a release na tag `v*` |
| `design/` | Mockups e capturas. **Fora do git** (`.gitignore`) — só local |
| `plano.md` | Especificação original. **Fora do git** — só local |

## Identidade visual (Obsidian & Ruby)

Mockup e spec: `design/soundz-dark.html`, que é a fonte da verdade da
identidade e mora no próprio repositório.

> O mockup também existe como artifact do Claude e como projeto no Claude
> Design, mas **os links não entram aqui**: o repositório é público e eles
> apontam para a conta pessoal do autor. Quem mantiver o projeto usa o HTML
> do repositório; quem tiver acesso aos originais sabe onde eles estão.

**Marca**: buraco negro — disco de acreção inclinado −18°, horizonte de eventos
e um arco de lente gravitacional por cima. Sem haste de nota (o usuário pediu
para tirar): o sentido "música" mora no wordmark, a marca carrega o clima.
Definida como `DrawingImage` no tema, em duas versões — `LogoImage` (traço 5)
e `LogoIcon` (traço 8, horizonte menor) para 16–24px, onde a detalhada some.

**Regra semântica das cores** — é ela que faz a paleta funcionar:

| Cor | Significa | Onde |
|---|---|---|
| Ruby `#9B111E` / RubyGlow `#FF2E4C` | está acontecendo agora | palco: card tocando, equalizador, botão Iniciar, FAB |
| Emerald `#10B981` / Deep `#0A7355` | está pronto e saudável | rail: chip VB-Cable, status rodando, toggle, ícone de fone |
| Amber `#FFB020` | precisa de atenção | erros e avisos, no rodapé do rail |

Vermelho é a marca **e** o botão primário, então erro não pode ser vermelho —
daí o âmbar. Divisão espacial: rail em esmeralda, palco em rubi.

**Contraste** (todos medidos contra Obsidian `#1B1B1B`): Ruby puro dá 2.04:1 e
por isso **só preenche, nunca vira texto**; RubyGlow dá 4.71:1 e é o tom de
texto/marca fina. Idem esmeralda: Deep 2.95:1 preenche, Emerald 6.79:1 marca.
Branco sobre Ruby: 8.45:1. Nunca colocar os dois tons da mesma família juntos.

Cinzas puxados para o violeta de propósito (`#232327`, `#7E7B8A`) — vermelho
sobre cinza neutro puro lê como "setup gamer", com violeta lê como espaço.

Tipografia: Segoe UI Variable (já vem no Win11) — Display nos títulos, Text no
resto. Arte dos cards: gradiente "nebulosa" determinístico pelo nome do arquivo
(`SoundItem.StableHash`, hash próprio porque `string.GetHashCode()` do .NET é
aleatorizado a cada execução e as cores mudariam a cada abertura).

## Arquitetura de áudio (AudioEngine)

Formato de mixagem fixo: **48kHz, estéreo, IEEE float**.

```
mic -> WasapiCapture -> BufferedWaveProvider -> resample/canais -> Volume (voz) -> Metering ─┐
                                                                                             ├─> mainMixer -> WasapiOut (CABLE Input)
sons -> AudioFileReader -> resample/canais -> effectsMixerMain -> VolumeSampleProvider (fx) ──┘
  └─ (se monitor ativo) 2º AudioFileReader -> effectsMixerMonitor -> Volume -> WasapiOut (fone)
```

Decisões importantes:

1. **Monitor toca só os efeitos, não a voz** — ouvir a própria voz com ~50ms de
   delay é desagradável (eco). O plano pede "escutar o efeito tocando".
2. **Dois AudioFileReaders por som quando o monitor está ativo** — um
   ISampleProvider não pode alimentar dois mixers (cada Read consome os dados).
   Ler o arquivo duas vezes é a solução mais simples e barata.
3. **Auto-limpeza de sons**: classe interna `AutoDisposeFileReader`. ⚠️ O
   `MixingSampleProvider` **descarta a entrada na primeira leitura CURTA**, não
   espera ela devolver 0 — e a última leitura de um arquivo quase sempre é
   curta. Por isso o wrapper **nunca devolve leitura parcial**: puxa em laço
   até encher o bloco e, no fim do arquivo, completa com silêncio, faz Dispose,
   marca `Finished` e devolve o bloco cheio; só na chamada seguinte devolve 0.
   Sem isso, `Finished` nunca era marcado (som eternamente "tocando" na
   interface) e o arquivo nunca era fechado (um handle vazado por som tocado).
   Comprovado com teste isolado; verificado depois: 20 sons = +3 handles.
4. `ReadFully = true` nos mixers = produzem silêncio quando vazios, mantendo a
   saída viva o tempo todo.
5. `DiscardOnBufferOverflow = true` no buffer do mic = se algo travar, descarta
   áudio em vez de crashar.
6. WasapiOut em modo **Shared**, event sync, latência 50ms.
7. **Não existe botão Iniciar.** O mixer liga sozinho no `Loaded` da janela e
   religa sozinho ao trocar qualquer dispositivo (`Device_Changed`). Os combos
   ficam sempre habilitados; a flag `loadingDevices` evita religar enquanto
   eles ainda estão sendo preenchidos, e `ready` evita religar antes da janela
   existir. Volumes são ao vivo (setters atualizam os VolumeSampleProviders).
7b. **Monitor (fone) é ao vivo, inclusive no meio de um som.** A saída do fone
   é montada no `Start` sempre que houver dispositivo válido, mesmo desligada;
   `MonitorEnabled` só zera/restaura o volume dela. Criar/destruir a saída no
   meio da reprodução exigiria abrir um leitor novo já posicionado no ponto
   certo. `MonitorAvailable` diz se a saída existe; `MonitorEnabled` se está
   audível. Trocar o *dispositivo* do fone ainda religa o mixer.
8. Sliders 0–200% viram fator 0.0–2.0 (`valor / 100.0`).
9. **Barra de progresso única, no rodapé** (decisão do usuário: uma só, não
   uma por card — sons sobrepostos ficam para depois). `PlaySound` retorna um
   `SoundPlayback` (Position/Duration/IsFinished/IsPaused/Seek/Stop). O
   `AutoDisposeFileReader` tem `lock (gate)` porque a thread de áudio (Read) e
   a da UI (Position/Seek) acessam o mesmo AudioFileReader; com monitor ativo o
   Seek move os dois leitores juntos. Duração lida ao adicionar o som
   (`GetSoundDuration` abre e fecha o arquivo).
9b. **A barra usa `CompositionTarget.Rendering`, não `DispatcherTimer`.** O
   evento dispara uma vez por quadro (~60x/s) sincronizado com o desenho, então
   a barra desliza lisa; um timer de 100ms movia em degraus visíveis. Fica
   ligado só enquanto há som (`StartProgressLoop`/`StopProgressLoop`). O texto
   do tempo só é reescrito quando muda de segundo (`lastTimeText`).
9c. **Modelo de interação (estilo Spotify, decidido com o usuário):**
   corpo do card = carrega na barra sem tocar (`SelectItem`); play do card
   (o FAB, um Button dentro do Button, com `e.Handled = true`) = toca na hora;
   play central = toca/pausa o som carregado, começando de onde a barra estiver;
   anterior/próximo dão a volta na lista e **só tocam se já estava tocando** —
   parados, apenas trocam o carregado. Arrastar a barra parado move o ponto de
   partida sem disparar som. `SoundItem.StateTag` ("", "selected", "playing")
   alimenta o `Tag` do card, que o ControlTemplate usa para a borda.
10. **`SoundPlayback.Stop()`**: fecha o arquivo e marca `Finished`. O `Read`
    seguinte devolve 0 e o `MixingSampleProvider` remove a entrada sozinho —
    mesmo caminho de quando o som acaba naturalmente, sem código especial.
11. **Um som por vez** (decisão do usuário, 2026-08-16). O engine aceita
    sobreposição e o `plano.md` pedia isso, mas com uma barra de reprodução só
    e botões de anterior/próximo, dois cards acesos confundem — a interface
    promete um. `PlayItem` chama `StopAllSounds()` antes de começar; apagar
    essa linha devolve a sobreposição. `MainWindow.active` continua sendo uma
    lista porque a limpeza por `IsFinished` já era escrita assim e ainda
    funciona se a sobreposição voltar.
12. **Grade por dados, não por código**: `ObservableCollection<SoundItem>` +
    `ItemsControl` com `DataTemplate`, em vez de criar controles na mão. O card
    é complexo demais para construir imperativamente, e `INotifyPropertyChanged`
    no `SoundItem` repinta o card sozinho quando `IsPlaying` muda.
13. **Sons persistem numa biblioteca em disco** (`MainWindow.LibraryFolder`,
    hoje `C:\soundz\audios`). "+ Adicionar som" **copia** o arquivo para lá;
    ao abrir, `LoadLibrary` varre a pasta e recria a grade. Copiar em vez de
    guardar uma lista de caminhos faz a grade sobreviver a mover/apagar o
    original, e dispensa arquivo de config. Dedup: mesmo nome + mesmo tamanho
    = já importado; mesmo nome com conteúdo diferente vira "nome (2).wav".
    A pasta é `%APPDATA%\Soundz\audios`, ao lado do `config.json`.
    ⚠️ **Não usar `AppContext.BaseDirectory`** (era o plano antigo, escrito
    aqui antes de existir instalador): com o app instalado em
    `C:\Program Files\Soundz`, gravar ao lado do executável exige
    administrador e falha para usuário comum. Dados do usuário vão em
    `%APPDATA%`, e ponto. `MigrateLegacyLibrary` move o que estava na pasta
    antiga (`C:\soundz\audios`) na primeira abertura depois da mudança —
    move arquivo por arquivo, nunca sobrescreve, e só apaga a pasta antiga se
    ela ficar completamente vazia.
14. **Remover som manda o arquivo para a Lixeira**, não apaga
    (`Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` com
    `RecycleOption.SendToRecycleBin` — o namespace vem no framework .NET 8, sem
    pacote extra). Como os sons agora moram na biblioteca, sumir da grade é
    sumir do disco: daí a confirmação e a lixeira. O botão de lixeira fica no
    canto oposto ao de play e só aparece no hover do card.
15. **Configuração persiste em `%APPDATA%\Soundz\config.json`** (decisão do
    usuário, 17/08). Guarda mic/saída/fone (Id **e** nome), o toggle do fone e
    os dois volumes. O Id é o identificador de verdade; o nome é plano B para
    quando o Windows reenumera um aparelho USB e troca o Id (`Remember`). Some
    o aparelho salvo, cai no palpite automático de sempre. AppData e não a
    pasta dos sons porque isto é bilhete do app para ele mesmo, não arquivo do
    usuário. Gravação com **debounce de 600ms** (`saveTimer`): arrastar um
    slider dispara centenas de eventos e sem isso o app escreveria no disco a
    cada pixel. `OnClosed` grava na hora, sem esperar. Toda I/O em `try/catch`
    vazio — perder preferência é chato, derrubar o app por causa dela é pior.
16. **Medidor de nível da voz** no rail, abaixo do slider "Minha voz".
    - Mede **depois** do volume (`MeteringSampleProvider` entre o
      `VolumeSampleProvider` da voz e o `mainMixer`): mostra o que está de fato
      saindo para o cabo, não o que o mic capta. Zerar a voz zera o medidor.
    - O evento `StreamVolume` chega **na thread de áudio**, então o handler só
      guarda um float `volatile`; quem desenha é a UI, no ritmo dela.
    - A escala é em **decibéis** (`LevelToBar`, piso -60dB). Linearmente, fala
      normal (pico ~0.1) acenderia 10% da barra e pareceria mic quebrado; em dB
      o mesmo 0.1 é -20dB e acende dois terços.
    - **Ataque instantâneo, queda suave** (fator 0.18/quadro). Caindo na hora, a
      barra piscaria a cada sílaba.
    - Os segmentos saem de uma **`OpacityMask` tijolada**: um `DrawingBrush` em
      `TileMode="Tile"`, azulejo de 11px com retângulo de 8px. Em vez de 20
      controles, é uma barra só; animar é mexer no `ScaleX` de um
      `ScaleTransform` — repinta sem recalcular layout 60x/s.
    - Cor: **Emerald**, não EmeraldDeep. A regra diz "Deep preenche, Emerald
      marca", e elementos gráficos de 6px de altura são marca fina, não massa —
      mesmo motivo de RubyGlow ser o rubi dos traços finos. Âmbar acima de 92%
      (está estourando = precisa de atenção).
17. **O app avisa quando a saída não é um cabo virtual** (`LooksVirtual`).
    O usuário tentou escolher o próprio headset como saída e estranhou o som
    não chegar no Discord. Motivo: o Windows **não deixa nenhum programa
    escrever num dispositivo de captura** — microfone é só de leitura. Um cabo
    virtual é um par ligado por driver (o app escreve no "CABLE Input", o
    Discord lê do "CABLE Output"), e é o único caminho. Sem aviso, o app
    parecia funcionar enquanto ninguém do outro lado ouvia. O chip embaixo da
    saída virou de dois estados (esmeralda/âmbar) e o status explica o que
    fazer. O palpite automático agora prefere **"CABLE Input"** exato antes de
    aceitar qualquer outro nome com cara de virtual (a máquina do usuário tem
    "CABLE In 16ch" listado antes, e era ele que vinha escolhido).
18. **O laço de quadro (`StartUiLoop`) agora roda enquanto o mixer estiver
    ligado**, não só durante um som: o medidor precisa dele o tempo todo. Ele
    se desliga quando não há som, o mixer está parado **e** o medidor já caiu a
    zero — daí o corte `micMeterValue < 0.001`, senão a barra ficaria em
    0,0001 para sempre e o laço nunca pararia.

19. **Assembly renomeado para `Soundz`** (pedido do usuário, 18/08). No
    `.csproj`: `AssemblyName` e `Product`. O `AssemblyTitle` não precisa ser
    declarado — ele herda do `AssemblyName` e é ele que vira o `FileDescription`
    do executável, que é o texto que o Windows usa na barra de tarefas, no
    Gerenciador de Tarefas e na dica do arquivo. O `.csproj` também foi
    renomeado para `Soundz.csproj`. `RootNamespace` e os `namespace Soundboard`
    do código **não** mudaram. Nada quebrou porque o tema é referenciado por
    caminho relativo (`Themes/Obsidian.xaml`), não por pack URI com o nome do
    assembly — se algum dia virar pack URI, este é o ponto a revisar. Foi
    preciso apagar `bin/` e `obj/`: sobravam arquivos gerados com o nome antigo.

20. **Projeto é público sob MIT** (decisão do usuário, 18/08). Consequências
    que já valeram mudança de código ou de arquivo:
    - **`design/` está no `.gitignore`.** Mockups e capturas de tela são
      material de trabalho e o usuário não quis publicá-los. Os arquivos
      continuam na máquina — foram excluídos do repositório, não apagados.
    - **Links da conta Claude foram removidos do `CLAUDE.md`** (URL do artifact
      e `projectId` do Claude Design). Eram links privados da conta pessoal do
      usuário; num repositório público seriam um vazamento. Não recolocar.
    - **O trailer `Claude-Session:` não entra nos commits.** É um link privado
      para a sessão e ficaria no histórico público para sempre.
    - Antes de qualquer push, rodar a varredura: e-mails, `C:\Users\...`,
      UUIDs e URLs `claude.ai`. Foi assim que os dois links acima apareceram.
21. **Instalador: Inno Setup + GitHub Actions, self-contained** (18/08).
    ⚠️ **Tudo neste item descreve o que foi ESCRITO, não o que foi testado** —
    ver o ⬜ do instalador em "Estado atual". Confirmar na primeira release.
    - **Self-contained** (`--self-contained true`, sem `PublishSingleFile`):
      162MB de pasta (medido). O tamanho do `Setup.exe` comprimido ainda **não
      foi medido** — o instalador nunca foi gerado. Em troca do peso, o usuário
      final não instala runtime nenhum e some a classe inteira de suporte
      "faltou o .NET". Single-file foi descartado porque o instalador já entrega
      uma pasta: não traria vantagem e pagaria o custo de descompactar a cada
      abertura.
    - **`PrivilegesRequired=lowest`**: instala em
      `%LOCALAPPDATA%\Programs\Soundz`, sem UAC (modelo do VS Code).
    - ⚠️ **O VB-Cable NÃO é embutido.** É donationware da VB-Audio e
      redistribuí-lo dentro de outro instalador exige acordo de distribuição
      com eles. O instalador mostra uma página explicando o pré-requisito e
      oferece abrir a página de download; a detecção de verdade é do app, em
      tempo de execução (`LooksVirtual`, decisão 17).
    - O instalador **não apaga** `%APPDATA%\Soundz` ao desinstalar: é a
      biblioteca de sons do usuário.
    - O `Setup.exe` **não é assinado** (certificado é pago), então o SmartScreen
      avisa. Documentado no README e no corpo da release.

22. **Ícone do executável** (18/08). O usuário reportou que a logo não
    aparecia; a verificação separou dois problemas diferentes:
    - `Window.Icon="{StaticResource LogoIcon}"` **funciona** — a janela aberta
      mostra a marca na barra de tarefas. Isso nunca esteve quebrado.
    - O `.exe` não tinha **recurso de ícone nenhum** (`ApplicationIcon` não
      estava declarado). Por isso o Explorador, os atalhos do menu Iniciar e da
      área de trabalho, o ícone fixado e o instalador mostravam o ícone
      genérico azul do Windows. Confirmado por extração antes e depois.
    - Correção: `<ApplicationIcon>Soundz.ico</ApplicationIcon>` no `.csproj`,
      mais `SetupIconFile` e `UninstallDisplayIcon` no `.iss`.
    - **O `.ico` é gerado, não desenhado**: `tools/gerar-icone.ps1` carrega
      `Themes/Obsidian.xaml`, pega o `LogoImage` e rasteriza cada tamanho
      **no seu próprio tamanho** a partir do vetor (não reduzindo de um grande
      só), que é o que mantém o traço nítido em 16px. Formato Pbgra32 para
      preservar o fundo transparente. Mexeu na marca? Rode o script de novo.
    - **Variante escolhida: a marca como está, sem ladrilho** (decisão do
      usuário). Foram testadas três — transparente, ladrilho escuro e ladrilho
      rubi — e a rubi era objetivamente a mais legível em 16px, inclusive
      porque branco sobre rubi dá 8.45:1 enquanto o anel fino de rubi sobre
      fundo escuro dá 2.04:1 (a própria regra de contraste do projeto). O
      usuário preferiu fidelidade à marca. **Não trocar sem perguntar de novo.**

23. **Assinatura de código: o Controle Inteligente de Aplicativos bloqueia o
    Soundz** (18/08). Confirmado na
    [documentação da Microsoft](https://learn.microsoft.com/windows/apps/develop/smart-app-control/overview),
    porque a internet repete três coisas erradas sobre isso:
    - **SmartScreen ≠ Smart App Control.** O primeiro é aviso de reputação e
      tem "Executar assim mesmo". O segundo **bloqueia sem saída** e **não tem
      lista de exceções por aplicativo**. Só afeta instalações limpas do Win11.
    - **Não precisa de certificado EV.** A documentação diz que o SAC libera
      qualquer binário assinado por CA do Trusted Root Program. Certificado OV
      resolve; o EV só acelera a reputação no SmartScreen.
    - **Desligar o SAC é irreversível**: ele "só pode ser ativado numa
      instalação limpa", então voltar atrás exige reinstalar/redefinir o
      Windows. Qualquer instrução que mande desligar precisa dizer isso.
    - Caminho pretendido: **SignPath Foundation**, que assina de graça projetos
      de código aberto com código público e licença reconhecida — o Soundz se
      enquadra. **Azure Trusted Signing foi descartado**: custa pouco (~US$10/mês)
      mas, para desenvolvedor individual, a confiança pública está limitada a
      EUA e Canadá, e o autor é brasileiro.
    - Enquanto não houver assinatura, o README oferece as duas saídas honestas:
      desligar a proteção (com o aviso de permanência) ou compilar do fonte.

## Investigação: "a voz só sai depois que eu toco um som" (18/08, em aberto)

Sintoma do usuário: com o CABLE Output como microfone no Discord, a voz não
chega do outro lado até ele dar play num som no Soundz; a partir daí a voz
passa a sair.

Já descartado, medindo com instrumentação temporária (`MixerInputEnded` +
log do formato):

- O mic do usuário entrega **48kHz mono float**, igual ao formato de mixagem —
  **não entra resampler** na cadeia da voz.
- O `mainMixer` **nunca removeu** a entrada da voz. A suspeita inicial era a
  armadilha da decisão 3 (leitura curta faz o `MixingSampleProvider` descartar
  a entrada), mas ela não acontece aqui.
- O medidor de nível acendeu com sinal real do mic Corsair, ou seja: o Soundz
  manda a voz para o cabo continuamente, desde que abre.

**Hipótese atual (não confirmada): é o portão de voz do Discord.** O slider
"Minha voz" estava em 20%, atenuando a voz 5x. A detecção de atividade de voz
do Discord não abre com sinal fraco; a música, que é alta, abre o portão e
então tudo passa junto — inclusive a voz. Suspeito secundário: a supressão de
ruído (Krisp) do Discord, que costuma comer áudio vindo de cabo virtual.

Se voltar ao assunto: subir "Minha voz" a 100%, desligar a sensibilidade
automática e a supressão de ruído no Discord, e usar o teste de microfone dele.
Nada disso é mudança de código — só será, se a hipótese cair.

## Como testar a interface daqui

O app é dirigido por UI Automation via PowerShell (`System.Windows.Automation`)
e a tela é capturada com `Graphics.CopyFromScreen` a partir do
`MainWindowHandle` do processo `Soundz`.

- **`.ps1` sem BOM é lido como ANSI** pelo Windows PowerShell 5.1: buscar
  elemento por nome com acento (`"Saída virtual"`) falha silenciosamente.
  Procurar por `ControlType` e índice em vez de por nome.
- **Nunca editar arquivo do projeto com `(Get-Content x) | Set-Content x`.**
  No Windows PowerShell 5.1 o `Get-Content` sem `-Encoding` lê na codepage ANSI
  do sistema: cada byte de um caractere UTF-8 vira um caractere solto, e o
  `Set-Content -Encoding utf8` re-codifica isso. Resultado: `Instalação` vira
  `InstalaÃ§Ã£o` no arquivo inteiro, mais um BOM que os arquivos do projeto não
  têm. Os fontes e a documentação são UTF-8 **sem BOM** e cheios de acento —
  usar `[System.IO.File]::ReadAllText($p, [Text.Encoding]::UTF8)` e
  `WriteAllText` com `UTF8Encoding $false`, ou editar por Python.
- `FindWindow(null, "Soundz")` não achou a janela; usar
  `Get-Process -Name Soundz` e o `MainWindowHandle`.

## Pegadinhas de WPF já resolvidas (não reintroduzir)

Todas foram encontradas rodando o app e olhando a tela, não compilando.

- **`Setter TargetName` não alcança um transform.** `ScaleTransform`/
  `TranslateTransform` são `Freezable`, não elementos — dá erro MC4111. Troque
  a propriedade `RenderTransform` inteira do elemento no Setter.
- **Comentário XML não aceita `--`.** Separadores tipo `<!-- ------ -->`
  quebram o build (MC3000). Usar `======`.
- **Não existe pílula automática.** `CornerRadius` maior que metade da altura
  não faz clamp como o CSS — o Border deforma num oval. Por isso as pílulas
  têm altura fixa e raio igual à metade exata (`PillHeight`/`CornerPill`).
- **O `Track` do Slider reserva a largura do thumb** entre as duas metades do
  trilho. Thumb invisível = buraco preto na barra. O thumb desenha ele mesmo a
  emenda, **dividida ao meio**: metade esquerda na cor de preenchimento, direita
  na cor de vazio — senão a barra vaza para além da bolinha.
- **A bolinha do slider fica sempre visível** (decisão do usuário; o Spotify só
  mostra no hover). Ela cresce de 12 para 14 no hover, e por isso o `Thumb` tem
  16 de largura — com 12 ela era recortada.
- **Só as pontas externas do trilho são arredondadas** (`2,0,0,2` e `0,2,2,0`),
  senão aparece um entalhe onde cada metade encosta no thumb.
- **Cantos arredondados da janela** vêm do DWM (`DwmSetWindowAttribute`,
  atributo 33 = `DWMWA_WINDOW_CORNER_PREFERENCE`, valor 2 = arredondado),
  chamado em `OnSourceInitialized`. Quem recorta é o compositor do Windows 11,
  então a sombra sai certa e maximizar volta a quadrado sozinho.
- **Janela sem barra nativa vaza ao maximizar.** `RootBorder.Margin = 7` quando
  `WindowState == Maximized` compensa a borda de redimensionamento.
- **Card precisa de `AutomationProperties.Name`.** O conteúdo é um StackPanel,
  então o WPF não deriva nome nenhum e leitor de tela lê só "botão". Todos os
  botões de ícone também receberam nome explícito.

## Fora de escopo (v1, conforme plano.md)

- Hotkeys globais (deixar preparado, não implementar).
- Volume individual por som.
- Driver de áudio próprio (usuário instala VB-Cable manualmente).

## Mudanças de escopo em relação ao plano.md

- **Sobreposição de sons foi desligada** (decisão 11) — o plano pedia vários
  ao mesmo tempo, mas a interface com barra única e anterior/próximo promete um.
- **Persistência de sons foi adicionada** (decisão 13) — o plano listava
  persistência como fora de escopo; o usuário pediu explicitamente em 16/08.
- **Persistência de configuração foi adicionada** (decisão 15) — mesma história,
  pedida em 17/08.
- **Medidor de nível do microfone foi adicionado** (decisão 16) — não estava no
  plano; pedido em 17/08, inspirado no do Discord.

## Comandos

```powershell
dotnet build   # compilar
dotnet run     # rodar o app
```

## Convenções

- Comentários do código em PT-BR, explicativos (o usuário está aprendendo C#).
- UI em PT-BR.
- Toda mudança relevante de arquitetura/estado deve ser registrada aqui.
