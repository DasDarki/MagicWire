namespace MagicWire.Test;

internal class Program
{
    private static async Task Main(string[] args)
    {
        TypeScriptGenerator.Generate(@"E:\Development\DasDarki\DuneBoardGame\DuneBoardGame.Frontend\src", TypeScriptGenerator.Mode.Vue);

        var obj = new TestObject();
        
        WireContainer.Instance.Start();
        Console.WriteLine("Started! Press any key to exit...");

        while (Console.ReadKey().Key != ConsoleKey.Escape) ;
        
        await WireContainer.Instance.StopAsync();
    }
}