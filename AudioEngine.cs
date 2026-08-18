using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard;

/// <summary>
/// Informações de um dispositivo de áudio (mic ou saída) para exibir na UI.
/// "record" é um tipo C# imutável, ótimo para dados simples.
/// </summary>
public record AudioDeviceInfo(string Id, string Name)
{
    // O ComboBox do WPF usa ToString() para mostrar o item na lista.
    public override string ToString() => Name;
}

/// <summary>
/// Motor de áudio: captura o microfone, mistura com os efeitos sonoros
/// e envia o resultado para a saída virtual (CABLE Input) e, opcionalmente,
/// só os efeitos para o fone do usuário (monitor).
///
/// Grafo de áudio:
///   mic -> buffer -> resample 48kHz estéreo -> volume voz ----\
///                                                              +-> mixer principal -> CABLE
///   som -> resample -> mixer de efeitos -> volume efeitos ----/
///     \-> (se monitor ativo) mixer monitor -> volume efeitos -> fone
/// </summary>
public sealed class AudioEngine : IDisposable
{
    // Formato de mixagem fixo: 48kHz, estéreo, float 32 bits.
    // Tudo que entra no mixer precisa estar neste formato.
    private const int MixSampleRate = 48000;
    private const int MixChannels = 2;

    private WasapiCapture? capture;                  // captura do microfone via WASAPI
    private BufferedWaveProvider? micBuffer;         // fila de bytes vindos do mic
    private WasapiOut? mainOutput;                   // saída principal (CABLE Input)
    private WasapiOut? monitorOutput;                // saída opcional (fone do usuário)

    private MixingSampleProvider? mainMixer;         // voz + efeitos -> CABLE
    private MixingSampleProvider? effectsMixerMain;  // só os efeitos (lado CABLE)
    private MixingSampleProvider? effectsMixerMonitor; // só os efeitos (lado fone)

    private VolumeSampleProvider? voiceVolumeProvider;
    private VolumeSampleProvider? effectsVolumeMainProvider;
    private VolumeSampleProvider? effectsVolumeMonitorProvider;

    private MeteringSampleProvider? voiceMeter;   // mede o pico da voz para o medidor da UI

    // "volatile" avisa o compilador que este campo é escrito por uma thread
    // (a de áudio) e lido por outra (a da UI), então ele não pode guardar o
    // valor em cache num registrador — a UI precisa enxergar o valor novo.
    private volatile float micLevel;

    private float voiceVolume = 1.0f;
    private float effectsVolume = 1.0f;
    private bool monitorEnabled;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Pico da voz no último bloco medido, de 0.0 (silêncio) a 1.0 (no talo).
    /// É o valor DEPOIS do slider de volume, ou seja: é o quanto de voz está
    /// realmente saindo para o cabo virtual, não o quanto o mic capta. Se o
    /// usuário zerar o volume da voz, o medidor zera junto — que é a verdade
    /// que ele precisa ver.
    /// </summary>
    public float MicLevel => micLevel;

    /// <summary>true se existe uma saída de fone montada (device escolhido).</summary>
    public bool MonitorAvailable => monitorOutput != null;

    /// <summary>
    /// Liga/desliga o som no fone AO VIVO, inclusive no meio de um som tocando.
    /// A saída do fone é montada uma vez no Start e nunca destruída; ligar e
    /// desligar só mexe no volume dela. Criar/destruir a saída no meio da
    /// reprodução exigiria abrir um novo leitor do arquivo já posicionado no
    /// ponto certo, com risco de estouro e de dessincronia.
    /// </summary>
    public bool MonitorEnabled
    {
        get => monitorEnabled;
        set
        {
            monitorEnabled = value;
            ApplyMonitorVolume();
        }
    }

    private void ApplyMonitorVolume()
    {
        if (effectsVolumeMonitorProvider != null)
            effectsVolumeMonitorProvider.Volume = monitorEnabled ? effectsVolume : 0f;
    }

