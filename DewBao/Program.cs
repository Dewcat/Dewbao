namespace DewBao;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // 单实例检测
        using var mutex = new System.Threading.Mutex(true, "DewBao_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("程序已在运行中，请查看系统托盘。", "DewBao",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}