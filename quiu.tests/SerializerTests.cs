using quiu.core;

namespace quiu.tests;

public class SerializerTests
    : IDisposable
{
    public SerializerTests ()
    {
    }

    [Fact]
    public void Test_ToJson ()
    {
        const string text = "hello, world";

        var data = Serializer.FromText ($@"{{ ""key"": ""{text}"" }}");

        var json = Serializer.ToJson (data);
        Assert.Equal (text, json.RootElement.GetProperty ("key").GetString ());
    }

    [Fact]
    public void Test_ToText ()
    {
        const string text = "hello, world";

        var data = Serializer.FromText (text);
        Assert.Equal (text, Serializer.ToText (data));
    }

    public void Dispose ()
    {
    }
}