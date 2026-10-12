using BusinessLogic.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

[TestClass]
public class BaseApiControllerTests
{
    private TestController _controller = null!;

    [TestInitialize]
    public void Initialize()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();

        _controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
            }
        };
    }

    [TestMethod]
    public void SuccessMapsToOk()
    {
        var actionResult = _controller.Map(Result<string>.Success("value"));

        Assert.IsInstanceOfType<OkObjectResult>(actionResult, out var ok);
        Assert.AreEqual(StatusCodes.Status200OK, ok.StatusCode);
        Assert.AreEqual("value", ok.Value);
    }

    [TestMethod]
    public void SuccessUsesProvidedResult()
    {
        var actionResult = _controller.MapCreated(Result<string>.Success("value"));

        Assert.IsInstanceOfType<ObjectResult>(actionResult, out var created);
        Assert.AreEqual(StatusCodes.Status201Created, created.StatusCode);
    }

    [TestMethod]
    public void NotFoundMapsToProblem404()
    {
        var actionResult = _controller.Map(Result<string>.NotFound());

        var problem = AssertProblem(actionResult, StatusCodes.Status404NotFound);
        Assert.IsFalse(problem.Extensions.ContainsKey(BaseApiController.ReasonExtension));
    }

    [TestMethod]
    public void ConflictMapsToProblem409WithReason()
    {
        var actionResult = _controller.Map(Result<string>.Conflict(ConflictReason.CapacityExceeded));

        var problem = AssertProblem(actionResult, StatusCodes.Status409Conflict);
        Assert.AreEqual("capacity-exceeded", problem.Extensions[BaseApiController.ReasonExtension]);
        Assert.IsNotNull(problem.Title);
    }

    private static ProblemDetails AssertProblem(IActionResult actionResult, int statusCode)
    {
        Assert.IsInstanceOfType<ObjectResult>(actionResult, out var objectResult);
        Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value, out var problem);

        Assert.AreEqual(statusCode, objectResult.StatusCode);
        Assert.AreEqual(statusCode, problem.Status);

        return problem;
    }

    private class TestController : BaseApiController
    {
        public IActionResult Map<T>(Result<T> result)
        {
            return ToOkResult(result);
        }

        public IActionResult MapCreated<T>(Result<T> result)
        {
            return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
        }
    }
}
