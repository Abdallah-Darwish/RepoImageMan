using System;
using System.Collections.Concurrent;
using System.ComponentModel;
namespace RepoImageMan
{
    public interface INotificationManager
    {
        INotificationManager Subscribe(string propName, PropertyChangedEventHandler handler);
        INotificationManager Unsubscribe(string propName, PropertyChangedEventHandler handler);
    }
    public sealed class NotificationManager : IDisposable, INotificationManager
    {
        private readonly INotifySpecificPropertyChanged _sender;
        private readonly ConcurrentDictionary<string, PropertyChangedEventHandler> _handlers = new ConcurrentDictionary<string, PropertyChangedEventHandler>();
        public INotificationManager Subscribe(string propName, PropertyChangedEventHandler handler)
        {
            _handlers.AddOrUpdate(propName, handler, (pn, existingHandler) => existingHandler += handler);
            return this;
        }
        public INotificationManager Unsubscribe(string propName, PropertyChangedEventHandler handler)
        {
            if (_handlers.TryGetValue(propName, out var existingHandler))
            {
                existingHandler -= handler;
                if (existingHandler == null)
                {
                    _handlers.TryRemove(propName, out var _);
                }
            }
            return this;
        }
        public void OnPropertyChanged(string propName)
        {
            if (_handlers.TryGetValue(propName, out var existingHandler))
            {
                existingHandler(_sender, new PropertyChangedEventArgs(propName));
            }
        }
        public NotificationManager(INotifySpecificPropertyChanged sender) => _sender = sender;


        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _handlers.Clear();
            }
        }
        #endregion
    }
}
