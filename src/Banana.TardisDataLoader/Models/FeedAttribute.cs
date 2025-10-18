namespace Banana.TardisDataLoader.Models;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class FeedAttribute(FeedType feed) : Attribute
{
    public FeedType Feed { get; } = feed;
}
