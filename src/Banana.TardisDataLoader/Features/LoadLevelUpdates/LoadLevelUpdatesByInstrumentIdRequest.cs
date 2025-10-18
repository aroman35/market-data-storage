using MediatR;

namespace Banana.TardisDataLoader.Features.LoadLevelUpdates;

public record LoadLevelUpdatesByInstrumentIdRequest(Guid InstrumentId, DateOnly Date) : IRequest;
