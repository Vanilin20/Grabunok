using Banda.Core;
using Banda.Heist;
using UnityEngine;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Основа екрана ролі: підписується на ITransport, коли почалась справа, і вмикає свою панель,
    /// лише коли локальна роль збігається з його роллю. Екран нічого не знає про симуляцію — лише знімки й дії.
    /// </summary>
    public abstract class RoleScreenBase : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;

        protected ITransport Transport { get; private set; }

        protected abstract RoleId Role { get; }

        protected bool IsShown => _panel != null && _panel.activeSelf;

        protected virtual void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        protected virtual void OnEnable()
        {
            if (HeistDirector.Instance != null)
            {
                HeistDirector.Instance.HeistStarted += Attach;
                HeistDirector.Instance.HeistStopped += Detach;
                if (HeistDirector.Instance.Transport != null)
                {
                    Attach(HeistDirector.Instance.Transport);
                }
            }
        }

        protected virtual void OnDisable()
        {
            if (HeistDirector.Instance != null)
            {
                HeistDirector.Instance.HeistStarted -= Attach;
                HeistDirector.Instance.HeistStopped -= Detach;
            }

            Detach();
        }

        private void Attach(ITransport transport)
        {
            Detach();
            Transport = transport;
            Transport.StaticReceived += HandleStatic;
            Transport.SnapshotReceived += HandleSnapshot;
            Transport.LocalRoleChanged += HandleRole;
            HandleRole(Transport.LocalRole);
            Transport.RequestState();
        }

        private void Detach()
        {
            if (Transport != null)
            {
                Transport.StaticReceived -= HandleStatic;
                Transport.SnapshotReceived -= HandleSnapshot;
                Transport.LocalRoleChanged -= HandleRole;
                Transport = null;
            }

            Show(false);
        }

        private void HandleRole(RoleId role)
        {
            Show(role == Role);
        }

        private void Show(bool show)
        {
            if (_panel != null && _panel.activeSelf != show)
            {
                _panel.SetActive(show);
                OnShown(show);
            }
        }

        private void HandleStatic(RoleStatic data)
        {
            if (data != null && data.Role == Role)
            {
                OnStatic(data);
            }
        }

        private void HandleSnapshot(RoleSnapshot snapshot)
        {
            if (snapshot?.View != null && snapshot.View.Role == Role)
            {
                OnSnapshot(snapshot);
            }
        }

        protected void Send(HeistAction action)
        {
            Transport?.Send(action);
        }

        protected virtual void OnShown(bool shown)
        {
        }

        protected abstract void OnStatic(RoleStatic data);

        protected abstract void OnSnapshot(RoleSnapshot snapshot);
    }
}
