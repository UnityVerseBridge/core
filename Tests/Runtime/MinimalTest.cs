using NUnit.Framework;

public class MinimalTest
{
    [Test]
    public void ThisShouldPass()
    {
        Assert.IsTrue(true);
        UnityEngine.Debug.Log("Minimal test executed!");
    }
}