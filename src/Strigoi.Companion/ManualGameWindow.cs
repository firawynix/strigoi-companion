using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strigoi.Companion;

// A small chooser only: the FamiliarCoordinator owns the actual capture,
// Chronicle, Ask and Watch lifecycle after selection.
internal sealed class ManualGameWindow : Window
{
    private readonly ComboBox targets = new() { MinWidth = 360, DisplayMemberPath = "Title", Margin = new Thickness(0, 8, 0, 10) };
    private readonly TextBlock status = new() { Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
    private readonly FamiliarCoordinator coordinator;
    public ManualGameWindow(FamiliarCoordinator coordinator)
    {
        this.coordinator = coordinator; Title = "Strigoi Companion · Escolher jogo"; Width = 440; Height = 230; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(27, 20, 37)); Foreground = Brushes.WhiteSmoke;
        var body = new StackPanel { Margin = new Thickness(22) }; Content = body;
        body.Children.Add(new TextBlock { Text = "Qual janela o Familiar deve acompanhar?", FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(217, 184, 255)) });
        body.Children.Add(new TextBlock { Text = "Isso inicia o mesmo acompanhamento automático, Chronicle e Watch do Familiar.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        body.Children.Add(targets); var buttons = new WrapPanel(); body.Children.Add(buttons);
        var start = new Button { Content = "Acompanhar esta janela", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        start.Click += async (_, _) => { if (targets.SelectedItem is not CaptureTarget target) { status.Text = "Escolha uma janela primeiro."; return; } start.IsEnabled = false; try { await coordinator.StartForTargetAsync(target, false); Close(); } catch (Exception ex) { status.Text = ex.Message; } finally { start.IsEnabled = true; } };
        buttons.Children.Add(start); var refresh = new Button { Content = "Atualizar", Padding = new Thickness(12, 7, 12, 7) }; refresh.Click += (_, _) => Refresh(); buttons.Children.Add(refresh); body.Children.Add(status); Refresh();
    }
    private void Refresh() { targets.ItemsSource = CaptureNative.Targets(); targets.SelectedIndex = -1; status.Text = ""; }
}
