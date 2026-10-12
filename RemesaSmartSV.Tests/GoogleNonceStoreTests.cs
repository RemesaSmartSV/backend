using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

public class GoogleNonceStoreTests
{
    [Fact]
    public void Create_GeneraNoncesDistintosDeUsoUnico()
    {
        var store = new GoogleNonceStore();
        var first = store.Create();
        var second = store.Create();

        Assert.NotEqual(first, second);
        Assert.True(store.TryConsume(first));
        Assert.False(store.TryConsume(first));
        Assert.True(store.TryConsume(second));
    }
}
