Quero criar um app Windows desktop simples (C#/.NET 8, WPF) chamado 
"Soundboard" que funciona como um soundboard/virtual mic mixer, similar 
ao Soundpad da Steam.

## Funcionalidade principal
1. Captura o áudio do microfone físico real do usuário (via NAudio 
   WasapiCapture).
2. Permite tocar arquivos de áudio (wav/mp3) sob demanda, um ou vários 
   ao mesmo tempo, através de botões numa grade na interface.
3. Mistura em tempo real: voz do microfone + efeitos sonoros tocando, 
   usando NAudio MixingSampleProvider.
4. Envia o áudio já misturado para:
   - Um dispositivo de saída obrigatório (será o "CABLE Input" do 
     VB-Audio Virtual Cable, que o usuário instala separadamente) — 
     é isso que Discord/jogos vão "ouvir" como microfone.
   - Opcionalmente, um segundo dispositivo de saída (o fone/caixa de 
     som real do usuário), para ele também escutar o efeito tocando, 
     controlado por um checkbox "também ouvir no meu fone".

## Controles de volume
- Slider "Volume da minha voz" (0% a 200%, padrão 100%) controlando 
  apenas o volume do microfone captado.
- Slider "Volume dos efeitos" (0% a 200%, padrão 100%) controlando 
  apenas o volume dos sons tocados.
- Os dois volumes devem ser ajustáveis em tempo real, mesmo com a 
  mixagem já rodando.

## Interface (WPF simples, funcional, não precisa ser bonita)
- ComboBox para escolher o microfone (listar dispositivos de captura 
  disponíveis).
- ComboBox para escolher a saída virtual (listar dispositivos de saída 
  disponíveis, tentar pré-selecionar automaticamente um que contenha 
  "CABLE" no nome).
- Checkbox + ComboBox para a saída de monitoramento opcional (fone real).
- Os dois sliders de volume descritos acima.
- Botão "Iniciar" / "Parar" para começar e parar a captura/mixagem.
- Botão "+ Adicionar som" que abre um FileDialog (aceita .wav e .mp3), 
  e cria um botão novo na grade para cada som adicionado.
- Grade (WrapPanel dentro de ScrollViewer) com um botão por som 
  adicionado; clicar no botão toca aquele som imediatamente, 
  misturado com a voz.

## Requisitos técnicos
- Usar a lib NAudio (pacote NuGet) para captura, mixagem e saída de 
  áudio via WASAPI.
- Resample automático caso o sample rate/canais do mic ou do arquivo 
  de áudio não bater com o formato de mixagem (48kHz, estéreo, float).
- Cada som tocado deve ser automaticamente removido/descartado do 
  mixer quando terminar de tocar (sem vazamento de memória, sem 
  precisar ficar acumulando streams mortos).
- Múltiplos sons devem poder tocar sobrepostos (clicar em dois botões 
  em sequência rápida toca os dois ao mesmo tempo).
- Tratar corretamente o Dispose de tudo (captura, saídas, streams de 
  arquivo) ao fechar o app ou clicar em "Parar".

## Não incluir nesta primeira versão (fora de escopo por agora)
- Atalhos de teclado globais (hotkeys) — deixar preparado pra 
  adicionar depois, mas não implementar ainda.
- Volume individual por som.
- Persistência de configuração entre sessões.
- Instalador/gerador de driver de áudio virtual próprio (o app vai 
  depender do usuário ter o VB-Audio Virtual Cable já instalado 
  manualmente).

## Entregável
- Projeto WPF completo (.csproj, App.xaml/.cs, MainWindow.xaml/.cs, 
  e uma classe AudioEngine separada com toda a lógica de áudio) 
  pronto para abrir no Visual Studio ou rodar com `dotnet run`.
- Um README.md curto explicando: como instalar o VB-Audio Virtual 
  Cable, como buildar/rodar o projeto, e como configurar o Discord/jogo 
  para usar "CABLE Output" como microfone.