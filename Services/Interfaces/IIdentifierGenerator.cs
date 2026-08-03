namespace FlowDesk.Services.Interfaces;

public interface IIdentifierGenerator
{
    Task<string> GenerateUserCodeAsync(string role);

    Task<string> GenerateWorkItemCodeAsync();
}
