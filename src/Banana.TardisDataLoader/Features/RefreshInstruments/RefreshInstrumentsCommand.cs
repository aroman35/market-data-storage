using MediatR;

namespace Banana.TardisDataLoader.Features.RefreshInstruments;

public record RefreshInstrumentsCommand(Exchange Exchange) : IRequest;
