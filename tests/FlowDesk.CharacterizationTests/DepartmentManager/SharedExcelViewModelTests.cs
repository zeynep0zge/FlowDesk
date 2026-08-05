using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.CharacterizationTests.DepartmentManager;

public sealed class SharedExcelViewModelTests
{
    [Fact]
    public void RequestPreview_LongRequest_IsLimitedAndHasDetailState()
    {
        var item = new SharedExcelItemViewModel
        {
            RequestDescription = new string('a', 181)
        };

        Assert.True(item.IsRequestTruncated);
        Assert.Equal(180, item.RequestPreview.Length);
        Assert.EndsWith("…", item.RequestPreview);
    }

    [Fact]
    public void RequestPreview_ShortRequest_ReturnsFullText()
    {
        const string request = "Kısa talep metni";
        var item = new SharedExcelItemViewModel
        {
            RequestDescription = request
        };

        Assert.False(item.IsRequestTruncated);
        Assert.Equal(request, item.RequestPreview);
    }
}
