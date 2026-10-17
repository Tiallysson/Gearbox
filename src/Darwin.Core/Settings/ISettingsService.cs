namespace Darwin.Core.Settings;

/// <summary>
/// Configurações persistidas em JSON em <c>%LocalAppData%\Darwin\settings\{id}.json</c>.
/// </summary>
public interface ISettingsService<T>
    where T : class, new()
{
    /// <summary>Lê o arquivo. Se ele não existir ou estiver corrompido, devolve os valores padrão.</summary>
    T Load();

    /// <summary>Grava o arquivo e dispara <see cref="Changed"/> na thread que chamou.</summary>
    void Save(T settings);

    /// <summary>Disparado depois de cada <see cref="Save"/> com uma cópia das configurações gravadas.</summary>
    event EventHandler<T>? Changed;
}
