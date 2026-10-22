using BetterGenshinImpact.UnitTest.CoreTests.RecognitionTests.OCRTests;
using BetterGenshinImpact.UnitTest.GameTaskTests.GetGridIconsTests;

namespace BetterGenshinImpact.UnitTest
{
    /// <summary>
    /// 与原版相同，去掉了 TorchSharp 相关的夹具
    /// </summary>
    [CollectionDefinition("Init Collection")]
    public class InitCollection : ICollectionFixture<PaddleFixture>, ICollectionFixture<GameTaskTests.AutoFishingTests.TorchFixture>, ICollectionFixture<LocalizationFixture>, ICollectionFixture<GridIconModelFixture>, ICollectionFixture<MainProjectConfigFixture>
    {
    }
}
