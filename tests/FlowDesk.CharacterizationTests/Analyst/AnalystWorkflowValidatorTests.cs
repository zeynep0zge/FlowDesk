using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Services;

namespace FlowDesk.CharacterizationTests.Analyst;

public sealed class AnalystWorkflowValidatorTests
{
    private readonly AnalystWorkflowValidator _validator = new();

    [Fact]
    public void ValidateSave_ValidInput_ReturnsNormalizedSuccess()
    {
        SaveAnalysisDto dto = ValidSaveDto();

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("Ready", result.ExpectedStatus);
        Assert.Equal(
            AnalystCurrentStatusOptions.UnderAnalystReview,
            result.CurrentStatus);
        Assert.Equal("Valid note", result.AnalystNote);
    }

    [Fact]
    public void ValidateSave_InvalidDateOrder_ReturnsFailure()
    {
        SaveAnalysisDto dto = ValidSaveDto();
        dto.ReleaseDate = new DateTime(2026, 9, 10);
        dto.BanksoftDeliveryDate = new DateTime(2026, 9, 11);

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Banksoft teslim tarihi, sürüm tarihinden sonra olamaz.",
            result.ErrorMessage);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ValidateSave_ExpectedStatusBoundary_IsEnforced(
        int length,
        bool expectedIsValid)
    {
        SaveAnalysisDto dto = ValidSaveDto();
        dto.ExpectedStatus = new string('x', length);

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.Equal(expectedIsValid, result.IsValid);
        if (!expectedIsValid)
        {
            Assert.Equal(
                "Beklenen statü en fazla 100 karakter olabilir.",
                result.ErrorMessage);
        }
    }

    [Fact]
    public void ValidateSave_LongAnalystNote_ReturnsFailure()
    {
        SaveAnalysisDto dto = ValidSaveDto();
        dto.AnalystNote = new string('x', 1001);

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Analist notu en fazla 1000 karakter olabilir.",
            result.ErrorMessage);
    }

    [Fact]
    public void ValidateSave_InvalidDeveloperId_ReturnsFailure()
    {
        SaveAnalysisDto dto = ValidSaveDto();
        dto.DeveloperId = 0;

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Yazılımcı ID 0'dan büyük olmalıdır.",
            result.ErrorMessage);
    }

    [Theory]
    [InlineData(AnalystCurrentStatusOptions.ReadyForRelease, true)]
    [InlineData("Sahte Statü", false)]
    public void ValidateSave_CurrentStatusWhitelist_IsEnforced(
        string currentStatus,
        bool expectedIsValid)
    {
        SaveAnalysisDto dto = ValidSaveDto();
        dto.CurrentStatus = currentStatus;

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.Equal(expectedIsValid, result.IsValid);
        if (!expectedIsValid)
        {
            Assert.Equal(
                "Geçerli bir mevcut statü seçiniz.",
                result.ErrorMessage);
        }
    }

    [Fact]
    public void ValidateSubmit_MissingRequiredFields_ListsAllFields()
    {
        SubmitForApprovalDto dto = new() { WorkItemId = 42 };

        AnalystWorkflowValidationResult result =
            _validator.ValidateSubmit(dto);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Yönetici onayına göndermeden önce şu alanları doldurun: " +
            "analist, yazılımcı, sürüm tarihi, Banksoft teslim tarihi, " +
            "beklenen statü.",
            result.ErrorMessage);
    }

    [Fact]
    public void ValidateSave_TextInputs_AreTrimmedAndWhitespaceIsNull()
    {
        SaveAnalysisDto dto = ValidSaveDto();
        dto.ExpectedStatus = "  Ready  ";
        dto.CurrentStatus =
            $"  {AnalystCurrentStatusOptions.ReadyForRelease}  ";
        dto.AnalystNote = "   ";

        AnalystWorkflowValidationResult result =
            _validator.ValidateSave(dto);

        Assert.True(result.IsValid);
        Assert.Equal("Ready", result.ExpectedStatus);
        Assert.Equal(
            AnalystCurrentStatusOptions.ReadyForRelease,
            result.CurrentStatus);
        Assert.Null(result.AnalystNote);
    }

    private static SaveAnalysisDto ValidSaveDto()
    {
        return new SaveAnalysisDto
        {
            WorkItemId = 42,
            AnalystId = 11,
            DeveloperId = 22,
            ReleaseDate = new DateTime(2026, 9, 20),
            BanksoftDeliveryDate = new DateTime(2026, 9, 10),
            ExpectedStatus = "Ready",
            CurrentStatus =
                AnalystCurrentStatusOptions.UnderAnalystReview,
            AnalystNote = "Valid note"
        };
    }
}
