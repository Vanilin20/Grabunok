using System;
using Banda.Core;

namespace Banda.Heist
{
    /// <summary>
    /// Канал між екраном гравця й справою. Екрани ролей знають ТІЛЬКИ цей інтерфейс:
    /// надсилають дії й отримують знімки своєї ролі. Де крутиться симуляція — на цьому телефоні
    /// (LocalTransport) чи на хості через мережу — екранам однаково.
    /// </summary>
    public interface ITransport
    {
        /// <summary>Роль гравця на цьому пристрої (у «гарячому місці» змінюється).</summary>
        RoleId LocalRole { get; }

        /// <summary>Статичні дані ролі: на старті справи, після зміни ролі й після перепідключення.</summary>
        event Action<RoleStatic> StaticReceived;

        /// <summary>Знімок стану 10–20 разів на секунду: спільне + тільки своя роль.</summary>
        event Action<RoleSnapshot> SnapshotReceived;

        /// <summary>Справу завершено (успіх / провал).</summary>
        event Action<HeistSummary> Finished;

        /// <summary>Локальна роль змінилась (тільки «гаряче місце» на одному пристрої).</summary>
        event Action<RoleId> LocalRoleChanged;

        /// <summary>Надіслати дію від імені локального гравця.</summary>
        void Send(HeistAction action);
    }
}
