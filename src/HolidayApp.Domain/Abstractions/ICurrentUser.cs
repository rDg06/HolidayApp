namespace HolidayApp.Domain.Abstractions;

/// <summary>
/// Identifica o utilizador do pedido atual.
/// Hoje devolve sempre o utilizador local; quando houver login, passa a vir
/// das claims de autenticação sem mudar o resto da aplicação.
/// </summary>
public interface ICurrentUser
{
    string UserId { get; }
}