    /// <summary>Volume da voz (1.0 = 100%). Pode ser alterado com a mixagem rodando.</summary>
    public float VoiceVolume
    {
        get => voiceVolume;
        set
        {
            voiceVolume = value;
            if (voiceVolumeProvider != null) voiceVolumeProvider.Volume = value;
        }
    }

    /// <summary>Volume dos efeitos (1.0 = 100%). Vale para o CABLE e para o monitor.</summary>
    public float EffectsVolume
    {
        get => effectsVolume;
        set
        {
            effectsVolume = value;
            if (effectsVolumeMainProvider != null) effectsVolumeMainProvider.Volume = value;
            ApplyMonitorVolume(); // respeita o fone estar ligado ou não
        }
    }

    /// <summary>
    /// Chamado pelo medidor NA THREAD DE ÁUDIO. Precisa ser curtíssimo: tudo
    /// que demora aqui atrasa o áudio e vira estalo. Por isso ele só guarda um
    /// número num campo; quem desenha é a UI, no ritmo dela.
    /// </summary>
    private void OnVoiceLevel(object? sender, StreamVolumeEventArgs e)
    {
        // MaxSampleValues traz um pico por canal (esquerdo e direito).
        // Ficamos com o maior dos dois.
        float peak = 0f;
        foreach (var value in e.MaxSampleValues)
            if (value > peak) peak = value;

        micLevel = peak;
    }

    /// <summary>Lista os microfones disponíveis no Windows.</summary>
    public static List<AudioDeviceInfo> GetCaptureDevices() => GetDevices(DataFlow.Capture);

    /// <summary>Lista os dispositivos de saída (caixas, fones, CABLE Input...).</summary>
    public static List<AudioDeviceInfo> GetRenderDevices() => GetDevices(DataFlow.Render);

