; ============================================================
;  Soundz — script do instalador (Inno Setup 6)
;
;  Gera um Setup.exe único a partir da pasta publish\, que o
;  `dotnet publish` produziu como self-contained: o .NET 8 vai
;  dentro do pacote, então o usuário final não instala runtime
;  nenhum. Só o VB-Cable continua sendo um passo à parte — ver
;  a página informativa no fim deste arquivo.
;
;  Compilar:  ISCC.exe installer\soundz.iss
;  Normalmente quem compila é o GitHub Actions (.github/workflows/release.yml).
; ============================================================

#define AppName        "Soundz"
#define AppPublisher   "Luis Eduardo Zanin de Toledo"
#define AppUrl         "https://github.com/LuisZanin/soundz"
#define AppExe         "Soundz.exe"

; A versão vem da linha de comando (ISCC /DAppVersion=1.2.3);
; sem ela, assume 0.0.0 para não travar um build local de teste.
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{8F3C2A19-6D4B-4E0A-9C7E-5B2D1A0F7E34}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases

; PrivilegesRequired=lowest instala na pasta do usuário
; (%LOCALAPPDATA%\Programs\Soundz) e NÃO pede UAC. É o modelo do
; VS Code. Instalar em Program Files exigiria administrador e não
; traz vantagem nenhuma para um app de um usuário só.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Ícone do próprio Setup.exe e da entrada em "Aplicativos instalados".
SetupIconFile=..\Soundz.ico
UninstallDisplayIcon={app}\{#AppExe}

OutputDir=Output
OutputBaseFilename=Soundz-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; O app é WPF x64 e o publish é win-x64: em 32 bits não roda.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809 é o piso do .NET 8 Desktop.
MinVersion=10.0.17763

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
; A pasta inteira do publish, incluindo o runtime .NET embutido.
; Excludes o .pdb: e o arquivo de simbolos de depuracao, so serve para
; desenvolvedor e sozinho ja pesa ~35KB de nada util para o usuario final.
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Abrir a página do VB-Cable é OPCIONAL e vem desmarcado para quem já tem.
; O instalador NÃO embute o VB-Cable: ele é software de terceiro
; (donationware da VB-Audio) e redistribuí-lo dentro de outro instalador
; exige acordo de distribuição com eles. Por isso, link.
Filename: "https://vb-audio.com/Cable/"; \
  Description: "Abrir a página de download do VB-Audio Virtual Cable"; \
  Flags: postinstall shellexec nowait unchecked

Filename: "{app}\{#AppExe}"; \
  Description: "Executar o {#AppName} agora"; \
  Flags: postinstall nowait skipifsilent

[UninstallDelete]
; A pasta de dados do usuário (%APPDATA%\Soundz) guarda os sons dele e as
; preferências. NÃO é apagada na desinstalação de propósito: quem reinstala
; espera reencontrar sua biblioteca. Quem quiser zerar, apaga na mão — o
; caminho está no README.

[Code]
{ Página extra avisando sobre o VB-Cable ANTES de instalar. Sem ele o app
  abre e funciona, mas ninguém do outro lado ouve — então o usuário precisa
  saber disso antes, não depois. }
var
  CablePage: TOutputMsgWizardPage;

procedure InitializeWizard();
begin
  CablePage := CreateOutputMsgPage(wpWelcome,
    'Pré-requisito: VB-Audio Virtual Cable',
    'O Soundz precisa de um cabo de áudio virtual para funcionar.',
    'O Windows não permite que nenhum programa escreva áudio dentro de um' + #13#10 +
    'microfone. Para o Discord ouvir sua voz misturada com os efeitos, o som' + #13#10 +
    'precisa passar por um "cabo virtual": o Soundz escreve no CABLE Input e' + #13#10 +
    'o Discord lê do CABLE Output.' + #13#10 + #13#10 +
    'O VB-Audio Virtual Cable é gratuito e se instala separadamente. Ao' + #13#10 +
    'final desta instalação você pode marcar a opção que abre a página de' + #13#10 +
    'download dele.' + #13#10 + #13#10 +
    'Se você já tem o VB-Cable instalado, pode ignorar este aviso e' + #13#10 +
    'continuar normalmente.');
end;
