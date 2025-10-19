using MediatR;

namespace Banana.TardisDataLoader.Features.SaveDatasetFile;

public record SaveDatasetFileByInstrumentIdRequest(Guid InstrumentId, DateOnly Date, FeedType Feed) : IRequest;
