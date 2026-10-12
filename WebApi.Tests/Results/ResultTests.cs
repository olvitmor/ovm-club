using BusinessLogic.Results;

namespace WebApi.Tests.Results;

[TestClass]
public class ResultTests
{
    [TestMethod]
    public void SuccessCarriesValue()
    {
        Result<int> result = 42;

        Assert.AreEqual(ResultStatus.Success, result.Status);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.Value);
        Assert.IsNull(result.Reason);
    }

    [TestMethod]
    public void ConflictCarriesReason()
    {
        Result<int> result = ConflictReason.ClassStarted;

        Assert.AreEqual(ResultStatus.Conflict, result.Status);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ConflictReason.ClassStarted, result.Reason);
        Assert.ThrowsException<InvalidOperationException>(() => result.Value);
    }

    [TestMethod]
    public void NotFoundHasNoValue()
    {
        var result = Result<int>.NotFound();

        Assert.AreEqual(ResultStatus.NotFound, result.Status);
        Assert.IsNull(result.Reason);
        Assert.ThrowsException<InvalidOperationException>(() => result.Value);
    }
}
