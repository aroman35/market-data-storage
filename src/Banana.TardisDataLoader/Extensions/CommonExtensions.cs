namespace Banana.TardisDataLoader.Extensions;

public static class CommonExtensions
{
    public static async IAsyncEnumerable<T[]> ChunkAsync<T>(this IAsyncEnumerable<T> enumerable, int chunkSize)
    {
        var currentSize = 0;
        var currentChunk = new T[chunkSize];
        await foreach (var item in enumerable)
        {
            if (currentSize == chunkSize)
            {
                yield return currentChunk;
            }
            Interlocked.CompareExchange(ref currentSize, 0, chunkSize);
            currentChunk[currentSize++] = item;
        }

        if (currentSize == 0)
            yield break;
        if (currentSize != chunkSize)
            Array.Resize(ref currentChunk, currentSize);
        yield return currentChunk;
    }
}
