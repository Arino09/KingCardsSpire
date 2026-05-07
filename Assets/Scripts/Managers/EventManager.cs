using System;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;

namespace KingCardsSpire.Managers
{
    public sealed class EventManager : PersistentMonoSingleton<EventManager>
    {
        readonly EventBus _bus = new();

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            _bus.Clear();
            ServiceLocator.Unregister<EventManager>();
            base.OnDestroy();
        }

        public void Subscribe<T>(Action<T> handler) where T : IEvent => _bus.Subscribe(handler);

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent => _bus.Unsubscribe(handler);

        public void Publish<T>(T evt) where T : IEvent => _bus.Publish(evt);
    }
}
