namespace AgentStudio.Service.Security;

public interface IUserContext
{
    string UserId { get; }
    string Username { get; }
}