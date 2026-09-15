; ─────────────────────────────────────────────
;  SK MacroHelper — Inno Setup Script
;  Desenvolvido por Aline Martins · Silk
; ─────────────────────────────────────────────

#define MyAppName      "SK MacroHelper"
#define MyAppPublisher "Silk · Aline Martins"
#define MyAppExeName   "MacroHelper.exe"
#define BuildDir       SourcePath + "MacroHelper.UI\bin\Release\net8.0-windows\win-x64\publish"

; A versão vem do executável compilado, que por sua vez vem de <Version> em
; MacroHelper.UI.csproj. Antes havia um número fixo aqui, e ele já divergia do csproj em duas
; versões — um instalador dizendo 1.1.0 ao instalar a 1.3.0. Agora o csproj é a fonte única.
;
; Falhar aqui é melhor que gerar um instalador silenciosamente errado: sem publish, não há
; versão para ler, e o que sairia seria um pacote vazio com número em branco.
#if !FileExists(BuildDir + "\" + MyAppExeName)
  #error Publique o app antes de compilar o instalador. Veja o Passo 1 de COMO_GERAR_INSTALADOR.md
#endif
; "FileVersion" e não a versão binária: o .NET grava ali exatamente o texto de <FileVersion>,
; então o instalador sai "2.0.0" como no csproj, e não "2.0.0.0".
; (Não use ProductVersion — o SourceLink acrescenta "+<sha do commit>" nela.)
#define MyAppVersion GetStringFileInfo(BuildDir + "\" + MyAppExeName, "FileVersion")

[Setup]
AppId={{B8D2F3A1-C4E5-4F6A-A7B8-C9D0E1F2A3B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SKMacroHelper
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Installer\Output
OutputBaseFilename=SKMacroHelper_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=SK MacroHelper — Ferramenta de produtividade por Aline Martins · Silk

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon";  Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:";
Name: "startupicon";  Description: "Iniciar automaticamente com o Windows"; GroupDescription: "Opções:";

; Copia a pasta de publicação inteira em vez de enumerar extensões.
;
; A lista anterior era .exe + *.dll + *.json + runtimes\, e deixava Resources\manual_sql_max.html
; para trás: a tela Manual SQL abria em branco no app instalado e funcionava no app rodado do
; código-fonte. Enumerar extensões só volta a errar quando alguém acrescentar um arquivo novo;
; copiar tudo o que foi publicado é a regra que não precisa ser mantida.
[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";             Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir SK MacroHelper agora"; Flags: nowait postinstall skipifsilent

; Um único mecanismo de início automático, e é o mesmo que Configurações › Comportamento liga e
; desliga (ver IniciarComWindowsHelper.NomeValor — os nomes precisam continuar idênticos).
;
; Havia também um atalho em {commonstartup}, com dois problemas: essa pasta é de TODOS os
; usuários e exige elevação, que PrivilegesRequired=lowest não tem; e o toggle do app só conhece
; a chave Run, então desmarcar a opção na tela deixaria o atalho para trás e o app continuaria
; abrindo sozinho, sem nada na interface explicando por quê.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SKMacroHelper"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Messages]
BeveledLabel=SK MacroHelper {#MyAppVersion} — by Aline Martins · Silk
