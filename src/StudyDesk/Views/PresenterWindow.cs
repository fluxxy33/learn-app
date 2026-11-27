using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;

namespace StudyDesk.Views;

/// <summary>Présentation plein écran avec transitions et notes de l'orateur.</summary>
public sealed class PresenterWindow : Window
{
    private readonly Slideshow _show;
    private readonly SlideTheme _theme;
    private int _index;
    private readonly Grid _stage = new() { ClipToBounds = true };
    private readonly Border _notes;
    private readonly TextBlock _notesText = new() { Foreground = Brushes.White, FontSize = 20, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notesInfo = new() { Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)), FontSize = 15, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Border _black = new() { Background = Brushes.Black, Visibility = Visibility.Collapsed };
    private readonly TextBlock _hint;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public PresenterWindow(Slideshow show, int start)
    {
        _show = show;
        _theme = SlideThemes.Get(show.ThemeId);
        _index = Math.Clamp(start, 0, show.Slides.Count - 1);
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.Arrow;
        Title = "Présentation";

        var root = new Grid();
        root.Children.Add(_stage);
        _notes = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(225, 18, 20, 28)), VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(28, 16, 28, 20),
            Visibility = Visibility.Collapsed, Child = new StackPanel { Children = { _notesInfo, _notesText } }, MaxHeight = 320
        };
        root.Children.Add(_notes);
        root.Children.Add(_black);
        _hint = new TextBlock
        {
            Text = "→ suivante · ← précédente · N notes · B écran noir · Échap quitter", Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
            Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), Padding = new Thickness(12, 6, 12, 6), FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 16, 0, 0)
        };
        root.Children.Add(_hint);
        Content = root;

        KeyDown += OnKey;
        MouseLeftButtonUp += (_, _) => Next();
        MouseRightButtonUp += (_, _) => Previous();
        MouseWheel += (_, e) => { if (e.Delta < 0) Next(); else Previous(); };
        _timer.Tick += (_, _) => UpdateNotes();
        _timer.Start();
        Loaded += (_, _) =>
        {
            Show(_index, 0);
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(800)) { BeginTime = TimeSpan.FromSeconds(3) };
            _hint.BeginAnimation(OpacityProperty, fade);
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right or Key.Space or Key.PageDown or Key.Enter or Key.Down: Next(); break;
            case Key.Left or Key.PageUp or Key.Back or Key.Up: Previous(); break;
            case Key.Home: Show(0, -1); break;
            case Key.End: Show(_show.Slides.Count - 1, 1); break;
            case Key.N: _notes.Visibility = _notes.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; UpdateNotes(); break;
            case Key.B or Key.OemPeriod: _black.Visibility = _black.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; break;
            case Key.Escape: Close(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void Next()
    {
        if (_black.Visibility == Visibility.Visible) { _black.Visibility = Visibility.Collapsed; return; }
        if (_index + 1 >= _show.Slides.Count) { ShowEnd(); return; }
        Show(_index + 1, 1);
    }

    private bool _atEnd;

    private void Previous()
    {
        if (_atEnd) { _atEnd = false; Show(_index, 0); return; }
        if (_index > 0) Show(_index - 1, -1);
    }

    private void ShowEnd()
    {
        if (_atEnd) { Close(); return; }
        _atEnd = true;
        _stage.Children.Clear();
        _stage.Children.Add(new TextBlock
        {
            Text = "Fin de la présentation. Clique ou appuie sur une touche pour quitter.", Foreground = Brushes.Gray, FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        });
    }

    private void Show(int index, int direction)
    {
        _atEnd = false;
        _index = Math.Clamp(index, 0, _show.Slides.Count - 1);
        var view = new Viewbox { Child = SlideRenderer.Render(_show.Slides[_index], _theme), Stretch = Stretch.Uniform };
        var old = _stage.Children.Count > 0 ? _stage.Children[^1] as FrameworkElement : null;
        _stage.Children.Add(view);
        var duration = TimeSpan.FromMilliseconds(420);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var transition = direction == 0 || !SystemParameters.ClientAreaAnimation ? SlideTransition.None : _show.Transition;

        void RemoveOld()
        {
            if (old != null) _stage.Children.Remove(old);
            while (_stage.Children.Count > 1) _stage.Children.RemoveAt(0);
        }

        switch (transition)
        {
            case SlideTransition.Fade:
                view.Opacity = 0;
                var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
                fade.Completed += (_, _) => RemoveOld();
                view.BeginAnimation(OpacityProperty, fade);
                break;
            case SlideTransition.Slide:
                var tt = new TranslateTransform(direction * ActualWidth, 0);
                view.RenderTransform = tt;
                var slide = new DoubleAnimation(direction * ActualWidth, 0, duration) { EasingFunction = ease };
                slide.Completed += (_, _) => RemoveOld();
                tt.BeginAnimation(TranslateTransform.XProperty, slide);
                if (old != null)
                {
                    var ot = new TranslateTransform();
                    old.RenderTransform = ot;
                    ot.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -direction * ActualWidth, duration) { EasingFunction = ease });
                }
                break;
            case SlideTransition.Zoom:
                view.RenderTransformOrigin = new Point(0.5, 0.5);
                var st = new ScaleTransform(0.85, 0.85);
                view.RenderTransform = st;
                view.Opacity = 0;
                var zoom = new DoubleAnimation(0.85, 1, duration) { EasingFunction = ease };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);
                var op = new DoubleAnimation(0, 1, duration);
                op.Completed += (_, _) => RemoveOld();
                view.BeginAnimation(OpacityProperty, op);
                break;
            default:
                RemoveOld();
                break;
        }
        UpdateNotes();
    }

    private void UpdateNotes()
    {
        var notes = _show.Slides[_index].Notes;
        _notesText.Text = string.IsNullOrWhiteSpace(notes) ? "(Pas de notes pour cette diapositive)" : notes;
        var el = _clock.Elapsed;
        _notesInfo.Text = $"Diapositive {_index + 1} / {_show.Slides.Count} · {(int)el.TotalMinutes:00}:{el.Seconds:00} écoulées · {DateTime.Now:HH:mm}";
    }
}