    private static List<AudioDeviceInfo> GetDevices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
            device.Dispose();
        }
        return result;
    }

    /// <summary>
    /// Inicia captura + mixagem + saídas.
    /// monitorDeviceId nulo = sem monitoramento no fone.
    /// </summary>
    public void Start(string micDeviceId, string mainOutputDeviceId, string? monitorDeviceId)
    {
        if (IsRunning) throw new InvalidOperationException("O engine já está rodando.");

        using var enumerator = new MMDeviceEnumerator();
        var micDevice = enumerator.GetDevice(micDeviceId);
        var mainDevice = enumerator.GetDevice(mainOutputDeviceId);
        var monitorDevice = monitorDeviceId != null ? enumerator.GetDevice(monitorDeviceId) : null;

        try
        {
            var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(MixSampleRate, MixChannels);

            // --- Captura do microfone ---
            capture = new WasapiCapture(micDevice);
            micBuffer = new BufferedWaveProvider(capture.WaveFormat)
            {
                // Se a UI travar e o buffer encher, descarta em vez de estourar.
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2),
            };
            // Evento disparado pelo WASAPI a cada bloco de áudio capturado (~10ms).
            capture.DataAvailable += (_, e) => micBuffer!.AddSamples(e.Buffer, 0, e.BytesRecorded);

            // Converte os bytes do mic para float e ajusta rate/canais para o formato do mixer.
            var micChain = ConformToMixFormat(micBuffer.ToSampleProvider());
            voiceVolumeProvider = new VolumeSampleProvider(micChain) { Volume = voiceVolume };

            // O medidor fica no caminho da voz, entre o volume e o mixer: ele
            // deixa o áudio passar intacto e, de tempos em tempos, avisa qual
            // foi a amostra mais alta do trecho. 48000/50 = a cada 960 quadros,
            // ou seja 50 avisos por segundo — mais que suficiente para uma
            // barrinha que a tela redesenha 60 vezes por segundo.
            voiceMeter = new MeteringSampleProvider(voiceVolumeProvider, MixSampleRate / 50);
            voiceMeter.StreamVolume += OnVoiceLevel;

            // --- Mixer de efeitos (lado principal/CABLE) ---
            // ReadFully = true faz o mixer produzir silêncio quando não há sons,
            // mantendo a saída viva continuamente.
            effectsMixerMain = new MixingSampleProvider(mixFormat) { ReadFully = true };
            effectsVolumeMainProvider = new VolumeSampleProvider(effectsMixerMain) { Volume = effectsVolume };

            // --- Mixer principal: voz + efeitos ---
            mainMixer = new MixingSampleProvider(mixFormat) { ReadFully = true };
            mainMixer.AddMixerInput(voiceMeter);
            mainMixer.AddMixerInput(effectsVolumeMainProvider);

            // --- Saída principal (CABLE Input) ---
            // Shared = divide o dispositivo com outros apps; 50ms de latência.
            mainOutput = new WasapiOut(mainDevice, AudioClientShareMode.Shared, useEventSync: true, latency: 50);
            mainOutput.Init(mainMixer);

            // --- Saída de monitoramento (fone), só com os efeitos ---
            // A saída do fone é sempre montada quando há um dispositivo escolhido,
            // mesmo com o monitoramento desligado — assim ligar no meio de um som
            // é só mexer no volume (ver MonitorEnabled).
            if (monitorDevice != null)
            {
                effectsMixerMonitor = new MixingSampleProvider(mixFormat) { ReadFully = true };
                effectsVolumeMonitorProvider = new VolumeSampleProvider(effectsMixerMonitor)
                {
                    Volume = monitorEnabled ? effectsVolume : 0f,
                };
                monitorOutput = new WasapiOut(monitorDevice, AudioClientShareMode.Shared, useEventSync: true, latency: 50);
                monitorOutput.Init(effectsVolumeMonitorProvider);
            }

            capture.StartRecording();
            mainOutput.Play();
            monitorOutput?.Play();
            IsRunning = true;
        }
        catch
        {
            // Se qualquer passo falhar, desfaz tudo que já foi criado.
            Stop();
            throw;
        }
        finally
        {
            micDevice.Dispose();
            mainDevice.Dispose();
            monitorDevice?.Dispose();
        }
    }

    /// <summary>
    /// Toca um arquivo .wav/.mp3 misturado à voz, opcionalmente a partir de
    /// um ponto do arquivo (ex: 1min de uma música de 2min). Vários sons podem
    /// tocar ao mesmo tempo; cada um é removido do mixer sozinho ao terminar.
    /// Retorna um "controle remoto" (SoundPlayback) para a UI acompanhar a
    /// posição e fazer seek; null se o engine não estiver rodando.
    /// </summary>
    public SoundPlayback? PlaySound(string filePath, TimeSpan startAt = default)
    {
        if (!IsRunning || effectsMixerMain == null) return null;

        // Um leitor de arquivo só pode alimentar UM mixer (cada leitura consome
        // os dados), então o monitor usa um segundo leitor independente.
        var mainInput = CreateFileChain(filePath, startAt);
        var monitorInput = effectsMixerMonitor != null ? CreateFileChain(filePath, startAt) : null;

        effectsMixerMain.AddMixerInput(mainInput);
        if (monitorInput != null) effectsMixerMonitor!.AddMixerInput(monitorInput);

        return new SoundPlayback(mainInput, monitorInput);
    }

    /// <summary>Duração total de um arquivo de áudio (para a barra de progresso).</summary>
    public static TimeSpan GetSoundDuration(string filePath)
    {
        // "using" garante que o arquivo é fechado ao sair do método.
        using var reader = new AudioFileReader(filePath);
        return reader.TotalTime;
    }

    /// <summary>Para tudo e libera os recursos de áudio. Seguro chamar mais de uma vez.</summary>
    public void Stop()
    {
        IsRunning = false;

        capture?.StopRecording();
        mainOutput?.Stop();
        monitorOutput?.Stop();

        // Desinscrever antes de soltar a referência: sem isso o engine antigo
        // continuaria preso na memória pelo próprio evento (o objeto que
        // escuta segura quem dispara... e aqui somos nós dois).
        if (voiceMeter != null) voiceMeter.StreamVolume -= OnVoiceLevel;
        micLevel = 0f;   // o medidor cai a zero quando o mixer desliga

        capture?.Dispose();
        mainOutput?.Dispose();
        monitorOutput?.Dispose();

        // RemoveAllMixerInputs força o Read final dos sons ainda tocando,
        // mas o descarte real é feito pelo AutoDisposeFileReader; aqui só
        // soltamos as referências para o garbage collector limpar.
        capture = null;
        micBuffer = null;
        mainOutput = null;
        monitorOutput = null;
        mainMixer = null;
        effectsMixerMain = null;
        effectsMixerMonitor = null;
        voiceMeter = null;
        voiceVolumeProvider = null;
        effectsVolumeMainProvider = null;
        effectsVolumeMonitorProvider = null;
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Monta a cadeia de leitura de um arquivo de áudio já no formato do mixer,
    /// com descarte automático do arquivo quando o som termina.
    /// </summary>
    private static AutoDisposeFileReader CreateFileChain(string filePath, TimeSpan startAt)
    {
        // AudioFileReader lê wav/mp3 e já entrega amostras em float.
        var reader = new AudioFileReader(filePath);

        if (startAt > TimeSpan.Zero)
        {
            if (startAt >= reader.TotalTime)
            {
                var total = reader.TotalTime;
                reader.Dispose();
                throw new ArgumentException(
                    $"Tempo inicial ({startAt:m\\:ss}) maior que a duração do som ({total:m\\:ss}).");
            }
            // CurrentTime faz "seek": pula direto para este ponto do arquivo.
            reader.CurrentTime = startAt;
        }

        var chain = ConformToMixFormat(reader);
        return new AutoDisposeFileReader(reader, chain);
    }

    /// <summary>
    /// Ajusta qualquer fonte de áudio para 48kHz estéreo float:
    /// resampleia se o sample rate for outro e converte mono -> estéreo.
    /// </summary>
    private static ISampleProvider ConformToMixFormat(ISampleProvider source)
    {
        var result = source;
        if (result.WaveFormat.SampleRate != MixSampleRate)
            result = new WdlResamplingSampleProvider(result, MixSampleRate);

        if (result.WaveFormat.Channels == 1)
            result = new MonoToStereoSampleProvider(result);
        else if (result.WaveFormat.Channels != MixChannels)
            throw new NotSupportedException(
                $"Áudio com {result.WaveFormat.Channels} canais não é suportado (só mono ou estéreo).");

        return result;
    }

}

