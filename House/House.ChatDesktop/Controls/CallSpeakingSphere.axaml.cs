using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace House.ChatDesktop.Controls;

public partial class CallSpeakingSphere : UserControl
{
    private bool _speaking;
    private double _level = 0.15;
    private readonly DispatcherTimer _timer;
    private ScaleTransform? _coreScale;
    private ScaleTransform? _glowScale;
    private Ellipse? _glowRing;

    public CallSpeakingSphere()
    {
        InitializeComponent();
        _glowRing = this.FindControl<Ellipse>("GlowRing");
        var core = this.FindControl<Ellipse>("CoreSphere");
        _glowScale = _glowRing?.RenderTransform as ScaleTransform;
        _coreScale = core?.RenderTransform as ScaleTransform;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public void SetSpeaking(bool speaking, double level = 0.5)
    {
        _speaking = speaking;
        _level = Math.Clamp(level, 0.05, 1.0);
        if (!speaking && _coreScale is not null && _glowScale is not null)
        {
            _coreScale.ScaleX = 1;
            _coreScale.ScaleY = 1;
            _glowScale.ScaleX = 1;
            _glowScale.ScaleY = 1;
            if (_glowRing is not null)
                _glowRing.Opacity = 0.2;
        }
    }

    private void Tick()
    {
        if (!_speaking || _coreScale is null || _glowScale is null)
            return;

        var t = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var wave = 0.5 + 0.5 * Math.Sin(t / 90.0);
        var pulse = 1.0 + (_level * 0.35 * wave);
        var glow = 1.0 + (_level * 0.55 * wave);
        _coreScale.ScaleX = pulse;
        _coreScale.ScaleY = pulse;
        _glowScale.ScaleX = glow;
        _glowScale.ScaleY = glow;
        if (_glowRing is not null)
            _glowRing.Opacity = 0.18 + 0.35 * _level * wave;
    }
}
