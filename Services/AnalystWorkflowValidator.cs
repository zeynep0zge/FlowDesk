using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;

namespace FlowDesk.Services;

public sealed class AnalystWorkflowValidator
{
    public const int ExpectedStatusMaximumLength = 100;
    public const int AnalystNoteMaximumLength = 1000;

    public string? ValidateWorkItemId(int workItemId)
    {
        return workItemId > 0 ? null : "Geçersiz talep ID.";
    }

    public AnalystWorkflowValidationResult ValidateSave(
        SaveAnalysisDto dto)
    {
        NormalizedAnalystInput input = Normalize(dto);
        List<string> errors = [];

        AddExpectedStatusError(input.ExpectedStatus, errors);
        AddCurrentStatusError(input.CurrentStatus, errors);

        if (dto.AnalystId.HasValue && dto.AnalystId.Value <= 0)
        {
            errors.Add("Analist ID 0'dan büyük olmalıdır.");
        }

        if (dto.DeveloperId.HasValue && dto.DeveloperId.Value <= 0)
        {
            errors.Add("Yazılımcı ID 0'dan büyük olmalıdır.");
        }

        AddDateOrderError(dto, errors);
        AddAnalystNoteError(input.AnalystNote, errors);

        return CreateResult(input, errors);
    }

    public AnalystWorkflowValidationResult ValidateSubmit(
        SubmitForApprovalDto dto)
    {
        NormalizedAnalystInput input = Normalize(dto);
        List<string> errors = [];

        AddExpectedStatusError(input.ExpectedStatus, errors);
        if (errors.Count > 0)
        {
            return CreateResult(input, errors);
        }

        List<string> missingFields = [];
        List<string> invalidFields = [];

        AddRequiredId(
            dto.AnalystId,
            "analist",
            "analist ID",
            missingFields,
            invalidFields);
        AddRequiredId(
            dto.DeveloperId,
            "yazılımcı",
            "yazılımcı ID",
            missingFields,
            invalidFields);

        if (!dto.ReleaseDate.HasValue)
        {
            missingFields.Add("sürüm tarihi");
        }

        if (!dto.BanksoftDeliveryDate.HasValue)
        {
            missingFields.Add("Banksoft teslim tarihi");
        }

        if (input.ExpectedStatus == null)
        {
            missingFields.Add("beklenen statü");
        }

        if (missingFields.Count > 0)
        {
            errors.Add(
                "Yönetici onayına göndermeden önce şu alanları doldurun: " +
                string.Join(", ", missingFields) +
                ".");
            return CreateResult(input, errors);
        }

        if (invalidFields.Count > 0)
        {
            errors.Add(
                "Şu alanlar 0'dan büyük olmalıdır: " +
                string.Join(", ", invalidFields) +
                ".");
            return CreateResult(input, errors);
        }

        AddAnalystNoteError(input.AnalystNote, errors);
        if (errors.Count == 0)
        {
            AddDateOrderError(dto, errors);
        }

        return CreateResult(input, errors);
    }

    private static NormalizedAnalystInput Normalize(SaveAnalysisDto dto)
    {
        return new NormalizedAnalystInput(
            NormalizeNullableText(dto.ExpectedStatus),
            NormalizeNullableText(dto.CurrentStatus),
            NormalizeNullableText(dto.AnalystNote));
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddExpectedStatusError(
        string? expectedStatus,
        ICollection<string> errors)
    {
        if (expectedStatus?.Length > ExpectedStatusMaximumLength)
        {
            errors.Add(
                "Beklenen statü en fazla 100 karakter olabilir.");
        }
    }

    private static void AddCurrentStatusError(
        string? currentStatus,
        ICollection<string> errors)
    {
        if (currentStatus?.Length >
            AnalystCurrentStatusOptions.MaximumLength)
        {
            errors.Add(
                "Mevcut statü en fazla " +
                $"{AnalystCurrentStatusOptions.MaximumLength} " +
                "karakter olabilir.");
        }
        else if (currentStatus != null &&
                 !AnalystCurrentStatusOptions.Contains(currentStatus))
        {
            errors.Add("Geçerli bir mevcut statü seçiniz.");
        }
    }

    private static void AddAnalystNoteError(
        string? analystNote,
        ICollection<string> errors)
    {
        if (analystNote?.Length > AnalystNoteMaximumLength)
        {
            errors.Add("Analist notu en fazla 1000 karakter olabilir.");
        }
    }

    private static void AddDateOrderError(
        SaveAnalysisDto dto,
        ICollection<string> errors)
    {
        if (dto.ReleaseDate.HasValue &&
            dto.BanksoftDeliveryDate.HasValue &&
            dto.BanksoftDeliveryDate.Value.Date >
            dto.ReleaseDate.Value.Date)
        {
            errors.Add(
                "Banksoft teslim tarihi, sürüm tarihinden sonra olamaz.");
        }
    }

    private static void AddRequiredId(
        int? value,
        string missingName,
        string invalidName,
        ICollection<string> missingFields,
        ICollection<string> invalidFields)
    {
        if (!value.HasValue)
        {
            missingFields.Add(missingName);
        }
        else if (value.Value <= 0)
        {
            invalidFields.Add(invalidName);
        }
    }

    private static AnalystWorkflowValidationResult CreateResult(
        NormalizedAnalystInput input,
        IReadOnlyCollection<string> errors)
    {
        return new AnalystWorkflowValidationResult(
            errors.Count == 0,
            errors.Count == 0 ? null : string.Join(" ", errors),
            input.ExpectedStatus,
            input.CurrentStatus,
            input.AnalystNote);
    }

    private sealed record NormalizedAnalystInput(
        string? ExpectedStatus,
        string? CurrentStatus,
        string? AnalystNote);
}

public sealed record AnalystWorkflowValidationResult(
    bool IsValid,
    string? ErrorMessage,
    string? ExpectedStatus,
    string? CurrentStatus,
    string? AnalystNote);
