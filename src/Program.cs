namespace NovaBrowser;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
{
    try
    {
        MessageBox.Show("Program started");

        ApplicationConfiguration.Initialize();

        MessageBox.Show("Configuration initialized");

        Form1 form = new Form1();

        MessageBox.Show("Form1 created");

        Application.Run(form);
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.ToString(),
            "Nova Browser Crash");
    }
}   
}