using Microsoft.AspNetCore.Http;
using NSubstitute;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Features.Audit.AuditDetails;
using Synentra.Application.Features.Audit.AuditList;
using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Results;
using Synentra.Endpoints;

namespace Synentra.UnitTests.Endpoints;

public class AuditEndpointTests
{
    private readonly IDispatcher _dispatcher;
    private static readonly ErrorCode TestCode = new(1003, ErrorCategory.Core);

    public AuditEndpointTests()
    {
        _dispatcher = Substitute.For<IDispatcher>();
    }

    [Fact]
    public async Task AuditList_Success_Returns200()
    {
        var items = new List<AuditListResult> { new() { Id = 1, Action = "GET /v1/data", Status = "Allow" } };
        var paginated = PaginatedResult<AuditListResult>.Success(items, 1, 25, 1);
        _dispatcher.Dispatch(Arg.Any<IAction<PaginatedResult<AuditListResult>>>(), Arg.Any<CancellationToken>())
                   .Returns(paginated);

        var result = await Audit.AuditList(_dispatcher, CancellationToken.None);

        AssertStatusCode(result, 200);
    }

    [Fact]
    public async Task AuditDetails_Found_Returns200()
    {
        var detail = new AuditDetailsResult { Id = 10, Action = "POST /v1/chat", Status = "Deny" };
        _dispatcher.Dispatch(Arg.Any<IAction<Result<AuditDetailsResult>>>(), Arg.Any<CancellationToken>())
                   .Returns(Result<AuditDetailsResult>.Success(detail));

        var result = await Audit.AuditDetails(10, _dispatcher, CancellationToken.None);

        AssertStatusCode(result, 200);
    }

    [Fact]
    public async Task AuditDetails_NotFound_Returns404()
    {
        var error = Error.NotFound(TestCode, "not found");
        _dispatcher.Dispatch(Arg.Any<IAction<Result<AuditDetailsResult>>>(), Arg.Any<CancellationToken>())
                   .Returns(Result<AuditDetailsResult>.Failure(error));

        var result = await Audit.AuditDetails(999, _dispatcher, CancellationToken.None);

        AssertStatusCode(result, 404);
    }

    private static void AssertStatusCode(IResult httpResult, int expected)
        => HttpTestHelpers.ExecuteAndGetStatusCode(httpResult).Should().Be(expected);
}
