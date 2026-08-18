using System.IO;
using System.Text.Json;

namespace Soundboard;

/// <summary>
/// A configuração que sobrevive ao fechar o app: quais dispositivos estavam
/// escolhidos, os volumes e se o fone estava ligado.
///
/// É gravada como JSON em %APPDATA%\Soundz\config.json — a pasta padrão do
/// Windows para preferências de programa. Os *sons* ficam noutro lugar
/// (C:\soundz\audios) porque são arquivos que o usuário mexe; isto aqui é
/// só um bilhete do app para ele mesmo.
///
/// Nada aqui pode derrubar o app: se o arquivo sumir, estiver corrompido ou
/// a pasta for somente-leitura, o app volta aos padrões e segue em frente.
/// Por isso todo acesso a disco está dentro de try/catch.
/// </summary>
public sealed class AppConfig
{
    // O System.Text.Json só grava/lê propriedades públicas com get E set.
    // Guardamos o Id (que é o identificador real do dispositivo) e também o
    // Nome, como plano B: se o Windows reenumerar o aparelho e o Id mudar,
    // ainda dá para reencontrá-lo por nome.
    public string? MicId { get; set; }
    public string? MicName { get; set; }

    public string? OutputId { get; set; }
    public string? OutputName { get; set; }

    public string? MonitorId { get; set; }
    public string? MonitorName { get; set; }

    public bool MonitorEnabled { get; set; }

    // Guardados na escala do slider (0 a 200), não no fator do engine (0.0 a
    // 2.0): assim o arquivo fica legível e a UI restaura o valor direto.
    public double VoiceVolume { get; set; } = 100;
    public double EffectsVolume { get; set; } = 100;

    private static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Soundz");

    public static string FilePath => Path.Combine(Folder, "config.json");

    // WriteIndented deixa o JSON com quebras de linha e recuo — o usuário
    // consegue abrir o arquivo no Bloco de Notas e entender o que está lá.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Lê a configuração salva. Qualquer problema devolve os padrões.</summary>
    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppConfig();
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch
        {
            // Arquivo corrompido ou sem permissão: começa limpo em vez de travar.
            return new AppConfig();
        }
    }

    /// <summary>Grava a configuração. Falha em silêncio de propósito.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Não conseguir salvar a preferência é chato, não é fatal —
            // interromper o usuário com um erro aqui seria pior que perdê-la.
        }
    }
}