/// <summary>
/// "Controle remoto" de um som tocando, devolvido por PlaySound.
/// A UI usa para desenhar a barra de progresso e fazer seek.
/// Quando o monitor está ativo existem dois leitores do mesmo arquivo
/// (um por saída); o seek move os dois para manterem sincronia.
/// </summary>
public sealed class SoundPlayback
{
    private readonly AutoDisposeFileReader main;
    private readonly AutoDisposeFileReader? monitor;

    internal SoundPlayback(AutoDisposeFileReader main, AutoDisposeFileReader? monitor)
    {
        this.main = main;
        this.monitor = monitor;
    }

    public TimeSpan Duration => main.Duration;
    public TimeSpan Position => main.Position;
    public bool IsFinished => main.Finished;

    /// <summary>
    /// Pausa/retoma sem perder a posição. Pausado, o som continua dentro do
    /// mixer mas entrega silêncio — por isso o Discord não ouve nada e a
    /// posição fica parada esperando o play.
    /// </summary>
    public bool IsPaused
    {
        get => main.Paused;
        set
        {
            main.Paused = value;
            if (monitor != null) monitor.Paused = value;
        }
    }

    /// <summary>Pula para um ponto do som (como arrastar a barra do Spotify).</summary>
    public void Seek(TimeSpan position)
    {
        main.Seek(position);
        monitor?.Seek(position);
    }

