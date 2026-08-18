using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace Soundboard;

/// <summary>
/// Um som da grade: caminho do arquivo, nome, duração, a arte gerada
/// e se está tocando agora.
///
/// Implementa INotifyPropertyChanged — essa é a interface que a interface
/// gráfica "escuta". Quando IsPlaying muda, o objeto avisa, e o card na
/// tela se repinta sozinho. Sem isso, mudar a propriedade no C# não teria
/// nenhum efeito visual.
/// </summary>
public sealed class SoundItem : INotifyPropertyChanged
{
    /// <summary>
    /// Paleta de "nebulosas" para a arte dos cards. Sons não têm capa de
    /// álbum, então geramos uma: cada arquivo recebe sempre o mesmo par de
    /// cores, escolhido pelo nome. Todas ficam na faixa ruby → magenta →
    /// violeta → índigo para não brigar com a paleta do app.
    /// </summary>
    private static readonly (string From, string To)[] Nebulas =
    {
        ("#FF5E0A13", "#FFC81E3A"),
        ("#FF2A1B4E", "#FF7C3AED"),
        ("#FF16213E", "#FF3B5BDB"),
        ("#FF4A0E2E", "#FFC2255C"),
        ("#FF3B0A1F", "#FF9B111E"),
        ("#FF1E1B3A", "#FF5B4FCF"),
        ("#FF2D1B3D", "#FF9333EA"),
        ("#FF611B2E", "#FFFF2E4C"),
    };

    public string Path { get; }
    public string Name { get; }
    public TimeSpan Duration { get; }
    public string DurationText { get; }
    public Brush Art { get; }

    private bool isPlaying;
    private bool isSelected;

    /// <summary>true enquanto este som está tocando. Repinta o card.</summary>
    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (isPlaying == value) return;
            isPlaying = value;
            Notify(nameof(IsPlaying));
            Notify(nameof(StateTag));
        }
    }

    /// <summary>true quando este é o som carregado na barra de baixo.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            Notify(nameof(IsSelected));
            Notify(nameof(StateTag));
        }
    }

    /// <summary>
    /// Estado do card em texto. O ControlTemplate usa a propriedade Tag do
    /// botão para decidir a borda, e Tag compara texto, não booleano.
    /// Tocando ganha de selecionado.
    /// </summary>
    public string StateTag => isPlaying ? "playing" : isSelected ? "selected" : "";

    public SoundItem(string path, TimeSpan duration)
    {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        Duration = duration;
        DurationText = FormatTime(duration);
        Art = MakeArt(Name);
    }

    /// <summary>Formata um tempo como "0:07" ou "1:24".</summary>
    public static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    /// <summary>Monta o gradiente da arte a partir do nome do arquivo.</summary>
    private static Brush MakeArt(string name)
    {
        var pair = Nebulas[StableHash(name) % Nebulas.Length];

        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1),
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(pair.From), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(pair.To), 1));

        // Freeze deixa o pincel imutável — o WPF então pode reutilizá-lo entre
        // threads e desenhar mais rápido. Vale para qualquer pincel que não muda.
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Hash próprio, estável entre execuções. O string.GetHashCode() do .NET
    /// muda a cada vez que o app abre (é aleatorizado por segurança), então
    /// o mesmo som ganharia cores diferentes a cada abertura.
    /// </summary>
    private static int StableHash(string s)
    {
        int h = 17;
        unchecked
        {
            foreach (char c in s) h = h * 31 + c;
        }
        return h & 0x7FFFFFFF; // descarta o bit de sinal para nunca dar negativo
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
