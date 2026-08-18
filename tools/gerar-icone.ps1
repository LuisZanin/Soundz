# ============================================================
#  Gera o Soundz.ico a partir da marca definida em
#  Themes/Obsidian.xaml — o tema é a fonte da verdade, este
#  script só rasteriza. Mexeu no LogoImage? Rode isto de novo.
#
#      powershell -File tools\gerar-icone.ps1
#
#  O .ico sai com 9 tamanhos, cada um rasterizado no seu próprio
#  tamanho a partir do vetor (e não reduzido de um grande só),
#  que é o que mantém o traço nítido em 16px.
# ============================================================

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Xaml
$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$tema = Join-Path $raiz "Themes\Obsidian.xaml"
$saida = Join-Path $raiz "Soundz.ico"

$stream = [System.IO.File]::OpenRead($tema)
$dict = [System.Windows.Markup.XamlReader]::Load($stream)
$stream.Close()

$logo = $dict["LogoImage"]
if ($null -eq $logo) { throw "LogoImage nao encontrado em $tema" }

function Render([int]$tamanho) {
  $visual = New-Object System.Windows.Media.DrawingVisual
  $ctx = $visual.RenderOpen()
  $ctx.DrawImage($logo, (New-Object System.Windows.Rect 0, 0, $tamanho, $tamanho))
  $ctx.Close()

  # Pbgra32 = 32 bits com canal alfa: o fundo transparente da marca
  # precisa dele, senao viraria preto solido.
  $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
           $tamanho, $tamanho, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
  $rtb.Render($visual)
  return $rtb
}

$tamanhos = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

# Cada quadro vai como PNG dentro do .ico — formato aceito pelo
# Windows desde o Vista, e bem menor que bitmap cru nos tamanhos grandes.
$quadros = @()
foreach ($t in $tamanhos) {
  $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
  $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create((Render $t)))
  $ms = New-Object System.IO.MemoryStream
  $enc.Save($ms)
  $quadros += ,@($t, $ms.ToArray())
  $ms.Close()
}

$fs = [System.IO.File]::Create($saida)
$bw = New-Object System.IO.BinaryWriter($fs)

# Cabecalho ICONDIR: reservado, tipo (1 = icone), quantidade
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$quadros.Count)

# Diretorio: 16 bytes por quadro, logo apos o cabecalho de 6
$offset = 6 + (16 * $quadros.Count)
foreach ($q in $quadros) {
  $t = $q[0]; $dados = $q[1]
  # Largura e altura em UM byte: 256 nao cabe, e por convencao vira 0.
  $bw.Write([Byte]$(if ($t -ge 256) { 0 } else { $t }))
  $bw.Write([Byte]$(if ($t -ge 256) { 0 } else { $t }))
  $bw.Write([Byte]0)        # cores da paleta (0 = sem paleta)
  $bw.Write([Byte]0)        # reservado
  $bw.Write([UInt16]1)      # planos de cor
  $bw.Write([UInt16]32)     # bits por pixel
  $bw.Write([UInt32]$dados.Length)
  $bw.Write([UInt32]$offset)
  $offset += $dados.Length
}
foreach ($q in $quadros) { $bw.Write($q[1]) }

$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output "gerado: $saida"
Write-Output "tamanhos: $($tamanhos -join ', ')  |  $((Get-Item $saida).Length) bytes"