    /// <summary>Corta o som antes de ele terminar (botão parar da barra de baixo).</summary>
    public void Stop()
    {
        main.Stop();
        monitor?.Stop();
    }
}

/// <summary>
/// Envelopa o leitor de arquivo e o descarta sozinho quando o som acaba.
/// O MixingSampleProvider remove automaticamente qualquer entrada cujo
/// Read retorne 0, então: som acabou -> Read devolve 0 -> mixer remove
/// a entrada -> arquivo é fechado. Sem vazamento de memória.
///
/// O "lock (gate)" existe porque duas threads mexem no mesmo leitor:
/// a thread de áudio (Read, a cada ~50ms) e a thread da UI (Position/Seek
/// da barra de progresso). O lock garante que uma espera a outra terminar.
/// </summary>
internal sealed class AutoDisposeFileReader : ISampleProvider
{
    private readonly AudioFileReader reader;
    private readonly ISampleProvider source;
    private readonly object gate = new();

    public AutoDisposeFileReader(AudioFileReader reader, ISampleProvider source)
    {
        this.reader = reader;
        this.source = source;
        Duration = reader.TotalTime;
    }

    public TimeSpan Duration { get; }

    /// <summary>true quando o som terminou e o arquivo já foi fechado.</summary>
    public bool Finished { get; private set; }

    public TimeSpan Position
    {
        get
        {
            lock (gate) return Finished ? Duration : reader.CurrentTime;
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (gate)
        {
            if (Finished) return;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (position > Duration) position = Duration;
            reader.CurrentTime = position;
        }
    }

    /// <summary>
    /// Encerra o som antes da hora: fecha o arquivo e marca como terminado.
    /// O Read seguinte devolve 0 e o MixingSampleProvider tira a entrada
    /// do mixer sozinho — o mesmo caminho de quando o som acaba naturalmente.
    /// </summary>
    public void Stop()
    {
        lock (gate)
        {
            if (Finished) return;
            reader.Dispose();
            Finished = true;
        }
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    /// <summary>Pausado: entrega silêncio sem avançar o arquivo.</summary>
    public bool Paused { get; set; }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (gate)
        {
            if (Finished) return 0;

            if (Paused)
            {
                // Devolver 0 faria o mixer descartar o som; devolver silêncio
                // mantém a entrada viva e o arquivo parado onde estava.
                Array.Clear(buffer, offset, count);
                return count;
            }

            // Puxa até encher o bloco inteiro.
            //
            // Isto NÃO é otimização, é correção: o MixingSampleProvider
            // descarta a entrada assim que uma leitura vem CURTA — não espera
            // ela devolver 0. Como a última leitura de um arquivo quase sempre
            // é curta, devolvê-la fazia o mixer nos remover antes da leitura
            // que devolveria 0. Resultado: Finished nunca era marcado (o som
            // ficava "tocando" para sempre na interface) e o Dispose nunca
            // rodava (um handle de arquivo vazado por som tocado).
            int got = 0;
            while (got < count)
            {
                int n = source.Read(buffer, offset + got, count - got);
                if (n == 0) break;
                got += n;
            }

            if (got < count)
            {
                // Fim do arquivo: completa o bloco com silêncio e encerra.
                // Devolvendo o bloco cheio, o mixer nos mantém mais uma
                // rodada; na próxima o "if (Finished)" acima devolve 0 e aí
                // sim ele nos remove — sem cortar o final do som.
                Array.Clear(buffer, offset + got, count - got);
                reader.Dispose();
                Finished = true;
                return got == 0 ? 0 : count;
            }

            return count;
        }
    }
}
