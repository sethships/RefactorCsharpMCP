public class TestService
{
    public void ProcessData(ILogger logger, IConfig config, string data)
    {
        logger.Log("Processing data");
        var settings = config.GetSettings();

        // Some processing logic
        var result = data.ToUpper();
        var length = result.Length;
        Console.WriteLine($"Processed: {result}, Length: {length}");

        logger.Log("Processing complete");
    }
}
