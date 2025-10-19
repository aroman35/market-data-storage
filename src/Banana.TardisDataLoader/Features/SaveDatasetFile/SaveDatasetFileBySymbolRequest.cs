using MediatR;

namespace Banana.TardisDataLoader.Features.SaveDatasetFile;

public record SaveDatasetFileBySymbolRequest(MarketDataHash Hash) : IRequest;
