namespace BlockBlast.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, @"Local\BlockBlast.WinForms.Deluxe", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Block Blast đang mở. Hãy quay lại cửa sổ game đang chơi.",
                "Block Blast", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        System.Windows.Forms.Application.Run(new MainForm());
    }
}
