using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Strigoi.Companion;

internal static class WorkAssistantBridge
{
    internal static string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "WorkAssistant", "Firaw.WorkAssistant.exe");
    internal static bool IsAvailable => File.Exists(ExecutablePath);

    internal static void Open()
    {
        try
        {
            if (!IsAvailable) throw new FileNotFoundException("O módulo de trabalho não está incluído nesta instalação.");
            Process.Start(new ProcessStartInfo(ExecutablePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(ExecutablePath)! });
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Strigoi Companion Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
