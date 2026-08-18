using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO; // só para mandar o arquivo à Lixeira
using Microsoft.Win32;

namespace Soundboard;

/// <summary>
/// Code-behind da janela: liga a interface (XAML) ao AudioEngine.
/// Os métodos *_Click / *_Changed são chamados pelo WPF quando o usuário
/// mexe nos controles (padrão "event handler").
///
/// Modelo de uso (decidido com o usuário):
///   • o mixer liga sozinho ao abrir e religa sozinho ao trocar dispositivo;
///   • clicar no CARD carrega o som na barra de baixo, sem tocar;
///   • clicar no PLAY DO CARD toca na hora;
///   • o play central toca/pausa o som carregado;
///   • anterior/próximo trocam o som carregado, e tocam se já estava tocando.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AudioEngine engine = new();

    /// <summary>
    /// Preferências da sessão anterior (dispositivos e volumes). Lidas do disco
    /// antes de qualquer coisa; a partir daí este objeto é a cópia viva do que
    /// será gravado de volta.
    /// </summary>
    private readonly AppConfig config = AppConfig.Load();

    /// <summary>
    /// Adia a gravação da configuração. Arrastar um slider dispara centenas de
    /// eventos por segundo; sem este atraso o app escreveria no disco a cada
    /// pixel do arrasto. O timer reinicia a cada mexida e só grava quando o
    /// usuário para de mexer — o famoso "debounce".
    /// </summary>
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };

    /// <summary>
    /// A lista de sons da grade. ObservableCollection avisa a interface
    /// sozinha quando um item entra ou sai — por isso a grade se atualiza
    /// sem precisarmos criar/remover controles na mão.
    /// </summary>
    private readonly ObservableCollection<SoundItem> sounds = new();

    /// <summary>
    /// Sons tocando neste instante. O engine deixa vários se sobreporem;
    /// esta lista serve para apagar o destaque do card quando cada um acaba.
    /// </summary>
    private readonly List<(SoundItem Item, SoundPlayback Playback)> active = new();

    // O som carregado na barra de baixo, e a reprodução dele se estiver tocando.
    private SoundItem? currentItem;
    private SoundPlayback? currentPlayback;

    // true enquanto o app mexe na barra de progresso — assim o handler de
    // ValueChanged distingue "app atualizando" de "usuário arrastando".
    private bool updatingSeek;

    // Evita religar o mixer enquanto os ComboBoxes ainda estão sendo preenchidos.
    private bool loadingDevices;
    private bool ready;

    // Controle do laço de atualização (ver StartUiLoop).
    private bool uiHooked;
    private string lastTimeText = "";

    // Estado desenhado do medidor de voz: a fração da barra acesa (0 a 1) e se
    // ela já está na faixa âmbar. Guardar o "hot" evita trocar o pincel 60
    // vezes por segundo quando nada mudou.
    private double micMeterValue;
    private bool micMeterHot;

    /// <summary>Piso do medidor em decibéis: abaixo disso a barra fica vazia.</summary>
    private const double MeterFloorDb = -60;

    /// <summary>
    /// Pedaços de nome que denunciam um cabo de áudio virtual. É só uma
    /// heurística para avisar o usuário — não bloqueia nada.
    /// </summary>
    private static readonly string[] VirtualHints =
        { "CABLE", "VoiceMeeter", "Virtual", "VAC", "Loopback" };

    /// <summary>Estado de saúde mostrado pela bolinha ao lado do status.</summary>
    private enum Health { Idle, Live, Error }

    public MainWindow()
    {
        InitializeComponent();

        // Restaura o que estava salvo ANTES de montar as listas de dispositivos:
        // o LoadDevices consulta a config para decidir o que vem pré-selecionado.
        VoiceSlider.Value = config.VoiceVolume;
        EffectsSlider.Value = config.EffectsVolume;
        MonitorCheck.IsChecked = config.MonitorEnabled;
        saveTimer.Tick += (_, _) => SaveConfigNow();

        SoundsList.ItemsSource = sounds;
        LoadDevices();
        MigrateLegacyLibrary();   // resgata sons da pasta antiga, se houver
        LoadLibrary();            // sons guardados de sessões anteriores
        UpdateTransport();

        // Só liga o mixer depois que a janela existe de verdade: se falhar,
        // a mensagem de erro precisa de controles já criados para aparecer.
        Loaded += (_, _) =>
        {
            ready = true;
            RestartEngine();
        };
    }

    // ================================================================
    // BARRA DE TÍTULO CUSTOM E CANTOS ARREDONDADOS
    // ================================================================

    /// <summary>
    /// DwmSetWindowAttribute é uma função do próprio Windows (não do .NET).
    /// DllImport é a ponte: declara a assinatura e o C# passa a poder chamá-la.
    /// </summary>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int WindowCornerPreference = 33; // DWMWA_WINDOW_CORNER_PREFERENCE
    private const int CornerRound = 2;             // DWMWCP_ROUND

    /// <summary>
    /// Chamado quando a janela ganha um handle do Windows, antes de aparecer.
    /// É o momento certo para pedir os cantos arredondados: quem arredonda é o
    /// compositor do Windows 11, então a sombra e o recorte saem corretos, e ao
    /// maximizar ele volta a quadrado sozinho. Em versões antigas do Windows a
    /// chamada simplesmente não faz nada — por isso o retorno é ignorado.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        int preference = CornerRound;
        DwmSetWindowAttribute(handle, WindowCornerPreference, ref preference, sizeof(int));
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Sem barra de título nativa, uma janela maximizada vaza alguns pixels
    /// para fora da tela (a largura da borda de redimensionamento). A margem
    /// compensa isso; ao restaurar, volta a zero.
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        bool max = WindowState == WindowState.Maximized;
        RootBorder.Margin = max ? new Thickness(7) : new Thickness(0);
        MaximizeIcon.Data = (Geometry)FindResource(max ? "IconRestore" : "IconMaximize");
        MaximizeButton.ToolTip = max ? "Restaurar" : "Maximizar";
    }

    // ================================================================
    // DISPOSITIVOS E CICLO DE VIDA DO MIXER
    // ================================================================

    /// <summary>Preenche os ComboBoxes com os dispositivos de áudio do Windows.</summary>
    private void LoadDevices()
    {
        // A flag evita que cada SelectedIndex daqui dispare um religamento.
        loadingDevices = true;
        try
        {
            var mics = AudioEngine.GetCaptureDevices();
            var outputs = AudioEngine.GetRenderDevices();

            MicCombo.ItemsSource = mics;
            OutputCombo.ItemsSource = outputs;
            MonitorCombo.ItemsSource = outputs;

            // Nos três: o que estava salvo ganha; se aquele aparelho não existe
            // mais, cai no palpite automático de sempre.
            MicCombo.SelectedIndex = Remember(mics, config.MicId, config.MicName,
                                              mics.Count > 0 ? 0 : -1);

            // "CABLE Input" é o nome canônico do VB-Cable — se existir, é ele.
            // Só depois aceita outro cabo virtual qualquer (VoiceMeeter, as
            // variantes de 16 canais…), que podem vir antes na lista.
            var cable = outputs.FindIndex(d => d.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
            if (cable < 0) cable = outputs.FindIndex(d => LooksVirtual(d.Name));

            OutputCombo.SelectedIndex = Remember(outputs, config.OutputId, config.OutputName,
                                                 cable >= 0 ? cable : (outputs.Count > 0 ? 0 : -1));

            // Para o fone, o palpite é o primeiro que NÃO seja cabo virtual —
            // devolver os efeitos para o cabo não faria sentido.
            var real = outputs.FindIndex(d => !LooksVirtual(d.Name));
            MonitorCombo.SelectedIndex = Remember(outputs, config.MonitorId, config.MonitorName,
                                                  real >= 0 ? real : (outputs.Count > 0 ? 0 : -1));
        }
        finally
        {
            loadingDevices = false;
        }

        UpdateCableChip();
    }

    /// <summary>
    /// Reencontra na lista atual o dispositivo que estava salvo: primeiro pelo
    /// Id (o identificador de verdade), depois pelo nome — plano B para quando
    /// o Windows reenumera um aparelho USB e troca o Id. Não achando nenhum
    /// dos dois, devolve o índice de reserva.
    /// </summary>
    private static int Remember(List<AudioDeviceInfo> devices, string? id, string? name, int fallback)
    {
        var index = devices.FindIndex(d => d.Id == id);

        if (index < 0 && !string.IsNullOrEmpty(name))
            index = devices.FindIndex(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : fallback;
    }

    /// <summary>
    /// Chuta se uma saída é um cabo de áudio virtual (VB-Cable, VoiceMeeter…).
    ///
    /// Por que isso importa: o Windows não deixa NENHUM programa escrever áudio
    /// num microfone — dispositivo de captura é só de leitura. Escolher aqui o
    /// fone ou a placa real faz o mix sair no alto-falante e mais nada; o
    /// Discord continua ouvindo o mic cru, sem os efeitos. Um cabo virtual é um
    /// par ligado por driver (o app escreve no "CABLE Input", o Discord lê do
    /// "CABLE Output") e é a única forma de o áudio misturado chegar lá.
    /// </summary>
    private static bool LooksVirtual(string name) =>
        VirtualHints.Any(hint => name.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pinta o chip embaixo da saída: esmeralda ("está pronto") quando ela é um
    /// cabo virtual, âmbar ("precisa de atenção") quando é um aparelho real.
    /// </summary>
    private void UpdateCableChip()
    {
        if (OutputCombo.SelectedItem is not AudioDeviceInfo output)
        {
            CableChip.Visibility = Visibility.Collapsed;
            return;
        }

        bool virtualCable = LooksVirtual(output.Name);

        CableChip.Visibility = Visibility.Visible;
        CableChip.Background = (Brush)FindResource(virtualCable ? "EmeraldFaint" : "WarnFaint");
        CableChipIcon.Data = (Geometry)FindResource(virtualCable ? "IconCheck" : "IconAlert");
        CableChipIcon.Stroke = (Brush)FindResource(virtualCable ? "Emerald" : "Warn");
        CableChipText.Foreground = (Brush)FindResource(virtualCable ? "Emerald" : "Warn");
        CableChipText.Text = virtualCable ? "Cabo virtual — o Discord ouve"
                                          : "Saída real — o Discord não ouve";
    }

    // ================================================================
    // CONFIGURAÇÃO QUE SOBREVIVE AO FECHAR
    // ================================================================

    /// <summary>Marca que há algo para salvar; grava quando o usuário parar de mexer.</summary>
    private void SaveConfigSoon()
    {
        if (!ready) return;

        // Parar e recomeçar reinicia a contagem dos 600ms.
        saveTimer.Stop();
        saveTimer.Start();
    }

    /// <summary>Copia o estado atual da tela para a config e grava no disco.</summary>
    private void SaveConfigNow()
    {
        saveTimer.Stop();

        config.MicId = (MicCombo.SelectedItem as AudioDeviceInfo)?.Id;
        config.MicName = (MicCombo.SelectedItem as AudioDeviceInfo)?.Name;
        config.OutputId = (OutputCombo.SelectedItem as AudioDeviceInfo)?.Id;
        config.OutputName = (OutputCombo.SelectedItem as AudioDeviceInfo)?.Name;
        config.MonitorId = (MonitorCombo.SelectedItem as AudioDeviceInfo)?.Id;
        config.MonitorName = (MonitorCombo.SelectedItem as AudioDeviceInfo)?.Name;
        config.MonitorEnabled = MonitorCheck.IsChecked == true;
        config.VoiceVolume = VoiceSlider.Value;
        config.EffectsVolume = EffectsSlider.Value;

        config.Save();
    }

    /// <summary>Trocar qualquer dispositivo religa o mixer — não há botão Iniciar.</summary>
    private void Device_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loadingDevices || !ready) return;

        UpdateCableChip();
        RestartEngine();
        SaveConfigSoon();
    }

    /// <summary>
    /// Para tudo e liga o mixer de novo com os dispositivos selecionados.
    /// Os sons que estavam tocando param — trocar dispositivo no meio de um
    /// som não teria como continuar de onde parou.
    /// </summary>
    private void RestartEngine()
    {
        StopAllSounds();
        NowBarUnload();
        engine.Stop();

        if (MicCombo.SelectedItem is not AudioDeviceInfo mic ||
            OutputCombo.SelectedItem is not AudioDeviceInfo output)
        {
            SetStatus("Escolha um microfone e uma saída para o mixer ligar.", Health.Error);
            return;
        }

        // O fone é montado sempre que houver um dispositivo válido, mesmo
        // desligado — assim o toggle funciona ao vivo (ver MonitorEnabled).
        string? monitorId = null;
        if (MonitorCombo.SelectedItem is AudioDeviceInfo monitor && monitor.Id != output.Id)
            monitorId = monitor.Id;

        try
        {
            engine.MonitorEnabled = MonitorCheck.IsChecked == true;
            engine.Start(mic.Id, output.Id, monitorId);

            // O medidor de voz precisa do laço mesmo sem nenhum som tocando.
            StartUiLoop();

            if (!LooksVirtual(output.Name))
            {
                // O mixer subiu, mas está apontado para um aparelho real: o som
                // sai no alto-falante e ninguém do outro lado ouve. Isso é aviso
                // (âmbar), não sucesso (verde) — ver LooksVirtual para o porquê.
                SetStatus($"Rodando, mas \"{output.Name}\" é uma saída real: o Discord não ouve "
                        + "por aí. Instale o VB-Cable e escolha \"CABLE Input\" acima.", Health.Error);
                return;
            }

            var extra = engine.MonitorEnabled
                ? (engine.MonitorAvailable
                    ? " Efeitos também no fone."
                    : " O fone não pode ser o mesmo dispositivo da saída virtual.")
                : "";

            SetStatus($"Rodando — misturando \"{mic.Name}\" para \"{output.Name}\".{extra}",
                      engine.MonitorEnabled && !engine.MonitorAvailable ? Health.Error : Health.Live);
        }
        catch (Exception ex)
        {
            engine.Stop();
            SetStatus($"Não consegui ligar o mixer: {ex.Message}", Health.Error);
        }
        finally
        {
            UpdateMonitorIcon();
        }
    }

    /// <summary>Liga/desliga o fone AO VIVO, sem religar o mixer.</summary>
    private void MonitorCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        engine.MonitorEnabled = MonitorCheck.IsChecked == true;
        UpdateMonitorIcon();
        SaveConfigSoon();

        if (engine.MonitorEnabled && !engine.MonitorAvailable)
            SetStatus("O fone não pode ser o mesmo dispositivo da saída virtual. "
                    + "Escolha outro na lista abaixo.", Health.Error);
    }

    /// <summary>O ícone de fone da barra de baixo é atalho para o mesmo toggle.</summary>
    private void MonitorIcon_Click(object sender, RoutedEventArgs e) =>
        MonitorCheck.IsChecked = MonitorCheck.IsChecked != true;

    private void UpdateMonitorIcon() =>
        MonitorIcon.Foreground = (Brush)FindResource(
            engine.MonitorEnabled && engine.MonitorAvailable ? "Emerald" : "TextMid");

    /// <summary>Escreve o status e ajusta a bolinha: verde pulsando, âmbar ou apagada.</summary>
    private void SetStatus(string text, Health health)
    {
        StatusText.Text = text;

        var pulse = (Storyboard)FindResource("StatusPulse");
        pulse.Stop(this);
        StatusDot.Opacity = 1;

        switch (health)
        {
            case Health.Live:
                StatusText.Foreground = (Brush)FindResource("Emerald");
                StatusDot.Fill = (Brush)FindResource("Emerald");
                pulse.Begin(this, true); // true = controlável, dá para parar depois
                break;

            case Health.Error:
                StatusText.Foreground = (Brush)FindResource("Warn");
                StatusDot.Fill = (Brush)FindResource("Warn");
                break;

            default:
                StatusText.Foreground = (Brush)FindResource("TextDim");
                StatusDot.Fill = (Brush)FindResource("Stroke");
                break;
        }
    }

    // ================================================================
    // SONS
    // ================================================================

    /// <summary>
    /// Pasta onde os sons ficam guardados entre sessões. Os arquivos escolhidos
    /// são COPIADOS para cá — assim a grade sobrevive mesmo que o original seja
    /// movido ou apagado, e não é preciso guardar uma lista de caminhos.
    ///
    /// Fica em %APPDATA%\Soundz\audios, ao lado do config.json. NÃO pode ficar
    /// ao lado do executável: com o app instalado em C:\Program Files\Soundz,
    /// gravar lá exige administrador e falharia para usuário comum. Dado do
    /// usuário mora no perfil do usuário.
    /// </summary>
    private static string LibraryFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Soundz", "audios");

    /// <summary>
    /// Onde a biblioteca morava antes do instalador existir. Mantido só para
    /// resgatar os sons de quem já usava o app; pode sumir daqui a algumas
    /// versões.
    /// </summary>
    private const string LegacyLibraryFolder = @"C:\soundz\audios";

    /// <summary>
    /// Traz os sons da pasta antiga para a nova, uma única vez.
    ///
    /// Move em vez de copiar (é a mesma unidade de disco, então é só um
    /// renomear, instantâneo e sem duplicar arquivo). Nunca sobrescreve nada
    /// que já exista no destino, e só apaga a pasta antiga se ela ficar
    /// completamente vazia — se sobrou qualquer coisa lá dentro, deixa quieto.
    /// </summary>
    private void MigrateLegacyLibrary()
    {
        try
        {
            if (!Directory.Exists(LegacyLibraryFolder)) return;

            Directory.CreateDirectory(LibraryFolder);

            int moved = 0;
            foreach (var file in Directory.EnumerateFiles(LegacyLibraryFolder).Where(IsSupportedAudio))
            {
                var destination = Path.Combine(LibraryFolder, Path.GetFileName(file));
                if (File.Exists(destination)) continue;   // já veio antes: não mexe

                File.Move(file, destination);
                moved++;
            }

            // Vazia de verdade (nem arquivo nem subpasta)? Some com ela.
            if (!Directory.EnumerateFileSystemEntries(LegacyLibraryFolder).Any())
                Directory.Delete(LegacyLibraryFolder);

            if (moved > 0)
                SetStatus($"{moved} som(ns) movidos da pasta antiga para {LibraryFolder}.", Health.Idle);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode impedir o app de abrir: os sons continuam
            // na pasta antiga e o usuário pode movê-los na mão.
            SetStatus($"Não consegui mover os sons da pasta antiga: {ex.Message}", Health.Error);
        }
    }

    /// <summary>Lê a pasta da biblioteca e recria a grade. Roda ao abrir o app.</summary>
    private void LoadLibrary()
    {
        try
        {
            Directory.CreateDirectory(LibraryFolder);

            var files = Directory.EnumerateFiles(LibraryFolder)
                .Where(IsSupportedAudio)
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase);

            foreach (var file in files) AddSound(file);
        }
        catch (Exception ex)
        {
            SetStatus($"Não consegui abrir a pasta de sons ({LibraryFolder}): {ex.Message}", Health.Error);
        }

        UpdateSoundsHeader();
    }

    private static bool IsSupportedAudio(string path) =>
        path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    private void AddSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolher sons",
            Filter = "Arquivos de áudio (*.wav;*.mp3)|*.wav;*.mp3",
            Multiselect = true,
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var path in dialog.FileNames) ImportSound(path);
        UpdateSoundsHeader();
    }

    /// <summary>Copia o arquivo escolhido para a biblioteca e põe na grade.</summary>
    private void ImportSound(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(LibraryFolder);

            // Já está na biblioteca? Só carrega, não duplica.
            if (IsInsideLibrary(sourcePath))
            {
                if (!AlreadyListed(sourcePath)) AddSound(sourcePath);
                return;
            }

            var destination = Path.Combine(LibraryFolder, Path.GetFileName(sourcePath));
            var info = new FileInfo(sourcePath);

            // Mesmo nome e mesmo tamanho = já foi importado antes.
            if (File.Exists(destination) && new FileInfo(destination).Length == info.Length)
            {
                if (!AlreadyListed(destination)) AddSound(destination);
                return;
            }

            // Mesmo nome mas conteúdo diferente: guarda os dois, numerando.
            destination = MakeUniquePath(destination);
            File.Copy(sourcePath, destination);
            AddSound(destination);
        }
        catch (Exception ex)
        {
            SetStatus($"Não consegui guardar \"{Path.GetFileName(sourcePath)}\": {ex.Message}", Health.Error);
        }
    }

    private static bool IsInsideLibrary(string path) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                      Path.GetFullPath(LibraryFolder).TrimEnd(Path.DirectorySeparatorChar),
                      StringComparison.OrdinalIgnoreCase);

    private bool AlreadyListed(string path) =>
        sounds.Any(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>"riso.wav" ocupado vira "riso (2).wav", "riso (3).wav"...</summary>
    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (int n = 2; n < 1000; n++)
        {
            var candidate = Path.Combine(folder, $"{name} ({n}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException($"Já existem cópias demais de \"{name}\" na pasta de sons.");
    }

    private void AddSound(string path)
    {
        try
        {
            // A duração é lida uma vez agora (abre e fecha o arquivo).
            var duration = AudioEngine.GetSoundDuration(path);
            sounds.Add(new SoundItem(path, duration));
        }
        catch (Exception ex)
        {
            SetStatus($"Não consegui ler \"{Path.GetFileName(path)}\": {ex.Message}", Health.Error);
        }
    }

    private void UpdateSoundsHeader()
    {
        EmptyState.Visibility = sounds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = sounds.Count switch
        {
            0 => "Nenhum som",
            1 => "1 som",
            _ => $"{sounds.Count} sons",
        };
        UpdateTransport();
    }

    /// <summary>Clique no corpo do card: só carrega o som na barra, não toca.</summary>
    private void Card_Click(object sender, RoutedEventArgs e)
    {
        // DataContext do botão é o SoundItem que o DataTemplate desenhou.
        if (sender is Button { DataContext: SoundItem item }) SelectItem(item);
    }

    /// <summary>
    /// Remove o som: tira da grade e manda o arquivo para a LIXEIRA do Windows
    /// (não apaga de vez). Como os sons agora moram em C:\soundz\audios, sumir
    /// da grade significa sumir do disco — por isso a confirmação e a lixeira.
    /// </summary>
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // não deixa o clique virar "carregar o card"
        if (sender is not Button { DataContext: SoundItem item }) return;

        var answer = MessageBox.Show(
            $"Remover \"{item.Name}\"?\n\nO arquivo vai para a Lixeira e some da grade.",
            "Soundz", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes) return;

        // Se estiver tocando ou carregado na barra, encerra antes de mexer no arquivo.
        foreach (var (playing, playback) in active.Where(a => ReferenceEquals(a.Item, item)).ToList())
        {
            playback.Stop();
            playing.IsPlaying = false;
        }
        active.RemoveAll(a => ReferenceEquals(a.Item, item));
        if (ReferenceEquals(currentItem, item)) NowBarUnload();

        try
        {
            FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (Exception ex)
        {
            SetStatus($"Não consegui remover \"{item.Name}\": {ex.Message}", Health.Error);
            return;
        }

        sounds.Remove(item);
        UpdateSoundsHeader();
    }

    /// <summary>Clique no botão de play do card: toca na hora.</summary>
    private void Fab_Click(object sender, RoutedEventArgs e)
    {
        // Handled = true impede o clique de subir para o card em volta, que
        // faria "carregar" logo depois de "tocar".
        e.Handled = true;
        if (sender is Button { DataContext: SoundItem item }) PlayItem(item, TimeSpan.Zero);
    }

    /// <summary>
    /// Carrega um som na barra de baixo sem tocar. Se ele já estiver tocando,
    /// a barra passa a acompanhar a posição real dele.
    /// </summary>
    private void SelectItem(SoundItem item)
    {
        foreach (var s in sounds) s.IsSelected = ReferenceEquals(s, item);
        currentItem = item;

        // Se este som já está no ar, a barra segue a reprodução existente.
        var running = active.FirstOrDefault(a => ReferenceEquals(a.Item, item));
        currentPlayback = running.Playback is { IsFinished: false } ? running.Playback : null;

        ShowInNowBar(item, currentPlayback?.Position ?? TimeSpan.Zero);
        if (currentPlayback != null) StartUiLoop();
    }

    private void PlayItem(SoundItem item, TimeSpan startAt)
    {
        if (!engine.IsRunning)
        {
            SetStatus("O mixer não está rodando — confira os dispositivos acima.", Health.Error);
            return;
        }
        if (!File.Exists(item.Path))
        {
            SetStatus($"Arquivo não encontrado: {item.Path}", Health.Error);
            return;
        }

        try
        {
            // Um som por vez. O engine aceita sobreposição (e o plano.md pedia
            // isso), mas com uma barra de reprodução só e botões de anterior/
            // próximo, dois cards acesos ao mesmo tempo confundem: a interface
            // promete um. Para reativar a sobreposição, basta apagar a linha.
            StopAllSounds();

            var playback = engine.PlaySound(item.Path, startAt);
            if (playback == null) return;

            active.Add((item, playback));
            item.IsPlaying = true;

            foreach (var s in sounds) s.IsSelected = ReferenceEquals(s, item);
            currentItem = item;
            currentPlayback = playback;

            ShowInNowBar(item, startAt);
            StartUiLoop();
        }
        catch (Exception ex)
        {
            SetStatus($"Não consegui tocar \"{item.Name}\": {ex.Message}", Health.Error);
        }
    }

    private void StopAllSounds()
    {
        foreach (var (item, playback) in active)
        {
            playback.Stop();
            item.IsPlaying = false;
        }
        active.Clear();
    }

    // ================================================================
    // TRANSPORTE (play/pausa, anterior, próximo)
    // ================================================================

    private void Transport_Click(object sender, RoutedEventArgs e)
    {
        if (currentPlayback is { IsFinished: false })
        {
            // Tocando ou pausado: alterna. A posição não se perde.
            currentPlayback.IsPaused = !currentPlayback.IsPaused;
            UpdateTransport();
            return;
        }

        var target = currentItem ?? sounds.FirstOrDefault();
        if (target == null) return;

        // Começa de onde a barra estiver — o usuário pode ter arrastado antes.
        var from = ReferenceEquals(target, currentItem)
            ? TimeSpan.FromSeconds(SeekSlider.Value)
            : TimeSpan.Zero;

        PlayItem(target, from);
    }

    private void Prev_Click(object sender, RoutedEventArgs e) => Step(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Step(+1);

    /// <summary>
    /// Anterior/próximo na ordem da grade, dando a volta nas pontas.
    /// Se algo estava tocando, o novo som já começa; se estava parado,
    /// só troca o que está carregado — igual ao Spotify.
    /// </summary>
    private void Step(int direction)
    {
        if (sounds.Count == 0) return;

        bool wasPlaying = currentPlayback is { IsFinished: false, IsPaused: false };

        int index = currentItem != null ? sounds.IndexOf(currentItem) : -1;
        int next = index < 0
            ? (direction > 0 ? 0 : sounds.Count - 1)
            : (index + direction + sounds.Count) % sounds.Count;

        var item = sounds[next];

        if (wasPlaying) PlayItem(item, TimeSpan.Zero);
        else SelectItem(item);
    }

    private void StopAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (_, playback) in active) playback.Stop();
    }

    // ================================================================
    // BARRA "TOCANDO AGORA"
    // ================================================================

    private void ShowInNowBar(SoundItem item, TimeSpan startAt)
    {
        NowArt.Background = item.Art;
        NowArtIcon.Opacity = 0.85;
        NowName.Text = item.Name;
        NowName.Foreground = (Brush)FindResource("TextHi");
        NowName.FontWeight = FontWeights.SemiBold;
        TimeTotal.Text = item.DurationText;
        TimeCurrent.Text = SoundItem.FormatTime(startAt);
        lastTimeText = TimeCurrent.Text;

        updatingSeek = true;
        SeekSlider.IsEnabled = true;
        SeekSlider.Maximum = Math.Max(0.1, item.Duration.TotalSeconds);
        SeekSlider.Value = Math.Min(startAt.TotalSeconds, SeekSlider.Maximum);
        updatingSeek = false;

        UpdateTransport();
    }

    /// <summary>O som acabou, mas continua carregado — dá para tocar de novo.</summary>
    private void NowBarStopped()
    {
        currentPlayback = null;
        if (currentItem != null) currentItem.IsSelected = true;

        TimeCurrent.Text = "0:00";
        lastTimeText = "0:00";

        updatingSeek = true;
        SeekSlider.Value = 0;
        updatingSeek = false;

        UpdateTransport();
    }

    /// <summary>Nenhum som carregado (estado inicial e ao religar o mixer).</summary>
    private void NowBarUnload()
    {
        foreach (var s in sounds) s.IsSelected = false;
        currentItem = null;
        currentPlayback = null;

        NowArt.Background = (Brush)FindResource("Hover");
        NowArtIcon.Opacity = 0.3;
        NowName.Text = "Nenhum som carregado";
        NowName.Foreground = (Brush)FindResource("TextMid");
        NowName.FontWeight = FontWeights.Normal;
        TimeCurrent.Text = "0:00";
        TimeTotal.Text = "0:00";
        lastTimeText = "0:00";

        updatingSeek = true;
        SeekSlider.Value = 0;
        SeekSlider.Maximum = 1;
        SeekSlider.IsEnabled = false;
        updatingSeek = false;

        UpdateTransport();
    }

    /// <summary>Ajusta o ícone do play central e a legenda embaixo do nome.</summary>
    private void UpdateTransport()
    {
        bool playing = currentPlayback is { IsFinished: false, IsPaused: false };
        TransportIcon.Data = (Geometry)FindResource(playing ? "IconPause" : "IconPlay");

        NowSub.Text = currentItem == null ? "Clique em um card para carregar"
                    : playing ? "Tocando"
                    : currentPlayback != null ? "Pausado"
                    : "Pronto para tocar";

        bool canStep = sounds.Count > 1;
        PrevButton.IsEnabled = canStep;
        NextButton.IsEnabled = canStep;
        PrevButton.Opacity = canStep ? 1 : 0.35;
        NextButton.Opacity = canStep ? 1 : 0.35;
    }

    /// <summary>
    /// Liga o laço de atualização da barra. Usa CompositionTarget.Rendering em
    /// vez de um DispatcherTimer: o evento dispara uma vez por quadro desenhado
    /// (~60x/s) e sincronizado com o próprio desenho da tela, então a barra
    /// desliza liso. Um timer de 100ms move a barra em degraus visíveis.
    ///
    /// Fica ligado enquanto houver som tocando OU o mixer rodando: o medidor de
    /// voz precisa dele o tempo todo, não só durante um som. O custo é um punhado
    /// de contas por quadro — desprezível perto de desenhar a tela.
    /// </summary>
    private void StartUiLoop()
    {
        if (uiHooked) return;
        CompositionTarget.Rendering += UpdateUi;
        uiHooked = true;
    }

    private void StopUiLoop()
    {
        if (!uiHooked) return;
        CompositionTarget.Rendering -= UpdateUi;
        uiHooked = false;
    }

    private void UpdateUi(object? sender, EventArgs e)
    {
        UpdateMicMeter();

        // Tira da lista os sons que terminaram e apaga o destaque do card.
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (!active[i].Playback.IsFinished) continue;

            var item = active[i].Item;
            active.RemoveAt(i);

            // O mesmo som pode estar tocando em duas cópias sobrepostas;
            // só apaga o destaque se nenhuma sobrou.
            if (!active.Any(a => ReferenceEquals(a.Item, item))) item.IsPlaying = false;
        }

        if (currentPlayback != null)
        {
            if (currentPlayback.IsFinished)
            {
                NowBarStopped();
            }
            else if (!currentPlayback.IsPaused)
            {
                var position = currentPlayback.Position;

                updatingSeek = true;
                SeekSlider.Value = Math.Min(position.TotalSeconds, SeekSlider.Maximum);
                updatingSeek = false;

                // O texto só muda de segundo em segundo; reescrever 60x por
                // segundo seria desperdício, então só escreve quando mudou.
                var text = SoundItem.FormatTime(position);
                if (text != lastTimeText)
                {
                    TimeCurrent.Text = text;
                    lastTimeText = text;
                }
            }
        }

        // Só desliga quando não há mais nada para animar: nenhum som tocando,
        // o mixer parado e o medidor já caído até zero.
        if (active.Count == 0 && currentPlayback == null && !engine.IsRunning && micMeterValue == 0)
            StopUiLoop();
    }

    // ================================================================
    // MEDIDOR DE VOZ
    // ================================================================

    /// <summary>
    /// Move a barrinha de nível da voz, uma vez por quadro.
    /// </summary>
    private void UpdateMicMeter()
    {
        double target = engine.IsRunning ? LevelToBar(engine.MicLevel) : 0;

        // Sobe na hora, desce devagar — é assim que um medidor de áudio de
        // verdade se comporta. Se descesse na hora, cada sílaba faria a barra
        // piscar e o olho não acompanharia nada.
        micMeterValue = target > micMeterValue
            ? target
            : micMeterValue + (target - micMeterValue) * 0.18;

        // Sem este corte a barra ficaria eternamente em 0,0001 — invisível na
        // tela, mas o bastante para o laço nunca achar que pode parar.
        if (micMeterValue < 0.001) micMeterValue = 0;

        // Mexer na escala do transform só repinta; mexer na largura obrigaria o
        // WPF a recalcular o layout do rail inteiro 60 vezes por segundo.
        ((ScaleTransform)MicMeterFill.RenderTransform).ScaleX = micMeterValue;

        // Perto do talo a barra vira âmbar: a voz está estourando e vai distorcer.
        bool hot = micMeterValue > 0.92;
        if (hot != micMeterHot)
        {
            micMeterHot = hot;
            MicMeterFill.Fill = (Brush)FindResource(hot ? "Warn" : "Emerald");
        }
    }

    /// <summary>
    /// Converte o pico do áudio (0.0 a 1.0) na fração da barra que deve acender.
    ///
    /// A conta é em decibéis porque o ouvido é logarítmico. Na régua crua, uma
    /// fala normal tem pico perto de 0,1 e acenderia só 10% da barra — pareceria
    /// microfone quebrado. Em decibéis esse mesmo 0,1 vira -20dB e acende dois
    /// terços, que é o que a pessoa espera ver ao falar normalmente.
    /// </summary>
    private static double LevelToBar(float peak)
    {
        if (peak <= 0.0001f) return 0;

        double db = 20 * Math.Log10(peak);
        return Math.Clamp((db - MeterFloorDb) / -MeterFloorDb, 0, 1);
    }

    /// <summary>
    /// Usuário mexeu na barra: tocando, pula de posição; parado, só move o
    /// ponto de partida — quem começa é o play central.
    /// </summary>
    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updatingSeek || currentItem == null) return;

        var target = TimeSpan.FromSeconds(e.NewValue);
        currentPlayback?.Seek(target);

        TimeCurrent.Text = SoundItem.FormatTime(target);
        lastTimeText = TimeCurrent.Text;
    }

    // ================================================================
    // VOLUMES (ao vivo, mesmo com a mixagem rodando)
    // ================================================================

    private void VoiceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Este evento dispara durante a construção da janela, antes dos outros
        // controles existirem — por isso a checagem de null.
        if (VoiceValueText == null) return;
        VoiceValueText.Text = $"{(int)e.NewValue}%";
        engine.VoiceVolume = (float)(e.NewValue / 100.0); // 100% -> 1.0
        SaveConfigSoon();
    }

    private void EffectsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (EffectsValueText == null) return;
        EffectsValueText.Text = $"{(int)e.NewValue}%";
        engine.EffectsVolume = (float)(e.NewValue / 100.0);
        SaveConfigSoon();
    }

    /// <summary>Janela fechando: grava a config, para o laço, o áudio e libera tudo.</summary>
    protected override void OnClosed(EventArgs e)
    {
        SaveConfigNow();   // agora, sem esperar os 600ms do debounce
        StopUiLoop();
        engine.Dispose();
        base.OnClosed(e);
    }
}
