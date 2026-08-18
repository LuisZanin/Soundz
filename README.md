# Soundz

Soundboard e mixer de microfone virtual para Windows. Mistura sua voz com
efeitos sonoros em tempo real e manda tudo para um microfone virtual, que o
Discord e os jogos enxergam como se fosse o seu mic.

Feito em C# / .NET 8 (WPF + [NAudio](https://github.com/naudio/NAudio)).
Licença MIT — use, modifique e redistribua à vontade.

> **Este projeto foi construído com IA.** O código, a identidade visual e a
> documentação foram escritos em conjunto com o Claude (Anthropic), sob
> direção, decisões de produto e testes com hardware real do autor. Fica
> registrado por transparência — e porque explica o tom do código, cujos
> comentários são propositalmente didáticos: o projeto também serviu para
> aprender C# e WPF.

## Instalação

Baixe o `Soundz-*-setup.exe` mais recente em
[Releases](https://github.com/LuisZanin/soundz/releases) e execute.

O instalador não pede administrador (instala na sua pasta de usuário) e traz o
.NET embutido — você não precisa instalar runtime nenhum.

> O instalador não é assinado digitalmente, porque certificado de assinatura é
> pago. O Windows vai mostrar um aviso do SmartScreen: clique em
> **Mais informações → Executar assim mesmo**. Todo o código-fonte está aqui.

Falta uma coisa, e ela é obrigatória:

## Pré-requisito: VB-Audio Virtual Cable

### Por que não dá para usar meu fone/headset direto?

Essa é a dúvida mais comum, então vai antes das instruções.

O Windows **não deixa nenhum programa escrever áudio num microfone**. Um
dispositivo de captura é só de leitura — o app consegue *ler* o seu mic, nunca
*colocar* som dentro dele. Se você escolher o seu headset na caixa "Saída
virtual", o mix vai sair no alto-falante do headset e o Discord vai continuar
ouvindo o microfone cru, sem os efeitos.

O cabo virtual resolve isso sendo um **par ligado por driver**:

- **CABLE Input** é uma *saída* — é nela que o Soundz escreve;
- **CABLE Output** é uma *entrada* — é dela que o Discord lê.

O driver liga uma na outra. É a única forma de o áudio misturado chegar no
Discord, e é por isso que o app avisa em âmbar quando a saída escolhida é um
aparelho de verdade em vez de um cabo virtual.

### Instalando o cabo

1. Baixe em <https://vb-audio.com/Cable/> (gratuito, donationware).
2. Extraia o zip e rode `VBCABLE_Setup_x64.exe` **como administrador**.
3. Reinicie o PC. Vão aparecer dois dispositivos novos:
   - **CABLE Input** (saída) — para onde este app envia o áudio misturado.
   - **CABLE Output** (microfone) — o que o Discord/jogo vai usar como mic.

O VB-Cable é software de terceiro e **não vem embutido** neste instalador:
redistribuí-lo dentro de outro pacote exige acordo com a VB-Audio. Por isso o
Soundz apenas aponta o caminho.

## Como usar

O mixer **liga sozinho** quando você abre o app — não há botão Iniciar. O rodapé
da coluna da esquerda mostra o estado: bolinha verde pulsando = rodando.

1. **Microfone**: escolha seu mic físico real.
2. **Saída virtual**: o app pré-seleciona automaticamente o "CABLE Input".
   Trocar qualquer dispositivo religa o mixer sozinho.
3. (Opcional) Ligue **"Ouvir no meu fone"** para escutar os efeitos enquanto
   tocam. Pode ligar e desligar no meio de um som (sua voz não é monitorada,
   só os efeitos — ouvir a própria voz com atraso incomoda).
4. Clique **+ Adicionar som** e escolha arquivos .wav/.mp3.
5. Para tocar:
   - **clique no corpo do card** → carrega o som na barra de baixo, sem tocar;
   - **clique no botão vermelho de play do card** → toca na hora;
   - **play central** → toca/pausa o som carregado;
   - **anterior/próximo** → trocam de som; só disparam áudio se já estava tocando.
6. Ajuste os sliders de volume (voz e efeitos) a qualquer momento, ao vivo.

### O medidor de voz

Embaixo do slider "Minha voz" há uma fileira de pontinhos que acende conforme
você fala — igual ao teste de microfone do Discord. Ele serve para responder
"está saindo voz?" sem precisar chamar alguém para testar.

- **Apagado enquanto você fala** → o mic errado está escolhido, ele está mudo
  no Windows, ou o volume da voz está em 0%.
- **Verde subindo até uns dois terços** → é o ponto certo.
- **Âmbar no talo** → a voz está estourando e vai distorcer; abaixe o slider.

Ele mede *depois* do slider de volume, ou seja, mostra o que está realmente
indo para o cabo — não o que o microfone capta.

### O que o app lembra

Microfone, saída virtual, dispositivo do fone, o toggle "Ouvir no meu fone" e
os dois volumes são gravados em `%APPDATA%\Soundz\config.json` e voltam
como estavam na próxima vez que você abrir. Apagar esse arquivo devolve o app
aos padrões — ele se detecta sozinho de novo.

### Onde ficam os sons

Os arquivos escolhidos são **copiados** para `%APPDATA%\Soundz\audios` e
voltam sozinhos toda vez que você abre o app — não precisa adicionar de novo.
(Cole esse caminho na barra do Explorador para abrir a pasta.)

Desinstalar o Soundz **não apaga** essa pasta: quem reinstala espera reencontrar
sua biblioteca. Para zerar de vez, apague a pasta na mão.

Para remover um som, passe o mouse sobre o card e clique na **lixeira** no canto
superior. O arquivo vai para a Lixeira do Windows, então dá para recuperar se
você se arrepender.

## Configurar o Discord/jogo

- **Discord**: Configurações → Voz e vídeo → Dispositivo de entrada →
  selecione **CABLE Output (VB-Audio Virtual Cable)**.
- **Jogos**: nas opções de áudio/voz, escolha **CABLE Output** como microfone.

Quem estiver do outro lado ouvirá sua voz + os efeitos misturados.

Deixe o Soundz **aberto**. Ele não precisa estar tocando nada — mas é ele que
leva sua voz até o cabo. Fechou o Soundz, o CABLE Output fica mudo.

## Problemas comuns

**"Minha voz só sai depois que eu toco um som."**
Provavelmente é o portão de voz do Discord, não o Soundz. Com sinal fraco, a
detecção de atividade de voz não abre; quando um som alto toca, ela abre e aí
tudo passa junto. Tente, nesta ordem:

1. Suba **"Minha voz"** para 100% no Soundz e confira o medidor.
2. No Discord: Configurações → Voz e vídeo → desligue **"Determinar
   automaticamente a sensibilidade de entrada"** e arraste a barra para a
   esquerda.
3. Ainda ali, desligue a **Supressão de ruído** (Krisp) — ela costuma comer
   áudio vindo de cabo virtual.

**"O chip embaixo da saída está âmbar."**
A saída escolhida é um aparelho real, não um cabo virtual. Escolha
"CABLE Input" na lista. Se ele não aparece, o VB-Cable não está instalado.

**"O medidor não acende quando eu falo."**
Microfone errado na lista, mic mudo no Windows, ou "Minha voz" em 0%.

## Compilar do código-fonte

Requer o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/LuisZanin/soundz.git
cd soundz
dotnet run
```

Ou abra a pasta no Visual Studio 2022 e aperte F5.

Para gerar o pacote que o instalador consome:

```powershell
dotnet publish Soundz.csproj -c Release -r win-x64 --self-contained true -o publish
```

E, com o [Inno Setup 6](https://jrsoftware.org/isdl.php) instalado:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 installer\soundz.iss
```

Na prática isso roda sozinho: empurrar uma tag `v*` dispara o workflow
`.github/workflows/release.yml`, que compila, empacota e publica o instalador
na aba Releases.

## Como funciona por dentro

O áudio é montado em 48kHz, estéreo, float, com NAudio:

```
mic  -> WasapiCapture -> buffer -> volume -> medidor --\
                                                        +-> mixer -> WasapiOut (CABLE Input)
sons -> AudioFileReader -> mixer de efeitos -> volume --/
   \-> (se o fone estiver ligado) 2º leitor -> mixer do fone -> WasapiOut (fone)
```

O monitor toca **só os efeitos**, nunca a voz: ouvir a própria voz com ~50ms de
atraso é desagradável.

`CLAUDE.md` documenta as decisões de arquitetura e as armadilhas de WPF/NAudio
já resolvidas — vale a leitura antes de mexer no código.

## Contribuindo

Issues e pull requests são bem-vindos. O código e os comentários estão em
português, e os comentários são propositalmente explicativos: o projeto nasceu
como aprendizado de C#/WPF. Mantenha esse tom.

## Licença

[MIT](LICENSE) © 2026 Luis Eduardo Zanin de Toledo
