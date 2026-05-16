namespace Library.Integration.Tests.Xunit;

internal sealed class TestOutputRelay : ITestOutputHelper
{
    private static readonly TestOutputRelay Instance = new();
    private static ITestOutputHelper _current;

    public static ITestOutputHelper Shared => Instance;

    public static void Use(ITestOutputHelper output)
    {
        _current = output;
    }

    public string Output => _current?.Output ?? string.Empty;

    public void Write(string message) => _current?.Write(message);

    public void Write(string format, params object[] args) => _current?.Write(format, args);

    public void WriteLine(string message) => _current?.WriteLine(message);

    public void WriteLine(string format, params object[] args) => _current?.WriteLine(format, args);
}
